#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    private float _hoverStart;
    private float _hoverSettings;
    private float _hoverQuit;
    private double _menuPulseTime;

    private void HookMenuHover(Control? startBtn, Control? settingsBtn, Control? quitBtn)
    {
        if (IsInstanceValid(startBtn))
        {
            startBtn!.MouseEntered -= OnStartHoverEnter;
            startBtn.MouseEntered += OnStartHoverEnter;
            startBtn.MouseExited -= OnStartHoverExit;
            startBtn.MouseExited += OnStartHoverExit;
        }

        if (IsInstanceValid(settingsBtn))
        {
            settingsBtn!.MouseEntered -= OnSettingsHoverEnter;
            settingsBtn.MouseEntered += OnSettingsHoverEnter;
            settingsBtn.MouseExited -= OnSettingsHoverExit;
            settingsBtn.MouseExited += OnSettingsHoverExit;
        }

        if (IsInstanceValid(quitBtn))
        {
            quitBtn!.MouseEntered -= OnQuitHoverEnter;
            quitBtn.MouseEntered += OnQuitHoverEnter;
            quitBtn.MouseExited -= OnQuitHoverExit;
            quitBtn.MouseExited += OnQuitHoverExit;
        }
    }

    private void OnStartHoverEnter() => _hoverStart = 1f;
    private void OnStartHoverExit() => _hoverStart = 0f;
    private void OnSettingsHoverEnter() => _hoverSettings = 1f;
    private void OnSettingsHoverExit() => _hoverSettings = 0f;
    private void OnQuitHoverEnter() => _hoverQuit = 1f;
    private void OnQuitHoverExit() => _hoverQuit = 0f;

    private void UpdateMenuMotion(double delta)
    {
        if (!IsInstanceValid(_panelRoot) || !_panelRoot.Visible)
            return;

        _menuPulseTime += delta;

        // Petite pulsation sur "Commencer" pour attirer l'œil, très subtile.
        var isReady = IsInstanceValid(_restartButton) && _restartButton.Visible && _restartButton.Text == "Commencer";
        var pulse = isReady ? (float)Math.Sin(_menuPulseTime * (Math.PI * 2.0) / 2.6) : 0f;
        var pulseScale = isReady ? 0.012f * pulse : 0f;

        ApplyMenuScale(_restartButton, _hoverStart, baseHoverScale: 0.04f, extra: pulseScale);
        ApplyMenuScale(_menuSettingsButton, _hoverSettings, baseHoverScale: 0.025f, extra: 0f);
        ApplyMenuScale(_menuQuitButton, _hoverQuit, baseHoverScale: 0.025f, extra: 0f);
    }

    private static void ApplyMenuScale(Control? control, float hover, float baseHoverScale, float extra)
    {
        if (!GodotObject.IsInstanceValid(control))
            return;

        var target = 1f + (hover * baseHoverScale) + extra;
        var cur = control!.Scale;

        // Lerp simple (pas de Tween) : rapide mais smooth.
        var next = new Vector2(
            Mathf.Lerp(cur.X, target, 0.18f),
            Mathf.Lerp(cur.Y, target, 0.18f));

        control.Scale = next;
    }
}
