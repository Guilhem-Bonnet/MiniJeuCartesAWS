#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    private OptionButton? _menuCertification;
    private OptionButton? _menuGameMode;
    private OptionButton? _menuTrainingDifficulty;
    private OptionButton? _menuProfile;
    private Button? _menuNewProfile;

    private readonly System.Collections.Generic.List<string> _menuCertificationIds = new();

    private Button? _menuSettingsButton;
    private Button? _menuRulesButton;
    private Button? _menuCoursesButton;
    private Button? _menuQuitButton;

    private bool _rulesVisible;

    private PopupPanel? _optionsPopup;
    private Button? _optionsApplyButton;
    private Button? _optionsCloseButton;

    private CheckButton? _optionsEnableAudio;
    private CheckButton? _optionsEnableAmbience;
    private HSlider? _optionsSfxVolume;
    private HSlider? _optionsAmbienceVolume;
    private SpinBox? _optionsTimeLimit;
    private SpinBox? _optionsExamTimeLimit;

    private CheckButton? _optionsFullscreen;
    private CheckButton? _optionsVsync;
    private OptionButton? _optionsResolution;

    private bool _pendingEnableAudio;
    private bool _pendingEnableAmbience;
    private float _pendingSfxVolumeDb;
    private float _pendingAmbienceVolumeDb;
    private int _pendingTimeLimitSeconds;
    private int _pendingExamTimeLimitSeconds;

    private bool _pendingDisplayFullscreen;
    private bool _pendingDisplayVsync;
    private Vector2I _pendingDisplayWindowSize;

    private ConfirmationDialog? _displayConfirmDialog;
    private int _displayConfirmToken;

    private static readonly Vector2I[] ResolutionPresets =
    {
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
        new(3840, 2160),
    };

    private void InitMenuAndSettingsUI()
    {
        // Profil
        _menuProfile = GetNodeOrNull<OptionButton>("Margin/Center/CardPanel/CardMargin/VBox/ProfileRow/Profile");
        _menuNewProfile = GetNodeOrNull<Button>("Margin/Center/CardPanel/CardMargin/VBox/ProfileRow/NewProfile");
        if (IsInstanceValid(_menuProfile))
        {
            _menuProfile!.ItemSelected -= OnProfileSelected;
            _menuProfile.ItemSelected += OnProfileSelected;
            RefreshProfileOption();
        }

        if (IsInstanceValid(_menuNewProfile))
        {
            _menuNewProfile!.Pressed -= OpenNewProfileDialog;
            _menuNewProfile.Pressed += OpenNewProfileDialog;
        }

        // Certification (deck)
        _menuCertification = GetNodeOrNull<OptionButton>("Margin/Center/CardPanel/CardMargin/VBox/ModeRow/Mode");
        if (IsInstanceValid(_menuCertification))
        {
            _menuCertificationIds.Clear();
            _menuCertification!.Clear();

            for (var i = 0; i < Certifications.Length; i++)
            {
                var c = Certifications[i];
                _menuCertificationIds.Add(c.Id);
                _menuCertification.AddItem(c.Label, i);
                if (string.Equals(c.Id, _selectedCertificationId, StringComparison.OrdinalIgnoreCase))
                    _menuCertification.Selected = i;
            }

            _menuCertification.Disabled = false;
            _menuCertification.ItemSelected -= OnCertificationSelected;
            _menuCertification.ItemSelected += OnCertificationSelected;
        }

        // Type de jeu
        _menuGameMode = GetNodeOrNull<OptionButton>("Margin/Center/CardPanel/CardMargin/VBox/GameRow/GameMode");
        if (IsInstanceValid(_menuGameMode))
        {
            RefreshGameModeOption();
        }

        // Difficulté (tirage)
        _menuTrainingDifficulty = GetNodeOrNull<OptionButton>("Margin/Center/CardPanel/CardMargin/VBox/DifficultyRow/Difficulty");
        if (IsInstanceValid(_menuTrainingDifficulty))
        {
            _menuTrainingDifficulty!.Clear();
            _menuTrainingDifficulty.AddItem("Débutant", (int)TrainingDifficulty.Beginner);
            _menuTrainingDifficulty.AddItem("Expert", (int)TrainingDifficulty.Expert);
            _menuTrainingDifficulty.AddItem("Maître", (int)TrainingDifficulty.Master);

            _menuTrainingDifficulty.Selected = (int)_selectedTrainingDifficulty;

            _menuTrainingDifficulty.ItemSelected -= OnTrainingDifficultySelected;
            _menuTrainingDifficulty.ItemSelected += OnTrainingDifficultySelected;
        }

        // Menu buttons
        _menuSettingsButton = GetNodeOrNull<Button>("Margin/Center/CardPanel/CardMargin/VBox/MenuRow/Settings");
        _menuRulesButton = GetNodeOrNull<Button>("Margin/Center/CardPanel/CardMargin/VBox/MenuRow/Rules");
        _menuCoursesButton = GetNodeOrNull<Button>("Margin/Center/CardPanel/CardMargin/VBox/MenuRow/Courses");
        _menuQuitButton = GetNodeOrNull<Button>("Margin/Center/CardPanel/CardMargin/VBox/MenuRow/Quit");

        if (!IsInstanceValid(_menuSettingsButton) || !IsInstanceValid(_menuQuitButton))
        {
            GD.PushWarning("[MiniJeuCartesAWS] Boutons menu introuvables (attendu: Margin/Center/CardPanel/.../MenuRow/Settings et Quit)");
        }

        if (IsInstanceValid(_menuSettingsButton))
        {
            // Events C# (sûrs): retirer puis ré-ajouter évite les doublons sans dépendre des signaux.
            _menuSettingsButton!.Pressed -= OpenOptionsPopup;
            _menuSettingsButton.Pressed += OpenOptionsPopup;
        }

        if (IsInstanceValid(_menuQuitButton))
        {
            _menuQuitButton!.Pressed -= QuitGame;
            _menuQuitButton.Pressed += QuitGame;
        }

        if (IsInstanceValid(_menuRulesButton))
        {
            _menuRulesButton!.Pressed -= ToggleRules;
            _menuRulesButton.Pressed += ToggleRules;
        }

        if (IsInstanceValid(_menuCoursesButton))
        {
            _menuCoursesButton!.Pressed -= OpenCourseOverlay;
            _menuCoursesButton.Pressed += OpenCourseOverlay;
        }

        // Options popup (fenêtre séparée)
        _optionsPopup = GetNodeOrNull<PopupPanel>("OptionsPopup");
        _optionsApplyButton = GetNodeOrNull<Button>("OptionsPopup/OptionsMargin/OptionsVBox/OptionsButtons/Apply");
        _optionsCloseButton = GetNodeOrNull<Button>("OptionsPopup/OptionsMargin/OptionsVBox/OptionsButtons/Close");

        _optionsEnableAudio = GetNodeOrNull<CheckButton>("OptionsPopup/OptionsMargin/OptionsVBox/EnableAudio");
        _optionsEnableAmbience = GetNodeOrNull<CheckButton>("OptionsPopup/OptionsMargin/OptionsVBox/EnableAmbience");
        _optionsSfxVolume = GetNodeOrNull<HSlider>("OptionsPopup/OptionsMargin/OptionsVBox/SfxRow/SfxVolume");
        _optionsAmbienceVolume = GetNodeOrNull<HSlider>("OptionsPopup/OptionsMargin/OptionsVBox/AmbienceRow/AmbienceVolume");
        _optionsTimeLimit = GetNodeOrNull<SpinBox>("OptionsPopup/OptionsMargin/OptionsVBox/TimeRow/TimeLimit");
        _optionsExamTimeLimit = GetNodeOrNull<SpinBox>("OptionsPopup/OptionsMargin/OptionsVBox/ExamTimeRow/ExamTimeLimit");

        _optionsFullscreen = GetNodeOrNull<CheckButton>("OptionsPopup/OptionsMargin/OptionsVBox/Fullscreen");
        _optionsVsync = GetNodeOrNull<CheckButton>("OptionsPopup/OptionsMargin/OptionsVBox/Vsync");
        _optionsResolution = GetNodeOrNull<OptionButton>("OptionsPopup/OptionsMargin/OptionsVBox/ResolutionRow/Resolution");

        if (IsInstanceValid(_optionsCloseButton))
        {
            _optionsCloseButton!.Pressed -= CloseOptionsPopup;
            _optionsCloseButton.Pressed += CloseOptionsPopup;
        }

        if (IsInstanceValid(_optionsApplyButton))
        {
            _optionsApplyButton!.Pressed -= ApplyOptions;
            _optionsApplyButton.Pressed += ApplyOptions;
        }

        if (IsInstanceValid(_optionsEnableAudio))
        {
            _optionsEnableAudio!.Toggled -= OnPendingEnableAudio;
            _optionsEnableAudio.Toggled += OnPendingEnableAudio;
        }

        if (IsInstanceValid(_optionsEnableAmbience))
        {
            _optionsEnableAmbience!.Toggled -= OnPendingEnableAmbience;
            _optionsEnableAmbience.Toggled += OnPendingEnableAmbience;
        }

        if (IsInstanceValid(_optionsSfxVolume))
        {
            _optionsSfxVolume!.ValueChanged -= OnPendingSfxVolume;
            _optionsSfxVolume.ValueChanged += OnPendingSfxVolume;
        }

        if (IsInstanceValid(_optionsAmbienceVolume))
        {
            _optionsAmbienceVolume!.ValueChanged -= OnPendingAmbienceVolume;
            _optionsAmbienceVolume.ValueChanged += OnPendingAmbienceVolume;
        }

        if (IsInstanceValid(_optionsTimeLimit))
        {
            _optionsTimeLimit!.ValueChanged -= OnPendingTimeLimit;
            _optionsTimeLimit.ValueChanged += OnPendingTimeLimit;
        }

        if (IsInstanceValid(_optionsExamTimeLimit))
        {
            _optionsExamTimeLimit!.ValueChanged -= OnPendingExamTimeLimit;
            _optionsExamTimeLimit.ValueChanged += OnPendingExamTimeLimit;
        }

        if (IsInstanceValid(_optionsFullscreen))
        {
            _optionsFullscreen!.Toggled -= OnPendingFullscreen;
            _optionsFullscreen.Toggled += OnPendingFullscreen;
        }

        if (IsInstanceValid(_optionsVsync))
        {
            _optionsVsync!.Toggled -= OnPendingVsync;
            _optionsVsync.Toggled += OnPendingVsync;
        }

        if (IsInstanceValid(_optionsResolution))
        {
            _optionsResolution!.ItemSelected -= OnPendingResolution;
            _optionsResolution.ItemSelected += OnPendingResolution;

            _optionsResolution.Clear();
            for (var i = 0; i < ResolutionPresets.Length; i++)
            {
                var r = ResolutionPresets[i];
                _optionsResolution.AddItem($"{r.X}×{r.Y}", i);
            }
        }

        HookMenuHover(_restartButton, _menuSettingsButton, _menuQuitButton);

        PullRuntimeIntoPending();
        ApplyPendingToOptionsUI();
    }

    private void OnTrainingDifficultySelected(long index)
    {
        var v = (int)Math.Clamp(index, 0, 2);
        _selectedTrainingDifficulty = (TrainingDifficulty)v;
        SaveSettingsToDisk();
        UpdateReadyScreenCopyIfVisible();
    }

    private void OnProfileSelected(long index)
    {
        if (index < 0 || index >= _profileStore.Profiles.Count)
            return;

        _currentProfileId = _profileStore.Profiles[(int)index].Id;
        SaveSettingsToDisk();
        RefreshGameModeOption();
        UpdateReadyScreenCopyIfVisible();
    }

    private void OnCertificationSelected(long index)
    {
        if (index < 0 || index >= _menuCertificationIds.Count)
            return;

        _selectedCertificationId = _menuCertificationIds[(int)index];
        SaveSettingsToDisk();

        LoadDeck();
        UpdateReadyScreenCopyIfVisible();
    }

    private void OnGameModeSelected(long index)
    {
        if (!IsInstanceValid(_menuGameMode))
            return;

        var idx = (int)index;
        if (idx < 0 || idx >= _menuGameMode!.ItemCount)
            return;

        var id = _menuGameMode.GetItemId(idx);
        var mode = (GameMode)Math.Clamp(id, 0, 3);

        if (mode == GameMode.Reinforcement && !IsReinforcementUnlocked(GetCurrentProfile()))
        {
            // Verrouillé => on revient en Chrono.
            _selectedGameMode = GameMode.Chrono;
            _menuGameMode.Selected = FindGameModeIndex(_menuGameMode, (int)_selectedGameMode);
            SaveSettingsToDisk();
            UpdateReadyScreenCopyIfVisible();
            return;
        }

        _selectedGameMode = mode;
        SaveSettingsToDisk();
        UpdateReadyScreenCopyIfVisible();
    }

    private void RefreshGameModeOption()
    {
        if (!IsInstanceValid(_menuGameMode))
            return;

        var unlocked = IsReinforcementUnlocked(GetCurrentProfile());

        _menuGameMode!.ItemSelected -= OnGameModeSelected;
        _menuGameMode.Clear();

        _menuGameMode.AddItem(GetGameModeLabel(GameMode.Chrono), (int)GameMode.Chrono);
        _menuGameMode.AddItem(GetGameModeLabel(GameMode.Infinite), (int)GameMode.Infinite);
        _menuGameMode.AddItem(GetGameModeLabel(GameMode.Exam), (int)GameMode.Exam);

        var reinforcementLabel = unlocked
            ? GetGameModeLabel(GameMode.Reinforcement)
            : $"{GetGameModeLabel(GameMode.Reinforcement)} (verrouillé)";
        _menuGameMode.AddItem(reinforcementLabel, (int)GameMode.Reinforcement);

        // Si on était en renforcement mais que le profil ne le permet plus (profil switch), fallback.
        if (_selectedGameMode == GameMode.Reinforcement && !unlocked)
            _selectedGameMode = GameMode.Chrono;

        _menuGameMode.Selected = FindGameModeIndex(_menuGameMode, (int)_selectedGameMode);
        _menuGameMode.ItemSelected += OnGameModeSelected;
    }

    private static int FindGameModeIndex(OptionButton b, int itemId)
    {
        for (var i = 0; i < b.ItemCount; i++)
        {
            if (b.GetItemId(i) == itemId)
                return i;
        }
        return 0;
    }

    private void ToggleRules()
    {
        if (_runActive)
            return;

        _rulesVisible = !_rulesVisible;

        HideSettingsPanel();

        if (!_rulesVisible)
        {
            ShowReadyScreen();
            return;
        }

        if (IsInstanceValid(_panelRoot))
            _panelRoot.Visible = true;

        if (IsInstanceValid(_panelTitleLabel))
            _panelTitleLabel.Text = "Règles du jeu";

        if (IsInstanceValid(_panelBodyLabel))
        {
            _panelBodyLabel.Text =
                "• Objectif : répondre à un maximum de questions.\n" +
                $"• Type : {GetSelectedGameModeLabel()} (la durée dépend du mode).\n" +
                "• Répondre : touches 1–4, ou clic sur la carte.\n" +
                "• Après réponse : la carte se retourne (explication), puis clique pour continuer.\n\n" +
                "Conseil : reste chill, vise la régularité plutôt que la vitesse.";
            _panelBodyLabel.SelfModulate = NeutralText;
        }
    }

    private void ToggleSettingsPanel()
    {
        OpenOptionsPopup();
    }

    private void HideSettingsPanel()
    {
        CloseOptionsPopup();
    }

    private void OpenOptionsPopup()
    {
        if (!IsInstanceValid(_optionsPopup))
            return;

        PullRuntimeIntoPending();
        ApplyPendingToOptionsUI();

        _optionsPopup!.PopupCentered(new Vector2I(920, 760));
    }

    private void CloseOptionsPopup()
    {
        if (IsInstanceValid(_optionsPopup))
            _optionsPopup!.Hide();
    }

    private void PullRuntimeIntoPending()
    {
        _pendingEnableAudio = EnableAudio;
        _pendingEnableAmbience = EnableAmbience;
        _pendingSfxVolumeDb = SfxVolumeDb;
        _pendingAmbienceVolumeDb = AmbienceVolumeDb;
        _pendingTimeLimitSeconds = TimeLimitSeconds;
        _pendingExamTimeLimitSeconds = ExamTimeLimitSeconds;

        _pendingDisplayFullscreen = _displayFullscreen;
        _pendingDisplayVsync = _displayVsync;
        _pendingDisplayWindowSize = _displayWindowSize;
    }

    private void ApplyPendingToOptionsUI()
    {
        if (IsInstanceValid(_optionsEnableAudio))
            _optionsEnableAudio!.ButtonPressed = _pendingEnableAudio;
        if (IsInstanceValid(_optionsEnableAmbience))
            _optionsEnableAmbience!.ButtonPressed = _pendingEnableAmbience;
        if (IsInstanceValid(_optionsSfxVolume))
            _optionsSfxVolume!.Value = _pendingSfxVolumeDb;
        if (IsInstanceValid(_optionsAmbienceVolume))
            _optionsAmbienceVolume!.Value = _pendingAmbienceVolumeDb;
        if (IsInstanceValid(_optionsTimeLimit))
            _optionsTimeLimit!.Value = _pendingTimeLimitSeconds;
        if (IsInstanceValid(_optionsExamTimeLimit))
            _optionsExamTimeLimit!.Value = _pendingExamTimeLimitSeconds;

        if (IsInstanceValid(_optionsFullscreen))
            _optionsFullscreen!.ButtonPressed = _pendingDisplayFullscreen;
        if (IsInstanceValid(_optionsVsync))
            _optionsVsync!.ButtonPressed = _pendingDisplayVsync;
        if (IsInstanceValid(_optionsResolution))
            _optionsResolution!.Selected = FindResolutionPresetIndex(_pendingDisplayWindowSize);
    }

    private void OnPendingEnableAudio(bool enabled) => _pendingEnableAudio = enabled;
    private void OnPendingEnableAmbience(bool enabled) => _pendingEnableAmbience = enabled;
    private void OnPendingSfxVolume(double value) => _pendingSfxVolumeDb = (float)value;
    private void OnPendingAmbienceVolume(double value) => _pendingAmbienceVolumeDb = (float)value;
    private void OnPendingTimeLimit(double value) => _pendingTimeLimitSeconds = (int)Math.Round(value);
    private void OnPendingExamTimeLimit(double value) => _pendingExamTimeLimitSeconds = (int)Math.Round(value);
    private void OnPendingFullscreen(bool enabled) => _pendingDisplayFullscreen = enabled;
    private void OnPendingVsync(bool enabled) => _pendingDisplayVsync = enabled;

    private void OnPendingResolution(long index)
    {
        var i = (int)index;
        if (i < 0 || i >= ResolutionPresets.Length)
            return;
        _pendingDisplayWindowSize = ResolutionPresets[i];
    }

    private void ApplyOptions()
    {
        // Non-affichage: application immédiate.
        EnableAudio = _pendingEnableAudio;
        EnableAmbience = _pendingEnableAmbience;
        SfxVolumeDb = _pendingSfxVolumeDb;
        AmbienceVolumeDb = _pendingAmbienceVolumeDb;
        TimeLimitSeconds = _pendingTimeLimitSeconds;
        ExamTimeLimitSeconds = _pendingExamTimeLimitSeconds;
        EnsureAudio();
        SaveSettingsToDisk();
        UpdateReadyScreenCopyIfVisible();

        // Affichage: nécessite confirmation avec rollback auto.
        var displayChanged = _pendingDisplayFullscreen != _displayFullscreen
            || _pendingDisplayVsync != _displayVsync
            || _pendingDisplayWindowSize != _displayWindowSize;

        if (!displayChanged)
        {
            CloseOptionsPopup();
            return;
        }

        var prevFullscreen = _displayFullscreen;
        var prevVsync = _displayVsync;
        var prevSize = _displayWindowSize;

        ApplyDisplayPreview(_pendingDisplayFullscreen, _pendingDisplayVsync, _pendingDisplayWindowSize);
        AskDisplayConfirmation(prevFullscreen, prevVsync, prevSize);
    }

    private void ApplyDisplayPreview(bool fullscreen, bool vsync, Vector2I size)
    {
        DisplayServer.WindowSetVsyncMode(vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        if (fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
        else
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(size);
        }
    }

    private void AskDisplayConfirmation(bool prevFullscreen, bool prevVsync, Vector2I prevSize)
    {
        EnsureDisplayConfirmDialog();
        if (!IsInstanceValid(_displayConfirmDialog))
            return;

        _displayConfirmToken++;
        var token = _displayConfirmToken;

        var dlg = _displayConfirmDialog!;
        dlg.Title = "Confirmer l'affichage";
        dlg.DialogText = "Garder ces paramètres d'affichage ?";
        dlg.GetOkButton().Text = "Confirmer (5)";
        dlg.GetCancelButton().Text = "Annuler";

        void Revert()
        {
            if (token != _displayConfirmToken)
                return;
            ApplyDisplayPreview(prevFullscreen, prevVsync, prevSize);
            PullRuntimeIntoPending();
            ApplyPendingToOptionsUI();
        }

        void Commit()
        {
            if (token != _displayConfirmToken)
                return;
            _displayFullscreen = _pendingDisplayFullscreen;
            _displayVsync = _pendingDisplayVsync;
            _displayWindowSize = _pendingDisplayWindowSize;
            ApplyDisplaySettings();
            SaveSettingsToDisk();
        }

        dlg.Confirmed -= Commit;
        dlg.Canceled -= Revert;
        dlg.Confirmed += Commit;
        dlg.Canceled += Revert;

        dlg.PopupCentered();
        _ = RunDisplayConfirmCountdown(token, Revert);
    }

    private async System.Threading.Tasks.Task RunDisplayConfirmCountdown(int token, Action onTimeout)
    {
        var dlg = _displayConfirmDialog;
        if (!IsInstanceValid(dlg))
            return;

        for (var remaining = 5; remaining >= 1; remaining--)
        {
            if (token != _displayConfirmToken)
                return;

            if (IsInstanceValid(dlg))
                dlg!.GetOkButton().Text = $"Confirmer ({remaining})";

            var t = GetTree().CreateTimer(1.0);
            await ToSignal(t, SceneTreeTimer.SignalName.Timeout);
        }

        if (token != _displayConfirmToken)
            return;

        if (IsInstanceValid(dlg) && dlg!.Visible)
            dlg.Hide();

        onTimeout();
    }

    private void EnsureDisplayConfirmDialog()
    {
        if (IsInstanceValid(_displayConfirmDialog))
            return;

        _displayConfirmDialog = new ConfirmationDialog();
        AddChild(_displayConfirmDialog);
    }

    private static int FindResolutionPresetIndex(Vector2I size)
    {
        for (var i = 0; i < ResolutionPresets.Length; i++)
        {
            if (ResolutionPresets[i].X == size.X && ResolutionPresets[i].Y == size.Y)
                return i;
        }

        // Si la taille actuelle n'est pas un preset, on retombe sur 1080p.
        for (var i = 0; i < ResolutionPresets.Length; i++)
        {
            if (ResolutionPresets[i].X == 1920 && ResolutionPresets[i].Y == 1080)
                return i;
        }
        return 0;
    }

    // Les handlers instantanés (ancienne UI) ont été remplacés par des valeurs "pending" + Apply.

    private void QuitGame()
    {
        GetTree().Quit();
    }
}
