using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class TextInput
{
    private readonly SceneTree _sceneTree;

    public TextInput(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Type(TypeRequest request)
    {
        var node = _sceneTree.Root.GetNodeOrNull(request.NodePath);
        if (node == null)
            return new ActionResult { Success = false, Error = $"Node not found: {request.NodePath}" };

        if (node is LineEdit lineEdit)
        {
            if (request.ClearFirst) lineEdit.Text = "";
            lineEdit.Text += request.Text;
            lineEdit.EmitSignal(LineEdit.SignalName.TextChanged, lineEdit.Text);
            return new ActionResult { Success = true };
        }

        if (node is TextEdit textEdit)
        {
            if (request.ClearFirst) textEdit.Text = "";
            textEdit.Text += request.Text;
            textEdit.EmitSignal(TextEdit.SignalName.TextChanged);
            return new ActionResult { Success = true };
        }

        return new ActionResult { Success = false, Error = $"Node {request.NodePath} is not a text input." };
    }

    public ActionResult SetProperty(SetPropertyRequest request)
    {
        var node = _sceneTree.Root.GetNodeOrNull(request.NodePath);
        if (node == null)
            return new ActionResult { Success = false, Error = $"Node not found: {request.NodePath}" };

        var variant = ParseVariant(request.Value);
        node.Set(request.PropertyName, variant);
        return new ActionResult { Success = true };
    }

    public ActionResult LoadScene(LoadSceneRequest request)
    {
        var error = _sceneTree.ChangeSceneToFile(request.ScenePath);
        if (error != Error.Ok)
            return new ActionResult { Success = false, Error = $"Failed to load scene: {error}" };
        return new ActionResult { Success = true };
    }

    public SceneInfo GetCurrentScene()
    {
        var scene = _sceneTree.CurrentScene;
        return new SceneInfo
        {
            ScenePath = scene?.SceneFilePath ?? "",
            RootNodePath = scene?.GetPath() ?? "",
            RootClassName = scene?.GetClass() ?? ""
        };
    }

    private static Variant ParseVariant(string value)
    {
        if (bool.TryParse(value, out var b)) return b;
        if (int.TryParse(value, out var i)) return i;
        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var f)) return f;
        return value;
    }
}
