using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class InputSimulator
{
    private readonly SceneTree _sceneTree;

    public InputSimulator(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Click(NodeRef nodeRef)
    {
        var node = _sceneTree.Root.GetNodeOrNull(nodeRef.Path);
        if (node == null)
            return new ActionResult { Success = false, Error = $"Node not found: {nodeRef.Path}" };

        if (node is BaseButton button)
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
            return new ActionResult { Success = true };
        }

        if (node is Control control)
        {
            var click = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = control.Size / 2
            };
            // Emit signal first — needed for nodes using gui_input.connect()
            // (e.g. tab bars, menu items, star nodes on galaxy maps).
            // Then call virtual method — needed for nodes overriding _gui_input()
            // (e.g. modal overlays, custom input handlers).
            control.EmitSignal(Control.SignalName.GuiInput, click);
            control._GuiInput(click);

            var release = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = false,
                Position = control.Size / 2
            };
            control.EmitSignal(Control.SignalName.GuiInput, release);
            control._GuiInput(release);

            return new ActionResult { Success = true };
        }

        return new ActionResult { Success = false, Error = $"Node {nodeRef.Path} is not a clickable Control." };
    }
}
