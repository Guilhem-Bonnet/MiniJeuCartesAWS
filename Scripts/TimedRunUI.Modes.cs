#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    private enum TrainingDifficulty
    {
        Beginner = 0,
        Expert = 1,
        Master = 2,
    }

    private enum GameMode
    {
        Chrono = 0,
        Infinite = 1,
        Exam = 2,
        Reinforcement = 3,
    }

    private sealed record CertificationDef(string Id, string Label, string DeckPath);

    private static readonly CertificationDef[] Certifications =
    {
        new("aws-ccp-fr", "AWS Cloud Practitioner (FR)", "res://Data/questions_practitioner.json"),
    };

    [Export] public int ExamTimeLimitSeconds { get; set; } = 3600;

    private string _selectedCertificationId = Certifications[0].Id;
    private GameMode _selectedGameMode = GameMode.Chrono;
    private TrainingDifficulty _selectedTrainingDifficulty = TrainingDifficulty.Beginner;

    private CertificationDef GetSelectedCertification()
    {
        foreach (var c in Certifications)
        {
            if (string.Equals(c.Id, _selectedCertificationId, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return Certifications[0];
    }

    private string GetSelectedCertificationLabel() => GetSelectedCertification().Label;

    private string GetActiveDeckPath() => GetSelectedCertification().DeckPath;

    private double GetInitialTimeLimitForSelectedMode()
    {
        return _selectedGameMode switch
        {
            GameMode.Infinite => double.PositiveInfinity,
            GameMode.Exam => Math.Clamp(ExamTimeLimitSeconds, 60, 8 * 3600),
            GameMode.Reinforcement => double.PositiveInfinity,
            _ => Math.Clamp(TimeLimitSeconds, 10, 3600),
        };
    }

    private string GetGameModeLabel(GameMode mode)
    {
        return mode switch
        {
            GameMode.Infinite => "Infini",
            GameMode.Exam => "Examen",
            GameMode.Reinforcement => "Renforcement",
            _ => "Chrono",
        };
    }

    private string GetSelectedGameModeLabel() => GetGameModeLabel(_selectedGameMode);

    private string GetTrainingDifficultyLabel(TrainingDifficulty d)
    {
        return d switch
        {
            TrainingDifficulty.Master => "Maître",
            TrainingDifficulty.Expert => "Expert",
            _ => "Débutant",
        };
    }

    private string GetSelectedTrainingDifficultyLabel() => GetTrainingDifficultyLabel(_selectedTrainingDifficulty);
}
