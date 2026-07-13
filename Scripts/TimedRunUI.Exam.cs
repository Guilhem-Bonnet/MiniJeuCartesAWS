#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TimedRunUI : Control
{
    // ============================================================================
    // EXAMEN BLANC (mode Exam)
    // - Jeu fixe de 65 questions sans répétition, réparties selon les poids CLF-C02
    // - Mode strict : pas de correction pendant l'examen, on enchaîne directement
    // - Fin : score sur 100-1000 (seuil 700), répartition par domaine, questions ratées
    // ============================================================================

    private const int ExamQuestionCount = 65;
    private const int ExamPassScore = 700;
    private const int ExamMissedListMax = 8;

    private readonly List<Question> _examQueue = new();
    private int _examCursor;
    private readonly List<(Question Question, string ChosenText, bool Correct)> _examAnswers = new();

    private bool IsExamMode() => _selectedGameMode == GameMode.Exam;

    // Compose le jeu d'examen : quotas par domaine (méthode du plus fort reste sur
    // DomainWeights), complétés depuis le reste du deck si un domaine manque de questions.
    private void BuildExamSet()
    {
        _examQueue.Clear();
        _examCursor = 0;
        _examAnswers.Clear();

        if (_allQuestions.Count == 0 || _indicesByDomain.Count == 0)
            return;

        var quotas = ComputeExamQuotas(ExamQuestionCount);
        var taken = new HashSet<int>();

        foreach (var (domain, quota) in quotas)
        {
            if (!_indicesByDomain.TryGetValue(domain, out var list) || list.Count == 0)
                continue;

            var pool = list.ToList();
            ShuffleInPlace(pool);

            foreach (var idx in pool.Take(quota))
                taken.Add(idx);
        }

        // Complément si des domaines n'avaient pas assez de questions.
        if (taken.Count < Math.Min(ExamQuestionCount, _allQuestions.Count))
        {
            var rest = Enumerable.Range(0, _allQuestions.Count).Where(i => !taken.Contains(i)).ToList();
            ShuffleInPlace(rest);
            foreach (var idx in rest)
            {
                if (taken.Count >= ExamQuestionCount)
                    break;
                taken.Add(idx);
            }
        }

        var queue = taken.Select(i => _allQuestions[i]).ToList();
        ShuffleInPlace(queue);
        _examQueue.AddRange(queue);
    }

    private List<(string Domain, int Quota)> ComputeExamQuotas(int total)
    {
        var exact = DomainWeights.Select(d => (d.Domain, Exact: total * (double)d.Weight)).ToList();
        var quotas = exact.Select(e => (e.Domain, Quota: (int)Math.Floor(e.Exact))).ToList();

        var remaining = total - quotas.Sum(q => q.Quota);
        foreach (var e in exact.OrderByDescending(e => e.Exact - Math.Floor(e.Exact)))
        {
            if (remaining <= 0)
                break;
            var i = quotas.FindIndex(q => q.Domain == e.Domain);
            quotas[i] = (e.Domain, quotas[i].Quota + 1);
            remaining--;
        }

        return quotas;
    }

    private void ShuffleInPlace<T>(List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool TryDrawExamQuestion(out Question q)
    {
        q = null!;
        if (_examCursor >= _examQueue.Count)
            return false;

        q = _examQueue[_examCursor];
        _examCursor++;
        return true;
    }

    private void RecordExamAnswer(Question q, string chosenText, bool ok)
    {
        _examAnswers.Add((q, chosenText, ok));
    }

    // Score façon AWS : échelle 100-1000, les questions non répondues comptent fausses.
    private int ComputeExamScaledScore()
    {
        var total = Math.Max(1, _examQueue.Count);
        return 100 + (int)Math.Round(900.0 * _correct / total);
    }

    private void ShowExamResults(EndRunReason reason)
    {
        var total = _examQueue.Count;
        var score = ComputeExamScaledScore();
        var passed = score >= ExamPassScore;
        var unanswered = Math.Max(0, total - _answered);

        _panelTitleLabel.Text =
            $"🎓 Examen blanc — {(passed ? "REÇU ✅" : "RECALÉ ❌")}\n\n" +
            $"Score: {score}/1000 (seuil {ExamPassScore})";

        var body = $"Bonnes réponses: {_correct}/{total}";
        if (reason == EndRunReason.TimeExpired && unanswered > 0)
            body += $"\n⏳ Temps écoulé — {unanswered} question(s) non répondue(s), comptée(s) fausse(s).";

        body += "\n\n" + BuildDomainBreakdown();

        var missed = _examAnswers.Where(a => !a.Correct).ToList();
        if (missed.Count > 0)
        {
            body += $"\n\n❌ Questions ratées ({missed.Count}):";
            foreach (var m in missed.Take(ExamMissedListMax))
            {
                var prompt = m.Question.Prompt.Length > 90 ? m.Question.Prompt[..90] + "…" : m.Question.Prompt;
                body += $"\n• {prompt}\n  → {m.Question.CorrectAnswer}";
            }
            if (missed.Count > ExamMissedListMax)
                body += $"\n… et {missed.Count - ExamMissedListMax} autre(s). Revois-les en mode Renforcement.";
        }

        _panelBodyLabel.Text = body;
        _panelBodyLabel.SelfModulate = NeutralText;
    }
}
