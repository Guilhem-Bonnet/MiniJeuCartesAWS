// Désactivé: l'utilisateur veut des animations via AnimationPlayer (pas de tweens).
#if false
#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    private void SuppressIdle(double seconds)
    {
        var now = Time.GetTicksMsec() / 1000.0;
        _idleResumeAt = Math.Max(_idleResumeAt, now + Math.Max(0, seconds));
        _idleAnchorValid = false;
    }

    private static void KillTween(ref Tween? tween)
    {
        if (tween == null)
            return;

        if (GodotObject.IsInstanceValid(tween))
            tween.Kill();

        tween = null;
    }

    private void CancelInFlightAnimations()
    {
        KillTween(ref _cardTween);
        KillTween(ref _deckTween);
        _idleAnchorValid = false;
    }

    private bool IsIdleAllowed()
    {
        var now = Time.GetTicksMsec() / 1000.0;
        return now >= _idleResumeAt;
    }

    private void AnimateDrawFromDeck(bool first)
    {
        SuppressIdle(0.60);

        // Petit son de pioche/pose.
        PlaySfx(_sfxDraw, pitch: 0.98f + (float)_rng.NextDouble() * 0.06f);

        if (!IsInstanceValid(_deckSpawn) || !IsInstanceValid(_cardFocus))
        {
            if (first) AnimateCardEnter(); else AnimateCardFlip();
            return;
        }

        // La carte "sort" du deck.
        var startT = _deckSpawn.GlobalTransform;
        startT.Basis = startT.Basis.Rotated(Vector3.Up, Mathf.DegToRad(-18f));

        var liftT = startT;
        liftT.Origin = liftT.Origin + new Vector3(0, 0.05f, 0);

        var focusT = GetFocusTransformGlobal();

        _cardRig.GlobalTransform = startT;

        KillTween(ref _cardTween);
        var tween = CreateTween();
        _cardTween = tween;
        tween.TweenProperty(_cardRig, "global_transform", liftT, 0.12)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_cardRig, "global_transform", focusT, 0.22)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void AnimateCardSendAwayAndMaybeShuffle(int runTokenAtStart, int questionTokenAtStart)
    {
        if (!_runActive)
            return;
        if (_runToken != runTokenAtStart)
            return;
        if (_questionToken != questionTokenAtStart)
            return;

        SuppressIdle(0.80);

        if (!IsInstanceValid(_discardTarget))
        {
            // Fallback: juste enchaîner
            if (_runActive) NextQuestion();
            return;
        }

        KillTween(ref _cardTween);
        var tween = CreateTween();
        _cardTween = tween;
        var startT = _cardRig.GlobalTransform;
        var liftT = startT;
        liftT.Origin = liftT.Origin + new Vector3(0, 0.02f, 0);

        var discardT = _discardTarget.GlobalTransform;
        discardT.Basis = discardT.Basis.Rotated(Vector3.Up, Mathf.DegToRad(25f));

        tween.TweenProperty(_cardRig, "global_transform", liftT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_cardRig, "global_transform", discardT, 0.20)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() =>
        {
            if (!_runActive)
                return;
            if (_runToken != runTokenAtStart)
                return;
            if (_questionToken != questionTokenAtStart)
                return;

            _visualDeckRemaining = Math.Max(0, _visualDeckRemaining - 1);
            UpdateDeckVisual();
        }));

        tween.TweenInterval(0.05);

        tween.TweenCallback(Callable.From(() =>
        {
            if (!_runActive)
                return;
            if (_runToken != runTokenAtStart)
                return;
            if (_questionToken != questionTokenAtStart)
                return;

            if (_visualDeckRemaining <= 0)
            {
                AnimateDeckShuffle();
                _visualDeckRemaining = _visualDeckCapacity;
                UpdateDeckVisual();
            }

            if (_runActive)
                NextQuestion();
        }));

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void AnimateDeckShuffle()
    {
        if (!IsInstanceValid(_deckRig))
            return;

        PlaySfx(_sfxShuffle, pitch: 0.96f + (float)_rng.NextDouble() * 0.08f);

        SuppressIdle(0.75);
        KillTween(ref _deckTween);
        var tween = CreateTween();
        _deckTween = tween;
        var startRot = _deckRig!.RotationDegrees;
        var startPos = _deckRig!.Position;

        // Petit "mélange" visible: lift + secousses + retour
        tween.TweenProperty(_deckRig, "position", startPos + new Vector3(0, 0.03f, 0), 0.10)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(_deckRig, "rotation_degrees", startRot + new Vector3(0, 0, 6f), 0.10)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_deckRig, "rotation_degrees", startRot + new Vector3(0, 0, -6f), 0.10)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_deckRig, "rotation_degrees", startRot + new Vector3(0, 0, 4f), 0.08)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_deckRig, "rotation_degrees", startRot, 0.08)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

        tween.Parallel().TweenProperty(_deckRig, "position", startPos, 0.16)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
    }

    private void FxCorrect()
    {
        SuppressIdle(0.30);
        PlaySfx(_sfxCorrect, pitch: 0.98f + (float)_rng.NextDouble() * 0.05f);
        if (IsInstanceValid(_sparkles))
        {
            _sparkles.Emitting = false;
            _sparkles.Restart();
            _sparkles.Emitting = true;
        }

        var tween = CreateTween();
        var baseRot = _cardRig.Rotation;
        tween.TweenProperty(_cardRig, "rotation", new Vector3(baseRot.X - 0.08f, baseRot.Y, baseRot.Z), 0.08);
        tween.TweenProperty(_cardRig, "rotation", baseRot, 0.14);
    }

    private void FxWrong()
    {
        SuppressIdle(0.20);
        PlaySfx(_sfxWrong, pitch: 0.98f + (float)_rng.NextDouble() * 0.05f);
        // Petit shake
        var tween = CreateTween();
        var p = _cardRig.Position;
        tween.TweenProperty(_cardRig, "position", p + new Vector3(0.03f, 0, 0), 0.04);
        tween.TweenProperty(_cardRig, "position", p - new Vector3(0.03f, 0, 0), 0.04);
        tween.TweenProperty(_cardRig, "position", p, 0.04);
    }

    private void AnimateCardEnter()
    {
        SuppressIdle(0.35);
        var tween = CreateTween();
        var basePos = _cardRig.Position;
        var baseRot = _cardRig.Rotation;
        _cardRig.Position = basePos + new Vector3(0, -0.05f, 0);
        _cardRig.Rotation = baseRot;
        tween.TweenProperty(_cardRig, "position", basePos, 0.18);
        tween.TweenProperty(_cardRig, "rotation", baseRot + new Vector3(0.02f, 0, 0), 0.18);
        tween.TweenProperty(_cardRig, "rotation", baseRot, 0.12);
    }

    private void AnimateCardFlip()
    {
        // Flip court utilisé uniquement en fallback (sans deck): pivot au centre (style croupier), sans changer la position finale.
        SuppressIdle(0.35);

        var tween = CreateTween();
        var startPos = _cardRig.Position;

        var liftPos = startPos + new Vector3(0, 0.03f, 0);

        tween.TweenProperty(_cardRig, "position", liftPos, 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        var sign = _flipLeftNext ? -1f : 1f;
        _flipLeftNext = !_flipLeftNext;

        // IMPORTANT: flip recto/verso => rotation autour d'un axe DANS le plan.
        // Style "croupier": bascule avant/arrière autour de l'axe largeur (X).
        tween.Parallel().TweenProperty(_cardRig, "rotation_degrees", new Vector3(105f * sign, 0f, 0f), 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() =>
        {
            _cardRig.RotationDegrees = new Vector3(-105f * sign, 0f, 0f);
        }));

        tween.TweenProperty(_cardRig, "position", startPos, 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(_cardRig, "rotation_degrees", Vector3.Zero, 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private Transform3D GetBackTransformGlobal()
    {
        var focusT = GetFocusTransformGlobal();
        var frontBasis = focusT.Basis;
        // La face de carte est une PlaneMesh (normale locale ~Y). Un vrai recto/verso nécessite
        // une rotation autour d'un axe dans le plan (X ou Z).
        // Flip "sur le côté" => rotation autour de l'axe hauteur (Z), pour garder le texte lisible.
        var axis = frontBasis.Z;
        var backBasis = frontBasis.Rotated(axis, Mathf.Pi);
        return WithBasisAtOrigin(backBasis, focusT.Origin);
    }

    private void AnimateCardRevealBack()
    {
        // Retourne la carte (verso) recto/verso avec pivot au centre (flip latéral).
        // Important: la position finale reste exactement celle d'avant le flip, sinon illisible.
        SuppressIdle(0.70);

        PlaySfx(_sfxFlip, pitch: 0.97f + (float)_rng.NextDouble() * 0.06f);

        var tween = CreateTween();
        _cardTween = tween;
        var startT = _cardRig.GlobalTransform;
        var baseCenter = IsInstanceValid(_cardFrontMesh) ? _cardFrontMesh.GlobalTransform.Origin : GetCardVisibleCenterWorld();
        var liftCenter = baseCenter + new Vector3(0, 0.025f, 0);

        // On calcule les transforms en utilisant l'offset local du recto (avant de basculer l'état).
        var frontLocalOffset = IsInstanceValid(_cardFrontMesh) ? _cardFrontMesh.Position : Vector3.Zero;

        _cardIsBackSide = true;

        // IMPORTANT: on part de la base actuelle (sinon la carte "glisse" car le pivot n'est pas au centre du Node).
        var frontBasis = startT.Basis;
        var axis = frontBasis.Z; // axe dans le plan (flip sur le côté)

        // La direction (droite/gauche) se voit dans l'intermédiaire, pas dans l'état final (π == -π).
        var sign = _flipLeftNext ? -1f : 1f;
        _flipLeftNext = !_flipLeftNext;
        _lastFlipSign = sign;

        var midBasis = frontBasis.Rotated(axis, sign * (Mathf.Pi * 0.5f));
        var backBasis = frontBasis.Rotated(axis, sign * Mathf.Pi);

        var liftT = WithBasisKeepingCardCenter(frontBasis, liftCenter, frontLocalOffset);
        var midLiftT = WithBasisKeepingCardCenter(midBasis, liftCenter, frontLocalOffset);
        var backLiftT = WithBasisKeepingCardCenter(backBasis, liftCenter, frontLocalOffset);
        // Position finale identique (centre de carte conservé).
        var backT = WithBasisKeepingCardCenter(backBasis, baseCenter, frontLocalOffset);

        tween.TweenProperty(_cardRig, "global_transform", liftT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        // Souplesse: courbure au milieu du flip.
        if (_cardFrontMat != null)
            tween.Parallel().TweenProperty(_cardFrontMat, "shader_parameter/bend", sign * 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        if (_cardBackMat != null)
            tween.Parallel().TweenProperty(_cardBackMat, "shader_parameter/bend", sign * 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_cardRig, "global_transform", midLiftT, 0.20)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_cardRig, "global_transform", backLiftT, 0.18)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);

        // Relâche la courbure en fin de flip.
        if (_cardFrontMat != null)
            tween.Parallel().TweenProperty(_cardFrontMat, "shader_parameter/bend", 0.0f, 0.18)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
        if (_cardBackMat != null)
            tween.Parallel().TweenProperty(_cardBackMat, "shader_parameter/bend", 0.0f, 0.18)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);

        tween.TweenProperty(_cardRig, "global_transform", backT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void AnimateCardReturnFront()
    {
        SuppressIdle(0.65);

        PlaySfx(_sfxFlip, pitch: 0.97f + (float)_rng.NextDouble() * 0.06f);
        var tween = CreateTween();
        _cardTween = tween;

        var startT = _cardRig.GlobalTransform;
        var startBasis = startT.Basis;
        var baseCenter = GetCardVisibleCenterWorld();
        var liftCenter = baseCenter + new Vector3(0, 0.02f, 0);

        // Offset local correspondant au verso (état courant).
        var backLocalOffset = IsInstanceValid(_cardBackMesh)
            ? _cardBackMesh.Position
            : (IsInstanceValid(_cardFrontMesh) ? _cardFrontMesh.Position : Vector3.Zero);

        var focusT = GetFocusTransformGlobal();
        var frontBasis = focusT.Basis;
        var axis = frontBasis.Z; // flip sur le côté

        // On revient par le même côté que l'aller, pour éviter les inversions étranges.
        var sign = _lastFlipSign;
        var midBasis = startBasis.Rotated(axis, -sign * (Mathf.Pi * 0.5f));

        var liftT = WithBasisKeepingCardCenter(startBasis, liftCenter, backLocalOffset);
        var midLiftT = WithBasisKeepingCardCenter(midBasis, liftCenter, backLocalOffset);
        var frontLiftT = WithBasisKeepingCardCenter(frontBasis, liftCenter, backLocalOffset);
        var frontT = WithBasisKeepingCardCenter(frontBasis, baseCenter, backLocalOffset);

        _cardIsBackSide = false;

        tween.TweenProperty(_cardRig, "global_transform", liftT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        // Ré-applique une petite courbure au milieu du retour.
        if (_cardFrontMat != null)
            tween.Parallel().TweenProperty(_cardFrontMat, "shader_parameter/bend", -sign * 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        if (_cardBackMat != null)
            tween.Parallel().TweenProperty(_cardBackMat, "shader_parameter/bend", -sign * 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_cardRig, "global_transform", midLiftT, 0.20)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_cardRig, "global_transform", frontLiftT, 0.18)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);

        if (_cardFrontMat != null)
            tween.Parallel().TweenProperty(_cardFrontMat, "shader_parameter/bend", 0.0f, 0.18)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
        if (_cardBackMat != null)
            tween.Parallel().TweenProperty(_cardBackMat, "shader_parameter/bend", 0.0f, 0.18)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);

        tween.TweenProperty(_cardRig, "global_transform", frontT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void AnimateCardIdle()
    {
        // Bobbing ultra léger, “chill”
        if (!_idleAnchorValid)
        {
            _idleAnchorPos = _cardRig.Position;
            _idleAnchorRot = _cardRig.Rotation;
            _idleAnchorValid = true;
        }

        var t = (float)Time.GetTicksMsec() / 1000f;
        _cardRig.Position = _idleAnchorPos + new Vector3(0, Mathf.Sin(t * 0.8f) * 0.01f, 0);
        _cardRig.Rotation = _idleAnchorRot + new Vector3(
            Mathf.Sin(t * 0.6f) * 0.01f,
            Mathf.Sin(t * 0.4f) * 0.015f,
            0);
    }
}

#endif
