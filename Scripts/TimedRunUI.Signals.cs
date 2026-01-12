#nullable enable

using Godot;

public partial class TimedRunUI : Control
{
    private static readonly StringName SignalPressed = new("pressed");
    private static readonly StringName SignalToggled = new("toggled");
    private static readonly StringName SignalValueChanged = new("value_changed");
    private static readonly StringName SignalAnimationFinished = new("animation_finished");

    private static void SafeConnectNoDup(GodotObject? obj, StringName signal, Callable callable)
    {
        if (!GodotObject.IsInstanceValid(obj))
            return;

        if (!obj!.IsConnected(signal, callable))
            obj.Connect(signal, callable);
    }

    private static void SafeReconnect(GodotObject? obj, StringName signal, Callable callable)
    {
        if (!GodotObject.IsInstanceValid(obj))
            return;

        if (obj!.IsConnected(signal, callable))
            obj.Disconnect(signal, callable);

        obj.Connect(signal, callable);
    }
}
