using GodotPlay.Protocol;

namespace GodotPlay;

public class NodeLocator
{
    public NodeLocator? Parent { get; }

    private readonly string? _path;
    private readonly string? _className;
    private readonly string? _namePattern;
    private readonly string? _group;
    private readonly GodotPlaySession? _session;

    public NodeLocator(
        string? path = null,
        string? className = null,
        string? namePattern = null,
        string? group = null,
        NodeLocator? parent = null,
        GodotPlaySession? session = null)
    {
        _path = path;
        _className = className;
        _namePattern = namePattern;
        _group = group;
        Parent = parent;
        _session = session ?? parent?._session;
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

    public async Task<IReadOnlyList<NodeInfo>> ResolveAsync(CancellationToken ct = default)
    {
        if (_session == null)
            throw new InvalidOperationException("NodeLocator is not bound to a session.");
        var result = await _session.FindNodesAsync(ToQuery(), ct);
        return result.Nodes;
    }

    public async Task<ActionResult> ClickAsync(CancellationToken ct = default)
    {
        if (_session == null)
            throw new InvalidOperationException("NodeLocator is not bound to a session.");

        if (!string.IsNullOrEmpty(_path))
        {
            return await _session.ClickAsync(new NodeRef { Path = _path }, ct);
        }

        var nodes = await ResolveAsync(ct);
        if (nodes.Count == 0)
            throw new InvalidOperationException($"No nodes found matching query: {ToQuery()}");
        return await _session.ClickAsync(new NodeRef { Path = nodes[0].Path }, ct);
    }
}
