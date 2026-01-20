#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    private void ApplyAnswerRowVisualState()
    {
        if (_cardAnswerPanels.Count != 4)
            return;

        if (_answerBarStyles.Count != 4 || _answerBarHoverStyles.Count != 4)
            return;
        if (_answerBarStylesNoBorder.Count != 4 || _answerBarHoverStylesNoBorder.Count != 4)
            return;

        var styles = _isDarkCardTheme ? _answerBarStylesNoBorder : _answerBarStyles;
        var hoverStyles = _isDarkCardTheme ? _answerBarHoverStylesNoBorder : _answerBarHoverStyles;

        var effectiveHoverIndex = (_answeredCurrent || _cardIsBackSide) ? -1 : _hoveredAnswerIndex;

        for (var i = 0; i < _cardAnswerPanels.Count; i++)
        {
            var panel = _cardAnswerPanels[i];
            if (!IsInstanceValid(panel))
                continue;

            var isHover = i == effectiveHoverIndex;

            // Important: ne pas utiliser SelfModulate pour teinter le PanelContainer,
            // sinon ça multiplie aussi la couleur du texte et peut le rendre illisible.
            panel.SelfModulate = Colors.White;
            panel.AddThemeStyleboxOverride("panel", isHover ? hoverStyles[i] : styles[i]);
        }
    }

    private void ApplyCardStyleForDifficulty(int difficulty)
    {
        // Facile: papier clair; Moyen: neutre; Difficile: sombre collector
        var d = Math.Clamp(difficulty, 1, 3);
        _isDarkCardTheme = d >= 3;

        if (_cardFrontMat != null)
        {
            if (d == 1)
            {
                _cardFrontMat.SetShaderParameter("bg_color", new Color(0.98f, 0.97f, 0.94f, 1f));
                _cardFrontMat.SetShaderParameter("base_tint", new Color(1.00f, 0.98f, 0.95f, 1f));
                _cardFrontMat.SetShaderParameter("grain_strength", 0.06f);
                _cardFrontMat.SetShaderParameter("grain_scale", 12.0f);
                _cardFrontMat.SetShaderParameter("contrast", 1.25f);
            }
            else if (d == 2)
            {
                _cardFrontMat.SetShaderParameter("bg_color", new Color(0.92f, 0.94f, 1.00f, 1f));
                _cardFrontMat.SetShaderParameter("base_tint", new Color(0.95f, 0.96f, 1.00f, 1f));
                _cardFrontMat.SetShaderParameter("grain_strength", 0.07f);
                _cardFrontMat.SetShaderParameter("grain_scale", 11.0f);
                _cardFrontMat.SetShaderParameter("contrast", 1.28f);
            }
            else
            {
                // Thème sombre, mais lisible: on garde un fond foncé tout en évitant de "tuer" l'UI.
                _cardFrontMat.SetShaderParameter("bg_color", new Color(0.10f, 0.105f, 0.13f, 1f));
                _cardFrontMat.SetShaderParameter("base_tint", new Color(0.95f, 0.96f, 1.00f, 1f));
                _cardFrontMat.SetShaderParameter("grain_strength", 0.08f);
                _cardFrontMat.SetShaderParameter("grain_scale", 10.0f);
                _cardFrontMat.SetShaderParameter("contrast", 1.10f);
            }
        }

        if (_cardBackMat != null)
        {
            // IMPORTANT: le shader multiplie TOUTE la couleur finale par base_tint.
            // Si on met un tint sombre ici, on "tue" aussi le texte du SubViewport.
            // On garde un verso sombre via bg_color (défini au setup), mais on laisse
            // la texture UI (texte) rester lisible.
            _cardBackMat.SetShaderParameter("base_tint", new Color(1f, 1f, 1f, 1f));
            _cardBackMat.SetShaderParameter("grain_strength", 0.07f);
            _cardBackMat.SetShaderParameter("grain_scale", 10.0f);
            _cardBackMat.SetShaderParameter("contrast", 1.05f);
        }

        // Lisibilité: carte sombre => titres dorés, réponses en blanc cassé.
        // Carte claire => texte noir.
        var faceText = d >= 3 ? DarkCardGold : LightCardBlack;
        var answersText = d >= 3 ? DarkCardAnswerText : faceText;
        if (IsInstanceValid(_cardDomainLabel))
            _cardDomainLabel.SelfModulate = faceText;
        if (IsInstanceValid(_cardQuestionLabel))
            _cardQuestionLabel.SelfModulate = faceText;
        foreach (var l in _cardAnswerLabels)
        {
            if (IsInstanceValid(l))
                l.SelfModulate = answersText;
        }

        // Le verso est sur fond sombre: on force un texte clair stable.
        if (IsInstanceValid(_cardBackContent))
            _cardBackContent.AddThemeColorOverride("default_color", NeutralText);

        // S'assure que le style des réponses suit le thème (bordure ou non).
        ApplyAnswerRowVisualState();
    }

    private void SetBackSidePending()
    {
        if (!IsInstanceValid(_cardBackContent))
            return;

        _cardBackContent.Text = "Réponds d'abord, puis retourne la carte.";
    }

    private void SetBackSideResult(int chosenIndex, bool ok)
    {
        if (!IsInstanceValid(_cardBackContent))
            return;

        var chosenText = _currentOptions[chosenIndex].Text;
        var correctIndex = Array.FindIndex(_currentOptions, o => o.IsCorrect);
        var correctText = correctIndex >= 0 && correctIndex < _currentOptions.Length
            ? _currentOptions[correctIndex].Text
            : _currentQuestion.CorrectAnswer;

        var chosenColor = ok ? "#2EE59C" : "#FF4D5A";
        var icon = ok ? "✅" : "❌";

        // Note: on ne liste pas les choix (doublon avec le recto) ; on affiche l’essentiel.
        _cardBackContent.Text =
            $"{icon} [b]Ta réponse:[/b] [color={chosenColor}]{EscapeBbcode(chosenText)}[/color]\n" +
            $"[b]Bonne réponse:[/b] [color=#2EE59C]{EscapeBbcode(correctText)}[/color]\n\n" +
            $"[b]Pourquoi ?[/b]\n{EscapeBbcode(_currentQuestion.Explanation)}\n\n" +
            $"[center][b]Clique sur la carte pour continuer[/b][/center]";
    }

    private static string EscapeBbcode(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s
            .Replace("[", "\\[")
            .Replace("]", "\\]");
    }

    private void UpdateTopRow()
    {
        var score = $"Score: {_correct}/{_answered}";
        var timer = $"{BuildMarker} ⏱️ {FormatTime(_timeRemaining)}";

        // Défensif: si la scène HUD a été modifiée (ex: suppression du post-it),
        // on évite un crash et on re-résout les refs si besoin.
        if (!IsInstanceValid(_topScoreLabel))
            _topScoreLabel = GetNodeOrNull<Label>("TopHUD/TopRow/Score") ?? _topScoreLabel;
        if (!IsInstanceValid(_topTimerLabel))
            _topTimerLabel = GetNodeOrNull<Label>("TopHUD/TopRow/Timer") ?? _topTimerLabel;

        if (IsInstanceValid(_topScoreLabel))
            _topScoreLabel.Text = score;
        if (IsInstanceValid(_topTimerLabel))
            _topTimerLabel.Text = timer;

        // Défensif: les UI de SubViewport peuvent être recréées / rechargées.
        if (!IsInstanceValid(_cardScoreFaceLabel) || !IsInstanceValid(_cardTimerFaceLabel))
        {
            var faceUi = GetNodeOrNull<Control>("../../CardRig/CardFrontViewport/Face");
            if (IsInstanceValid(faceUi))
            {
                if (!IsInstanceValid(_cardScoreFaceLabel))
                    _cardScoreFaceLabel = faceUi.GetNodeOrNull<Label>("%FaceScore") ?? _cardScoreFaceLabel;
                if (!IsInstanceValid(_cardTimerFaceLabel))
                    _cardTimerFaceLabel = faceUi.GetNodeOrNull<Label>("%FaceTimer") ?? _cardTimerFaceLabel;
            }
        }

        if (IsInstanceValid(_cardScoreFaceLabel))
            _cardScoreFaceLabel.Text = score;
        if (IsInstanceValid(_cardTimerFaceLabel))
            _cardTimerFaceLabel.Text = $"⏱️ {FormatTime(_timeRemaining)}";
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsPositiveInfinity(seconds))
            return "∞";

        var s = Math.Max(0, (int)Math.Ceiling(seconds));
        var m = s / 60;
        var r = s % 60;
        return $"{m:00}:{r:00}";
    }

    private string FormatDomain(string domain)
    {
        var icon = DomainIcons.TryGetValue(domain, out var i) ? i : "🧩";
        return $"Domaine: {icon} {domain}";
    }

    private string FormatDomainFace(string domain)
    {
        var icon = DomainIcons.TryGetValue(domain, out var i) ? i : "🧩";
        return $"{icon} {domain}";
    }

    private void HighlightAnswers(int chosenIndex)
    {
        var correctIndex = Array.FindIndex(_currentOptions, o => o.IsCorrect);
        _chosenAnswerIndex = chosenIndex;
        _correctAnswerIndex = correctIndex;

        // Recto (sur la carte): indicateur explicite à droite, sans changer les teintes de fond.
        for (var i = 0; i < _cardAnswerResultLabels.Count; i++)
        {
            var label = _cardAnswerResultLabels[i];
            if (!IsInstanceValid(label))
                continue;

            if (i == correctIndex)
            {
                label.Text = "Bonne";
                label.SelfModulate = NeutralText;
            }
            else if (i == chosenIndex)
            {
                label.Text = "Mauvaise";
                label.SelfModulate = NeutralText;
            }
            else
            {
                label.Text = "";
                label.SelfModulate = NeutralText;
            }
        }

        // Ne pas recolorer les boutons UI par code: on laisse le thème gérer l'apparence.
    }
}
