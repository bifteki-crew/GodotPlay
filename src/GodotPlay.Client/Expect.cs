namespace GodotPlay;

public static class Expect
{
    public static NodeExpectation That(NodeLocator locator) => new(locator);
}

public class NodeExpectation
{
    private readonly NodeLocator _locator;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public NodeExpectation(NodeLocator locator)
    {
        _locator = locator;
    }

    public async Task ToExistAsync(TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var nodes = await _locator.ResolveAsync(ct);
            if (nodes.Count > 0) return;
            await Task.Delay(PollInterval, ct);
        }
        throw new TimeoutException(
            $"Expected node matching {_locator.ToQuery()} to exist, but it was not found within {timeout ?? DefaultTimeout}.");
    }

    public async Task ToHaveCountAsync(int expected, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        int lastCount = 0;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var nodes = await _locator.ResolveAsync(ct);
            lastCount = nodes.Count;
            if (lastCount == expected) return;
            await Task.Delay(PollInterval, ct);
        }
        throw new TimeoutException(
            $"Expected {expected} nodes matching {_locator.ToQuery()}, but found {lastCount} within {timeout ?? DefaultTimeout}.");
    }

    public async Task ToHavePropertyAsync(string propertyName, string expectedValue, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        string lastValue = "";
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var nodes = await _locator.ResolveAsync(ct);
            if (nodes.Count > 0 && nodes[0].Properties.TryGetValue(propertyName, out var val))
            {
                lastValue = val;
                if (val == expectedValue) return;
            }
            await Task.Delay(PollInterval, ct);
        }
        throw new TimeoutException(
            $"Expected property '{propertyName}' to be '{expectedValue}', but was '{lastValue}'.");
    }
}
