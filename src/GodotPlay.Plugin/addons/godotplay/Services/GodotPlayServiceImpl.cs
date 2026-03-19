using Grpc.Core;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class GodotPlayServiceImpl : GodotPlayService.GodotPlayServiceBase
{
    private readonly GodotPlayServer _server;

    public GodotPlayServiceImpl(GodotPlayServer server)
    {
        _server = server;
    }

    public override Task<PingResponse> Ping(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new PingResponse { Version = "0.1.0", Ready = true });
    }

    public override Task<Empty> Shutdown(Empty request, ServerCallContext context)
    {
        _server.CallDeferred(nameof(GodotPlayServer.QuitGame));
        return Task.FromResult(new Empty());
    }

    public override Task<SceneTreeResponse> GetSceneTree(SceneTreeRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.Inspector.GetSceneTree(request));
        return Task.FromResult(result);
    }

    public override Task<NodeList> FindNodes(NodeQuery request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.Inspector.FindNodes(request));
        return Task.FromResult(result);
    }

    public override Task<PropertyMap> GetNodeProperties(NodeRef request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.Inspector.GetNodeProperties(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> Click(NodeRef request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.InputSimulator.Click(request));
        return Task.FromResult(result);
    }

    public override Task<ScreenshotResponse> TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.ScreenshotCapture.Capture(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> Type(TypeRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TextInput.Type(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> SetProperty(SetPropertyRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TextInput.SetProperty(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> LoadScene(LoadSceneRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TextInput.LoadScene(request));
        return Task.FromResult(result);
    }

    public override Task<SceneInfo> GetCurrentScene(Empty request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TextInput.GetCurrentScene());
        return Task.FromResult(result);
    }

    public override async Task<NodeRef> WaitForNode(WaitRequest request, ServerCallContext context)
    {
        return await _server.Waiter.WaitForNode(request, _server);
    }

    public override async Task<SignalData> WaitForSignal(SignalWaitRequest request, ServerCallContext context)
    {
        return await _server.Waiter.WaitForSignal(request, _server);
    }

    public override async Task SubscribeEvents(EventFilter request,
        Grpc.Core.IServerStreamWriter<GameEvent> responseStream,
        ServerCallContext context)
    {
        while (!context.CancellationToken.IsCancellationRequested)
        {
            while (_server.EventStreamer.TryDequeue(out var evt))
            {
                if (evt != null)
                    await responseStream.WriteAsync(evt);
            }
            await Task.Delay(100, context.CancellationToken);
        }
    }
}
