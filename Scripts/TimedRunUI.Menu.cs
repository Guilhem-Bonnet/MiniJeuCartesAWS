#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    private Button? _menuSettingsButton;
    private Button? _menuQuitButton;

    private PanelContainer? _settingsPanel;
    private Button? _settingsCloseButton;
    private CheckButton? _settingsEnableAudio;
    private CheckButton? _settingsEnableAmbience;
    private HSlider? _settingsSfxVolume;
    private HSlider? _settingsAmbienceVolume;
    private SpinBox? _settingsTimeLimit;

    private void InitMenuAndSettingsUI()
    {
        // Menu buttons
        _menuSettingsButton = GetNodeOrNull<Button>("Margin/Center/CardPanel/CardMargin/VBox/MenuRow/Settings");
        _menuQuitButton = GetNodeOrNull<Button>("Margin/Center/CardPanel/CardMargin/VBox/MenuRow/Quit");

        if (IsInstanceValid(_menuSettingsButton))
        {
            _menuSettingsButton!.Pressed -= ToggleSettingsPanel;
            _menuSettingsButton!.Pressed += ToggleSettingsPanel;
        }

        if (IsInstanceValid(_menuQuitButton))
        {
            _menuQuitButton!.Pressed -= QuitGame;
            _menuQuitButton!.Pressed += QuitGame;
        }

        // Settings panel
        _settingsPanel = GetNodeOrNull<PanelContainer>("Margin/Center/CardPanel/CardMargin/VBox/SettingsPanel");
        _settingsCloseButton = GetNodeOrNull<Button>("Margin/Center/CardPanel/CardMargin/VBox/SettingsPanel/SettingsMargin/SettingsVBox/Close");
        _settingsEnableAudio = GetNodeOrNull<CheckButton>("Margin/Center/CardPanel/CardMargin/VBox/SettingsPanel/SettingsMargin/SettingsVBox/EnableAudio");
        _settingsEnableAmbience = GetNodeOrNull<CheckButton>("Margin/Center/CardPanel/CardMargin/VBox/SettingsPanel/SettingsMargin/SettingsVBox/EnableAmbience");
        _settingsSfxVolume = GetNodeOrNull<HSlider>("Margin/Center/CardPanel/CardMargin/VBox/SettingsPanel/SettingsMargin/SettingsVBox/SfxRow/SfxVolume");
        _settingsAmbienceVolume = GetNodeOrNull<HSlider>("Margin/Center/CardPanel/CardMargin/VBox/SettingsPanel/SettingsMargin/SettingsVBox/AmbienceRow/AmbienceVolume");
        _settingsTimeLimit = GetNodeOrNull<SpinBox>("Margin/Center/CardPanel/CardMargin/VBox/SettingsPanel/SettingsMargin/SettingsVBox/TimeRow/TimeLimit");

        if (IsInstanceValid(_settingsCloseButton))
        {
            _settingsCloseButton!.Pressed -= HideSettingsPanel;
            _settingsCloseButton!.Pressed += HideSettingsPanel;
        }

        if (IsInstanceValid(_settingsEnableAudio))
        {
            _settingsEnableAudio!.Toggled -= OnEnableAudioToggled;
            _settingsEnableAudio!.Toggled += OnEnableAudioToggled;
        }

        if (IsInstanceValid(_settingsEnableAmbience))
        {
            _settingsEnableAmbience!.Toggled -= OnEnableAmbienceToggled;
            _settingsEnableAmbience!.Toggled += OnEnableAmbienceToggled;
        }

        if (IsInstanceValid(_settingsSfxVolume))
        {
            _settingsSfxVolume!.ValueChanged -= OnSfxVolumeChanged;
            _settingsSfxVolume!.ValueChanged += OnSfxVolumeChanged;
        }

        if (IsInstanceValid(_settingsAmbienceVolume))
        {
            _settingsAmbienceVolume!.ValueChanged -= OnAmbienceVolumeChanged;
            _settingsAmbienceVolume!.ValueChanged += OnAmbienceVolumeChanged;
        }

        if (IsInstanceValid(_settingsTimeLimit))
        {
            _settingsTimeLimit!.ValueChanged -= OnTimeLimitChanged;
            _settingsTimeLimit!.ValueChanged += OnTimeLimitChanged;
        }

        ApplyRuntimeToSettingsUI();
    }

    private void ToggleSettingsPanel()
    {
        if (!IsInstanceValid(_settingsPanel))
            return;

        _settingsPanel!.Visible = !_settingsPanel.Visible;
        if (_settingsPanel.Visible)
            ApplyRuntimeToSettingsUI();
    }

    private void HideSettingsPanel()
    {
        if (IsInstanceValid(_settingsPanel))
            _settingsPanel!.Visible = false;
    }

    private void ApplyRuntimeToSettingsUI()
    {
        if (IsInstanceValid(_settingsEnableAudio))
            _settingsEnableAudio!.ButtonPressed = EnableAudio;
        if (IsInstanceValid(_settingsEnableAmbience))
            _settingsEnableAmbience!.ButtonPressed = EnableAmbience;
        if (IsInstanceValid(_settingsSfxVolume))
            _settingsSfxVolume!.Value = SfxVolumeDb;
        if (IsInstanceValid(_settingsAmbienceVolume))
            _settingsAmbienceVolume!.Value = AmbienceVolumeDb;
        if (IsInstanceValid(_settingsTimeLimit))
            _settingsTimeLimit!.Value = TimeLimitSeconds;
    }

    private void OnEnableAudioToggled(bool enabled)
    {
        EnableAudio = enabled;
        EnsureAudio();
    }

    private void OnEnableAmbienceToggled(bool enabled)
    {
        EnableAmbience = enabled;
        EnsureAudio();
    }

    private void OnSfxVolumeChanged(double value)
    {
        SfxVolumeDb = (float)value;
        EnsureAudio();
    }

    private void OnAmbienceVolumeChanged(double value)
    {
        AmbienceVolumeDb = (float)value;
        EnsureAudio();
    }

    private void OnTimeLimitChanged(double value)
    {
        TimeLimitSeconds = (int)Math.Round(value);

        // Met à jour l'affichage du timer quand on est au menu.
        if (!_runActive)
        {
            if (IsInstanceValid(_timerLabel))
                _timerLabel.Text = $"{BuildMarker} ⏱️ {FormatTime(TimeLimitSeconds)}";
            if (IsInstanceValid(_topTimerLabel))
                _topTimerLabel.Text = $"{BuildMarker} ⏱️ {FormatTime(TimeLimitSeconds)}";
        }
    }

    private void QuitGame()
    {
        GetTree().Quit();
    }
}
