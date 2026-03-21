using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class KeyboardInput
{
    private readonly SceneTree _sceneTree;

    public KeyboardInput(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Down(KeyRequest request)
    {
        try
        {
            var key = ResolveKey(request.Keycode, request.KeyLabel);
            var ev = CreateKeyEvent(key, true, request.Shift, request.Ctrl, request.Alt, request.Meta);
            Input.ParseInputEvent(ev);
            return new ActionResult { Success = true };
        }
        catch (ArgumentException ex)
        {
            return new ActionResult { Success = false, Error = ex.Message };
        }
    }

    public ActionResult Up(KeyRequest request)
    {
        try
        {
            var key = ResolveKey(request.Keycode, request.KeyLabel);
            var ev = CreateKeyEvent(key, false, request.Shift, request.Ctrl, request.Alt, request.Meta);
            Input.ParseInputEvent(ev);
            return new ActionResult { Success = true };
        }
        catch (ArgumentException ex)
        {
            return new ActionResult { Success = false, Error = ex.Message };
        }
    }

    public ActionResult Press(KeyPressRequest request)
    {
        try
        {
            var key = ResolveKey(request.Keycode, request.KeyLabel);

            var down = CreateKeyEvent(key, true, request.Shift, request.Ctrl, request.Alt, request.Meta);
            Input.ParseInputEvent(down);

            var up = CreateKeyEvent(key, false, request.Shift, request.Ctrl, request.Alt, request.Meta);
            Input.ParseInputEvent(up);

            return new ActionResult { Success = true };
        }
        catch (ArgumentException ex)
        {
            return new ActionResult { Success = false, Error = ex.Message };
        }
    }

    private static InputEventKey CreateKeyEvent(Key key, bool pressed, bool shift, bool ctrl, bool alt, bool meta)
    {
        var ev = new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = pressed,
            ShiftPressed = shift,
            CtrlPressed = ctrl,
            AltPressed = alt,
            MetaPressed = meta
        };

        if (key >= Key.Space && key <= Key.Asciitilde)
        {
            var c = (char)key;
            if (shift && char.IsLetter(c))
                c = char.ToUpper(c);
            else if (!shift && char.IsLetter(c))
                c = char.ToLower(c);
            ev.Unicode = c;
        }

        return ev;
    }

    private static Key ResolveKey(int keycode, string keyLabel)
    {
        if (!string.IsNullOrEmpty(keyLabel))
        {
            return keyLabel.ToLowerInvariant() switch
            {
                "enter" or "return" => Key.Enter,
                "space" => Key.Space,
                "tab" => Key.Tab,
                "escape" or "esc" => Key.Escape,
                "backspace" => Key.Backspace,
                "delete" or "del" => Key.Delete,
                "insert" => Key.Insert,
                "home" => Key.Home,
                "end" => Key.End,
                "pageup" => Key.Pageup,
                "pagedown" => Key.Pagedown,
                "up" => Key.Up,
                "down" => Key.Down,
                "left" => Key.Left,
                "right" => Key.Right,
                "shift" => Key.Shift,
                "ctrl" or "control" => Key.Ctrl,
                "alt" => Key.Alt,
                "meta" or "super" or "win" or "cmd" => Key.Meta,
                "capslock" => Key.Capslock,
                "f1" => Key.F1, "f2" => Key.F2, "f3" => Key.F3, "f4" => Key.F4,
                "f5" => Key.F5, "f6" => Key.F6, "f7" => Key.F7, "f8" => Key.F8,
                "f9" => Key.F9, "f10" => Key.F10, "f11" => Key.F11, "f12" => Key.F12,
                _ => Enum.TryParse<Key>(keyLabel, ignoreCase: true, out var parsed)
                    ? parsed
                    : keyLabel.Length == 1
                        ? (Key)char.ToUpper(keyLabel[0])
                        : throw new ArgumentException($"Unknown key: {keyLabel}")
            };
        }

        if (keycode > 0)
            return (Key)keycode;

        throw new ArgumentException("Either keycode or key_label must be provided");
    }
}
