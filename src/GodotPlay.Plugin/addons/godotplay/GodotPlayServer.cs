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

    public SceneTreeInspector Inspector => _inspector!;
    public InputSimulator InputSimulator => _inputSimulator!;
    public ScreenshotCapture ScreenshotCapture => _screenshotCapture!;

    public override void _Ready()
    {
        _inspector = new SceneTreeInspector(GetTree());
        _inputSimulator = new InputSimulator(GetTree());
        _screenshotCapture = new ScreenshotCapture(GetTree());

        StartServer();
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
}
