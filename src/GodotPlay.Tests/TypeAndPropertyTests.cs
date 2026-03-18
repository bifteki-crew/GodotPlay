using GodotPlay;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GodotPlay.Tests;

[TestFixture]
public class TypeAndPropertyTests
{
    private WebApplication? _mockServer;
    private int _port;
    private GodotPlaySession? _session;

    [SetUp]
    public async Task Setup()
    {
        _port = Random.Shared.Next(52000, 52999);
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
    public async Task TypeAsync_ReturnsSuccess()
    {
        var result = await _session!.TypeAsync("/root/Main/Input", "hello");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task SetPropertyAsync_ReturnsSuccess()
    {
        var result = await _session!.SetPropertyAsync("/root/Main/Label", "text", "new text");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task GetCurrentScene_ReturnsSceneInfo()
    {
        var info = await _session!.GetCurrentSceneAsync();
        Assert.That(info.ScenePath, Is.EqualTo("res://scenes/main.tscn"));
        Assert.That(info.RootClassName, Is.EqualTo("Control"));
    }

    [Test]
    public async Task LoadScene_ReturnsSuccess()
    {
        var result = await _session!.LoadSceneAsync("res://scenes/game.tscn");
        Assert.That(result.Success, Is.True);
    }
}
