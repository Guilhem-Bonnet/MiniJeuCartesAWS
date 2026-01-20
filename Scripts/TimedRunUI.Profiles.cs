#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class TimedRunUI : Control
{
    private const string ProfilesPath = "user://mini_jeu_cartes_aws_profiles.json";

    private ProfileStore _profileStore = new();
    private string _currentProfileId = "";

    private AcceptDialog? _newProfileDialog;
    private LineEdit? _newProfileNameEdit;

    private sealed class ProfileStore
    {
        public List<PlayerProfile> Profiles { get; set; } = new();
    }

    private sealed class PlayerProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Profil";

        public int RunsCount { get; set; }
        public int BestCorrect { get; set; }
        public int BestAnswered { get; set; }

        public int TotalCorrect { get; set; }
        public int TotalAnswered { get; set; }

        public List<RunRecord> History { get; set; } = new();
        public List<string> Achievements { get; set; } = new();

        // Stats détaillées pour adapter le tirage.
        public Dictionary<string, QuestionStats> QuestionStatsById { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, TopicStats> TopicStatsByKey { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class QuestionStats
    {
        public int Asked { get; set; }
        public int Correct { get; set; }
        public int WrongStreak { get; set; }
        public int CorrectStreak { get; set; }
        public long LastAskedUnixSeconds { get; set; }
        public long LastWrongUnixSeconds { get; set; }
        public long LastCorrectUnixSeconds { get; set; }
    }

    private sealed class TopicStats
    {
        public int Asked { get; set; }
        public int Correct { get; set; }
        public long LastAskedUnixSeconds { get; set; }
    }

    private sealed class RunRecord
    {
        public long UnixSeconds { get; set; }
        public int Correct { get; set; }
        public int Answered { get; set; }
        public string CertificationId { get; set; } = "";
        public int GameMode { get; set; }
    }

    private void LoadProfilesFromDisk()
    {
        try
        {
            if (!FileAccess.FileExists(ProfilesPath))
            {
                EnsureDefaultProfile();
                return;
            }

            using var f = FileAccess.Open(ProfilesPath, FileAccess.ModeFlags.Read);
            var json = f.GetAsText();
            var store = JsonSerializer.Deserialize<ProfileStore>(json);
            _profileStore = store ?? new ProfileStore();
        }
        catch (Exception e)
        {
            GD.PushWarning($"[MiniJeuCartesAWS] Load profiles failed: {e.Message}");
            _profileStore = new ProfileStore();
        }
        finally
        {
            EnsureDefaultProfile();
        }
    }

    private void SaveProfilesToDisk()
    {
        try
        {
            var json = JsonSerializer.Serialize(_profileStore, new JsonSerializerOptions { WriteIndented = true });
            using var f = FileAccess.Open(ProfilesPath, FileAccess.ModeFlags.Write);
            f.StoreString(json);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[MiniJeuCartesAWS] Save profiles failed: {e.Message}");
        }
    }

    private void EnsureDefaultProfile()
    {
        _profileStore.Profiles ??= new List<PlayerProfile>();

        if (_profileStore.Profiles.Count == 0)
        {
            var p = new PlayerProfile { Name = "Profil 1" };
            _profileStore.Profiles.Add(p);
            _currentProfileId = p.Id;
            SaveProfilesToDisk();
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentProfileId) || !_profileStore.Profiles.Any(p => p.Id == _currentProfileId))
            _currentProfileId = _profileStore.Profiles[0].Id;
    }

    private PlayerProfile GetCurrentProfile()
    {
        var p = _profileStore.Profiles.FirstOrDefault(x => x.Id == _currentProfileId);
        return p ?? _profileStore.Profiles[0];
    }

    private static bool IsReinforcementUnlocked(PlayerProfile p)
    {
        // Le renforcement a du sens quand on a un minimum de données.
        // (Assez permissif pour ne pas frustrer, mais évite un mode "vide".)
        if (p.TotalAnswered < 30)
            return false;

        var uniqueSeen = 0;
        if (p.QuestionStatsById != null)
            uniqueSeen = p.QuestionStatsById.Count(kv => kv.Value != null && kv.Value.Asked > 0);

        return uniqueSeen >= 12;
    }

    private void CreateProfile(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            trimmed = $"Profil {_profileStore.Profiles.Count + 1}";

        var p = new PlayerProfile { Name = trimmed };
        _profileStore.Profiles.Add(p);
        _currentProfileId = p.Id;
        SaveProfilesToDisk();
        SaveSettingsToDisk();

        RefreshProfileOption();
        UpdateReadyScreenCopyIfVisible();
    }

    private void RecordRunToCurrentProfile()
    {
        if (_answered <= 0)
            return;

        var p = GetCurrentProfile();
        p.RunsCount++;

        p.TotalAnswered += _answered;
        p.TotalCorrect += _correct;

        if (_correct > p.BestCorrect)
        {
            p.BestCorrect = _correct;
            p.BestAnswered = _answered;
        }

        p.History.Add(new RunRecord
        {
            UnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Correct = _correct,
            Answered = _answered,
            CertificationId = _selectedCertificationId,
            GameMode = (int)_selectedGameMode,
        });

        ApplyAchievements(p);
        SaveProfilesToDisk();
    }

    private void ApplyAchievements(PlayerProfile p)
    {
        var set = new HashSet<string>(p.Achievements ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        set.Add("premiere_partie");

        if (_answered >= 10 && _correct == _answered)
            set.Add("sans_faute_10");

        if (_correct >= 20)
            set.Add("20_bonnes_reponses");

        if (p.TotalAnswered >= 100)
            set.Add("100_questions_total");

        p.Achievements = set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private string BuildProfileSummaryText()
    {
        var p = GetCurrentProfile();
        var bestProfile = _profileStore.Profiles
            .OrderByDescending(x => x.BestCorrect)
            .ThenByDescending(x => x.RunsCount)
            .FirstOrDefault();

        var bestProfileLine = bestProfile == null
            ? "Meilleur profil : —"
            : $"Meilleur profil : {bestProfile.Name} ({bestProfile.BestCorrect}/{bestProfile.BestAnswered})";

        var acc = p.TotalAnswered <= 0 ? 0 : (int)Math.Round(100.0 * p.TotalCorrect / p.TotalAnswered);

        var bestAcc = p.BestAnswered <= 0 ? 0 : (int)Math.Round(100.0 * p.BestCorrect / p.BestAnswered);
        var best = p.BestAnswered <= 0 ? "—" : $"{p.BestCorrect}/{p.BestAnswered} ({bestAcc}%)";

        var trophies = p.Achievements?.Count ?? 0;

        var curve = BuildProgressCurve(p);

         return $"Profil : {p.Name}\n" +
             bestProfileLine + "\n" +
               $"Runs : {p.RunsCount}\n" +
               $"Meilleur : {best}\n" +
               $"Global : {p.TotalCorrect}/{p.TotalAnswered} ({acc}%)\n" +
               $"Trophées : {trophies}\n" +
               $"Progression : {curve}";
    }

    private static string BuildProgressCurve(PlayerProfile p)
    {
        if (p.History == null || p.History.Count == 0)
            return "—";

        var blocks = new[] { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };
        var last = p.History.TakeLast(10).ToList();

        char Map(double ratio)
        {
            ratio = Math.Clamp(ratio, 0.0, 1.0);
            var idx = (int)Math.Round(ratio * (blocks.Length - 1));
            idx = Math.Clamp(idx, 0, blocks.Length - 1);
            return blocks[idx];
        }

        var chars = new List<char>(last.Count);
        foreach (var r in last)
        {
            var ratio = r.Answered <= 0 ? 0.0 : (double)r.Correct / r.Answered;
            chars.Add(Map(ratio));
        }

        return new string(chars.ToArray());
    }

    private void EnsureNewProfileDialog()
    {
        if (IsInstanceValid(_newProfileDialog))
            return;

        _newProfileDialog = new AcceptDialog
        {
            Title = "Nouveau profil",
            DialogText = "Nom du profil :",
        };

        _newProfileNameEdit = new LineEdit
        {
            PlaceholderText = "Ex: Alice",
        };

        _newProfileDialog.AddChild(_newProfileNameEdit);
        AddChild(_newProfileDialog);

        _newProfileDialog.Confirmed += () =>
        {
            var name = IsInstanceValid(_newProfileNameEdit) ? _newProfileNameEdit!.Text : "";
            CreateProfile(name);
            if (IsInstanceValid(_newProfileNameEdit))
                _newProfileNameEdit!.Text = "";
        };
    }

    private void OpenNewProfileDialog()
    {
        EnsureNewProfileDialog();
        if (!IsInstanceValid(_newProfileDialog))
            return;

        _newProfileDialog!.PopupCentered();
        if (IsInstanceValid(_newProfileNameEdit))
        {
            _newProfileNameEdit!.GrabFocus();
            _newProfileNameEdit.CaretColumn = _newProfileNameEdit.Text.Length;
        }
    }

    private void RefreshProfileOption()
    {
        if (!IsInstanceValid(_menuProfile))
            return;

        _menuProfile!.Clear();
        for (var i = 0; i < _profileStore.Profiles.Count; i++)
        {
            var p = _profileStore.Profiles[i];
            _menuProfile.AddItem(p.Name, i);
            if (p.Id == _currentProfileId)
                _menuProfile.Selected = i;
        }
    }

    private void UpdateReadyScreenCopyIfVisible()
    {
        if (_runActive)
            return;
        if (!IsInstanceValid(_panelRoot) || !_panelRoot.Visible)
            return;

        // Rafraîchit simplement le texte de la zone "Body" si on est au menu.
        ShowReadyScreen();
    }
}
