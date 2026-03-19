using GodotPlay;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GodotPlay.Tests;

[TestFixture]
public class InputTests
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

    // --- Mouse ---

    [Test]
    public async Task MouseMoveAsync_ReturnsSuccess()
    {
        var result = await _session!.MouseMoveAsync(100f, 200f);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task MouseClickAsync_ReturnsSuccess()
    {
        var result = await _session!.MouseClickAsync(100f, 200f);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task MouseClickAsync_WithRightButton_ReturnsSuccess()
    {
        var result = await _session!.MouseClickAsync(100f, 200f, button: 2);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task MouseClickAsync_DoubleClick_ReturnsSuccess()
    {
        var result = await _session!.MouseClickAsync(100f, 200f, clickCount: 2);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task MouseDownAsync_ReturnsSuccess()
    {
        var result = await _session!.MouseDownAsync(100f, 200f);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task MouseUpAsync_ReturnsSuccess()
    {
        var result = await _session!.MouseUpAsync(100f, 200f);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task MouseWheelAsync_ReturnsSuccess()
    {
        var result = await _session!.MouseWheelAsync(100f, 200f, deltaY: -1f);
        Assert.That(result.Success, Is.True);
    }

    // --- Keyboard ---

    [Test]
    public async Task KeyPressAsync_ReturnsSuccess()
    {
        var result = await _session!.KeyPressAsync("Enter");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task KeyPressAsync_WithModifiers_ReturnsSuccess()
    {
        var result = await _session!.KeyPressAsync("A", ctrl: true, shift: true);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task KeyDownAsync_ReturnsSuccess()
    {
        var result = await _session!.KeyDownAsync("Space");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task KeyUpAsync_ReturnsSuccess()
    {
        var result = await _session!.KeyUpAsync("Space");
        Assert.That(result.Success, Is.True);
    }

    // --- Touch ---

    [Test]
    public async Task TouchAsync_ReturnsSuccess()
    {
        var result = await _session!.TouchAsync(0, 100f, 200f, pressed: true);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task TouchDragAsync_ReturnsSuccess()
    {
        var result = await _session!.TouchDragAsync(100f, 200f, 300f, 400f);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task GestureAsync_Pinch_ReturnsSuccess()
    {
        var result = await _session!.GestureAsync("pinch", 200f, 300f, factor: 1.5f);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task GestureAsync_Pan_ReturnsSuccess()
    {
        var result = await _session!.GestureAsync("pan", 200f, 300f, deltaX: 50f, deltaY: 0f);
        Assert.That(result.Success, Is.True);
    }

    // --- Gamepad ---

    [Test]
    public async Task GamepadButtonAsync_ReturnsSuccess()
    {
        var result = await _session!.GamepadButtonAsync("a");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task GamepadAxisAsync_ReturnsSuccess()
    {
        var result = await _session!.GamepadAxisAsync("left_x", 0.5f);
        Assert.That(result.Success, Is.True);
    }

    // --- Actions ---

    [Test]
    public async Task ActionPressAsync_ReturnsSuccess()
    {
        var result = await _session!.ActionPressAsync("jump");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task ActionEventAsync_ReturnsSuccess()
    {
        var result = await _session!.ActionEventAsync("jump", pressed: true);
        Assert.That(result.Success, Is.True);
    }

    // --- High-Level ---

    [Test]
    public async Task HoverAsync_ReturnsSuccess()
    {
        var result = await _session!.HoverAsync("/root/Main/Button");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task DragToAsync_ReturnsSuccess()
    {
        var result = await _session!.DragToAsync("/root/Main/ItemA", "/root/Main/ItemB");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task ClickNodeAsync_ReturnsSuccess()
    {
        var result = await _session!.ClickNodeAsync("/root/Main/Button");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task ClickNodeAsync_DoubleClick_ReturnsSuccess()
    {
        var result = await _session!.ClickNodeAsync("/root/Main/Button", clickCount: 2);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task ClickNodeAsync_RightClick_ReturnsSuccess()
    {
        var result = await _session!.ClickNodeAsync("/root/Main/Button", button: 2);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task ScrollNodeAsync_ReturnsSuccess()
    {
        var result = await _session!.ScrollNodeAsync("/root/Main/ScrollContainer", deltaY: -1f);
        Assert.That(result.Success, Is.True);
    }

    // --- NodeLocator ---

    [Test]
    public async Task Locator_HoverAsync_ReturnsSuccess()
    {
        var locator = _session!.Locator(path: "/root/Main/Button");
        var result = await locator.HoverAsync();
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Locator_DoubleClickAsync_ReturnsSuccess()
    {
        var locator = _session!.Locator(path: "/root/Main/Button");
        var result = await locator.DoubleClickAsync();
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Locator_RightClickAsync_ReturnsSuccess()
    {
        var locator = _session!.Locator(path: "/root/Main/Button");
        var result = await locator.RightClickAsync();
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Locator_ScrollAsync_ReturnsSuccess()
    {
        var locator = _session!.Locator(path: "/root/Main/ScrollContainer");
        var result = await locator.ScrollAsync(deltaY: -1f);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Locator_DragToAsync_ReturnsSuccess()
    {
        var source = _session!.Locator(path: "/root/Main/ItemA");
        var target = _session!.Locator(path: "/root/Main/ItemB");
        var result = await source.DragToAsync(target);
        Assert.That(result.Success, Is.True);
    }
}
