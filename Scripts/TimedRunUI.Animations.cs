#if false
#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    // ============================================================================
    // ANIMATIONS / TWEENS
    // - Gestion tweens + AnimationPlayer
    // - Alignement "offset parent" pour éviter les snaps des tracks `transform`
    // ============================================================================

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

        try { tween.Dispose(); } catch { }

        tween = null;
    }

    private void CancelInFlightAnimations()
    {
        KillTween(ref _cardTween);
        KillTween(ref _deckTween);
        if (IsInstanceValid(_cardAnim))
            _cardAnim!.Stop();
        if (IsInstanceValid(_deckAnim))
            _deckAnim!.Stop();
        _idleAnchorValid = false;

        _pendingAnimAction = PendingAnimAction.None;
        _pendingAnimName = "";
    }

    private static bool TryPlayAnim(AnimationPlayer? player, string name)
    {
        if (!IsInstanceValid(player))
            return false;
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (!player!.HasAnimation(name))
            return false;

        var anim = player.GetAnimation(name);
        if (anim == null)
            return false;
        if (anim.GetTrackCount() <= 0)
            return false;

        player.Play(name);
        return true;
    }

    private static Node3D EnsureOffsetParent(Node3D node, string offsetName)
    {
        // Crée un Node3D parent "offset" au-dessus de `node`.
        // Conserve exactement la transform globale visuelle.
        var parent = node.GetParent();
        if (parent is not Node3D parent3d)
            return node; // inattendu, on ne prend pas de risque.

        // Si déjà sous un offset (réouverture/duplication), on réutilise.
        if (parent3d.Name == offsetName)
            return parent3d;

        var offset = new Node3D { Name = offsetName };
        parent3d.AddChild(offset);

        // Place l'offset sur l'ancienne transform globale du node.
        offset.GlobalTransform = node.GlobalTransform;

        // Reparent en conservant la pose mondiale.
        parent3d.RemoveChild(node);
        offset.AddChild(node);
        node.Transform = Transform3D.Identity;
        return offset;
    }

    private static bool TryGetAnimTransformSample(Animation anim, double time, out Transform3D transform)
    {
        transform = default;

        try
        {
            var trackCount = anim.GetTrackCount();
            for (var i = 0; i < trackCount; i++)
            {
                if (anim.TrackGetType(i) != Animation.TrackType.Value)
                    continue;

                var path = anim.TrackGetPath(i).ToString();
                if (!path.EndsWith(":transform", StringComparison.Ordinal) && !path.EndsWith(":global_transform", StringComparison.Ordinal))
                    continue;

                var v = anim.ValueTrackInterpolate(i, (float)time);
                transform = (Transform3D)v;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool TryPlayAnim(AnimationPlayer? player, string name, float speed, bool fromEnd)
    {
        if (!IsInstanceValid(player))
            return false;
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (!player!.HasAnimation(name))
            return false;

        var anim = player.GetAnimation(name);
        if (anim == null)
            return false;
        if (anim.GetTrackCount() <= 0)
            return false;

        player.Play(name, customBlend: -1, customSpeed: speed, fromEnd: fromEnd);
        return true;
    }

    private bool TryPlayAlignedTransformAnim(AnimationPlayer? player, Node3D? rig, Node3D? rigOffset, string animName, Transform3D desiredRigStartGlobal, float speed, bool fromEnd)
    {
        if (!IsInstanceValid(player) || !IsInstanceValid(rig) || !IsInstanceValid(rigOffset))
            return false;
        if (string.IsNullOrWhiteSpace(animName))
            return false;
        if (!player!.HasAnimation(animName))
            return false;

        var anim = player.GetAnimation(animName);
        if (anim == null)
            return false;
        if (anim.GetTrackCount() <= 0)
            return false;

        // Si le clip anime un `transform` (track value), on l'aligne pour que
        // la première frame corresponde EXACTEMENT à la pose désirée.
        // => plus de téléportation.
        if (TryGetAnimTransformSample(anim, fromEnd ? anim.Length : 0.0, out var animStartLocal))
        {
            // On veut: rigOffsetGlobal * animStartLocal == desiredRigStartGlobal
            // Donc: rigOffsetGlobal == desiredRigStartGlobal * inverse(animStartLocal)
            rigOffset!.GlobalTransform = desiredRigStartGlobal * animStartLocal.AffineInverse();
            rig!.Transform = animStartLocal;
        }

        player.Play(animName, customBlend: -1, customSpeed: speed, fromEnd: fromEnd);
        return true;
    }

    private void OnDeckAnimationFinished(StringName animName)
    {
        // Pour l’instant: aucune action dépendante d’une animation deck.
    }

    private void OnCardAnimationFinished(StringName animName)
    {
        if (_pendingAnimAction == PendingAnimAction.None)
            return;

        var name = animName.ToString();
        if (!string.Equals(name, _pendingAnimName, StringComparison.Ordinal))
            return;

        var action = _pendingAnimAction;
        var runTokenAtStart = _pendingRunToken;
        var questionTokenAtStart = _pendingQuestionToken;

        _pendingAnimAction = PendingAnimAction.None;
        _pendingAnimName = "";

        if (!_runActive)
            return;
        if (_runToken != runTokenAtStart)
            return;
        if (_questionToken != questionTokenAtStart)
            return;

        if (action == PendingAnimAction.CardSendAwayAndMaybeShuffle)
        {
            _visualDeckRemaining = Math.Max(0, _visualDeckRemaining - 1);
            UpdateDeckVisual();

            if (_visualDeckRemaining <= 0)
            {
                AnimateDeckShuffle();
                _visualDeckRemaining = _visualDeckCapacity;
                UpdateDeckVisual();
            }

            if (_runActive)
                NextQuestion();
        }
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

        // AnimationPlayer (si configuré dans la scène)
        if (TryPlayAlignedTransformAnim(_cardAnim, _cardRig, _cardRigOffset, AnimCardDraw, startT, speed: 1.0f, fromEnd: false))
        {
            KillTween(ref _cardTween);
            return;
        }

        KillTween(ref _cardTween);
        var tween = CreateTween();
        _cardTween = tween;

        _cardRig.GlobalTransform = startT;

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

        var sendStart = _cardRig.GlobalTransform;
        if (TryPlayAlignedTransformAnim(_cardAnim, _cardRig, _cardRigOffset, AnimCardSendAway, sendStart, speed: 1.0f, fromEnd: false))
        {
            KillTween(ref _cardTween);
            _pendingAnimAction = PendingAnimAction.CardSendAwayAndMaybeShuffle;
            _pendingAnimName = AnimCardSendAway;
            _pendingRunToken = runTokenAtStart;
            _pendingQuestionToken = questionTokenAtStart;
            return;
        }

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

        if (IsInstanceValid(_deckAnim) && !string.IsNullOrWhiteSpace(AnimDeckShuffle) && TryPlayAlignedTransformAnim(_deckAnim, _deckRig, _deckRigOffset, AnimDeckShuffle, _deckRig!.GlobalTransform, speed: 1.0f, fromEnd: false))
        {
            KillTween(ref _deckTween);
            SuppressIdle(0.75);
            return;
        }

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
        tween.TweenProperty(_cardRig, "rotation", new Vector3(baseRot.X - 0.08f, baseRot.Y, baseRot.Z), 0.08)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_cardRig, "rotation", baseRot, 0.14)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
    }

    private void FxWrong()
    {
        SuppressIdle(0.20);
        PlaySfx(_sfxWrong, pitch: 0.98f + (float)_rng.NextDouble() * 0.05f);

        // Petit shake
        var tween = CreateTween();
        var p = _cardRig.Position;
        tween.TweenProperty(_cardRig, "position", p + new Vector3(0.03f, 0, 0), 0.04)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_cardRig, "position", p - new Vector3(0.03f, 0, 0), 0.04)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_cardRig, "position", p, 0.04)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
    }

    private void AnimateCardEnter()
    {
        SuppressIdle(0.35);
        // Pas d'anim dédiée "enter" dans la version 3-clips.
        // On garde un tween simple et fiable.

        KillTween(ref _cardTween);
        var tween = CreateTween();
        _cardTween = tween;
        var basePos = _cardRig.Position;
        var baseRot = _cardRig.Rotation;

        _cardRig.Position = basePos + new Vector3(0, -0.05f, 0);
        _cardRig.Rotation = baseRot;

        tween.TweenProperty(_cardRig, "position", basePos, 0.18)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        tween.Parallel().TweenProperty(_cardRig, "rotation", baseRot + new Vector3(0.02f, 0, 0), 0.18)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_cardRig, "rotation", baseRot, 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void AnimateCardFlip()
    {
        // Flip court utilisé uniquement en fallback (sans deck): pivot au centre (style croupier), sans changer la position finale.
        SuppressIdle(0.35);

        var sign = _flipLeftNext ? -1f : 1f;
        _flipLeftNext = !_flipLeftNext;
        _lastFlipSign = sign;
        var flipStart = _cardRig.GlobalTransform;
        if (TryPlayAlignedTransformAnim(_cardAnim, _cardRig, _cardRigOffset, AnimCardFlip, flipStart, speed: sign < 0 ? -1.0f : 1.0f, fromEnd: sign < 0))
            return;

        var tween = CreateTween();
        var startPos = _cardRig.Position;

        var liftPos = startPos + new Vector3(0, 0.03f, 0);

        tween.TweenProperty(_cardRig, "position", liftPos, 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
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

        var sign = _flipLeftNext ? -1f : 1f;
        _flipLeftNext = !_flipLeftNext;
        _lastFlipSign = sign;

        _cardIsBackSide = true;
        var revealStart = _cardRig.GlobalTransform;
        if (TryPlayAlignedTransformAnim(_cardAnim, _cardRig, _cardRigOffset, AnimCardFlip, revealStart, speed: sign < 0 ? -1.0f : 1.0f, fromEnd: sign < 0))
        {
            KillTween(ref _cardTween);
            return;
        }

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

        var sign = _lastFlipSign;
        _cardIsBackSide = false;
        // Même anim de flip jouée à l’envers pour revenir au recto.
        var returnStart = _cardRig.GlobalTransform;
        var backSign = -sign;
        if (TryPlayAlignedTransformAnim(_cardAnim, _cardRig, _cardRigOffset, AnimCardFlip, returnStart, speed: backSign < 0 ? -1.0f : 1.0f, fromEnd: backSign < 0))
        {
            KillTween(ref _cardTween);
            return;
        }

        var tween = CreateTween();
        _cardTween = tween;

        var startT = _cardRig.GlobalTransform;
        var startBasis = startT.Basis;
        var baseCenter = GetCardVisibleCenterWorld();
        var liftCenter = baseCenter + new Vector3(0, 0.02f, 0);

        // Offset local correspondant au verso (état courant).
        var backLocalOffset = IsInstanceValid(_cardBackMesh) ? _cardBackMesh.Position : (IsInstanceValid(_cardFrontMesh) ? _cardFrontMesh.Position : Vector3.Zero);

        var focusT = GetFocusTransformGlobal();
        var frontBasis = focusT.Basis;
        var axis = frontBasis.Z; // flip sur le côté

        // On revient par le même côté que l'aller, pour éviter les inversions étranges.
        var midBasis = startBasis.Rotated(axis, -sign * (Mathf.Pi * 0.5f));

        var liftT = WithBasisKeepingCardCenter(startBasis, liftCenter, backLocalOffset);
        var midLiftT = WithBasisKeepingCardCenter(midBasis, liftCenter, backLocalOffset);
        var frontLiftT = WithBasisKeepingCardCenter(frontBasis, liftCenter, backLocalOffset);
        var frontT = WithBasisKeepingCardCenter(frontBasis, baseCenter, backLocalOffset);

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
        _cardRig.Rotation = _idleAnchorRot + new Vector3(Mathf.Sin(t * 0.6f) * 0.01f, Mathf.Sin(t * 0.4f) * 0.015f, 0);
    }
}

#endif
