using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class HighLevelInput
{
    private readonly SceneTree _sceneTree;
    private readonly MouseInput _mouseInput;

    public HighLevelInput(SceneTree sceneTree, MouseInput mouseInput)
    {
        _sceneTree = sceneTree;
        _mouseInput = mouseInput;
    }

    public ActionResult Hover(HoverRequest request)
    {
        if (!TryGetNodeCenter(request.NodePath, out var center, out var error))
            return error;
        return _mouseInput.Move(new MouseMoveRequest { X = center.X, Y = center.Y });
    }

    public ActionResult ClickNode(ClickNodeRequest request)
    {
        if (!TryGetNodeCenter(request.NodePath, out var center, out var error))
            return error;

        return _mouseInput.ClickAt(new MouseClickRequest
        {
            X = center.X,
            Y = center.Y,
            Button = request.Button > 0 ? request.Button : 1,
            ClickCount = request.ClickCount > 0 ? request.ClickCount : 1
        });
    }

    public ActionResult DragTo(DragRequest request)
    {
        if (!TryGetNodeCenter(request.FromNodePath, out var from, out var fromError))
            return fromError;
        if (!TryGetNodeCenter(request.ToNodePath, out var to, out var toError))
            return toError;

        var steps = request.Steps > 0 ? request.Steps : 10;

        // Mouse down at source
        _mouseInput.ButtonEvent(new MouseButtonRequest
        {
            X = from.X, Y = from.Y,
            Button = 1, Pressed = true
        });

        // Move through intermediate points
        for (int i = 1; i <= steps; i++)
        {
            var t = (float)i / steps;
            var pos = from.Lerp(to, t);
            _mouseInput.Move(new MouseMoveRequest { X = pos.X, Y = pos.Y });
        }

        // Mouse up at target
        _mouseInput.ButtonEvent(new MouseButtonRequest
        {
            X = to.X, Y = to.Y,
            Button = 1, Pressed = false
        });

        return new ActionResult { Success = true };
    }

    public ActionResult ScrollNode(ScrollNodeRequest request)
    {
        if (!TryGetNodeCenter(request.NodePath, out var center, out var error))
            return error;

        return _mouseInput.Wheel(new MouseWheelRequest
        {
            X = center.X,
            Y = center.Y,
            DeltaX = request.DeltaX,
            DeltaY = request.DeltaY
        });
    }

    private bool TryGetNodeCenter(string nodePath, out Vector2 center, out ActionResult error)
    {
        center = Vector2.Zero;
        error = new ActionResult { Success = true };

        var node = _sceneTree.Root.GetNodeOrNull(nodePath);
        if (node == null)
        {
            error = new ActionResult { Success = false, Error = $"Node not found: {nodePath}" };
            return false;
        }

        if (node is Control control)
        {
            center = control.GlobalPosition + control.Size / 2;
            return true;
        }

        if (node is Node2D node2d)
        {
            center = node2d.GlobalPosition;
            return true;
        }

        if (node is Node3D node3d)
        {
            var camera = _sceneTree.Root.GetCamera3D();
            if (camera != null && !camera.IsPositionBehind(node3d.GlobalPosition))
            {
                center = camera.UnprojectPosition(node3d.GlobalPosition);
                return true;
            }
            error = new ActionResult { Success = false, Error = $"Cannot project 3D position for {nodePath} (no camera or behind camera)" };
            return false;
        }

        error = new ActionResult { Success = false, Error = $"Cannot determine position for {nodePath} (not Control, Node2D, or Node3D)" };
        return false;
    }
}
