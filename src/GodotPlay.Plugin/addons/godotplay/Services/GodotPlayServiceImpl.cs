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

    public override Task<ActionResult> MouseMove(MouseMoveRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.MouseInput2.Move(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> MouseButtonEvent(MouseButtonRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.MouseInput2.ButtonEvent(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> MouseClickAt(MouseClickRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.MouseInput2.ClickAt(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> MouseWheel(MouseWheelRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.MouseInput2.Wheel(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> KeyDown(KeyRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.KeyboardInput.Down(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> KeyUp(KeyRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.KeyboardInput.Up(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> KeyPress(KeyPressRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.KeyboardInput.Press(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> TouchEvent(TouchRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TouchInput.Touch(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> TouchDrag(TouchDragRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TouchInput.Drag(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> Gesture(GestureRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TouchInput.HandleGesture(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> GamepadButtonEvent(GamepadButtonRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.GamepadInput.Button(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> GamepadAxisEvent(GamepadAxisRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.GamepadInput.Axis(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> ActionEvent(ActionRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.ActionInput.Event(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> ActionPress(ActionPressRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.ActionInput.Press(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> DragTo(DragRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.HighLevelInput.DragTo(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> Hover(HoverRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.HighLevelInput.Hover(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> ClickNode(ClickNodeRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.HighLevelInput.ClickNode(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> ScrollNode(ScrollNodeRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.HighLevelInput.ScrollNode(request));
        return Task.FromResult(result);
    }
}
