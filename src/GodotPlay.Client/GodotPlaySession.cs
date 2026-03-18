using System.Diagnostics;
using Grpc.Net.Client;
using GodotPlay.Protocol;

namespace GodotPlay;

public class GodotPlaySession : IGodotPlaySession
{
    private readonly GrpcChannel _channel;
    private readonly GodotPlayService.GodotPlayServiceClient _client;
    private Process? _godotProcess;
    private string _currentScenePath = "";

    public string CurrentScenePath => _currentScenePath;

    private GodotPlaySession(GrpcChannel channel)
    {
        _channel = channel;
        _client = new GodotPlayService.GodotPlayServiceClient(channel);
    }

    public static async Task<GodotPlaySession> ConnectAsync(
        string address,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                // Allow HTTP/2 without TLS (h2c)
            }
        });
        // Enable HTTP/2 unencrypted support
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var session = new GodotPlaySession(channel);

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var ping = await session.PingAsync(ct);
                if (ping.Ready)
                    return session;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await Task.Delay(200, ct);
            }
        }

        throw new TimeoutException($"Could not connect to GodotPlay server at {address}");
    }

    public async Task<PingResponse> PingAsync(CancellationToken ct = default)
    {
        return await _client.PingAsync(new Empty(), cancellationToken: ct);
    }

    public async Task<SceneTreeResponse> GetSceneTreeAsync(CancellationToken ct = default)
    {
        var response = await _client.GetSceneTreeAsync(new Empty(), cancellationToken: ct);
        _currentScenePath = response.CurrentScenePath;
        return response;
    }

    public async Task<NodeList> FindNodesAsync(NodeQuery query, CancellationToken ct = default)
    {
        return await _client.FindNodesAsync(query, cancellationToken: ct);
    }

    public async Task<ActionResult> ClickAsync(NodeRef nodeRef, CancellationToken ct = default)
    {
        return await _client.ClickAsync(nodeRef, cancellationToken: ct);
    }

    public async Task<ScreenshotResponse> ScreenshotAsync(CancellationToken ct = default)
    {
        return await _client.TakeScreenshotAsync(new ScreenshotRequest(), cancellationToken: ct);
    }

    public async Task<ActionResult> TypeAsync(TypeRequest request, CancellationToken ct = default)
        => await _client.TypeAsync(request, cancellationToken: ct);

    public async Task<ActionResult> TypeAsync(string nodePath, string text, bool clearFirst = false, CancellationToken ct = default)
        => await _client.TypeAsync(new TypeRequest { NodePath = nodePath, Text = text, ClearFirst = clearFirst }, cancellationToken: ct);

    public async Task<ActionResult> SetPropertyAsync(SetPropertyRequest request, CancellationToken ct = default)
        => await _client.SetPropertyAsync(request, cancellationToken: ct);

    public async Task<ActionResult> SetPropertyAsync(string nodePath, string propertyName, string value, CancellationToken ct = default)
        => await _client.SetPropertyAsync(new SetPropertyRequest { NodePath = nodePath, PropertyName = propertyName, Value = value }, cancellationToken: ct);

    public async Task<ActionResult> LoadSceneAsync(string scenePath, CancellationToken ct = default)
        => await _client.LoadSceneAsync(new LoadSceneRequest { ScenePath = scenePath }, cancellationToken: ct);

    public async Task<SceneInfo> GetCurrentSceneAsync(CancellationToken ct = default)
        => await _client.GetCurrentSceneAsync(new Empty(), cancellationToken: ct);

    public async Task<NodeRef> WaitForNodeAsync(string nodePath, int timeoutMs = 5000, CancellationToken ct = default)
        => await _client.WaitForNodeAsync(new WaitRequest { NodePath = nodePath, TimeoutMs = timeoutMs }, cancellationToken: ct);

    public async Task<SignalData> WaitForSignalAsync(string nodePath, string signalName, int timeoutMs = 5000, CancellationToken ct = default)
        => await _client.WaitForSignalAsync(new SignalWaitRequest { NodePath = nodePath, SignalName = signalName, TimeoutMs = timeoutMs }, cancellationToken: ct);

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await _client.ShutdownAsync(new Empty(), cancellationToken: ct);
    }

    public NodeLocator Locator(string? path = null, string? className = null, string? namePattern = null)
    {
        return new NodeLocator(path: path, className: className, namePattern: namePattern, session: this);
    }

    public void AttachProcess(Process process)
    {
        _godotProcess = process;
    }

    public async ValueTask DisposeAsync()
    {
        try { await ShutdownAsync(); } catch { }
        _channel.Dispose();
        if (_godotProcess != null && !_godotProcess.HasExited)
        {
            _godotProcess.Kill();
            await _godotProcess.WaitForExitAsync();
        }
    }
}
