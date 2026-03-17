using GodotPlay;
using GodotPlay.Protocol;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GodotPlay.Tests;

[TestFixture]
public class ExpectTests
{
    private WebApplication? _mockServer;
    private int _port;
    private GodotPlaySession? _session;

    [SetUp]
    public async Task Setup()
    {
        _port = Random.Shared.Next(51000, 51999);
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
    public async Task ToExistAsync_Succeeds_WhenNodeExists()
    {
        var locator = _session!.Locator(className: "Button");
        await Expect.That(locator).ToExistAsync();
    }

    [Test]
    public void ToExistAsync_Throws_WhenNodeDoesNotExist()
    {
        var locator = _session!.Locator(className: "NonExistentClass");
        Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await Expect.That(locator).ToExistAsync(timeout: TimeSpan.FromMilliseconds(500));
        });
    }

    [Test]
    public async Task ToHaveCountAsync_Succeeds_WhenCountMatches()
    {
        var locator = _session!.Locator(className: "Button");
        await Expect.That(locator).ToHaveCountAsync(1);
    }
}
