using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class MouseInput
{
    private readonly SceneTree _sceneTree;
    private Vector2 _lastMousePosition;

    public MouseInput(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Move(MouseMoveRequest request)
    {
        var pos = new Vector2(request.X, request.Y);
        var relative = pos - _lastMousePosition;

        var ev = new InputEventMouseMotion
        {
            Position = pos,
            GlobalPosition = pos,
            Relative = relative,
            Velocity = relative.Normalized() * 1000
        };
        Input.ParseInputEvent(ev);
        Input.WarpMouse(pos);
        _lastMousePosition = pos;
        return new ActionResult { Success = true };
    }

    public ActionResult ButtonEvent(MouseButtonRequest request)
    {
        var pos = new Vector2(request.X, request.Y);
        var button = request.Button > 0 ? (MouseButton)request.Button : MouseButton.Left;
        var ev = new InputEventMouseButton
        {
            Position = pos,
            GlobalPosition = pos,
            ButtonIndex = button,
            Pressed = request.Pressed,
            DoubleClick = request.DoubleClick,
            ShiftPressed = request.Shift,
            CtrlPressed = request.Ctrl,
            AltPressed = request.Alt,
            MetaPressed = request.Meta
        };
        Input.ParseInputEvent(ev);
        _lastMousePosition = pos;
        return new ActionResult { Success = true };
    }

    public ActionResult ClickAt(MouseClickRequest request)
    {
        var pos = new Vector2(request.X, request.Y);
        var button = request.Button > 0 ? (MouseButton)request.Button : MouseButton.Left;
        var count = request.ClickCount > 0 ? request.ClickCount : 1;

        // Move mouse to position first
        Move(new MouseMoveRequest { X = request.X, Y = request.Y });

        for (int i = 0; i < count; i++)
        {
            var press = new InputEventMouseButton
            {
                Position = pos,
                GlobalPosition = pos,
                ButtonIndex = button,
                Pressed = true,
                DoubleClick = i == 1,
                ShiftPressed = request.Shift,
                CtrlPressed = request.Ctrl,
                AltPressed = request.Alt,
                MetaPressed = request.Meta
            };
            Input.ParseInputEvent(press);

            var release = new InputEventMouseButton
            {
                Position = pos,
                GlobalPosition = pos,
                ButtonIndex = button,
                Pressed = false,
                ShiftPressed = request.Shift,
                CtrlPressed = request.Ctrl,
                AltPressed = request.Alt,
                MetaPressed = request.Meta
            };
            Input.ParseInputEvent(release);
        }

        return new ActionResult { Success = true };
    }

    public ActionResult Wheel(MouseWheelRequest request)
    {
        var pos = new Vector2(request.X, request.Y);

        if (request.DeltaY != 0)
        {
            var button = request.DeltaY > 0 ? MouseButton.WheelUp : MouseButton.WheelDown;
            var ev = new InputEventMouseButton
            {
                Position = pos,
                GlobalPosition = pos,
                ButtonIndex = button,
                Pressed = true,
                Factor = Math.Abs(request.DeltaY)
            };
            Input.ParseInputEvent(ev);
        }

        if (request.DeltaX != 0)
        {
            var button = request.DeltaX > 0 ? MouseButton.WheelRight : MouseButton.WheelLeft;
            var ev = new InputEventMouseButton
            {
                Position = pos,
                GlobalPosition = pos,
                ButtonIndex = button,
                Pressed = true,
                Factor = Math.Abs(request.DeltaX)
            };
            Input.ParseInputEvent(ev);
        }

        return new ActionResult { Success = true };
    }
}
