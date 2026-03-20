using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class SceneTreeInspector
{
    private readonly SceneTree _sceneTree;

    // Skip internal/infrastructure nodes that bloat the tree
    private static readonly HashSet<string> SkipNodeNames = new()
    {
        "GodotPlayServer", "McpBase"
    };

    public SceneTreeInspector(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public SceneTreeResponse GetSceneTree(SceneTreeRequest request = null)
    {
        var maxDepth = request?.MaxDepth > 0 ? request.MaxDepth : 4;

        Node startNode = _sceneTree.Root;
        if (!string.IsNullOrEmpty(request?.NodePath))
        {
            startNode = _sceneTree.Root.GetNodeOrNull(request.NodePath);
            if (startNode == null)
                return new SceneTreeResponse
                {
                    Root = new NodeInfo { Name = "error", Properties = { ["error"] = $"Node not found: {request.NodePath}" } },
                    CurrentScenePath = _sceneTree.CurrentScene?.SceneFilePath ?? ""
                };
        }

        return new SceneTreeResponse
        {
            Root = SerializeNode(startNode, maxDepth: maxDepth),
            CurrentScenePath = _sceneTree.CurrentScene?.SceneFilePath ?? ""
        };
    }

    public NodeList FindNodes(NodeQuery query)
    {
        var result = new NodeList();
        FindNodesRecursive(_sceneTree.Root, query, result);
        return result;
    }

    public PropertyMap GetNodeProperties(NodeRef nodeRef)
    {
        var node = _sceneTree.Root.GetNodeOrNull(nodeRef.Path);
        if (node == null) return new PropertyMap();

        var props = new PropertyMap();
        props.Properties["name"] = node.Name;
        props.Properties["class"] = node.GetClass();
        props.Properties["visible"] = (node is CanvasItem ci && ci.Visible).ToString();

        if (node is Control control)
        {
            props.Properties["size"] = $"{control.Size.X},{control.Size.Y}";
            props.Properties["position"] = $"{control.Position.X},{control.Position.Y}";
        }

        if (node is BaseButton button)
        {
            props.Properties["disabled"] = button.Disabled.ToString();
            props.Properties["text"] = (node as Button)?.Text ?? "";
        }

        if (node is Label label)
        {
            props.Properties["text"] = label.Text;
        }

        if (node is LineEdit lineEdit)
        {
            props.Properties["text"] = lineEdit.Text;
            props.Properties["placeholder"] = lineEdit.PlaceholderText;
        }

        return props;
    }

    private NodeInfo SerializeNode(Node node, int depth = 0, int maxDepth = 4)
    {
        var info = new NodeInfo
        {
            Path = node.GetPath(),
            ClassName = node.GetClass(),
            Name = node.Name
        };

        // Add key properties inline to reduce need for separate GetNodeProperties calls
        if (node is Button btn && !string.IsNullOrEmpty(btn.Text))
            info.Properties["text"] = btn.Text;
        if (node is Label lbl && !string.IsNullOrEmpty(lbl.Text))
            info.Properties["text"] = lbl.Text;
        if (node is CanvasItem ci && !ci.Visible)
            info.Properties["visible"] = "false";

        if (depth < maxDepth)
        {
            foreach (var child in node.GetChildren())
            {
                // Skip infrastructure autoloads and internal nodes
                if (depth == 0 && SkipNodeNames.Contains(child.Name))
                    continue;

                // Skip nodes with too many children (likely generated/data nodes)
                // but still show them as leaf nodes
                info.Children.Add(SerializeNode(child, depth + 1, maxDepth));
            }
        }
        else if (node.GetChildCount() > 0)
        {
            // Indicate there are more children without serializing them
            info.Properties["_childCount"] = node.GetChildCount().ToString();
        }

        return info;
    }

    private void FindNodesRecursive(Node node, NodeQuery query, NodeList result)
    {
        if (MatchesQuery(node, query))
            result.Nodes.Add(SerializeNode(node, maxDepth: 1));

        foreach (var child in node.GetChildren())
            FindNodesRecursive(child, query, result);
    }

    private bool MatchesQuery(Node node, NodeQuery query)
    {
        if (!string.IsNullOrEmpty(query.Path) && node.GetPath() != query.Path)
            return false;
        if (!string.IsNullOrEmpty(query.ClassName) && node.GetClass() != query.ClassName)
            return false;
        if (!string.IsNullOrEmpty(query.NamePattern))
        {
            var pattern = query.NamePattern;
            if (pattern.Contains('*'))
            {
                var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(node.Name, regex))
                    return false;
            }
            else if (node.Name != pattern)
                return false;
        }
        if (!string.IsNullOrEmpty(query.Group) && !node.IsInGroup(query.Group))
            return false;
        return true;
    }
}
