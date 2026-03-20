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

    public async Task<SceneTreeResponse> GetSceneTreeAsync(string? nodePath = null, int maxDepth = 0, CancellationToken ct = default)
    {
        var request = new SceneTreeRequest
        {
            NodePath = nodePath ?? "",
            MaxDepth = maxDepth
        };
        var response = await _client.GetSceneTreeAsync(request, cancellationToken: ct);
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

    // --- Low-Level Input ---

    public async Task<ActionResult> MouseMoveAsync(float x, float y, CancellationToken ct = default)
        => await _client.MouseMoveAsync(new MouseMoveRequest { X = x, Y = y }, cancellationToken: ct);

    public async Task<ActionResult> MouseClickAsync(float x, float y, int button = 1, int clickCount = 1, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default)
        => await _client.MouseClickAtAsync(new MouseClickRequest { X = x, Y = y, Button = button, ClickCount = clickCount, Shift = shift, Ctrl = ctrl, Alt = alt, Meta = meta }, cancellationToken: ct);

    public async Task<ActionResult> MouseDownAsync(float x, float y, int button = 1, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default)
        => await _client.MouseButtonEventAsync(new MouseButtonRequest { X = x, Y = y, Button = button, Pressed = true, Shift = shift, Ctrl = ctrl, Alt = alt, Meta = meta }, cancellationToken: ct);

    public async Task<ActionResult> MouseUpAsync(float x, float y, int button = 1, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default)
        => await _client.MouseButtonEventAsync(new MouseButtonRequest { X = x, Y = y, Button = button, Pressed = false, Shift = shift, Ctrl = ctrl, Alt = alt, Meta = meta }, cancellationToken: ct);

    public async Task<ActionResult> MouseWheelAsync(float x, float y, float deltaX = 0, float deltaY = 0, CancellationToken ct = default)
        => await _client.MouseWheelAsync(new MouseWheelRequest { X = x, Y = y, DeltaX = deltaX, DeltaY = deltaY }, cancellationToken: ct);

    public async Task<ActionResult> KeyPressAsync(string keyLabel, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default)
        => await _client.KeyPressAsync(new KeyPressRequest { KeyLabel = keyLabel, Shift = shift, Ctrl = ctrl, Alt = alt, Meta = meta }, cancellationToken: ct);

    public async Task<ActionResult> KeyDownAsync(string keyLabel, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default)
        => await _client.KeyDownAsync(new KeyRequest { KeyLabel = keyLabel, Pressed = true, Shift = shift, Ctrl = ctrl, Alt = alt, Meta = meta }, cancellationToken: ct);

    public async Task<ActionResult> KeyUpAsync(string keyLabel, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default)
        => await _client.KeyUpAsync(new KeyRequest { KeyLabel = keyLabel, Pressed = false, Shift = shift, Ctrl = ctrl, Alt = alt, Meta = meta }, cancellationToken: ct);

    public async Task<ActionResult> TouchAsync(int index, float x, float y, bool pressed, CancellationToken ct = default)
        => await _client.TouchEventAsync(new TouchRequest { Index = index, X = x, Y = y, Pressed = pressed }, cancellationToken: ct);

    public async Task<ActionResult> TouchDragAsync(float fromX, float fromY, float toX, float toY, int steps = 10, CancellationToken ct = default)
        => await _client.TouchDragAsync(new TouchDragRequest { FromX = fromX, FromY = fromY, ToX = toX, ToY = toY, Steps = steps }, cancellationToken: ct);

    public async Task<ActionResult> GestureAsync(string type, float x, float y, float factor = 1, float deltaX = 0, float deltaY = 0, CancellationToken ct = default)
        => await _client.GestureAsync(new GestureRequest { Type = type, X = x, Y = y, Factor = factor, DeltaX = deltaX, DeltaY = deltaY }, cancellationToken: ct);

    public async Task<ActionResult> GamepadButtonAsync(string buttonName, bool pressed = true, int device = 0, CancellationToken ct = default)
        => await _client.GamepadButtonEventAsync(new GamepadButtonRequest { ButtonName = buttonName, Pressed = pressed, Device = device, Pressure = pressed ? 1f : 0f }, cancellationToken: ct);

    public async Task<ActionResult> GamepadAxisAsync(string axisName, float value, int device = 0, CancellationToken ct = default)
        => await _client.GamepadAxisEventAsync(new GamepadAxisRequest { AxisName = axisName, Value = value, Device = device }, cancellationToken: ct);

    public async Task<ActionResult> ActionPressAsync(string action, float strength = 1, CancellationToken ct = default)
        => await _client.ActionPressAsync(new ActionPressRequest { Action = action, Strength = strength }, cancellationToken: ct);

    public async Task<ActionResult> ActionEventAsync(string action, bool pressed, float strength = 1, CancellationToken ct = default)
        => await _client.ActionEventAsync(new ActionRequest { Action = action, Pressed = pressed, Strength = strength }, cancellationToken: ct);

    // --- High-Level Input ---

    public async Task<ActionResult> HoverAsync(string nodePath, CancellationToken ct = default)
        => await _client.HoverAsync(new HoverRequest { NodePath = nodePath }, cancellationToken: ct);

    public async Task<ActionResult> DragToAsync(string fromNodePath, string toNodePath, int steps = 10, CancellationToken ct = default)
        => await _client.DragToAsync(new DragRequest { FromNodePath = fromNodePath, ToNodePath = toNodePath, Steps = steps }, cancellationToken: ct);

    public async Task<ActionResult> ClickNodeAsync(string nodePath, int button = 1, int clickCount = 1, CancellationToken ct = default)
        => await _client.ClickNodeAsync(new ClickNodeRequest { NodePath = nodePath, Button = button, ClickCount = clickCount }, cancellationToken: ct);

    public async Task<ActionResult> ScrollNodeAsync(string nodePath, float deltaX = 0, float deltaY = 0, CancellationToken ct = default)
        => await _client.ScrollNodeAsync(new ScrollNodeRequest { NodePath = nodePath, DeltaX = deltaX, DeltaY = deltaY }, cancellationToken: ct);

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
