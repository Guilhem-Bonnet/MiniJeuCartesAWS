#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

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

        var deckPath = GetActiveDeckPath();

        try
        {
            if (!ResourceLoader.Exists(deckPath))
            {
                GD.PrintErr($"[MiniJeuCartesAWS] Deck missing: {deckPath}");
                BuildFallbackDeck();
                IndexDeck();
                return;
            }

            using var f = FileAccess.Open(deckPath, FileAccess.ModeFlags.Read);
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
                var category = (q.Category ?? string.Empty).Trim();
                var problem = (q.Problem ?? string.Empty).Trim();
                var services = (q.Services ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
                var tags = (q.Tags ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
                var id = string.IsNullOrWhiteSpace(q.Id)
                    ? ComputeQuestionId(deck.DeckId, domain, category, q.Prompt)
                    : q.Id.Trim();

                _allQuestions.Add(new Question(
                    id,
                    domain,
                    category,
                    problem,
                    services,
                    tags,
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

        // Profil courant: base de l'adaptatif.
        var profile = GetCurrentProfile();

        // Anti-répétition courte (tous domaines confondus).
        _recentQuestionIds ??= new Queue<string>();

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

        // Tirage adaptatif: score par question (profil) puis weighted random.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            // On conserve l'équilibre par domaines via un choix pondéré,
            // puis on score finement les questions de ce domaine.
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

            if (!TryPickAdaptive(candidates, profile, out var picked))
                continue;

            used.Add(picked);
            q = _allQuestions[picked];

            RememberRecentQuestion(q.Id);
            return true;
        }

        return false;
    }

    private Queue<string>? _recentQuestionIds;
    private const int RecentQuestionWindow = 10;

    private void RememberRecentQuestion(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        _recentQuestionIds ??= new Queue<string>();

        // Empêche les répétitions immédiates (même si le deck est petit).
        if (_recentQuestionIds.Count > 0 && string.Equals(_recentQuestionIds.Last(), id, StringComparison.OrdinalIgnoreCase))
            return;

        _recentQuestionIds.Enqueue(id);
        while (_recentQuestionIds.Count > RecentQuestionWindow)
            _recentQuestionIds.Dequeue();
    }

    private bool IsRecent(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _recentQuestionIds == null)
            return false;
        return _recentQuestionIds.Contains(id, StringComparer.OrdinalIgnoreCase);
    }

    private bool TryPickAdaptive(List<int> candidates, PlayerProfile profile, out int picked)
    {
        picked = -1;
        if (candidates.Count == 0)
            return false;

        // Mode "Renforcement": cible agressivement les difficultés.
        var reinforcement = _selectedGameMode == GameMode.Reinforcement;

        // Soft-filter: en mode normal, on évite de repiocher trop souvent des questions maîtrisées
        // si on a suffisamment de variété restante. (Ne doit jamais bloquer un petit deck.)
        if (!reinforcement && candidates.Count >= 10)
        {
            profile.QuestionStatsById ??= new Dictionary<string, QuestionStats>(StringComparer.OrdinalIgnoreCase);

            bool IsMastered(Question q)
            {
                if (!profile.QuestionStatsById.TryGetValue(q.Id, out var qs) || qs == null)
                    return false;

                var asked = qs.Asked;
                if (asked < 4)
                    return false;

                var acc = asked <= 0 ? 0.0f : (float)qs.Correct / asked;
                return acc >= 0.90f;
            }

            var mastered = candidates.Where(i => IsMastered(_allQuestions[i])).ToList();
            // On ne filtre que si au moins ~40% de candidates restent.
            if (mastered.Count > 0 && mastered.Count <= (int)(candidates.Count * 0.60f))
            {
                var filtered = candidates.Where(i => !mastered.Contains(i)).ToList();
                if (filtered.Count >= 3)
                    candidates = filtered;
            }
        }

        float Score(int idx)
        {
            var q = _allQuestions[idx];

            // Poids domaine (équilibre global).
            var domainWeight = 1.0f;
            foreach (var d in DomainWeights)
            {
                if (string.Equals(d.Domain, q.Domain, StringComparison.OrdinalIgnoreCase))
                {
                    domainWeight = d.Weight;
                    break;
                }
            }
            domainWeight = Math.Max(0.05f, domainWeight);

            // Difficulté
            var difficultyWeight = q.Difficulty == 1 ? 1.0f : (q.Difficulty == 2 ? 1.25f : 1.55f);

            // Difficulté d'entraînement (menu): on biaise le tirage sans filtrer dur.
            // - Débutant: favorise les questions simples et évite les pièges.
            // - Expert: mix, légère préférence pour scénarios.
            // - Maître: favorise les questions difficiles et les questions "pièges"/"à ne pas confondre".
            bool HasTag(string t)
            {
                if (q.Tags == null) return false;
                foreach (var x in q.Tags)
                {
                    if (string.Equals(x, t, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            var isTrap = HasTag("trap") || HasTag("confusable") || HasTag("best-solution") || HasTag("best-practice");
            var isScenario = HasTag("scenario") || HasTag("case") || HasTag("course");

            var trainingBias = 1.0f;
            switch (_selectedTrainingDifficulty)
            {
                case TrainingDifficulty.Beginner:
                    trainingBias *= q.Difficulty == 1 ? 1.35f : (q.Difficulty == 2 ? 0.80f : 0.45f);
                    if (isTrap) trainingBias *= 0.55f;
                    if (isScenario && q.Difficulty >= 2) trainingBias *= 0.80f;
                    break;

                case TrainingDifficulty.Expert:
                    trainingBias *= q.Difficulty == 1 ? 0.95f : (q.Difficulty == 2 ? 1.15f : 1.25f);
                    if (isScenario) trainingBias *= 1.10f;
                    if (isTrap) trainingBias *= 1.05f;
                    break;

                case TrainingDifficulty.Master:
                    trainingBias *= q.Difficulty == 1 ? 0.55f : (q.Difficulty == 2 ? 1.20f : 1.55f);
                    if (isScenario) trainingBias *= 1.20f;
                    if (isTrap) trainingBias *= 1.45f;
                    break;
            }

            profile.QuestionStatsById ??= new Dictionary<string, QuestionStats>(StringComparer.OrdinalIgnoreCase);
            profile.TopicStatsByKey ??= new Dictionary<string, TopicStats>(StringComparer.OrdinalIgnoreCase);

            profile.QuestionStatsById.TryGetValue(q.Id, out var qs);
            var asked = qs?.Asked ?? 0;
            var correct = qs?.Correct ?? 0;
            var acc = asked <= 0 ? 0.0f : (float)correct / asked;

            // Favorise fort les jamais vues.
            var novelty = asked == 0 ? 3.4f : 1.0f;

            // Évite les cartes "maîtrisées" (beaucoup de bonnes réponses).
            var masteryPenalty = (asked >= 3 && acc >= 0.85f) ? 0.30f : 1.0f;

            // Cible l'échec et la difficulté.
            var weaknessBoost = asked == 0 ? 1.0f : (1.0f + (1.0f - acc) * 1.8f);
            var streakBoost = 1.0f + Math.Clamp(qs?.WrongStreak ?? 0, 0, 6) * 0.55f;

            // Anti-récence (au sein d'une session).
            var recentPenalty = IsRecent(q.Id) ? 0.10f : 1.0f;

            // Ciblage par thèmes/problématiques/services (si stats suffisantes)
            float TopicWeakness(string key)
            {
                if (!profile.TopicStatsByKey.TryGetValue(key, out var ts))
                    return 1.0f;
                if (ts.Asked < 4)
                    return 1.0f;
                var tacc = ts.Asked <= 0 ? 0.0f : (float)ts.Correct / ts.Asked;
                if (tacc < 0.55f) return 1.65f;
                if (tacc < 0.70f) return 1.35f;
                if (tacc < 0.82f) return 1.10f;
                return 0.95f;
            }

            // IMPORTANT: éviter de multiplier 8–10 facteurs (services/tags) => score instable.
            // On utilise une moyenne géométrique, puis on clippe.
            static float ClampMul(float v) => Math.Clamp(v, 0.60f, 1.80f);

            var topicMultipliers = new List<float>(12);
            if (!string.IsNullOrWhiteSpace(q.Category)) topicMultipliers.Add(ClampMul(TopicWeakness($"cat:{q.Category}")));
            if (!string.IsNullOrWhiteSpace(q.Problem)) topicMultipliers.Add(ClampMul(TopicWeakness($"prob:{q.Problem}")));
            if (q.Services != null)
            {
                foreach (var s in q.Services)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        topicMultipliers.Add(ClampMul(TopicWeakness($"svc:{s}")));
                }
            }
            if (q.Tags != null)
            {
                foreach (var t in q.Tags)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                        topicMultipliers.Add(ClampMul(TopicWeakness($"tag:{t}")));
                }
            }

            var theme = 1.0f;
            if (topicMultipliers.Count > 0)
            {
                var g = 1.0f;
                foreach (var m in topicMultipliers)
                    g *= m;
                theme = MathF.Pow(g, 1.0f / topicMultipliers.Count);
                theme = Math.Clamp(theme, 0.70f, 1.60f);
            }

            // Mode renforcement: on pousse encore les cartes "à travailler" et on réduit les maîtrisées.
            if (reinforcement)
            {
                if (asked == 0)
                    novelty *= 1.25f;

                // Si maîtrisé, on la met quasiment de côté.
                if (asked >= 3 && acc >= 0.85f)
                    masteryPenalty *= 0.15f;

                // Si déjà ratée, on insiste.
                if (asked > 0 && acc <= 0.70f)
                    weaknessBoost *= 1.35f;
            }

            var score = domainWeight * difficultyWeight * trainingBias * novelty * masteryPenalty * weaknessBoost * streakBoost * theme * recentPenalty;

            // Plancher pour éviter zéro (et garder un tirage possible).
            return Math.Max(0.0001f, score);
        }

        var total = 0.0f;
        foreach (var idx in candidates)
            total += Score(idx);

        if (total <= 0.0f)
            return false;

        var r = (float)(_rng.NextDouble() * total);
        foreach (var idx in candidates)
        {
            r -= Score(idx);
            if (r <= 0)
            {
                picked = idx;
                return true;
            }
        }

        picked = candidates[0];
        return true;
    }

    private string ChooseDomainWeighted()
    {
        // On ne doit jamais choisir un domaine absent du deck, sinon le tirage peut échouer
        // (et faire terminer la partie instantanément). On pondère donc uniquement les domaines
        // effectivement présents, en utilisant DomainWeights quand disponible.

        if (_indicesByDomain.Count == 0)
            return DomainWeights[0].Domain;

        var weightByDomain = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in DomainWeights)
            weightByDomain[d.Domain] = d.Weight;

        var domains = _indicesByDomain
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => kv.Key)
            .ToList();

        if (domains.Count == 0)
            return _indicesByDomain.Keys.First();

        var total = 0f;
        foreach (var domain in domains)
            total += weightByDomain.TryGetValue(domain, out var w) ? w : 1.0f;

        var r = (float)(_rng.NextDouble() * total);
        foreach (var domain in domains)
        {
            r -= weightByDomain.TryGetValue(domain, out var w) ? w : 1.0f;
            if (r <= 0)
                return domain;
        }

        return domains[0];
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
            ComputeQuestionId("fallback", "Technology", "Storage", "Quel service AWS est un stockage d'objets ?"),
            "Technology",
            "Storage",
            "Identifier le service adapté",
            new[] { "Amazon S3" },
            Array.Empty<string>(),
            1,
            "Quel service AWS est un stockage d'objets ?",
            new[] { "Amazon S3", "Amazon RDS", "AWS Lambda", "Amazon EC2" },
            "Amazon S3",
            "Amazon S3 est un service de stockage d'objets (buckets/objets)."));

        _allQuestions.Add(new Question(
            ComputeQuestionId("fallback", "Security", "IAM", "Quel service gère des rôles et des politiques d'accès ?"),
            "Security",
            "IAM",
            "Gouvernance des accès",
            new[] { "AWS IAM" },
            Array.Empty<string>(),
            1,
            "Quel service gère des rôles et des politiques d'accès ?",
            new[] { "AWS IAM", "Amazon VPC", "Amazon Route 53", "Amazon CloudFront" },
            "AWS IAM",
            "IAM (Identity and Access Management) permet de gérer utilisateurs, rôles et politiques."));

        _allQuestions.Add(new Question(
            ComputeQuestionId("fallback", "Billing", "CostManagement", "Quel outil aide à optimiser les coûts AWS ?"),
            "Billing",
            "CostManagement",
            "Optimisation coûts",
            new[] { "AWS Cost Explorer" },
            Array.Empty<string>(),
            1,
            "Quel outil aide à optimiser les coûts AWS ?",
            new[] { "AWS Cost Explorer", "AWS Shield", "AWS Snowball", "AWS WAF" },
            "AWS Cost Explorer",
            "Cost Explorer sert à analyser l'usage et les coûts pour détecter des optimisations."));

    }

    private static string NormalizeDomain(string? domain, string? category)
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
        string Id,
        string Domain,
        string Category,
        string Problem,
        string[] Services,
        string[] Tags,
        int Difficulty,
        string Prompt,
        string[] Answers,
        string CorrectAnswer,
        string Explanation);

    private static string ComputeQuestionId(string deckId, string domain, string category, string prompt)
    {
        // Id stable et indépendant des index: utile pour les stats par profil.
        // (On garde volontairement court pour rester lisible dans les fichiers JSON.)
        var seed = $"{deckId}|{domain}|{category}|{prompt}";
        var bytes = Encoding.UTF8.GetBytes(seed);
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).Substring(0, 12).ToLowerInvariant();
    }

    private sealed class QuestionDeck
    {
        public string DeckId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public QuestionItem[] Questions { get; set; } = Array.Empty<QuestionItem>();
    }

    private sealed class QuestionItem
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Problem { get; set; } = string.Empty;
        public string[] Services { get; set; } = Array.Empty<string>();
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string Domain { get; set; } = string.Empty;
        public int Difficulty { get; set; } = 1;

        public string Prompt { get; set; } = string.Empty;
        public string[] Answers { get; set; } = Array.Empty<string>();
        public int CorrectIndex { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }
}
