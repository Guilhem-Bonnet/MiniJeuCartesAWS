#nullable enable

using Godot;
using System;
using System.Linq;

public partial class TimedRunUI : Control
{
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed)
            return;

        if (!_runActive)
        {
            if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space)
            {
                if (IsInstanceValid(_restartButton) && _restartButton.Visible && !_restartButton.Disabled)
                    StartRun();
            }
            return;
        }

        if (key.Keycode == Key.Key1) Choose(0);
        else if (key.Keycode == Key.Key2) Choose(1);
        else if (key.Keycode == Key.Key3) Choose(2);
        else if (key.Keycode == Key.Key4) Choose(3);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_runActive)
            return;

        // Clic gauche sur la carte => choisir une réponse (sans UI overlay).
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (_answeredCurrent && _cardIsBackSide && _awaitingContinueClick)
                TryContinueByCardClick();
            else if (!_answeredCurrent && !_cardIsBackSide)
                TryChooseByCardClick();
            return;
        }

        // Clavier 1–4 (plus "tôt" que _UnhandledInput)
        if (@event is InputEventKey key && key.Pressed)
        {
            if (key.Keycode == Key.Key1) Choose(0);
            else if (key.Keycode == Key.Key2) Choose(1);
            else if (key.Keycode == Key.Key3) Choose(2);
            else if (key.Keycode == Key.Key4) Choose(3);
        }
    }

    private void TryChooseByCardClick()
    {
        if (!_runActive || _answeredCurrent || _cardIsBackSide)
            return;
        if (!IsInstanceValid(_camera) || !IsInstanceValid(_cardFrontMesh) || !IsInstanceValid(_cardClickArea))
            return;

        var vp = GetViewport();
        var mouse = vp.GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var dir = _camera.ProjectRayNormal(mouse);
        var to = from + dir * 10.0f;

        var world = _camera.GetWorld3D();
        if (world == null)
            return;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = CardClickCollisionMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var hit = world.DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
            return;

        if (!hit.TryGetValue("collider", out var colliderObj))
            return;
        var colliderGo = colliderObj.AsGodotObject();
        if (colliderGo == null || colliderGo != _cardClickArea)
            return;

        if (!hit.TryGetValue("position", out var posObj))
            return;
        var hitWorld = (Vector3)posObj;

        if (!TryMapHitToAnswerIndex(hitWorld, out var idx))
            return;

        Choose(idx);
    }

    private void TryContinueByCardClick()
    {
        if (!_runActive || !_answeredCurrent || !_cardIsBackSide)
            return;
        if (!_awaitingContinueClick)
            return;
        if (!IsInstanceValid(_camera) || !IsInstanceValid(_cardClickArea))
            return;

        var vp = GetViewport();
        var mouse = vp.GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var dir = _camera.ProjectRayNormal(mouse);
        var to = from + dir * 10.0f;

        var world = _camera.GetWorld3D();
        if (world == null)
            return;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = CardClickCollisionMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var hit = world.DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
            return;

        if (!hit.TryGetValue("collider", out var colliderObj))
            return;
        var colliderGo = colliderObj.AsGodotObject();
        if (colliderGo == null || colliderGo != _cardClickArea)
            return;

        _awaitingContinueClick = false;
        AnimateCardSendAwayAndMaybeShuffle(_continueRunToken, _continueQuestionToken);
    }

    private bool TryMapHitToAnswerIndex(Vector3 hitWorld, out int answerIndex)
    {
        answerIndex = -1;

        // Convertit la position hit en coordonnées locales de la face (PlaneMesh en XZ).
        var p = _cardFrontMesh.ToLocal(hitWorld);
        var plane = GetCardPlaneSize();
        var w = Math.Max(0.0001f, plane.X);
        var h = Math.Max(0.0001f, plane.Y);

        var u = (p.X / w) + 0.5f;
        var v = (p.Z / h) + 0.5f; // 0 en haut (Z=-h/2), 1 en bas (Z=+h/2)

        // Mapping précis: on utilise la mise en page réelle (rectangles des 4 réponses dans le SubViewport).
        if (IsInstanceValid(_cardFrontViewport) && _cardAnswerRows.Count == 4 && _cardAnswerRows.All(IsInstanceValid))
        {
            var vpSize = _cardFrontViewport.Size;
            var pt = new Vector2(u * vpSize.X, v * vpSize.Y);

            for (var i = 0; i < _cardAnswerRows.Count; i++)
            {
                var rect = _cardAnswerRows[i].GetGlobalRect();
                if (rect.HasPoint(pt))
                {
                    answerIndex = i;
                    return true;
                }
            }
        }

        // Fallback (ancienne logique par bandes)
        if (u < ClickAnswerRegionUMargin || u > 1.0f - ClickAnswerRegionUMargin)
            return false;

        var top = Mathf.Min(ClickAnswerRegionVTop, ClickAnswerRegionVBottom);
        var bottom = Mathf.Max(ClickAnswerRegionVTop, ClickAnswerRegionVBottom);
        if (v < top || v > bottom)
            return false;

        var t = (v - top) / Mathf.Max(0.0001f, bottom - top);
        var idx = Mathf.Clamp((int)(t * 4.0f), 0, 3);
        answerIndex = idx;
        return true;
    }

    private Vector2 GetCardPlaneSize()
    {
        try
        {
            if (IsInstanceValid(_cardFrontMesh) && _cardFrontMesh.Mesh is PlaneMesh pm)
                return pm.Size;
        }
        catch
        {
        }

        // Fallback (paysage)
        return new Vector2(1.344f, 0.96f);
    }
}
