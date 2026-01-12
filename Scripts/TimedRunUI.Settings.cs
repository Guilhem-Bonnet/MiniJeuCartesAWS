#if false
#nullable enable

using Godot;
using System;
using System.Text.Json;

public partial class TimedRunUI : Control
{
    // ============================================================================
    // SETTINGS / OPTIONS
    // - UI du panneau settings
    // - persistance SettingsSave
    // - application runtime (audio + time limit)
    // ============================================================================

    private void ToggleSettings()
    {
        if (!IsInstanceValid(_settingsPanel))
            return;

        _settingsPanel.Visible = !_settingsPanel.Visible;
        if (_settingsPanel.Visible)
            ApplyRuntimeToSettingsUI();
    }

    private void HideSettings()
    {
        if (IsInstanceValid(_settingsPanel))
            _settingsPanel.Visible = false;
    }

    private void OnEnableAudioToggled(bool enabled)
    {
        EnableAudio = enabled;
        _settings.EnableAudio = enabled;
        SaveSettings();
        RefreshAudioFromSettings();
    }

    private void OnEnableAmbienceToggled(bool enabled)
    {
        EnableAmbience = enabled;
        _settings.EnableAmbience = enabled;
        SaveSettings();
        RefreshAudioFromSettings();
    }

    private void OnSfxVolumeChanged(double value)
    {
        SfxVolumeDb = (float)value;
        _settings.SfxVolumeDb = SfxVolumeDb;
        SaveSettings();
        RefreshAudioFromSettings();
    }

    private void OnAmbienceVolumeChanged(double value)
    {
        AmbienceVolumeDb = (float)value;
        _settings.AmbienceVolumeDb = AmbienceVolumeDb;
        SaveSettings();
        RefreshAudioFromSettings();
    }

    private void OnTimeLimitChanged(double value)
    {
        TimeLimitSeconds = (int)Math.Round(value);
        _settings.TimeLimitSeconds = TimeLimitSeconds;
        SaveSettings();

        // Met à jour l'affichage du timer quand on est au menu.
        if (!_runActive)
        {
            if (IsInstanceValid(_timerLabel))
                _timerLabel.Text = $"{BuildMarker} ⏱️ {FormatTime(TimeLimitSeconds)}";
            if (IsInstanceValid(_topTimerLabel))
                _topTimerLabel.Text = $"{BuildMarker} ⏱️ {FormatTime(TimeLimitSeconds)}";
        }
    }

    private void ApplyRuntimeToSettingsUI()
    {
        if (IsInstanceValid(_settingsEnableAudio))
            _settingsEnableAudio.ButtonPressed = EnableAudio;
        if (IsInstanceValid(_settingsEnableAmbience))
            _settingsEnableAmbience.ButtonPressed = EnableAmbience;
        if (IsInstanceValid(_settingsSfxVolume))
            _settingsSfxVolume.Value = SfxVolumeDb;
        if (IsInstanceValid(_settingsAmbienceVolume))
            _settingsAmbienceVolume.Value = AmbienceVolumeDb;
        if (IsInstanceValid(_settingsTimeLimit))
            _settingsTimeLimit.Value = TimeLimitSeconds;
    }

    private void LoadSettings()
    {
        _settings = new SettingsSave();

        try
        {
            if (!FileAccess.FileExists(SettingsPath))
                return;

            using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
            var json = f.GetAsText();
            var loaded = JsonSerializer.Deserialize<SettingsSave>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (loaded != null)
                _settings = loaded;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] Settings load error: {e.Message}");
            _settings = new SettingsSave();
        }
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings);
            using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
            f.StoreString(json);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] Settings save error: {e.Message}");
        }
    }

    private void ApplySettingsToRuntime()
    {
        EnableAudio = _settings.EnableAudio;
        EnableAmbience = _settings.EnableAmbience;
        if (_settings.TimeLimitSeconds > 0)
            TimeLimitSeconds = _settings.TimeLimitSeconds;

        // Volumes (0 dB est une valeur valide).
        SfxVolumeDb = _settings.SfxVolumeDb;
        AmbienceVolumeDb = _settings.AmbienceVolumeDb;
    }

    private void RefreshAudioFromSettings()
    {
        // Si l'audio est coupé, on stoppe tout de suite (sans détruire les nodes).
        if (!EnableAudio)
        {
            if (IsInstanceValid(_ambiencePlayer))
                _ambiencePlayer!.Stop();
            foreach (var p in _sfxPlayers)
            {
                if (IsInstanceValid(p))
                    p.Stop();
            }
            return;
        }

        EnsureAudio();

        // Applique volumes
        foreach (var p in _sfxPlayers)
        {
            if (IsInstanceValid(p))
                p.VolumeDb = SfxVolumeDb;
        }
        if (IsInstanceValid(_ambiencePlayer))
            _ambiencePlayer!.VolumeDb = AmbienceVolumeDb;

        // Applique ambiance (start/stop)
        if (!EnableAmbience)
        {
            if (IsInstanceValid(_ambiencePlayer))
                _ambiencePlayer!.Stop();
        }
        else
        {
            if (IsInstanceValid(_ambiencePlayer) && _ambiencePlayer!.Stream != null && !_ambiencePlayer.Playing)
                _ambiencePlayer.Play();
        }
    }

    private sealed class SettingsSave
    {
        public bool EnableAudio { get; set; } = true;
        public bool EnableAmbience { get; set; } = true;
        public float SfxVolumeDb { get; set; } = -7.0f;
        public float AmbienceVolumeDb { get; set; } = -18.0f;
        public int TimeLimitSeconds { get; set; } = 120;
    }
}

#endif
