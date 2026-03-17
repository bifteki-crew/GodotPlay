using GodotPlay.Protocol;

namespace GodotPlay;

public class NodeLocator
{
    public NodeLocator? Parent { get; }

    private readonly string? _path;
    private readonly string? _className;
    private readonly string? _namePattern;
    private readonly string? _group;

    public NodeLocator(
        string? path = null,
        string? className = null,
        string? namePattern = null,
        string? group = null,
        NodeLocator? parent = null)
    {
        _path = path;
        _className = className;
        _namePattern = namePattern;
        _group = group;
        Parent = parent;
    }

    public NodeQuery ToQuery()
    {
        return new NodeQuery
        {
            Path = _path ?? "",
            ClassName = _className ?? "",
            NamePattern = _namePattern ?? "",
            Group = _group ?? ""
        };
    }

    public NodeLocator Locator(
        string? path = null,
        string? className = null,
        string? namePattern = null,
        string? group = null)
    {
        return new NodeLocator(path, className, namePattern, group, parent: this);
    }
}
