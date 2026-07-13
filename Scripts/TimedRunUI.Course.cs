#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class TimedRunUI : Control
{
    private const string CoursePath = "res://Data/course_practitioner.json";

    private PanelContainer? _courseOverlay;
    private ItemList? _courseList;
    private Label? _courseTitle;
    private RichTextLabel? _courseBody;
    private Button? _courseClose;

    private CourseDeck? _courseDeck;

    // Index de la leçon liée à la question courante (-1 si aucune / bonne réponse).
    private int _pendingLessonIndex = -1;

    private sealed class CourseDeck
    {
        public string CourseId { get; set; } = "";
        public string Title { get; set; } = "Cours";
        public CourseLesson[] Lessons { get; set; } = Array.Empty<CourseLesson>();
    }

    private sealed class CourseLesson
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Body { get; set; } = "";
        public string[] Services { get; set; } = Array.Empty<string>();
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    private void InitCourseUI()
    {
        // Lazy: overlay construit à la demande.
        // On charge juste le contenu une première fois pour éviter une pause au clic.
        _courseDeck = LoadCourseDeck();
    }

    private CourseDeck LoadCourseDeck()
    {
        try
        {
            if (!ResourceLoader.Exists(CoursePath))
                return BuildFallbackCourse();

            using var f = FileAccess.Open(CoursePath, FileAccess.ModeFlags.Read);
            var json = f.GetAsText();

            var deck = JsonSerializer.Deserialize<CourseDeck>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (deck == null || deck.Lessons == null || deck.Lessons.Length == 0)
                return BuildFallbackCourse();

            // Nettoyage minimal
            deck.Title = (deck.Title ?? "Cours").Trim();
            deck.Lessons = deck.Lessons
                .Where(l => l != null)
                .Select(l =>
                {
                    l.Title = (l.Title ?? "").Trim();
                    l.Summary = (l.Summary ?? "").Trim();
                    l.Body = (l.Body ?? "").Trim();
                    l.Services ??= Array.Empty<string>();
                    l.Tags ??= Array.Empty<string>();
                    return l;
                })
                .Where(l => !string.IsNullOrWhiteSpace(l.Title))
                .ToArray();

            if (deck.Lessons.Length == 0)
                return BuildFallbackCourse();

            return deck;
        }
        catch (Exception e)
        {
            GD.PushWarning($"[MiniJeuCartesAWS] Load course failed: {e.Message}");
            return BuildFallbackCourse();
        }
    }

    private static CourseDeck BuildFallbackCourse()
    {
        return new CourseDeck
        {
            CourseId = "fallback",
            Title = "Cours",
            Lessons = new[]
            {
                new CourseLesson
                {
                    Id = "fallback-iam",
                    Title = "IAM (rappel)",
                    Summary = "Identités, rôles et permissions.",
                    Body = "IAM gère qui peut faire quoi sur AWS (utilisateurs, rôles, policies).",
                    Services = new[] { "AWS IAM" },
                    Tags = new[] { "security", "iam" },
                }
            }
        };
    }

    private void EnsureCourseOverlay()
    {
        if (IsInstanceValid(_courseOverlay))
            return;

        var root = new PanelContainer
        {
            Name = "CourseOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
        };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Fond semi-opaque
        root.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.04f, 0.06f, 0.92f),
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18,
        });

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
        };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);

        var v = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
        };

        var header = new HBoxContainer();
        var title = new Label
        {
            Text = "Cours",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        title.AddThemeColorOverride("font_color", NeutralText);

        var close = new Button
        {
            Text = "Fermer",
        };
        close.Pressed += CloseCourseOverlay;

        header.AddChild(title);
        header.AddChild(close);

        var bodyRow = new HBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        bodyRow.AddThemeConstantOverride("separation", 14);

        var list = new ItemList
        {
            CustomMinimumSize = new Vector2(320, 420),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        list.ItemSelected += OnCourseSelected;

        var right = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        var lessonTitle = new Label
        {
            Text = "",
        };
        lessonTitle.AddThemeColorOverride("font_color", NeutralText);
        lessonTitle.AddThemeFontSizeOverride("font_size", 20);

        var lessonBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        lessonBody.AddThemeColorOverride("default_color", NeutralText);

        right.AddChild(lessonTitle);
        right.AddChild(lessonBody);

        bodyRow.AddChild(list);
        bodyRow.AddChild(right);

        v.AddChild(header);
        v.AddChild(bodyRow);
        margin.AddChild(v);
        root.AddChild(margin);

        AddChild(root);
        MoveChild(root, GetChildCount() - 1);

        _courseOverlay = root;
        _courseList = list;
        _courseTitle = title;
        _courseBody = lessonBody;
        _courseClose = close;

        _courseDeck ??= LoadCourseDeck();
    }

    private void OpenCourseOverlay()
    {
        if (_runActive)
            return;

        OpenCourseOverlayAtLesson(0);
    }

    // Ouvre l'overlay directement sur une leçon donnée. Utilisable pendant un run
    // (verso d'une question ratée → revoir la leçon) : le chrono continue, c'est le
    // choix du joueur de prendre ce temps.
    private void OpenCourseOverlayAtLesson(int lessonIndex)
    {
        _rulesVisible = false;
        HideSettingsPanel();

        EnsureCourseOverlay();
        if (!IsInstanceValid(_courseOverlay) || !IsInstanceValid(_courseList) || !IsInstanceValid(_courseTitle) || !IsInstanceValid(_courseBody))
            return;

        _courseDeck ??= LoadCourseDeck();

        _courseTitle!.Text = _courseDeck!.Title;

        _courseList!.Clear();
        foreach (var lesson in _courseDeck.Lessons)
            _courseList.AddItem(lesson.Title);

        if (_courseDeck.Lessons.Length > 0)
        {
            var idx = Math.Clamp(lessonIndex, 0, _courseDeck.Lessons.Length - 1);
            _courseList.Select(idx);
            _courseList.EnsureCurrentIsVisible();
            ApplyCourseToUI(idx);
        }
        else
        {
            _courseBody!.Text = "Aucun contenu.";
        }

        _courseOverlay!.Visible = true;
    }

    // ============================================================================
    // LIAISON QUESTION ↔ LEÇON
    // Pas de champ lessonId dans les données : on matche via les services AWS
    // (noms normalisés), avec repli sur les tags/catégorie.
    // ============================================================================

    private static string NormalizeServiceName(string s)
    {
        var t = s.Trim().ToLowerInvariant();
        if (t.StartsWith("aws ")) t = t[4..];
        if (t.StartsWith("amazon ")) t = t[7..];
        return t;
    }

    private int FindLessonIndexForQuestion(Question q)
    {
        _courseDeck ??= LoadCourseDeck();
        var lessons = _courseDeck?.Lessons;
        if (lessons == null || lessons.Length == 0)
            return -1;

        var qServices = (q.Services ?? Array.Empty<string>()).Select(NormalizeServiceName).ToHashSet();
        var qTags = (q.Tags ?? Array.Empty<string>()).Select(t => t.Trim().ToLowerInvariant()).ToHashSet();
        var qCategory = (q.Category ?? "").Trim().ToLowerInvariant();

        var bestIdx = -1;
        var bestScore = 0;

        for (var i = 0; i < lessons.Length; i++)
        {
            var l = lessons[i];
            var score = 0;

            // Services communs : critère principal (x3).
            foreach (var s in l.Services ?? Array.Empty<string>())
            {
                if (qServices.Contains(NormalizeServiceName(s)))
                    score += 3;
            }

            // Tags communs / catégorie : repli.
            foreach (var t in l.Tags ?? Array.Empty<string>())
            {
                var lt = t.Trim().ToLowerInvariant();
                if (qTags.Contains(lt)) score += 1;
                if (lt == qCategory && qCategory.Length > 0) score += 1;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    private string GetLessonTitle(int index)
    {
        var lessons = _courseDeck?.Lessons;
        if (lessons == null || index < 0 || index >= lessons.Length)
            return "";
        return lessons[index].Title;
    }

    private void OnCourseSelected(long index)
    {
        ApplyCourseToUI((int)index);
    }

    private void ApplyCourseToUI(int index)
    {
        if (_courseDeck == null || !IsInstanceValid(_courseTitle) || !IsInstanceValid(_courseBody))
            return;

        var lessons = _courseDeck.Lessons;
        if (index < 0 || index >= lessons.Length)
            return;

        var l = lessons[index];

        var services = (l.Services ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        var tags = (l.Tags ?? Array.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

        var metaLine = "";
        if (services.Length > 0)
            metaLine += $"[color=#8FE0FF]Services:[/color] {EscapeBbcode(string.Join(", ", services))}\n";
        if (tags.Length > 0)
            metaLine += $"[color=#D7B6FF]Tags:[/color] {EscapeBbcode(string.Join(", ", tags))}\n";

        _courseTitle!.Text = l.Title;
        _courseBody!.Text =
            (metaLine.Length > 0 ? metaLine + "\n" : "") +
            $"[b]{EscapeBbcode(l.Summary)}[/b]\n\n{EscapeBbcode(l.Body)}";
    }

    private void CloseCourseOverlay()
    {
        if (IsInstanceValid(_courseOverlay))
            _courseOverlay!.Visible = false;
    }

    private bool IsCourseOverlayVisible()
    {
        return IsInstanceValid(_courseOverlay) && _courseOverlay!.Visible;
    }
}
