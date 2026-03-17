using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class SceneTreeInspector
{
    private readonly SceneTree _sceneTree;

    public SceneTreeInspector(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public SceneTreeResponse GetSceneTree()
    {
        var root = _sceneTree.Root;
        return new SceneTreeResponse
        {
            Root = SerializeNode(root),
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

        return props;
    }

    private NodeInfo SerializeNode(Node node, int depth = 0, int maxDepth = 10)
    {
        var info = new NodeInfo
        {
            Path = node.GetPath(),
            ClassName = node.GetClass(),
            Name = node.Name
        };

        if (depth < maxDepth)
        {
            foreach (var child in node.GetChildren())
            {
                info.Children.Add(SerializeNode(child, depth + 1, maxDepth));
            }
        }

        return info;
    }

    private void FindNodesRecursive(Node node, NodeQuery query, NodeList result)
    {
        if (MatchesQuery(node, query))
            result.Nodes.Add(SerializeNode(node, maxDepth: 0));

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
