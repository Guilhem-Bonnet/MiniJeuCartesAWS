#nullable enable

using Godot;
using System;

public partial class TimedRunUI : Control
{
    [Export] public bool EnableDebugOverlayByDefault { get; set; } = true;

    private Label? _debugLabel;
    private bool _debugOverlayVisible;
    private double _debugOverlayNextUpdateAt;
    private string _lastRuntimeError = "";

    private void InitDebugOverlay()
    {
        // Par défaut: visible seulement en debug build, et uniquement si l’export le permet.
        _debugOverlayVisible = EnableDebugOverlayByDefault && OS.IsDebugBuild();

        try
        {
            var topHud = GetNodeOrNull<Control>("TopHUD");
            if (!IsInstanceValid(topHud))
                return;

            var label = new Label
            {
                Name = "DebugOverlay",
                Visible = _debugOverlayVisible,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                MouseFilter = MouseFilterEnum.Ignore,
                Text = "",
            };

            // Style minimal et lisible.
            label.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f, 0.95f));
            label.AddThemeFontSizeOverride("font_size", 14);

            topHud!.AddChild(label);
            label.AnchorLeft = 0;
            label.AnchorTop = 0;
            label.AnchorRight = 0;
            label.AnchorBottom = 0;
            label.OffsetLeft = 30;
            label.OffsetTop = 58;
            label.OffsetRight = 900;
            label.OffsetBottom = 280;

            _debugLabel = label;
        }
        catch
        {
            // Pas de debug overlay si la scène change.
        }
    }

    private void ToggleDebugOverlay()
    {
        _debugOverlayVisible = !_debugOverlayVisible;
        if (IsInstanceValid(_debugLabel))
            _debugLabel!.Visible = _debugOverlayVisible;
        _debugOverlayNextUpdateAt = 0;
    }

    private void SetLastRuntimeError(string where, Exception ex)
    {
        var full = ex.ToString();
        if (full.Length > 800)
            full = full[..800] + "…";

        _lastRuntimeError = $"{where}: {full}";
        GD.PushError($"[MiniJeuCartesAWS] {where}: {ex}");
    }

    private void SetLastRuntimeError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _lastRuntimeError = message.Length > 800 ? message[..800] + "…" : message;
        GD.PushWarning($"[MiniJeuCartesAWS] {message}");
    }

    private void UpdateDebugOverlay(double delta)
    {
        if (!_debugOverlayVisible)
            return;
        if (!IsInstanceValid(_debugLabel))
            return;

        var now = Time.GetTicksMsec() / 1000.0;
        if (now < _debugOverlayNextUpdateAt)
            return;
        _debugOverlayNextUpdateAt = now + 0.15;

        var cardAnimOk = IsInstanceValid(_cardAnim);
        var deckAnimOk = IsInstanceValid(_deckAnim);
        var hasDraw = cardAnimOk && _cardAnim!.HasAnimation(AnimCardDraw);
        var playing = cardAnimOk && _cardAnim!.IsPlaying();

        var cardRigPos = IsInstanceValid(_cardRig) ? _cardRig.GlobalPosition : new Vector3(float.NaN, float.NaN, float.NaN);
        var focusPos = IsInstanceValid(_cardFocus) ? _cardFocus!.GlobalPosition : new Vector3(float.NaN, float.NaN, float.NaN);
        var spawnPos = IsInstanceValid(_deckSpawn) ? _deckSpawn!.GlobalPosition : new Vector3(float.NaN, float.NaN, float.NaN);

        var scene = GetTree().CurrentScene;
        var sceneInfo = scene == null ? "null" : $"{scene.Name} ({scene.GetPath()})";
        var hudInfo = $"{Name} ({GetPath()})";

        _debugLabel!.Text =
            "[DEBUG F3]\n" +
            $"scene={sceneInfo}\n" +
            $"hud={hudInfo}\n" +
            $"runActive={_runActive} qTok={_questionToken} hasQ={_hasCurrentQuestion} answered={_answeredCurrent} back={_cardIsBackSide}\n" +
            $"deckSpawn={IsInstanceValid(_deckSpawn)} cardFocus={IsInstanceValid(_cardFocus)}\n" +
            $"cardAnim={cardAnimOk} has(card_draw)={hasDraw} playing={playing} deckAnim={deckAnimOk}\n" +
            $"cardRigPos={cardRigPos} focusPos={focusPos} spawnPos={spawnPos}\n" +
            $"lastError={_lastRuntimeError}";
    }
}
