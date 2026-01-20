#nullable enable

using Godot;
using System;
using System.Text.Json;

public partial class TimedRunUI : Control
{
    private const string SettingsPath = "user://mini_jeu_cartes_aws_settings.json";

    // Affichage (persisté)
    private bool _displayFullscreen;
    private bool _displayVsync = true;
    private Vector2I _displayWindowSize = new(1920, 1080);

    private sealed class SettingsData
    {
        public bool EnableAudio { get; set; } = true;
        public bool EnableAmbience { get; set; } = true;
        public float SfxVolumeDb { get; set; } = -7.0f;
        public float AmbienceVolumeDb { get; set; } = -18.0f;
        public int TimeLimitSeconds { get; set; } = 120;

        public string CertificationId { get; set; } = "aws-ccp-fr";
        public int GameMode { get; set; } = 0;
        public int TrainingDifficulty { get; set; } = 0;
        public int ExamTimeLimitSeconds { get; set; } = 3600;
        public string CurrentProfileId { get; set; } = "";

        public bool DisplayFullscreen { get; set; }
        public bool DisplayVsync { get; set; } = true;
        public int WindowWidth { get; set; } = 1920;
        public int WindowHeight { get; set; } = 1080;
    }

    private void LoadSettingsFromDiskAndApply()
    {
        try
        {
            if (!FileAccess.FileExists(SettingsPath))
            {
                ApplyDisplaySettings();
                return;
            }

            using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
            var json = f.GetAsText();
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data == null)
            {
                ApplyDisplaySettings();
                return;
            }

            EnableAudio = data.EnableAudio;
            EnableAmbience = data.EnableAmbience;
            SfxVolumeDb = Math.Clamp(data.SfxVolumeDb, -40.0f, 6.0f);
            AmbienceVolumeDb = Math.Clamp(data.AmbienceVolumeDb, -40.0f, 6.0f);
            TimeLimitSeconds = Math.Clamp(data.TimeLimitSeconds, 10, 3600);

            _selectedCertificationId = string.IsNullOrWhiteSpace(data.CertificationId)
                ? _selectedCertificationId
                : data.CertificationId;
            _selectedGameMode = (GameMode)Math.Clamp(data.GameMode, 0, 3);
            _selectedTrainingDifficulty = (TrainingDifficulty)Math.Clamp(data.TrainingDifficulty, 0, 2);
            ExamTimeLimitSeconds = Math.Clamp(data.ExamTimeLimitSeconds, 60, 8 * 3600);
            _currentProfileId = data.CurrentProfileId ?? "";

            _displayFullscreen = data.DisplayFullscreen;
            _displayVsync = data.DisplayVsync;

            var w = Math.Clamp(data.WindowWidth, 800, 7680);
            var h = Math.Clamp(data.WindowHeight, 600, 4320);
            _displayWindowSize = new Vector2I(w, h);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[MiniJeuCartesAWS] Load settings failed: {e.Message}");
        }
        finally
        {
            ApplyDisplaySettings();
        }
    }

    private void SaveSettingsToDisk()
    {
        try
        {
            var data = new SettingsData
            {
                EnableAudio = EnableAudio,
                EnableAmbience = EnableAmbience,
                SfxVolumeDb = SfxVolumeDb,
                AmbienceVolumeDb = AmbienceVolumeDb,
                TimeLimitSeconds = TimeLimitSeconds,

                CertificationId = _selectedCertificationId,
                GameMode = (int)_selectedGameMode,
                TrainingDifficulty = (int)_selectedTrainingDifficulty,
                ExamTimeLimitSeconds = ExamTimeLimitSeconds,
                CurrentProfileId = _currentProfileId,

                DisplayFullscreen = _displayFullscreen,
                DisplayVsync = _displayVsync,
                WindowWidth = _displayWindowSize.X,
                WindowHeight = _displayWindowSize.Y,
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
            f.StoreString(json);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[MiniJeuCartesAWS] Save settings failed: {e.Message}");
        }
    }

    private void ApplyDisplaySettings()
    {
        try
        {
            DisplayServer.WindowSetVsyncMode(_displayVsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);

            if (_displayFullscreen)
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            }
            else
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetSize(_displayWindowSize);
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"[MiniJeuCartesAWS] Apply display settings failed: {e.Message}");
        }
    }

    private void SetDisplayFullscreen(bool enabled)
    {
        _displayFullscreen = enabled;
        ApplyDisplaySettings();
        SaveSettingsToDisk();
    }

    private void SetDisplayVsync(bool enabled)
    {
        _displayVsync = enabled;
        ApplyDisplaySettings();
        SaveSettingsToDisk();
    }

    private void SetDisplayWindowSize(Vector2I size)
    {
        _displayWindowSize = size;
        ApplyDisplaySettings();
        SaveSettingsToDisk();
    }
}
