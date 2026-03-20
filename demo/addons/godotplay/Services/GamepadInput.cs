using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class GamepadInput
{
    private readonly SceneTree _sceneTree;

    public GamepadInput(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Button(GamepadButtonRequest request)
    {
        var button = ResolveButton(request.Button, request.ButtonName);
        var ev = new InputEventJoypadButton
        {
            Device = request.Device,
            ButtonIndex = button,
            Pressed = request.Pressed,
            Pressure = request.Pressed ? (request.Pressure > 0 ? request.Pressure : 1f) : 0f
        };
        Input.ParseInputEvent(ev);
        return new ActionResult { Success = true };
    }

    public ActionResult Axis(GamepadAxisRequest request)
    {
        var axis = ResolveAxis(request.Axis, request.AxisName);
        var ev = new InputEventJoypadMotion
        {
            Device = request.Device,
            Axis = axis,
            AxisValue = request.Value
        };
        Input.ParseInputEvent(ev);
        return new ActionResult { Success = true };
    }

    private static JoyButton ResolveButton(int button, string buttonName)
    {
        if (!string.IsNullOrEmpty(buttonName))
        {
            return buttonName.ToLowerInvariant() switch
            {
                "a" or "cross" => JoyButton.A,
                "b" or "circle" => JoyButton.B,
                "x" or "square" => JoyButton.X,
                "y" or "triangle" => JoyButton.Y,
                "lb" or "l1" or "left_shoulder" => JoyButton.LeftShoulder,
                "rb" or "r1" or "right_shoulder" => JoyButton.RightShoulder,
                "back" or "select" => JoyButton.Back,
                "start" => JoyButton.Start,
                "guide" or "home" => JoyButton.Guide,
                "left_stick" or "l3" => JoyButton.LeftStick,
                "right_stick" or "r3" => JoyButton.RightStick,
                "dpad_up" => JoyButton.DpadUp,
                "dpad_down" => JoyButton.DpadDown,
                "dpad_left" => JoyButton.DpadLeft,
                "dpad_right" => JoyButton.DpadRight,
                _ => Enum.TryParse<JoyButton>(buttonName, ignoreCase: true, out var parsed)
                    ? parsed
                    : throw new ArgumentException($"Unknown gamepad button: {buttonName}")
            };
        }
        return (JoyButton)button;
    }

    private static JoyAxis ResolveAxis(int axis, string axisName)
    {
        if (!string.IsNullOrEmpty(axisName))
        {
            return axisName.ToLowerInvariant() switch
            {
                "left_x" or "lx" => JoyAxis.LeftX,
                "left_y" or "ly" => JoyAxis.LeftY,
                "right_x" or "rx" => JoyAxis.RightX,
                "right_y" or "ry" => JoyAxis.RightY,
                "trigger_left" or "lt" or "l2" => JoyAxis.TriggerLeft,
                "trigger_right" or "rt" or "r2" => JoyAxis.TriggerRight,
                _ => Enum.TryParse<JoyAxis>(axisName, ignoreCase: true, out var parsed)
                    ? parsed
                    : throw new ArgumentException($"Unknown gamepad axis: {axisName}")
            };
        }
        return (JoyAxis)axis;
    }
}
