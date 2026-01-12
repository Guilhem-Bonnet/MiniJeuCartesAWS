#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    // ============================================================================
    // ANIMATIONS (AnimationPlayer only)
    // - Pas de Tween: on s'appuie sur les clips déjà créés dans la scène.
    // - Alignement via "offset parent" pour éviter les snaps des tracks `transform`.
    // ============================================================================

    private void SuppressIdle(double seconds)
    {
        var now = Time.GetTicksMsec() / 1000.0;
        _idleResumeAt = Math.Max(_idleResumeAt, now + Math.Max(0, seconds));
        _idleAnchorValid = false;
    }

    private void CancelInFlightAnimations()
    {
        if (IsInstanceValid(_cardAnim))
            _cardAnim!.Stop();
        if (IsInstanceValid(_deckAnim))
            _deckAnim!.Stop();

        _cardMotionAnchorValid = false;

        _pendingAnimAction = PendingAnimAction.None;
        _pendingAnimName = "";
        _idleAnchorValid = false;
    }

    internal static Node3D EnsureOffsetParent(Node3D node, string offsetName)
    {
        var parent = node.GetParent();
        if (parent is not Node3D parent3d)
            return node;

        if (parent3d.Name == offsetName)
            return parent3d;

        var offset = new Node3D { Name = offsetName };
        parent3d.AddChild(offset);

        offset.GlobalTransform = node.GlobalTransform;
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
                if (!path.EndsWith(":transform", StringComparison.Ordinal) &&
                    !path.EndsWith(":global_transform", StringComparison.Ordinal))
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

    private bool TryPlayAlignedTransformAnim(
        AnimationPlayer? player,
        Node3D? rig,
        Node3D? rigOffset,
        string animName,
        Transform3D desiredRigStartGlobal,
        float speed,
        bool fromEnd)
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

        if (TryGetAnimTransformSample(anim, fromEnd ? anim.Length : 0.0, out var animStartLocal))
        {
            rigOffset!.GlobalTransform = desiredRigStartGlobal * animStartLocal.AffineInverse();
            rig!.Transform = animStartLocal;
        }

        player.Play(animName, customBlend: -1, customSpeed: speed, fromEnd: fromEnd);
        return true;
    }

    private void OnDeckAnimationFinished(StringName animName)
    {
        // Rien à enchaîner pour le deck.
    }

    private void OnCardAnimationFinished(StringName animName)
    {
        // Après la pioche: on fige la base de micro-motion sur la pose finale du clip,
        // pour éviter tout snap au moment où l'utilisateur peut choisir.
        if (string.Equals(animName.ToString(), AnimCardDraw, StringComparison.Ordinal))
        {
            _cardMotionAnchorValid = true;
            _cardMotionAnchorGlobal = _cardRig.GlobalTransform;
        }

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

            _visualDiscardCount = Math.Min(_visualDeckCapacity, _visualDiscardCount + 1);
            UpdateDiscardVisual();

            if (_visualDeckRemaining <= 0)
            {
                AnimateDeckShuffle();
                _visualDeckRemaining = _visualDeckCapacity;
                UpdateDeckVisual();

                // Après shuffle: la défausse retourne dans le deck (visuel).
                _visualDiscardCount = 0;
                UpdateDiscardVisual();
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
        _cardIsBackSide = false;
        _cardMotionAnchorValid = false;

        PlaySfx(_sfxDraw, pitch: 0.98f + (float)_rng.NextDouble() * 0.06f);

        if (!IsInstanceValid(_deckSpawn) || !IsInstanceValid(_cardFocus))
        {
            _cardRig.GlobalTransform = GetFocusTransformGlobal();
            _cardMotionAnchorValid = true;
            _cardMotionAnchorGlobal = _cardRig.GlobalTransform;
            return;
        }

        var startT = _deckSpawn!.GlobalTransform;
        if (TryPlayAlignedTransformAnim(_cardAnim, _cardRig, _cardRigOffset, AnimCardDraw, startT, speed: 1.0f, fromEnd: false))
            return;

        _cardRig.GlobalTransform = GetFocusTransformGlobal();
        _cardMotionAnchorValid = true;
        _cardMotionAnchorGlobal = _cardRig.GlobalTransform;
    }

    private void AnimateCardRevealBack()
    {
        SuppressIdle(0.70);
        PlaySfx(_sfxFlip, pitch: 0.97f + (float)_rng.NextDouble() * 0.06f);

        _cardIsBackSide = true;
        var startT = _cardRig.GlobalTransform;

        // Important: ne jamais jouer le clip "à l'envers" (customSpeed négatif / fromEnd)
        // car ça inverse parfois le sens du flip par rapport à l'animation voulue.
        if (TryPlayAlignedTransformAnim(_cardAnim, _cardRig, _cardRigOffset, AnimCardFlipFallbackLeft, startT, speed: 1.0f, fromEnd: false))
            return;
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

        var startT = _cardRig.GlobalTransform;
        if (TryPlayAlignedTransformAnim(_cardAnim, _cardRig, _cardRigOffset, AnimCardSendAway, startT, speed: 1.0f, fromEnd: false))
        {
            _pendingAnimAction = PendingAnimAction.CardSendAwayAndMaybeShuffle;
            _pendingAnimName = AnimCardSendAway;
            _pendingRunToken = runTokenAtStart;
            _pendingQuestionToken = questionTokenAtStart;
            return;
        }

        _visualDeckRemaining = Math.Max(0, _visualDeckRemaining - 1);
        UpdateDeckVisual();

        _visualDiscardCount = Math.Min(_visualDeckCapacity, _visualDiscardCount + 1);
        UpdateDiscardVisual();

        if (_visualDeckRemaining <= 0)
        {
            AnimateDeckShuffle();
            _visualDeckRemaining = _visualDeckCapacity;
            UpdateDeckVisual();

            _visualDiscardCount = 0;
            UpdateDiscardVisual();
        }

        if (_runActive)
            NextQuestion();
    }

    private void AnimateDeckShuffle()
    {
        if (!IsInstanceValid(_deckRig))
            return;

        SuppressIdle(0.75);
        PlaySfx(_sfxShuffle, pitch: 0.96f + (float)_rng.NextDouble() * 0.08f);

        var startT = _deckRig!.GlobalTransform;
        TryPlayAlignedTransformAnim(_deckAnim, _deckRig, _deckRigOffset, AnimDeckShuffle, startT, speed: 1.0f, fromEnd: false);
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
    }

    private void FxWrong()
    {
        SuppressIdle(0.20);
        PlaySfx(_sfxWrong, pitch: 0.98f + (float)_rng.NextDouble() * 0.05f);
    }

    private void AnimateCardIdle()
    {
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
