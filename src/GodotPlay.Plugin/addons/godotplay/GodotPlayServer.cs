using System.Collections.Concurrent;
using Godot;
using Grpc.Core;
using GodotPlay.Plugin.Services;

namespace GodotPlay.Plugin;

public partial class GodotPlayServer : Node
{
    [Export] public int Port { get; set; } = 50051;

    private Server? _grpcServer;
    private SceneTreeInspector? _inspector;
    private InputSimulator? _inputSimulator;
    private ScreenshotCapture? _screenshotCapture;
    private TextInput? _textInput;
    private Waiter? _waiter;
    private EventStreamer? _eventStreamer;

    private readonly ConcurrentQueue<MainThreadWork> _workQueue = new();

    public SceneTreeInspector Inspector => _inspector!;
    public InputSimulator InputSimulator => _inputSimulator!;
    public ScreenshotCapture ScreenshotCapture => _screenshotCapture!;
    public TextInput TextInput => _textInput!;
    public Waiter Waiter => _waiter!;
    public EventStreamer EventStreamer => _eventStreamer!;

    public override void _Ready()
    {
        _inspector = new SceneTreeInspector(GetTree());
        _inputSimulator = new InputSimulator(GetTree());
        _screenshotCapture = new ScreenshotCapture(GetTree());
        _textInput = new TextInput(GetTree());
        _waiter = new Waiter(GetTree());
        _eventStreamer = new EventStreamer(GetTree());

        StartServer();
    }

    public override void _Process(double delta)
    {
        // Process all pending main-thread work
        while (_workQueue.TryDequeue(out var work))
        {
            try
            {
                work.Result = work.Action();
                work.Completed.Set();
            }
            catch (Exception ex)
            {
                work.Exception = ex;
                work.Completed.Set();
            }
        }

        _eventStreamer?.Poll();
    }

    /// <summary>
    /// Execute a function on the main thread and wait for the result.
    /// Call this from gRPC background threads.
    /// </summary>
    public T RunOnMainThread<T>(Func<T> action)
    {
        var work = new MainThreadWork { Action = () => action()! };
        _workQueue.Enqueue(work);
        work.Completed.Wait();
        if (work.Exception != null)
            throw work.Exception;
        return (T)work.Result!;
    }

    private void StartServer()
    {
        var serviceImpl = new GodotPlayServiceImpl(this);

        _grpcServer = new Server
        {
            Services = { GodotPlay.Protocol.GodotPlayService.BindService(serviceImpl) },
            Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
        };

        _grpcServer.Start();
        GD.Print($"[GodotPlay] gRPC server listening on http://localhost:{Port}");
    }

    public override void _ExitTree()
    {
        if (_grpcServer != null)
        {
            _grpcServer.ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
            GD.Print("[GodotPlay] gRPC server stopped.");
        }
    }

    public void QuitGame()
    {
        GetTree().Quit();
    }

    private class MainThreadWork
    {
        public required Func<object> Action;
        public object? Result;
        public Exception? Exception;
        public ManualResetEventSlim Completed = new(false);
    }
}
