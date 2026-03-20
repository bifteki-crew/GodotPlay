using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class ActionInput
{
    private readonly SceneTree _sceneTree;

    public ActionInput(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Event(ActionRequest request)
    {
        if (!InputMap.HasAction(request.Action))
            return new ActionResult { Success = false, Error = $"Unknown action: {request.Action}. Check your Input Map." };

        var ev = new InputEventAction
        {
            Action = request.Action,
            Pressed = request.Pressed,
            Strength = request.Strength > 0 ? request.Strength : 1f
        };
        Input.ParseInputEvent(ev);
        return new ActionResult { Success = true };
    }

    public ActionResult Press(ActionPressRequest request)
    {
        if (!InputMap.HasAction(request.Action))
            return new ActionResult { Success = false, Error = $"Unknown action: {request.Action}. Check your Input Map." };

        var strength = request.Strength > 0 ? request.Strength : 1f;

        // Press and release synchronously (no timer — RunOnMainThread requires synchronous completion)
        var press = new InputEventAction
        {
            Action = request.Action,
            Pressed = true,
            Strength = strength
        };
        Input.ParseInputEvent(press);

        var release = new InputEventAction
        {
            Action = request.Action,
            Pressed = false,
            Strength = 0
        };
        Input.ParseInputEvent(release);

        return new ActionResult { Success = true };
    }
}
