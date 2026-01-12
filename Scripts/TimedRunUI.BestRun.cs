#if false
#nullable enable

using Godot;
using System;
using System.Text.Json;

public partial class TimedRunUI : Control
{
    // ============================================================================
    // PERSISTENCE: MEILLEUR RUN
    // ============================================================================

    private void LoadBestRun()
    {
        _bestRun = new BestRunSave();

        try
        {
            if (!FileAccess.FileExists(BestRunPath))
                return;

            using var f = FileAccess.Open(BestRunPath, FileAccess.ModeFlags.Read);
            var json = f.GetAsText();
            var loaded = JsonSerializer.Deserialize<BestRunSave>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (loaded != null)
                _bestRun = loaded;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] BestRun load error: {e.Message}");
            _bestRun = new BestRunSave();
        }
    }

    private void SaveBestRun()
    {
        try
        {
            var json = JsonSerializer.Serialize(_bestRun);
            using var f = FileAccess.Open(BestRunPath, FileAccess.ModeFlags.Write);
            f.StoreString(json);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] BestRun save error: {e.Message}");
        }
    }

    private bool TryUpdateBestRun(int correct, int answered)
    {
        if (answered <= 0)
            return false;

        if (_bestRun.BestAnswered <= 0)
        {
            _bestRun = new BestRunSave { BestCorrect = correct, BestAnswered = answered };
            SaveBestRun();
            return true;
        }

        if (!IsRunBetter(correct, answered, _bestRun.BestCorrect, _bestRun.BestAnswered))
            return false;

        _bestRun = new BestRunSave { BestCorrect = correct, BestAnswered = answered };
        SaveBestRun();
        return true;
    }

    private static bool IsRunBetter(int correctA, int answeredA, int correctB, int answeredB)
    {
        if (correctA != correctB)
            return correctA > correctB;

        var accA = CalcAccuracyPercent(correctA, answeredA);
        var accB = CalcAccuracyPercent(correctB, answeredB);
        if (accA != accB)
            return accA > accB;

        return answeredA > answeredB;
    }

    private static int CalcAccuracyPercent(int correct, int answered)
    {
        if (answered <= 0)
            return 0;
        return (int)Math.Round(100.0 * correct / answered);
    }

    private sealed class BestRunSave
    {
        public int BestCorrect { get; set; }
        public int BestAnswered { get; set; }
    }
}

#endif
