using GodotPlay;
using GodotPlay.Protocol;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GodotPlay.Tests;

[TestFixture]
public class SessionTests
{
    private WebApplication? _mockServer;
    private int _port;

    [SetUp]
    public async Task Setup()
    {
        _port = Random.Shared.Next(50100, 50999);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.ListenLocalhost(_port, o => o.Protocols = HttpProtocols.Http2);
        });
        builder.Services.AddGrpc();
        _mockServer = builder.Build();
        _mockServer.MapGrpcService<MockGodotPlayService>();
        await _mockServer.StartAsync();
    }

    [TearDown]
    public async Task Teardown()
    {
        if (_mockServer != null)
        {
            await _mockServer.StopAsync();
            await _mockServer.DisposeAsync();
        }
    }

    [Test]
    public async Task ConnectToExisting_PingsSuccessfully()
    {
        await using var session = await GodotPlaySession.ConnectAsync($"http://localhost:{_port}");
        var ping = await session.PingAsync();
        Assert.That(ping.Ready, Is.True);
        Assert.That(ping.Version, Is.EqualTo("0.1.0"));
    }

    [Test]
    public async Task GetSceneTree_ReturnsTree()
    {
        await using var session = await GodotPlaySession.ConnectAsync($"http://localhost:{_port}");
        var tree = await session.GetSceneTreeAsync();
        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Root.Name, Is.EqualTo("Root"));
        Assert.That(tree.CurrentScenePath, Is.EqualTo("res://scenes/main.tscn"));
    }

    [Test]
    public async Task FindNodes_ReturnsMatchingNodes()
    {
        await using var session = await GodotPlaySession.ConnectAsync($"http://localhost:{_port}");
        var locator = session.Locator(className: "Button");
        var nodes = await locator.ResolveAsync();
        Assert.That(nodes, Has.Count.GreaterThan(0));
        Assert.That(nodes[0].ClassName, Is.EqualTo("Button"));
    }

    [Test]
    public async Task Click_ReturnsSuccess()
    {
        await using var session = await GodotPlaySession.ConnectAsync($"http://localhost:{_port}");
        var locator = session.Locator(path: "/root/Main/StartButton");
        var result = await locator.ClickAsync();
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Screenshot_ReturnsPngData()
    {
        await using var session = await GodotPlaySession.ConnectAsync($"http://localhost:{_port}");
        var screenshot = await session.ScreenshotAsync();
        Assert.That(screenshot.PngData, Is.Not.Empty);
        Assert.That(screenshot.Width, Is.GreaterThan(0));
    }
}

// Mock gRPC service for testing
public class MockGodotPlayService : GodotPlayService.GodotPlayServiceBase
{
    public override Task<PingResponse> Ping(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new PingResponse { Version = "0.1.0", Ready = true });
    }

    public override Task<SceneTreeResponse> GetSceneTree(Empty request, ServerCallContext context)
    {
        var root = new NodeInfo { Path = "/root", ClassName = "Window", Name = "Root" };
        root.Children.Add(new NodeInfo { Path = "/root/Main", ClassName = "Control", Name = "Main" });
        root.Children[0].Children.Add(new NodeInfo { Path = "/root/Main/StartButton", ClassName = "Button", Name = "StartButton" });
        return Task.FromResult(new SceneTreeResponse { Root = root, CurrentScenePath = "res://scenes/main.tscn" });
    }

    public override Task<NodeList> FindNodes(NodeQuery request, ServerCallContext context)
    {
        var result = new NodeList();
        if (request.ClassName == "Button")
        {
            result.Nodes.Add(new NodeInfo { Path = "/root/Main/StartButton", ClassName = "Button", Name = "StartButton" });
        }
        return Task.FromResult(result);
    }

    public override Task<ActionResult> Click(NodeRef request, ServerCallContext context)
    {
        return Task.FromResult(new ActionResult { Success = true });
    }

    public override Task<ScreenshotResponse> TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
    {
        byte[] minimalPng = [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE
        ];
        return Task.FromResult(new ScreenshotResponse
        {
            PngData = Google.Protobuf.ByteString.CopyFrom(minimalPng),
            Width = 1,
            Height = 1
        });
    }

    public override Task<Empty> Shutdown(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Empty());
    }
}
