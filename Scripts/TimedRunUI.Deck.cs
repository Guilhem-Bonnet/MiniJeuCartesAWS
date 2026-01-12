#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class TimedRunUI : Control
{
    // ============================================================================
    // DECK / QUESTIONS
    // - Chargement JSON
    // - Indexation par domaine
    // - Tirage pondéré + anti-répétition
    // ============================================================================

    private void LoadDeck()
    {
        _allQuestions.Clear();
        _indicesByDomain.Clear();
        _usedByDomain.Clear();

        try
        {
            if (!ResourceLoader.Exists(DeckPath))
            {
                GD.PrintErr($"[MiniJeuCartesAWS] Deck missing: {DeckPath}");
                BuildFallbackDeck();
                IndexDeck();
                return;
            }

            using var f = FileAccess.Open(DeckPath, FileAccess.ModeFlags.Read);
            var json = f.GetAsText();

            var deck = JsonSerializer.Deserialize<QuestionDeck>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (deck?.Questions == null || deck.Questions.Length == 0)
            {
                BuildFallbackDeck();
                IndexDeck();
                return;
            }

            foreach (var q in deck.Questions)
            {
                if (q.Answers == null || q.Answers.Length != 4)
                    continue;
                if (q.CorrectIndex < 0 || q.CorrectIndex > 3)
                    continue;
                if (string.IsNullOrWhiteSpace(q.Prompt))
                    continue;
                if (q.Answers.Any(string.IsNullOrWhiteSpace))
                    continue;

                var domain = NormalizeDomain(q.Domain, q.Category);
                var difficulty = Math.Clamp(q.Difficulty <= 0 ? 1 : q.Difficulty, 1, 3);
                var correctAnswer = q.Answers[q.CorrectIndex];

                _allQuestions.Add(new Question(
                    domain,
                    difficulty,
                    q.Prompt,
                    q.Answers,
                    correctAnswer,
                    q.Explanation ?? ""));
            }

            if (_allQuestions.Count == 0)
                BuildFallbackDeck();

            IndexDeck();
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] Deck load error: {e.Message}");
            BuildFallbackDeck();
            IndexDeck();
        }
    }

    private bool TryDrawQuestionWeighted(out Question q)
    {
        q = null!;

        // Si un domaine est épuisé (toutes questions marquées utilisées), on le recycle.
        foreach (var kv in _indicesByDomain)
        {
            var domain = kv.Key;
            var indices = kv.Value;
            if (!_usedByDomain.TryGetValue(domain, out var used))
                continue;
            if (indices.Count > 0 && used.Count >= indices.Count)
                used.Clear();
        }

        // Si tout est vide (deck minuscule), on recycle tout.
        if (_indicesByDomain.Count > 0 && _usedByDomain.Values.All(u => u.Count == 0) == false)
        {
            // ok
        }

        // Tirage pondéré par domaine + biais difficulté
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var domain = ChooseDomainWeighted();
            if (!_indicesByDomain.TryGetValue(domain, out var list) || list.Count == 0)
                continue;

            if (!_usedByDomain.TryGetValue(domain, out var used))
            {
                used = new HashSet<int>();
                _usedByDomain[domain] = used;
            }

            var candidates = list.Where(i => !used.Contains(i)).ToList();
            if (candidates.Count == 0)
            {
                used.Clear();
                candidates = list.ToList();
            }

            // Pondération difficulté 1/2/3
            var picked = PickByDifficulty(candidates);
            used.Add(picked);
            q = _allQuestions[picked];
            return true;
        }

        return false;
    }

    private string ChooseDomainWeighted()
    {
        var total = 0f;
        foreach (var d in DomainWeights)
            total += d.Weight;

        var r = (float)(_rng.NextDouble() * total);
        foreach (var d in DomainWeights)
        {
            r -= d.Weight;
            if (r <= 0)
                return d.Domain;
        }

        return DomainWeights[0].Domain;
    }

    private int PickByDifficulty(List<int> candidates)
    {
        // Bias vers difficile (comme avant)
        float WeightFor(int idx)
        {
            var d = _allQuestions[idx].Difficulty;
            return d == 1 ? 1.0f : (d == 2 ? 1.3f : 1.6f);
        }

        var total = 0f;
        foreach (var c in candidates)
            total += WeightFor(c);

        var r = (float)(_rng.NextDouble() * total);
        foreach (var c in candidates)
        {
            r -= WeightFor(c);
            if (r <= 0)
                return c;
        }

        return candidates[0];
    }

    private void IndexDeck()
    {
        for (var i = 0; i < _allQuestions.Count; i++)
        {
            var d = _allQuestions[i].Domain;
            if (!_indicesByDomain.TryGetValue(d, out var list))
            {
                list = new List<int>();
                _indicesByDomain[d] = list;
                _usedByDomain[d] = new HashSet<int>();
            }

            list.Add(i);
        }
    }

    private void BuildFallbackDeck()
    {
        _allQuestions.Clear();
        _allQuestions.Add(new Question(
            "Technology",
            1,
            "Quel service AWS est un stockage d'objets ?",
            new[] { "Amazon S3", "Amazon RDS", "AWS Lambda", "Amazon EC2" },
            "Amazon S3",
            "Amazon S3 est un service de stockage d'objets (buckets/objets)."));

        _allQuestions.Add(new Question(
            "Security",
            1,
            "Quel service gère des rôles et des politiques d'accès ?",
            new[] { "AWS IAM", "Amazon VPC", "Amazon Route 53", "Amazon CloudFront" },
            "AWS IAM",
            "IAM (Identity and Access Management) permet de gérer utilisateurs, rôles et politiques."));

        _allQuestions.Add(new Question(
            "Billing",
            1,
            "Quel outil aide à optimiser les coûts AWS ?",
            new[] { "AWS Cost Explorer", "AWS Shield", "AWS Snowball", "AWS WAF" },
            "AWS Cost Explorer",
            "Cost Explorer sert à analyser l'usage et les coûts pour détecter des optimisations."));

    }

    private static string NormalizeDomain(string domain, string category)
    {
        var d = (domain ?? string.Empty).Trim();
        if (d.Length > 0)
            return d;

        // Compat: si l'ancien deck n'a que `category`
        var c = (category ?? string.Empty).Trim();
        if (c.Equals("Security", StringComparison.OrdinalIgnoreCase) || c.Equals("Compliance", StringComparison.OrdinalIgnoreCase))
            return "Security";
        if (c.Equals("Billing", StringComparison.OrdinalIgnoreCase))
            return "Billing";

        // Tout le reste: services/tech
        return "Technology";
    }

    private AnswerOption[] BuildShuffledOptions(Question q)
    {
        var opts = q.Answers
            .Select(a => new AnswerOption(a, a == q.CorrectAnswer))
            .ToList();

        // Shuffle
        for (var i = opts.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (opts[i], opts[j]) = (opts[j], opts[i]);
        }

        return opts.ToArray();
    }

    private readonly record struct AnswerOption(string Text, bool IsCorrect);

    private sealed record Question(
        string Domain,
        int Difficulty,
        string Prompt,
        string[] Answers,
        string CorrectAnswer,
        string Explanation);

    private sealed class QuestionDeck
    {
        public string DeckId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public QuestionItem[] Questions { get; set; } = Array.Empty<QuestionItem>();
    }

    private sealed class QuestionItem
    {
        public string Category { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public int Difficulty { get; set; } = 1;

        public string Prompt { get; set; } = string.Empty;
        public string[] Answers { get; set; } = Array.Empty<string>();
        public int CorrectIndex { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }
}
