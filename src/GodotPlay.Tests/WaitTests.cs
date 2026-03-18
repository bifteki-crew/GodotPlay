using GodotPlay;
using GodotPlay.Protocol;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GodotPlay.Tests;

[TestFixture]
public class WaitTests
{
    private WebApplication? _mockServer;
    private int _port;
    private GodotPlaySession? _session;

    [SetUp]
    public async Task Setup()
    {
        _port = Random.Shared.Next(53000, 53999);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.ListenLocalhost(_port, o => o.Protocols = HttpProtocols.Http2);
        });
        builder.Services.AddGrpc();
        _mockServer = builder.Build();
        _mockServer.MapGrpcService<MockGodotPlayService>();
        await _mockServer.StartAsync();
        _session = await GodotPlaySession.ConnectAsync($"http://localhost:{_port}");
    }

    [TearDown]
    public async Task Teardown()
    {
        if (_session != null) await _session.DisposeAsync();
        if (_mockServer != null)
        {
            await _mockServer.StopAsync();
            await _mockServer.DisposeAsync();
        }
    }

    [Test]
    public async Task WaitForNode_ReturnsNodeRef()
    {
        var result = await _session!.WaitForNodeAsync("/root/Main/Button");
        Assert.That(result.Path, Is.EqualTo("/root/Main/Button"));
    }

    [Test]
    public async Task WaitForSignal_ReturnsSignalData()
    {
        var result = await _session!.WaitForSignalAsync("/root/Main/Button", "pressed");
        Assert.That(result.SignalName, Is.EqualTo("pressed"));
        Assert.That(result.NodePath, Is.EqualTo("/root/Main/Button"));
    }
}
