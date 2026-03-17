using Godot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using GodotPlay.Plugin.Services;

namespace GodotPlay.Plugin;

public partial class GodotPlayServer : Node
{
    [Export] public int Port { get; set; } = 50051;

    private WebApplication? _app;
    private Task? _serverTask;
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
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.ListenLocalhost(Port, o => o.Protocols = HttpProtocols.Http2);
        });
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(this);

        _app = builder.Build();
        _app.MapGrpcService<GodotPlayServiceImpl>();

        _serverTask = Task.Run(async () =>
        {
            await _app.RunAsync();
        });

        GD.Print($"[GodotPlay] gRPC server listening on http://localhost:{Port}");
    }

    public override void _ExitTree()
    {
        if (_app != null)
        {
            _app.StopAsync().Wait(TimeSpan.FromSeconds(5));
            GD.Print("[GodotPlay] gRPC server stopped.");
        }
    }

    // Called by gRPC service to quit
    public void QuitGame()
    {
        GetTree().Quit();
    }
}
