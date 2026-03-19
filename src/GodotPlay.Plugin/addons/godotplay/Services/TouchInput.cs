using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class TouchInput
{
    private readonly SceneTree _sceneTree;

    public TouchInput(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Touch(TouchRequest request)
    {
        var ev = new InputEventScreenTouch
        {
            Index = request.Index,
            Position = new Vector2(request.X, request.Y),
            Pressed = request.Pressed
        };
        Input.ParseInputEvent(ev);
        return new ActionResult { Success = true };
    }

    public ActionResult Drag(TouchDragRequest request)
    {
        var from = new Vector2(request.FromX, request.FromY);
        var to = new Vector2(request.ToX, request.ToY);
        var steps = request.Steps > 0 ? request.Steps : 10;
        var index = request.Index;

        var touchDown = new InputEventScreenTouch
        {
            Index = index,
            Position = from,
            Pressed = true
        };
        Input.ParseInputEvent(touchDown);

        for (int i = 1; i <= steps; i++)
        {
            var t = (float)i / steps;
            var pos = from.Lerp(to, t);
            var prev = from.Lerp(to, (float)(i - 1) / steps);

            var drag = new InputEventScreenDrag
            {
                Index = index,
                Position = pos,
                Relative = pos - prev,
                Velocity = (to - from).Normalized() * 1000
            };
            Input.ParseInputEvent(drag);
        }

        var touchUp = new InputEventScreenTouch
        {
            Index = index,
            Position = to,
            Pressed = false
        };
        Input.ParseInputEvent(touchUp);

        return new ActionResult { Success = true };
    }

    public ActionResult HandleGesture(GestureRequest request)
    {
        var pos = new Vector2(request.X, request.Y);

        switch (request.Type.ToLowerInvariant())
        {
            case "pinch":
            {
                var ev = new InputEventMagnifyGesture
                {
                    Position = pos,
                    Factor = request.Factor
                };
                Input.ParseInputEvent(ev);
                break;
            }
            case "pan":
            {
                var ev = new InputEventPanGesture
                {
                    Position = pos,
                    Delta = new Vector2(request.DeltaX, request.DeltaY)
                };
                Input.ParseInputEvent(ev);
                break;
            }
            default:
                return new ActionResult { Success = false, Error = $"Unknown gesture type: {request.Type}. Use 'pinch' or 'pan'." };
        }

        return new ActionResult { Success = true };
    }
}
