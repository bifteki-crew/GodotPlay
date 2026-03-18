using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class Waiter
{
    private readonly SceneTree _sceneTree;

    public Waiter(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public async Task<NodeRef> WaitForNode(WaitRequest request, GodotPlayServer server)
    {
        var timeout = request.TimeoutMs > 0 ? request.TimeoutMs : 5000;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);

        while (DateTime.UtcNow < deadline)
        {
            var found = server.RunOnMainThread(() =>
            {
                var node = _sceneTree.Root.GetNodeOrNull(request.NodePath);
                if (node == null) return (NodeRef?)null;
                if (!string.IsNullOrEmpty(request.ClassName) && node.GetClass() != request.ClassName)
                    return null;
                return new NodeRef { Path = node.GetPath() };
            });

            if (found != null) return found;
            await Task.Delay(100);
        }

        throw new Grpc.Core.RpcException(new Grpc.Core.Status(
            Grpc.Core.StatusCode.DeadlineExceeded,
            $"Node {request.NodePath} not found within {timeout}ms"));
    }

    public async Task<SignalData> WaitForSignal(SignalWaitRequest request, GodotPlayServer server)
    {
        var timeout = request.TimeoutMs > 0 ? request.TimeoutMs : 5000;
        var tcs = new TaskCompletionSource<SignalData>();

        server.RunOnMainThread<object?>(() =>
        {
            var node = _sceneTree.Root.GetNodeOrNull(request.NodePath);
            if (node == null)
            {
                tcs.SetException(new Grpc.Core.RpcException(new Grpc.Core.Status(
                    Grpc.Core.StatusCode.NotFound, $"Node not found: {request.NodePath}")));
                return null;
            }

            var callback = Callable.From(() =>
            {
                tcs.TrySetResult(new SignalData
                {
                    SignalName = request.SignalName,
                    NodePath = request.NodePath
                });
            });
            node.Connect(request.SignalName, callback, (uint)GodotObject.ConnectFlags.OneShot);
            return null;
        });

        var timeoutTask = Task.Delay(timeout);
        var completed = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completed == timeoutTask)
            throw new Grpc.Core.RpcException(new Grpc.Core.Status(
                Grpc.Core.StatusCode.DeadlineExceeded,
                $"Signal {request.SignalName} on {request.NodePath} not received within {timeout}ms"));

        return await tcs.Task;
    }
}
