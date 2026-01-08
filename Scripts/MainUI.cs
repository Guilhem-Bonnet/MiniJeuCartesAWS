using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public partial class MainUI : Control
{
    private static readonly Color NeutralText = new(0.92f, 0.94f, 0.98f, 1f);
    private static readonly Color CorrectAccent = new(0.15f, 0.85f, 0.55f, 1f);
    private static readonly Color WrongAccent = new(1f, 0.3f, 0.35f, 1f);

    private static readonly Dictionary<string, string> CategoryIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Storage"] = "🗄️",
        ["Compute"] = "🖥️",
        ["Database"] = "🗃️",
        ["Networking"] = "🌐",
        ["Security"] = "🔐",
        ["Monitoring"] = "📈",
        ["Messaging"] = "📨",
        ["CDN"] = "🚀",
        ["Compliance"] = "✅",
        ["Billing"] = "💳",
    };

    private Label _questionLabel = null!;
    private Label _scoreLabel = null!;
    private Label _categoryLabel = null!;
    private Label _progressLabel = null!;
    private Label _feedbackLabel = null!;
    private Button _nextButton = null!;

    private readonly List<Button> _answers = new();
    private readonly Random _rng = new();

    private List<CardQuestion> _deck = new();
    private int _index;

    private bool _answeredCurrent;

    private SaveData _save = new();

    private const string SavePath = "user://mini_jeu_cartes_save.json";
    private const string DeckPath = "res://Data/questions_practitioner.json";

    public override void _Ready()
    {
        _scoreLabel = GetNode<Label>("Margin/Center/Card/CardMargin/VBox/TopBar/Score");
        _categoryLabel = GetNode<Label>("Margin/Center/Card/CardMargin/VBox/TopBar/Category");
        _progressLabel = GetNode<Label>("Margin/Center/Card/CardMargin/VBox/TopBar/Progress");
        _questionLabel = GetNode<Label>("Margin/Center/Card/CardMargin/VBox/QuestionCard/QuestionCardMargin/Question");
        _feedbackLabel = GetNode<Label>("Margin/Center/Card/CardMargin/VBox/Feedback");
        _nextButton = GetNode<Button>("Margin/Center/Card/CardMargin/VBox/Next");

        _answers.Add(GetNode<Button>("Margin/Center/Card/CardMargin/VBox/Answers/A"));
        _answers.Add(GetNode<Button>("Margin/Center/Card/CardMargin/VBox/Answers/B"));
        _answers.Add(GetNode<Button>("Margin/Center/Card/CardMargin/VBox/Answers/C"));
        _answers.Add(GetNode<Button>("Margin/Center/Card/CardMargin/VBox/Answers/D"));

        for (var i = 0; i < _answers.Count; i++)
        {
            var idx = i;
            _answers[i].Pressed += () => Choose(idx);
        }

        _nextButton.Pressed += Next;

        LoadSave();
        BuildDeck();
        Shuffle(_deck);

        _index = 0;
        ShowCurrent();

        // Focus initial sur la 1ère réponse (clavier)
        if (_answers.Count > 0)
            _answers[0].GrabFocus();
    }

    private void BuildDeck()
    {
        // Source principale: deck JSON (extensible)
        try
        {
            if (ResourceLoader.Exists(DeckPath))
            {
                using var f = FileAccess.Open(DeckPath, FileAccess.ModeFlags.Read);
                var json = f.GetAsText();
                var deck = JsonSerializer.Deserialize<QuestionDeck>(json);

                if (deck?.Questions != null)
                {
                    _deck = new List<CardQuestion>();
                    foreach (var q in deck.Questions)
                    {
                        if (q.Answers == null || q.Answers.Length != 4)
                            continue;
                        if (q.CorrectIndex < 0 || q.CorrectIndex > 3)
                            continue;

                        _deck.Add(new CardQuestion(
                            q.Prompt ?? "(Question)",
                            q.Answers,
                            q.CorrectIndex,
                            q.Explanation ?? "",
                            q.Category ?? ""));
                    }

                    if (_deck.Count > 0)
                        return;
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] Deck load error: {e.Message}");
        }

        // Fallback minimal (si JSON absent/corrompu)
        _deck = new List<CardQuestion>
        {
            new(
                "Quel service AWS est un stockage d'objets ?",
                new [] { "Amazon S3", "Amazon RDS", "AWS Lambda", "Amazon EC2" },
                0,
                "S3 est un stockage d'objets (buckets/objets).",
                "Storage"),
        };
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void ShowCurrent()
    {
        _answeredCurrent = false;
        _feedbackLabel.Text = "";
        _feedbackLabel.SelfModulate = NeutralText;
        _nextButton.Disabled = true;
        foreach (var b in _answers)
        {
            b.Disabled = false;
            b.SelfModulate = NeutralText;
        }

        if (_index >= _deck.Count)
        {
            _questionLabel.Text = "Fin !";
            _categoryLabel.Text = "Catégorie: 🧩 -";
            foreach (var b in _answers) b.Visible = false;
            _nextButton.Text = "Rejouer";
            _nextButton.Disabled = false;
            UpdateScore();
            UpdateProgress();
            return;
        }

        var q = _deck[_index];
        _questionLabel.Text = q.Prompt;
        _categoryLabel.Text = FormatCategoryLabel(q.Category);
        for (var i = 0; i < _answers.Count; i++)
        {
            _answers[i].Visible = true;
            _answers[i].Text = q.Answers[i];
        }

        _nextButton.Text = "Suivant";
        UpdateScore();
        UpdateProgress();

        if (_answers.Count > 0)
            _answers[0].GrabFocus();
    }

    private void Choose(int chosen)
    {
        if (_index >= _deck.Count) return;
        if (_answeredCurrent) return;
        if (chosen < 0 || chosen >= _answers.Count) return;

        _answeredCurrent = true;

        var q = _deck[_index];
        var ok = chosen == q.CorrectIndex;

        _save.Total += 1;
        if (ok) _save.Correct += 1;

        _feedbackLabel.Text = ok
            ? $"✅ Bien joué ! {q.Explanation}"
            : $"❌ Raté. Réponse: {q.Answers[q.CorrectIndex]}. {q.Explanation}";

        _feedbackLabel.SelfModulate = ok ? CorrectAccent : WrongAccent;

        HighlightAnswers(chosen, q.CorrectIndex);
        AnimateAnswer(chosen, ok);

        foreach (var b in _answers) b.Disabled = true;
        _nextButton.Disabled = false;

        UpdateScore();
        UpdateProgress();
        Save();

        _nextButton.GrabFocus();
    }

    private void HighlightAnswers(int chosenIndex, int correctIndex)
    {
        for (var i = 0; i < _answers.Count; i++)
        {
            if (i == correctIndex)
                _answers[i].SelfModulate = CorrectAccent;
            else if (i == chosenIndex)
                _answers[i].SelfModulate = WrongAccent;
            else
                _answers[i].SelfModulate = NeutralText;
        }
    }

    private void AnimateAnswer(int index, bool isCorrect)
    {
        var btn = _answers[index];
        var tween = CreateTween();

        var startScale = btn.Scale;
        var upScale = startScale * 1.03f;
        var duration = isCorrect ? 0.08f : 0.06f;

        tween.TweenProperty(btn, "scale", upScale, duration);
        tween.TweenProperty(btn, "scale", startScale, duration);
    }

    private static string FormatCategoryLabel(string category)
    {
        var trimmed = (category ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return "Catégorie: 🧩 -";

        if (CategoryIcons.TryGetValue(trimmed, out var icon))
            return $"Catégorie: {icon} {trimmed}";

        return $"Catégorie: 🧩 {trimmed}";
    }

    private void Next()
    {
        if (_index >= _deck.Count)
        {
            Shuffle(_deck);
            _index = 0;
            ShowCurrent();
            return;
        }

        _index++;
        ShowCurrent();
    }

    private void UpdateScore() => _scoreLabel.Text = $"Score: {_save.Correct}/{_save.Total}";

    private void UpdateProgress()
    {
        if (_deck.Count <= 0)
        {
            _progressLabel.Text = "0/0";
            return;
        }

        var current = Math.Min(_index + 1, _deck.Count);
        _progressLabel.Text = $"{current}/{_deck.Count}";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed)
            return;

        // 1-4: choisir une réponse
        if (key.Keycode == Key.Key1) Choose(0);
        else if (key.Keycode == Key.Key2) Choose(1);
        else if (key.Keycode == Key.Key3) Choose(2);
        else if (key.Keycode == Key.Key4) Choose(3);

        // Entrée: suivant
        if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
        {
            if (!_nextButton.Disabled)
                Next();
        }
    }

    private void LoadSave()
    {
        try
        {
            if (!FileAccess.FileExists(SavePath)) return;
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            var json = f.GetAsText();
            var loaded = JsonSerializer.Deserialize<SaveData>(json);
            if (loaded != null) _save = loaded;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] LoadSave error: {e.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_save, new JsonSerializerOptions { WriteIndented = true });
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            f.StoreString(json);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] Save error: {e.Message}");
        }
    }

    private record CardQuestion(string Prompt, string[] Answers, int CorrectIndex, string Explanation, string Category);

    private class QuestionDeck
    {
        public string DeckId { get; set; }
        public string Title { get; set; }
        public QuestionItem[] Questions { get; set; }
    }

    private class QuestionItem
    {
        public string Category { get; set; }
        public string Prompt { get; set; }
        public string[] Answers { get; set; }
        public int CorrectIndex { get; set; }
        public string Explanation { get; set; }
    }

    private class SaveData
    {
        public int Total { get; set; }
        public int Correct { get; set; }
    }
}
