#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    private Control? _pauseOverlay;
    private Button? _pauseResumeButton;
    private Button? _pauseOptionsButton;
    private Button? _pauseMenuButton;
    private Button? _pauseQuitButton;

    private bool _isPaused;
    private double _pauseStartedAtMonotonicSeconds;

    private float _pausedCardAnimSpeedScale = 1.0f;
    private float _pausedDeckAnimSpeedScale = 1.0f;

    private void InitPauseUI()
    {
        // Overlay plein écran (sans modifier la scène).
        _pauseOverlay = new Control
        {
            Name = "PauseOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetTop = 0,
            OffsetRight = 0,
            OffsetBottom = 0,
        };

        var dim = new ColorRect
        {
            Name = "Dim",
            Color = new Color(0, 0, 0, 0.62f),
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetTop = 0,
            OffsetRight = 0,
            OffsetBottom = 0,
            MouseFilter = MouseFilterEnum.Stop,
        };
        _pauseOverlay.AddChild(dim);

        var center = new CenterContainer
        {
            Name = "Center",
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetTop = 0,
            OffsetRight = 0,
            OffsetBottom = 0,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _pauseOverlay.AddChild(center);

        var panel = new PanelContainer
        {
            Name = "Panel",
            CustomMinimumSize = new Vector2(520, 280),
            MouseFilter = MouseFilterEnum.Stop,
        };
        center.AddChild(panel);

        var margin = new MarginContainer
        {
            Name = "Margin",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);

        var vbox = new VBoxContainer
        {
            Name = "VBox",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        var title = new Label
        {
            Name = "Title",
            Text = "Pause",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 40);
        vbox.AddChild(title);

        var subtitle = new Label
        {
            Name = "Subtitle",
            Text = "Échap : reprendre",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(subtitle);

        vbox.AddChild(new HSeparator());

        _pauseResumeButton = new Button { Name = "Resume", Text = "Reprendre" };
        _pauseOptionsButton = new Button { Name = "Options", Text = "Options" };
        _pauseMenuButton = new Button { Name = "Menu", Text = "Revenir au menu" };
        _pauseQuitButton = new Button { Name = "Quit", Text = "Quitter" };

        _pauseResumeButton.Pressed += ResumeFromPause;
        _pauseOptionsButton.Pressed += OpenOptionsFromPause;
        _pauseMenuButton.Pressed += ReturnToMenuFromPause;
        _pauseQuitButton.Pressed += QuitFromPause;

        vbox.AddChild(_pauseResumeButton);
        vbox.AddChild(_pauseOptionsButton);
        vbox.AddChild(_pauseMenuButton);
        vbox.AddChild(_pauseQuitButton);

        AddChild(_pauseOverlay);
    }

    private bool IsPauseOverlayVisible()
    {
        return IsInstanceValid(_pauseOverlay) && _pauseOverlay!.Visible;
    }

    private void TogglePauseMenu()
    {
        if (!_runActive)
            return;

        if (IsPauseOverlayVisible())
            ResumeFromPause();
        else
            PauseRun();
    }

    private void PauseRun()
    {
        if (!_runActive || _isPaused)
            return;

        _isPaused = true;
        _pauseStartedAtMonotonicSeconds = Time.GetTicksMsec() / 1000.0;

        // Geler les AnimationPlayer sans casser l'état (resume propre).
        if (IsInstanceValid(_cardAnim))
        {
            _pausedCardAnimSpeedScale = _cardAnim!.SpeedScale;
            _cardAnim.SpeedScale = 0.0f;
        }
        if (IsInstanceValid(_deckAnim))
        {
            _pausedDeckAnimSpeedScale = _deckAnim!.SpeedScale;
            _deckAnim.SpeedScale = 0.0f;
        }

        if (IsInstanceValid(_pauseOverlay))
        {
            _pauseOverlay!.Visible = true;
            if (IsInstanceValid(_pauseResumeButton))
                _pauseResumeButton!.GrabFocus();
        }
    }

    private void ResumeFromPause()
    {
        if (!_isPaused)
            return;

        var now = Time.GetTicksMsec() / 1000.0;
        var pausedFor = Math.Max(0.0, now - _pauseStartedAtMonotonicSeconds);

        // Décale la base du chrono pour que le timer n'avance pas pendant la pause.
        _runStartMonotonicSeconds += pausedFor;

        _isPaused = false;

        if (IsInstanceValid(_pauseOverlay))
            _pauseOverlay!.Visible = false;

        // Restore animations.
        if (IsInstanceValid(_cardAnim))
            _cardAnim!.SpeedScale = _pausedCardAnimSpeedScale;
        if (IsInstanceValid(_deckAnim))
            _deckAnim!.SpeedScale = _pausedDeckAnimSpeedScale;
    }

    private void ForceExitPause()
    {
        if (IsPauseOverlayVisible())
            ResumeFromPause();
        else
            _isPaused = false;

        if (IsInstanceValid(_pauseOverlay))
            _pauseOverlay!.Visible = false;

        if (IsInstanceValid(_cardAnim))
            _cardAnim!.SpeedScale = 1.0f;
        if (IsInstanceValid(_deckAnim))
            _deckAnim!.SpeedScale = 1.0f;
    }

    private void OpenOptionsFromPause()
    {
        // On reste en pause; on ouvre simplement le popup existant.
        OpenOptionsPopup();
    }

    private void ReturnToMenuFromPause()
    {
        ForceExitPause();
        ShowReadyScreen();
    }

    private void QuitFromPause()
    {
        QuitGame();
    }
}
