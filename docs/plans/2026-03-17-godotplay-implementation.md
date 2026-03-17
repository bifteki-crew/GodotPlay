# GodotPlay MVP Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build an end-to-end thin slice proving that a Playwright-like test framework for Godot works — plugin starts gRPC server, external runner sends commands, MCP server wraps the API for AI agents.

**Architecture:** C# Godot plugin embeds a gRPC server (Kestrel) that exposes scene tree inspection, node interaction, and screenshot capture. A .NET client library provides a Playwright-inspired API. A TypeScript MCP server wraps the gRPC API for AI agent integration.

**Tech Stack:** .NET 8, Godot 4.x, gRPC (Grpc.AspNetCore), Protobuf, NUnit, TypeScript, @modelcontextprotocol/sdk, zod

**Prerequisites:**
- Godot 4.x (.NET version) installed and on PATH
- .NET 8 SDK installed
- Node.js 18+ installed
- `protoc` (Protocol Buffers compiler) installed — comes with `Grpc.Tools` NuGet

---

### Task 1: Repository Setup & Proto Definition

**Files:**
- Create: `proto/godotplay.proto`
- Create: `src/GodotPlay.Client/GodotPlay.Client.csproj`
- Create: `src/GodotPlay.Plugin/GodotPlay.Plugin.csproj`
- Create: `src/GodotPlay.Tests/GodotPlay.Tests.csproj`
- Create: `GodotPlay.sln`
- Create: `.gitignore`

**Step 1: Create .gitignore**

```
# .NET
bin/
obj/
*.user
*.suo
.vs/

# Godot
.godot/
*.import

# Node
node_modules/
dist/

# OS
.DS_Store
Thumbs.db
```

**Step 2: Create the protobuf definition (MVP subset only)**

File: `proto/godotplay.proto`

```protobuf
syntax = "proto3";

package godotplay;

option csharp_namespace = "GodotPlay.Protocol";

// --- Messages ---

message Empty {}

message PingResponse {
  string version = 1;
  bool ready = 2;
}

message NodeInfo {
  string path = 1;           // e.g. "/root/MainMenu/StartButton"
  string class_name = 2;     // e.g. "Button"
  string name = 3;           // e.g. "StartButton"
  repeated NodeInfo children = 4;
  map<string, string> properties = 5; // key-value of common properties
}

message SceneTreeResponse {
  NodeInfo root = 1;
  string current_scene_path = 2; // e.g. "res://scenes/main_menu.tscn"
}

message NodeQuery {
  string path = 1;           // exact node path (optional)
  string class_name = 2;     // filter by class (optional)
  string name_pattern = 3;   // filter by node name, supports * wildcard (optional)
  string group = 4;          // filter by group membership (optional)
  map<string, string> property_filters = 5; // match property values (optional)
}

message NodeList {
  repeated NodeInfo nodes = 1;
}

message NodeRef {
  string path = 1;           // absolute node path
}

message PropertyMap {
  map<string, string> properties = 1;
}

message ActionResult {
  bool success = 1;
  string error = 2;          // empty if success
}

message ScreenshotRequest {
  string node_path = 1;      // optional: capture specific viewport/node, empty = main viewport
}

message ScreenshotResponse {
  bytes png_data = 1;
  int32 width = 2;
  int32 height = 3;
}

// --- Service ---

service GodotPlayService {
  // Lifecycle
  rpc Ping(Empty) returns (PingResponse);
  rpc Shutdown(Empty) returns (Empty);

  // Scene Tree
  rpc GetSceneTree(Empty) returns (SceneTreeResponse);
  rpc FindNodes(NodeQuery) returns (NodeList);
  rpc GetNodeProperties(NodeRef) returns (PropertyMap);

  // Interaction
  rpc Click(NodeRef) returns (ActionResult);

  // Capture
  rpc TakeScreenshot(ScreenshotRequest) returns (ScreenshotResponse);
}
```

**Step 3: Create .NET solution and projects**

Run:
```bash
cd D:/ai/playgodot

# Create solution
dotnet new sln -n GodotPlay

# Client library
dotnet new classlib -n GodotPlay.Client -o src/GodotPlay.Client -f net8.0
dotnet sln add src/GodotPlay.Client/GodotPlay.Client.csproj

# Test project
dotnet new nunit -n GodotPlay.Tests -o src/GodotPlay.Tests -f net8.0
dotnet sln add src/GodotPlay.Tests/GodotPlay.Tests.csproj
dotnet add src/GodotPlay.Tests reference src/GodotPlay.Client
```

**Step 4: Add NuGet packages to Client project**

Edit `src/GodotPlay.Client/GodotPlay.Client.csproj` to contain:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>GodotPlay</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="3.28.0" />
    <PackageReference Include="Grpc.Net.Client" Version="2.65.0" />
    <PackageReference Include="Grpc.Tools" Version="2.66.0" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="../../proto/godotplay.proto" GrpcServices="Client" Link="Protos/godotplay.proto" />
  </ItemGroup>
</Project>
```

**Step 5: Add NuGet packages to Tests project**

Edit `src/GodotPlay.Tests/GodotPlay.Tests.csproj` to add:

```xml
<ItemGroup>
  <PackageReference Include="Grpc.AspNetCore" Version="2.65.0" />
  <PackageReference Include="Google.Protobuf" Version="3.28.0" />
  <PackageReference Include="Grpc.Tools" Version="2.66.0" PrivateAssets="All" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
</ItemGroup>

<ItemGroup>
  <Protobuf Include="../../proto/godotplay.proto" GrpcServices="Server" Link="Protos/godotplay.proto" />
</ItemGroup>
```

**Step 6: Verify build**

Run:
```bash
dotnet build GodotPlay.sln
```
Expected: Build succeeds, proto generates C# code.

**Step 7: Commit**

```bash
git add .gitignore proto/ src/GodotPlay.Client/ src/GodotPlay.Tests/ GodotPlay.sln
git commit -m "feat: initialize repo with proto definition and .NET projects"
```

---

### Task 2: gRPC Client Library — Core Types

**Files:**
- Create: `src/GodotPlay.Client/LaunchOptions.cs`
- Create: `src/GodotPlay.Client/IGodotPlaySession.cs`
- Create: `src/GodotPlay.Client/NodeLocator.cs`
- Create: `src/GodotPlay.Client/GodotPlaySession.cs`
- Test: `src/GodotPlay.Tests/NodeLocatorTests.cs`

**Step 1: Write tests for NodeLocator query building**

File: `src/GodotPlay.Tests/NodeLocatorTests.cs`

```csharp
using GodotPlay;
using GodotPlay.Protocol;

namespace GodotPlay.Tests;

[TestFixture]
public class NodeLocatorTests
{
    [Test]
    public void Locator_ByPath_BuildsCorrectQuery()
    {
        var locator = new NodeLocator(path: "/root/UI/StartButton");

        var query = locator.ToQuery();

        Assert.That(query.Path, Is.EqualTo("/root/UI/StartButton"));
    }

    [Test]
    public void Locator_ByClassName_BuildsCorrectQuery()
    {
        var locator = new NodeLocator(className: "Button");

        var query = locator.ToQuery();

        Assert.That(query.ClassName, Is.EqualTo("Button"));
    }

    [Test]
    public void Locator_ByNamePattern_BuildsCorrectQuery()
    {
        var locator = new NodeLocator(namePattern: "Start*");

        var query = locator.ToQuery();

        Assert.That(query.NamePattern, Is.EqualTo("Start*"));
    }

    [Test]
    public void Locator_Combined_BuildsCorrectQuery()
    {
        var locator = new NodeLocator(className: "Button", namePattern: "Start*");

        var query = locator.ToQuery();

        Assert.That(query.ClassName, Is.EqualTo("Button"));
        Assert.That(query.NamePattern, Is.EqualTo("Start*"));
    }

    [Test]
    public void Locator_Chain_CreatesChildLocator()
    {
        var parent = new NodeLocator(className: "VBoxContainer");
        var child = parent.Locator(className: "Button");

        // Child locator should have parent reference
        Assert.That(child.Parent, Is.SameAs(parent));
        Assert.That(child.ToQuery().ClassName, Is.EqualTo("Button"));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/GodotPlay.Tests --filter NodeLocatorTests -v n`
Expected: FAIL — `NodeLocator` class doesn't exist yet.

**Step 3: Create LaunchOptions**

File: `src/GodotPlay.Client/LaunchOptions.cs`

```csharp
namespace GodotPlay;

public class LaunchOptions
{
    public required string ProjectPath { get; init; }
    public bool Headless { get; init; } = true;
    public string? Scene { get; init; }
    public int Port { get; init; } = 50051;
    public string GodotPath { get; init; } = "godot";
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
```

**Step 4: Create IGodotPlaySession interface**

File: `src/GodotPlay.Client/IGodotPlaySession.cs`

```csharp
using GodotPlay.Protocol;

namespace GodotPlay;

public interface IGodotPlaySession : IAsyncDisposable
{
    NodeLocator Locator(string? path = null, string? className = null, string? namePattern = null);
    Task<SceneTreeResponse> GetSceneTreeAsync(CancellationToken ct = default);
    Task<ScreenshotResponse> ScreenshotAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
    string CurrentScenePath { get; }
}
```

**Step 5: Create NodeLocator**

File: `src/GodotPlay.Client/NodeLocator.cs`

```csharp
using GodotPlay.Protocol;

namespace GodotPlay;

public class NodeLocator
{
    public NodeLocator? Parent { get; }

    private readonly string? _path;
    private readonly string? _className;
    private readonly string? _namePattern;
    private readonly string? _group;

    public NodeLocator(
        string? path = null,
        string? className = null,
        string? namePattern = null,
        string? group = null,
        NodeLocator? parent = null)
    {
        _path = path;
        _className = className;
        _namePattern = namePattern;
        _group = group;
        Parent = parent;
    }

    public NodeQuery ToQuery()
    {
        return new NodeQuery
        {
            Path = _path ?? "",
            ClassName = _className ?? "",
            NamePattern = _namePattern ?? "",
            Group = _group ?? ""
        };
    }

    public NodeLocator Locator(
        string? path = null,
        string? className = null,
        string? namePattern = null,
        string? group = null)
    {
        return new NodeLocator(path, className, namePattern, group, parent: this);
    }
}
```

**Step 6: Delete auto-generated Class1.cs**

Run: `rm src/GodotPlay.Client/Class1.cs` (if it exists)

**Step 7: Run tests to verify they pass**

Run: `dotnet test src/GodotPlay.Tests --filter NodeLocatorTests -v n`
Expected: All 5 tests PASS.

**Step 8: Commit**

```bash
git add src/GodotPlay.Client/ src/GodotPlay.Tests/
git commit -m "feat: add NodeLocator with query building and chaining"
```

---

### Task 3: gRPC Client Library — Session & Process Management

**Files:**
- Create: `src/GodotPlay.Client/GodotPlaySession.cs`
- Create: `src/GodotPlay.Client/GodotPlay.cs` (static entry point)
- Test: `src/GodotPlay.Tests/SessionTests.cs`

**Step 1: Write tests for session with a mock gRPC server**

File: `src/GodotPlay.Tests/SessionTests.cs`

We test the session against an in-process mock gRPC server (no Godot needed).

```csharp
using GodotPlay;
using GodotPlay.Protocol;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
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
            await _mockServer.StopAsync();
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

// Mock gRPC service for testing the client
public class MockGodotPlayService : GodotPlayService.GodotPlayServiceBase
{
    public override Task<PingResponse> Ping(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new PingResponse { Version = "0.1.0", Ready = true });
    }

    public override Task<SceneTreeResponse> GetSceneTree(Empty request, ServerCallContext context)
    {
        var root = new NodeInfo
        {
            Path = "/root",
            ClassName = "Window",
            Name = "Root"
        };
        root.Children.Add(new NodeInfo
        {
            Path = "/root/Main",
            ClassName = "Control",
            Name = "Main"
        });
        root.Children[0].Children.Add(new NodeInfo
        {
            Path = "/root/Main/StartButton",
            ClassName = "Button",
            Name = "StartButton"
        });

        return Task.FromResult(new SceneTreeResponse
        {
            Root = root,
            CurrentScenePath = "res://scenes/main.tscn"
        });
    }

    public override Task<NodeList> FindNodes(NodeQuery request, ServerCallContext context)
    {
        var result = new NodeList();
        if (request.ClassName == "Button")
        {
            result.Nodes.Add(new NodeInfo
            {
                Path = "/root/Main/StartButton",
                ClassName = "Button",
                Name = "StartButton"
            });
        }
        return Task.FromResult(result);
    }

    public override Task<ActionResult> Click(NodeRef request, ServerCallContext context)
    {
        return Task.FromResult(new ActionResult { Success = true });
    }

    public override Task<ScreenshotResponse> TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
    {
        // Return a tiny 1x1 white PNG
        byte[] minimalPng = [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/GodotPlay.Tests --filter SessionTests -v n`
Expected: FAIL — `GodotPlaySession` doesn't exist yet.

**Step 3: Implement GodotPlaySession**

File: `src/GodotPlay.Client/GodotPlaySession.cs`

```csharp
using Grpc.Net.Client;
using GodotPlay.Protocol;

namespace GodotPlay;

public class GodotPlaySession : IGodotPlaySession
{
    private readonly GrpcChannel _channel;
    private readonly GodotPlayService.GodotPlayServiceClient _client;
    private string _currentScenePath = "";

    public string CurrentScenePath => _currentScenePath;

    private GodotPlaySession(GrpcChannel channel)
    {
        _channel = channel;
        _client = new GodotPlayService.GodotPlayServiceClient(channel);
    }

    public static async Task<GodotPlaySession> ConnectAsync(
        string address,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var channel = GrpcChannel.ForAddress(address);
        var session = new GodotPlaySession(channel);

        // Verify connection with ping
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var ping = await session.PingAsync(ct);
                if (ping.Ready)
                    return session;
            }
            catch (Grpc.Core.RpcException)
            {
                await Task.Delay(200, ct);
            }
        }

        throw new TimeoutException($"Could not connect to GodotPlay server at {address}");
    }

    public async Task<PingResponse> PingAsync(CancellationToken ct = default)
    {
        return await _client.PingAsync(new Empty(), cancellationToken: ct);
    }

    public async Task<SceneTreeResponse> GetSceneTreeAsync(CancellationToken ct = default)
    {
        var response = await _client.GetSceneTreeAsync(new Empty(), cancellationToken: ct);
        _currentScenePath = response.CurrentScenePath;
        return response;
    }

    public async Task<NodeList> FindNodesAsync(NodeQuery query, CancellationToken ct = default)
    {
        return await _client.FindNodesAsync(query, cancellationToken: ct);
    }

    public async Task<ActionResult> ClickAsync(NodeRef nodeRef, CancellationToken ct = default)
    {
        return await _client.ClickAsync(nodeRef, cancellationToken: ct);
    }

    public async Task<ScreenshotResponse> ScreenshotAsync(CancellationToken ct = default)
    {
        return await _client.TakeScreenshotAsync(new ScreenshotRequest(), cancellationToken: ct);
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await _client.ShutdownAsync(new Empty(), cancellationToken: ct);
    }

    public NodeLocator Locator(string? path = null, string? className = null, string? namePattern = null)
    {
        return new NodeLocator(path: path, className: className, namePattern: namePattern, session: this);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Dispose();
    }
}
```

**Step 4: Update NodeLocator to support session and async operations**

Update `src/GodotPlay.Client/NodeLocator.cs`:

```csharp
using GodotPlay.Protocol;

namespace GodotPlay;

public class NodeLocator
{
    public NodeLocator? Parent { get; }

    private readonly string? _path;
    private readonly string? _className;
    private readonly string? _namePattern;
    private readonly string? _group;
    private readonly GodotPlaySession? _session;

    public NodeLocator(
        string? path = null,
        string? className = null,
        string? namePattern = null,
        string? group = null,
        NodeLocator? parent = null,
        GodotPlaySession? session = null)
    {
        _path = path;
        _className = className;
        _namePattern = namePattern;
        _group = group;
        Parent = parent;
        _session = session ?? parent?._session;
    }

    public NodeQuery ToQuery()
    {
        return new NodeQuery
        {
            Path = _path ?? "",
            ClassName = _className ?? "",
            NamePattern = _namePattern ?? "",
            Group = _group ?? ""
        };
    }

    public NodeLocator Locator(
        string? path = null,
        string? className = null,
        string? namePattern = null,
        string? group = null)
    {
        return new NodeLocator(path, className, namePattern, group, parent: this);
    }

    public async Task<IReadOnlyList<NodeInfo>> ResolveAsync(CancellationToken ct = default)
    {
        if (_session == null)
            throw new InvalidOperationException("NodeLocator is not bound to a session.");

        var result = await _session.FindNodesAsync(ToQuery(), ct);
        return result.Nodes;
    }

    public async Task<ActionResult> ClickAsync(CancellationToken ct = default)
    {
        if (_session == null)
            throw new InvalidOperationException("NodeLocator is not bound to a session.");

        // If we have a direct path, use it
        if (!string.IsNullOrEmpty(_path))
        {
            return await _session.ClickAsync(new NodeRef { Path = _path }, ct);
        }

        // Otherwise resolve first, then click the first match
        var nodes = await ResolveAsync(ct);
        if (nodes.Count == 0)
            throw new InvalidOperationException($"No nodes found matching query: {ToQuery()}");

        return await _session.ClickAsync(new NodeRef { Path = nodes[0].Path }, ct);
    }
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test src/GodotPlay.Tests -v n`
Expected: All tests PASS (both NodeLocatorTests and SessionTests).

**Step 6: Commit**

```bash
git add src/
git commit -m "feat: add GodotPlaySession with gRPC client and mock server tests"
```

---

### Task 4: Expect/Assertion API with Auto-Retry

**Files:**
- Create: `src/GodotPlay.Client/Expect.cs`
- Test: `src/GodotPlay.Tests/ExpectTests.cs`

**Step 1: Write tests for the Expect API**

File: `src/GodotPlay.Tests/ExpectTests.cs`

```csharp
using GodotPlay;
using GodotPlay.Protocol;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
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
        if (_mockServer != null) await _mockServer.StopAsync();
    }

    [Test]
    public async Task ToExistAsync_Succeeds_WhenNodeExists()
    {
        var locator = _session!.Locator(className: "Button");

        // Should not throw
        await GodotPlay.Expect.That(locator).ToExistAsync();
    }

    [Test]
    public void ToExistAsync_Throws_WhenNodeDoesNotExist()
    {
        var locator = _session!.Locator(className: "NonExistentClass");

        Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await GodotPlay.Expect.That(locator).ToExistAsync(timeout: TimeSpan.FromMilliseconds(500));
        });
    }

    [Test]
    public async Task ToHaveCountAsync_Succeeds_WhenCountMatches()
    {
        var locator = _session!.Locator(className: "Button");

        await GodotPlay.Expect.That(locator).ToHaveCountAsync(1);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/GodotPlay.Tests --filter ExpectTests -v n`
Expected: FAIL — `Expect` class doesn't exist.

**Step 3: Implement Expect API**

File: `src/GodotPlay.Client/Expect.cs`

```csharp
namespace GodotPlay;

public static class Expect
{
    public static NodeExpectation That(NodeLocator locator) => new(locator);
}

public class NodeExpectation
{
    private readonly NodeLocator _locator;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public NodeExpectation(NodeLocator locator)
    {
        _locator = locator;
    }

    public async Task ToExistAsync(TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var nodes = await _locator.ResolveAsync(ct);
            if (nodes.Count > 0)
                return;
            await Task.Delay(PollInterval, ct);
        }

        throw new TimeoutException(
            $"Expected node matching {_locator.ToQuery()} to exist, but it was not found within {timeout ?? DefaultTimeout}.");
    }

    public async Task ToHaveCountAsync(int expected, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        int lastCount = 0;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var nodes = await _locator.ResolveAsync(ct);
            lastCount = nodes.Count;
            if (lastCount == expected)
                return;
            await Task.Delay(PollInterval, ct);
        }

        throw new TimeoutException(
            $"Expected {expected} nodes matching {_locator.ToQuery()}, but found {lastCount} within {timeout ?? DefaultTimeout}.");
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test src/GodotPlay.Tests --filter ExpectTests -v n`
Expected: All 3 tests PASS.

**Step 5: Commit**

```bash
git add src/
git commit -m "feat: add Expect API with auto-retry assertions"
```

---

### Task 5: Static Entry Point — GodotPlay.Launch()

**Files:**
- Create: `src/GodotPlay.Client/GodotPlayLauncher.cs`
- Test: `src/GodotPlay.Tests/LauncherTests.cs`

**Step 1: Write tests for launch argument building**

We can't test actual Godot launching in unit tests, but we can test that the correct CLI arguments are built.

File: `src/GodotPlay.Tests/LauncherTests.cs`

```csharp
using GodotPlay;

namespace GodotPlay.Tests;

[TestFixture]
public class LauncherTests
{
    [Test]
    public void BuildArgs_Headless_IncludesFlag()
    {
        var options = new LaunchOptions
        {
            ProjectPath = "/tmp/project",
            Headless = true,
            Scene = "res://scenes/main.tscn",
            Port = 50051
        };

        var args = GodotPlayLauncher.BuildGodotArgs(options);

        Assert.That(args, Does.Contain("--headless"));
        Assert.That(args, Does.Contain("--path"));
        Assert.That(args, Does.Contain("/tmp/project"));
        Assert.That(args, Does.Contain("res://scenes/main.tscn"));
    }

    [Test]
    public void BuildArgs_NotHeadless_OmitsFlag()
    {
        var options = new LaunchOptions
        {
            ProjectPath = "/tmp/project",
            Headless = false
        };

        var args = GodotPlayLauncher.BuildGodotArgs(options);

        Assert.That(args, Does.Not.Contain("--headless"));
    }

    [Test]
    public void BuildArgs_NoScene_OmitsScenePath()
    {
        var options = new LaunchOptions
        {
            ProjectPath = "/tmp/project",
            Headless = true
        };

        var args = GodotPlayLauncher.BuildGodotArgs(options);

        Assert.That(args, Does.Not.Contain("res://"));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/GodotPlay.Tests --filter LauncherTests -v n`
Expected: FAIL.

**Step 3: Implement GodotPlayLauncher**

File: `src/GodotPlay.Client/GodotPlayLauncher.cs`

```csharp
using System.Diagnostics;

namespace GodotPlay;

public static class GodotPlayLauncher
{
    public static async Task<GodotPlaySession> LaunchAsync(
        LaunchOptions options,
        CancellationToken ct = default)
    {
        var args = BuildGodotArgs(options);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = options.GodotPath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        try
        {
            var session = await GodotPlaySession.ConnectAsync(
                $"http://localhost:{options.Port}",
                options.StartupTimeout,
                ct);
            session.AttachProcess(process);
            return session;
        }
        catch
        {
            if (!process.HasExited)
                process.Kill();
            throw;
        }
    }

    public static List<string> BuildGodotArgs(LaunchOptions options)
    {
        var args = new List<string>();

        args.Add("--path");
        args.Add(options.ProjectPath);

        if (options.Headless)
            args.Add("--headless");

        if (!string.IsNullOrEmpty(options.Scene))
            args.Add(options.Scene);

        return args;
    }
}

// Static convenience class
public static class GodotPlay
{
    public static Task<GodotPlaySession> LaunchAsync(
        LaunchOptions options,
        CancellationToken ct = default)
        => GodotPlayLauncher.LaunchAsync(options, ct);
}
```

**Step 4: Add AttachProcess to GodotPlaySession**

Add to `src/GodotPlay.Client/GodotPlaySession.cs`:

```csharp
// Add this field at the top of the class:
private Process? _godotProcess;

// Add this method:
public void AttachProcess(Process process)
{
    _godotProcess = process;
}

// Update DisposeAsync:
public async ValueTask DisposeAsync()
{
    try
    {
        await ShutdownAsync();
    }
    catch { /* ignore shutdown errors during dispose */ }

    _channel.Dispose();

    if (_godotProcess != null && !_godotProcess.HasExited)
    {
        _godotProcess.Kill();
        await _godotProcess.WaitForExitAsync();
    }
}
```

Add `using System.Diagnostics;` to the top of the file.

**Step 5: Run all tests**

Run: `dotnet test src/GodotPlay.Tests -v n`
Expected: All tests PASS.

**Step 6: Commit**

```bash
git add src/
git commit -m "feat: add GodotPlay.Launch with Godot process management"
```

---

### Task 6: Godot Plugin — gRPC Server in Godot

**Files:**
- Create: `src/GodotPlay.Plugin/addons/godotplay/plugin.cfg`
- Create: `src/GodotPlay.Plugin/addons/godotplay/GodotPlayPlugin.cs`
- Create: `src/GodotPlay.Plugin/addons/godotplay/GodotPlayServer.cs`
- Create: `src/GodotPlay.Plugin/addons/godotplay/Services/GodotPlayServiceImpl.cs`
- Create: `src/GodotPlay.Plugin/addons/godotplay/Services/SceneTreeInspector.cs`
- Create: `src/GodotPlay.Plugin/addons/godotplay/Services/InputSimulator.cs`
- Create: `src/GodotPlay.Plugin/addons/godotplay/Services/ScreenshotCapture.cs`
- Create: `src/GodotPlay.Plugin/GodotPlay.Plugin.csproj`

**Important context:** This is NOT a standalone Godot project. It's a NuGet-style library that gets added to target Godot projects. However, for development, we structure it as Godot addon files. The `.csproj` is used only for building/testing the gRPC service separately — the actual Godot integration requires these files to be copied into a target project's `addons/` folder.

**Step 1: Create plugin.cfg**

File: `src/GodotPlay.Plugin/addons/godotplay/plugin.cfg`

```ini
[plugin]

name="GodotPlay"
description="Playwright-like test automation server for Godot"
author="GodotPlay"
version="0.1.0"
script="GodotPlayPlugin.cs"
```

**Step 2: Create the EditorPlugin**

File: `src/GodotPlay.Plugin/addons/godotplay/GodotPlayPlugin.cs`

```csharp
#if TOOLS
using Godot;

namespace GodotPlay.Plugin;

[Tool]
public partial class GodotPlayPlugin : EditorPlugin
{
    public override void _EnterTree()
    {
        AddAutoloadSingleton("GodotPlayServer", "res://addons/godotplay/GodotPlayServer.cs");
        GD.Print("[GodotPlay] Plugin enabled — GodotPlayServer autoload registered.");
    }

    public override void _ExitTree()
    {
        RemoveAutoloadSingleton("GodotPlayServer");
        GD.Print("[GodotPlay] Plugin disabled — GodotPlayServer autoload removed.");
    }
}
#endif
```

**Step 3: Create the AutoLoad server node**

File: `src/GodotPlay.Plugin/addons/godotplay/GodotPlayServer.cs`

```csharp
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
        GD.Print($"[GodotPlay] gRPC server starting on port {Port}...");
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
        StopServer();
    }

    private void StopServer()
    {
        if (_app != null)
        {
            _app.StopAsync().Wait(TimeSpan.FromSeconds(5));
            GD.Print("[GodotPlay] gRPC server stopped.");
        }
    }
}
```

**Step 4: Implement the gRPC service**

File: `src/GodotPlay.Plugin/addons/godotplay/Services/GodotPlayServiceImpl.cs`

```csharp
using Godot;
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
        return Task.FromResult(new PingResponse
        {
            Version = "0.1.0",
            Ready = true
        });
    }

    public override Task<Empty> Shutdown(Empty request, ServerCallContext context)
    {
        // Schedule quit on the main thread
        _server.CallDeferred("_quit_game");
        return Task.FromResult(new Empty());
    }

    public override Task<SceneTreeResponse> GetSceneTree(Empty request, ServerCallContext context)
    {
        return Task.FromResult(_server.Inspector.GetSceneTree());
    }

    public override Task<NodeList> FindNodes(NodeQuery request, ServerCallContext context)
    {
        return Task.FromResult(_server.Inspector.FindNodes(request));
    }

    public override Task<PropertyMap> GetNodeProperties(NodeRef request, ServerCallContext context)
    {
        return Task.FromResult(_server.Inspector.GetNodeProperties(request));
    }

    public override Task<ActionResult> Click(NodeRef request, ServerCallContext context)
    {
        return Task.FromResult(_server.InputSimulator.Click(request));
    }

    public override Task<ScreenshotResponse> TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
    {
        return Task.FromResult(_server.ScreenshotCapture.Capture(request));
    }
}
```

**Step 5: Implement SceneTreeInspector**

File: `src/GodotPlay.Plugin/addons/godotplay/Services/SceneTreeInspector.cs`

```csharp
using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class SceneTreeInspector
{
    private readonly SceneTree _sceneTree;

    public SceneTreeInspector(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public SceneTreeResponse GetSceneTree()
    {
        var root = _sceneTree.Root;
        var response = new SceneTreeResponse
        {
            Root = SerializeNode(root),
            CurrentScenePath = _sceneTree.CurrentScene?.SceneFilePath ?? ""
        };
        return response;
    }

    public NodeList FindNodes(NodeQuery query)
    {
        var result = new NodeList();
        var root = _sceneTree.Root;
        FindNodesRecursive(root, query, result);
        return result;
    }

    public PropertyMap GetNodeProperties(NodeRef nodeRef)
    {
        var node = _sceneTree.Root.GetNodeOrNull(nodeRef.Path);
        if (node == null)
            return new PropertyMap();

        var props = new PropertyMap();
        // Common properties
        props.Properties["name"] = node.Name;
        props.Properties["class"] = node.GetClass();
        props.Properties["visible"] = (node is CanvasItem ci && ci.Visible).ToString();

        if (node is Control control)
        {
            props.Properties["size"] = $"{control.Size.X},{control.Size.Y}";
            props.Properties["position"] = $"{control.Position.X},{control.Position.Y}";
        }

        if (node is BaseButton button)
        {
            props.Properties["disabled"] = button.Disabled.ToString();
            props.Properties["text"] = (node as Button)?.Text ?? "";
        }

        return props;
    }

    private NodeInfo SerializeNode(Node node, int depth = 0, int maxDepth = 10)
    {
        var info = new NodeInfo
        {
            Path = node.GetPath(),
            ClassName = node.GetClass(),
            Name = node.Name
        };

        if (depth < maxDepth)
        {
            foreach (var child in node.GetChildren())
            {
                info.Children.Add(SerializeNode(child, depth + 1, maxDepth));
            }
        }

        return info;
    }

    private void FindNodesRecursive(Node node, NodeQuery query, NodeList result)
    {
        if (MatchesQuery(node, query))
        {
            result.Nodes.Add(SerializeNode(node, maxDepth: 0));
        }

        foreach (var child in node.GetChildren())
        {
            FindNodesRecursive(child, query, result);
        }
    }

    private bool MatchesQuery(Node node, NodeQuery query)
    {
        if (!string.IsNullOrEmpty(query.Path) && node.GetPath() != query.Path)
            return false;

        if (!string.IsNullOrEmpty(query.ClassName) && node.GetClass() != query.ClassName)
            return false;

        if (!string.IsNullOrEmpty(query.NamePattern))
        {
            var pattern = query.NamePattern;
            if (pattern.Contains('*'))
            {
                // Simple wildcard matching
                var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(node.Name, regex))
                    return false;
            }
            else if (node.Name != pattern)
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(query.Group) && !node.IsInGroup(query.Group))
            return false;

        return true;
    }
}
```

**Step 6: Implement InputSimulator**

File: `src/GodotPlay.Plugin/addons/godotplay/Services/InputSimulator.cs`

```csharp
using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class InputSimulator
{
    private readonly SceneTree _sceneTree;

    public InputSimulator(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Click(NodeRef nodeRef)
    {
        var node = _sceneTree.Root.GetNodeOrNull(nodeRef.Path);
        if (node == null)
            return new ActionResult { Success = false, Error = $"Node not found: {nodeRef.Path}" };

        if (node is BaseButton button)
        {
            // Direct signal emission — works in headless mode
            button.EmitSignal(BaseButton.SignalName.Pressed);
            return new ActionResult { Success = true };
        }

        if (node is Control control)
        {
            // For non-button controls, simulate a click input event
            var click = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = control.Size / 2 // center of the control
            };
            control._GuiInput(click);

            var release = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = false,
                Position = control.Size / 2
            };
            control._GuiInput(release);

            return new ActionResult { Success = true };
        }

        return new ActionResult { Success = false, Error = $"Node {nodeRef.Path} is not a clickable Control." };
    }
}
```

**Step 7: Implement ScreenshotCapture**

File: `src/GodotPlay.Plugin/addons/godotplay/Services/ScreenshotCapture.cs`

```csharp
using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class ScreenshotCapture
{
    private readonly SceneTree _sceneTree;

    public ScreenshotCapture(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ScreenshotResponse Capture(ScreenshotRequest request)
    {
        var viewport = _sceneTree.Root.GetViewport();
        var image = viewport.GetTexture().GetImage();

        var pngBytes = image.SavePngToBuffer();

        return new ScreenshotResponse
        {
            PngData = Google.Protobuf.ByteString.CopyFrom(pngBytes),
            Width = image.GetWidth(),
            Height = image.GetHeight()
        };
    }
}
```

**Step 8: Create the .csproj (for IDE support / standalone build only)**

File: `src/GodotPlay.Plugin/GodotPlay.Plugin.csproj`

Note: This .csproj is for IDE support and code generation. In a real Godot project, the plugin files live under `addons/` and are compiled by Godot's own build system. This project lets us verify the proto codegen and service implementation compile.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>GodotPlay.Plugin</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" Version="2.65.0" />
    <PackageReference Include="Google.Protobuf" Version="3.28.0" />
    <PackageReference Include="Grpc.Tools" Version="2.66.0" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="../../proto/godotplay.proto" GrpcServices="Server" Link="Protos/godotplay.proto" />
  </ItemGroup>

  <!-- Godot SDK reference — uncomment when used inside a Godot project -->
  <!-- <ItemGroup>
    <PackageReference Include="Godot.NET.Sdk" Version="4.3.0" />
  </ItemGroup> -->
</Project>
```

**Step 9: Add Plugin project to solution**

Run:
```bash
dotnet sln add src/GodotPlay.Plugin/GodotPlay.Plugin.csproj
```

Note: The Plugin project won't fully compile standalone (it references Godot types). It will compile when used inside a Godot project. For now, verify proto codegen works.

**Step 10: Commit**

```bash
git add src/GodotPlay.Plugin/
git commit -m "feat: add Godot plugin with gRPC server, scene inspector, input simulator, screenshot capture"
```

---

### Task 7: Demo Godot Project

**Files:**
- Create: `demo/project.godot`
- Create: `demo/scenes/main_menu.tscn`
- Create: `demo/scenes/game.tscn`
- Create: `demo/scripts/MainMenu.cs`
- Create: `demo/scripts/Game.cs`
- Copy: Plugin files into `demo/addons/godotplay/`

**Step 1: Create the demo Godot project**

This task requires manual Godot interaction or scripting. Create the minimal project files.

File: `demo/project.godot`

```ini
; Engine configuration file.
; It's best edited using the editor UI and not directly,
; but it can also be edited just fine manually.

config_version=5

[application]

config/name="GodotPlay Demo"
config/features=PackedStringArray("4.3", "C#", "Forward Plus")
run/main_scene="res://scenes/main_menu.tscn"

[autoload]

GodotPlayServer="*res://addons/godotplay/GodotPlayServer.cs"

[dotnet]

project/assembly_name="GodotPlayDemo"

[editor_plugins]

enabled=PackedStringArray("res://addons/godotplay/plugin.cfg")
```

**Step 2: Create MainMenu scene**

File: `demo/scenes/main_menu.tscn`

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://scripts/MainMenu.cs" id="1"]

[node name="MainMenu" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
script = ExtResource("1")

[node name="VBoxContainer" type="VBoxContainer" parent="."]
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -100.0
offset_top = -60.0
offset_right = 100.0
offset_bottom = 60.0

[node name="Title" type="Label" parent="VBoxContainer"]
layout_mode = 2
text = "GodotPlay Demo"
horizontal_alignment = 1

[node name="StartButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
text = "Start Game"

[node name="QuitButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
text = "Quit"
```

**Step 3: Create MainMenu script**

File: `demo/scripts/MainMenu.cs`

```csharp
using Godot;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var startButton = GetNode<Button>("VBoxContainer/StartButton");
        var quitButton = GetNode<Button>("VBoxContainer/QuitButton");

        startButton.Pressed += OnStartPressed;
        quitButton.Pressed += OnQuitPressed;
    }

    private void OnStartPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/game.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
```

**Step 4: Create Game scene**

File: `demo/scenes/game.tscn`

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://scripts/Game.cs" id="1"]

[node name="Game" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
script = ExtResource("1")

[node name="Label" type="Label" parent="."]
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -100.0
offset_top = -20.0
offset_right = 100.0
offset_bottom = 20.0
text = "Game Scene Active!"
horizontal_alignment = 1

[node name="BackButton" type="Button" parent="."]
layout_mode = 1
anchors_preset = 7
anchor_left = 0.5
anchor_top = 1.0
anchor_right = 0.5
anchor_bottom = 1.0
offset_left = -60.0
offset_top = -60.0
offset_right = 60.0
offset_bottom = -20.0
text = "Back to Menu"
```

**Step 5: Create Game script**

File: `demo/scripts/Game.cs`

```csharp
using Godot;

public partial class Game : Control
{
    public override void _Ready()
    {
        var backButton = GetNode<Button>("BackButton");
        backButton.Pressed += OnBackPressed;
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
    }
}
```

**Step 6: Copy plugin files to demo project**

Run:
```bash
cp -r src/GodotPlay.Plugin/addons demo/addons
cp src/GodotPlay.Plugin/addons/godotplay/plugin.cfg demo/addons/godotplay/
```

**Step 7: Create demo .csproj for Godot**

File: `demo/GodotPlayDemo.csproj`

```xml
<Project Sdk="Godot.NET.Sdk/4.3.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>GodotPlayDemo</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" Version="2.65.0" />
    <PackageReference Include="Google.Protobuf" Version="3.28.0" />
    <PackageReference Include="Grpc.Tools" Version="2.66.0" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="../proto/godotplay.proto" GrpcServices="Server" Link="Protos/godotplay.proto" />
  </ItemGroup>
</Project>
```

**Step 8: Test that the demo project builds**

Run:
```bash
cd demo && dotnet build
```
Expected: Builds successfully (may have warnings about Godot types if not running from Godot editor).

**Step 9: Commit**

```bash
cd D:/ai/playgodot
git add demo/
git commit -m "feat: add demo Godot project with MainMenu and Game scenes"
```

---

### Task 8: Integration Test — End-to-End

**Files:**
- Create: `src/GodotPlay.Tests/Integration/EndToEndTests.cs`

**Important:** These tests require Godot to be installed and on PATH. They are marked with `[Category("Integration")]` so they can be excluded from unit test runs.

**Step 1: Write the E2E test**

File: `src/GodotPlay.Tests/Integration/EndToEndTests.cs`

```csharp
using GodotPlay;
using GodotPlay.Protocol;

namespace GodotPlay.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class EndToEndTests
{
    private GodotPlaySession? _session;

    [SetUp]
    public async Task Setup()
    {
        _session = await GodotPlayLauncher.LaunchAsync(new LaunchOptions
        {
            ProjectPath = Path.GetFullPath("../../../../demo"),
            Headless = false, // Visual for debugging; change to true for CI
            Scene = "res://scenes/main_menu.tscn",
            Port = 50051,
            GodotPath = "godot",
            StartupTimeout = TimeSpan.FromSeconds(30)
        });
    }

    [TearDown]
    public async Task Teardown()
    {
        if (_session != null)
            await _session.DisposeAsync();
    }

    [Test]
    public async Task Ping_ReturnsReady()
    {
        var ping = await _session!.PingAsync();

        Assert.That(ping.Ready, Is.True);
        Assert.That(ping.Version, Is.EqualTo("0.1.0"));
    }

    [Test]
    public async Task GetSceneTree_ReturnsMainMenu()
    {
        var tree = await _session!.GetSceneTreeAsync();

        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.CurrentScenePath, Does.Contain("main_menu"));
    }

    [Test]
    public async Task FindNodes_FindsStartButton()
    {
        var locator = _session!.Locator(className: "Button", namePattern: "StartButton");

        await Expect.That(locator).ToExistAsync();
    }

    [Test]
    public async Task ClickStartButton_NavigatesToGameScene()
    {
        var startButton = _session!.Locator(path: "/root/MainMenu/VBoxContainer/StartButton");

        await startButton.ClickAsync();

        // Wait for scene change
        await Task.Delay(500);
        var tree = await _session!.GetSceneTreeAsync();

        Assert.That(tree.CurrentScenePath, Does.Contain("game"));
    }

    [Test]
    public async Task TakeScreenshot_ReturnsPngData()
    {
        var screenshot = await _session!.ScreenshotAsync();

        Assert.That(screenshot.PngData.Length, Is.GreaterThan(0));
        Assert.That(screenshot.Width, Is.GreaterThan(0));
        Assert.That(screenshot.Height, Is.GreaterThan(0));

        // Optionally save for visual inspection
        var path = Path.Combine(Path.GetTempPath(), "godotplay_screenshot.png");
        await File.WriteAllBytesAsync(path, screenshot.PngData.ToByteArray());
        TestContext.WriteLine($"Screenshot saved to: {path}");
    }
}
```

**Step 2: Run unit tests only (to verify nothing broke)**

Run: `dotnet test src/GodotPlay.Tests --filter "Category!=Integration" -v n`
Expected: All unit tests PASS.

**Step 3: Run integration tests (requires Godot on PATH)**

Run: `dotnet test src/GodotPlay.Tests --filter "Category=Integration" -v n`
Expected: Tests connect to Godot, inspect scene tree, click button, take screenshot.

Note: This step will likely need debugging. Common issues:
- Godot not on PATH → set `GodotPath` to absolute path
- Port conflict → change port
- gRPC not starting in Godot → check Godot output for errors
- Scene not loading → verify demo project paths

**Step 4: Commit**

```bash
git add src/GodotPlay.Tests/
git commit -m "feat: add end-to-end integration tests for GodotPlay"
```

---

### Task 9: MCP Server (TypeScript)

**Files:**
- Create: `src/godotplay-mcp/package.json`
- Create: `src/godotplay-mcp/tsconfig.json`
- Create: `src/godotplay-mcp/src/index.ts`
- Create: `src/godotplay-mcp/src/godot-client.ts`

**Step 1: Initialize the npm project**

Run:
```bash
cd D:/ai/playgodot
mkdir -p src/godotplay-mcp/src
cd src/godotplay-mcp
npm init -y
npm install @modelcontextprotocol/sdk zod @grpc/grpc-js @grpc/proto-loader
npm install -D typescript @types/node
```

**Step 2: Create tsconfig.json**

File: `src/godotplay-mcp/tsconfig.json`

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "Node16",
    "moduleResolution": "Node16",
    "outDir": "./dist",
    "rootDir": "./src",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "declaration": true
  },
  "include": ["src/**/*"]
}
```

**Step 3: Update package.json**

Edit `src/godotplay-mcp/package.json` to set:

```json
{
  "name": "godotplay-mcp",
  "version": "0.1.0",
  "description": "MCP server for GodotPlay test automation",
  "type": "module",
  "main": "dist/index.js",
  "bin": {
    "godotplay-mcp": "dist/index.js"
  },
  "scripts": {
    "build": "tsc",
    "start": "node dist/index.js"
  }
}
```

**Step 4: Create the gRPC client wrapper**

File: `src/godotplay-mcp/src/godot-client.ts`

```typescript
import * as grpc from "@grpc/grpc-js";
import * as protoLoader from "@grpc/proto-loader";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROTO_PATH = path.resolve(__dirname, "../../../proto/godotplay.proto");

const packageDefinition = protoLoader.loadSync(PROTO_PATH, {
  keepCase: false,
  longs: String,
  enums: String,
  defaults: true,
  oneofs: true,
});

const protoDescriptor = grpc.loadPackageDefinition(packageDefinition) as any;
const GodotPlayService = protoDescriptor.godotplay.GodotPlayService;

export class GodotPlayClient {
  private client: any;

  constructor(address: string = "localhost:50051") {
    this.client = new GodotPlayService(
      address,
      grpc.credentials.createInsecure()
    );
  }

  ping(): Promise<{ version: string; ready: boolean }> {
    return this.callUnary("ping", {});
  }

  getSceneTree(): Promise<any> {
    return this.callUnary("getSceneTree", {});
  }

  findNodes(query: {
    path?: string;
    className?: string;
    namePattern?: string;
    group?: string;
  }): Promise<{ nodes: any[] }> {
    return this.callUnary("findNodes", query);
  }

  click(path: string): Promise<{ success: boolean; error: string }> {
    return this.callUnary("click", { path });
  }

  takeScreenshot(
    nodePath?: string
  ): Promise<{ pngData: Buffer; width: number; height: number }> {
    return this.callUnary("takeScreenshot", { nodePath: nodePath || "" });
  }

  shutdown(): Promise<void> {
    return this.callUnary("shutdown", {});
  }

  close(): void {
    this.client.close();
  }

  private callUnary(method: string, request: any): Promise<any> {
    return new Promise((resolve, reject) => {
      this.client[method](request, (err: any, response: any) => {
        if (err) reject(err);
        else resolve(response);
      });
    });
  }
}
```

**Step 5: Create the MCP server**

File: `src/godotplay-mcp/src/index.ts`

```typescript
#!/usr/bin/env node

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { GodotPlayClient } from "./godot-client.js";
import { exec } from "child_process";

let godotClient: GodotPlayClient | null = null;
let godotProcess: ReturnType<typeof exec> | null = null;

const server = new McpServer({
  name: "godotplay-mcp",
  version: "0.1.0",
});

// --- Tools ---

server.tool(
  "godot_launch",
  "Launch a Godot instance with the GodotPlay plugin",
  {
    projectPath: z.string().describe("Path to the Godot project directory"),
    headless: z
      .boolean()
      .default(false)
      .describe("Run in headless mode (no window)"),
    scene: z
      .string()
      .optional()
      .describe("Scene to load (e.g., res://scenes/main.tscn)"),
    port: z.number().default(50051).describe("gRPC server port"),
    godotPath: z
      .string()
      .default("godot")
      .describe("Path to Godot executable"),
  },
  async ({ projectPath, headless, scene, port, godotPath }) => {
    const args = ["--path", projectPath];
    if (headless) args.push("--headless");
    if (scene) args.push(scene);

    godotProcess = exec(`${godotPath} ${args.join(" ")}`);

    // Wait for gRPC server to be ready
    godotClient = new GodotPlayClient(`localhost:${port}`);
    const maxRetries = 30;
    for (let i = 0; i < maxRetries; i++) {
      try {
        const ping = await godotClient.ping();
        if (ping.ready) {
          return {
            content: [
              {
                type: "text" as const,
                text: `Godot launched successfully. gRPC server ready on port ${port}. Version: ${ping.version}`,
              },
            ],
          };
        }
      } catch {
        await new Promise((r) => setTimeout(r, 1000));
      }
    }
    return {
      content: [
        { type: "text" as const, text: "Failed to connect to Godot gRPC server within timeout." },
      ],
      isError: true,
    };
  }
);

server.tool(
  "godot_inspect_tree",
  "Get the current scene tree from the running Godot instance",
  {},
  async () => {
    if (!godotClient) {
      return {
        content: [
          { type: "text" as const, text: "No Godot instance running. Use godot_launch first." },
        ],
        isError: true,
      };
    }
    const tree = await godotClient.getSceneTree();
    return {
      content: [
        {
          type: "text" as const,
          text: JSON.stringify(tree, null, 2),
        },
      ],
    };
  }
);

server.tool(
  "godot_click",
  "Click a node in the running Godot instance",
  {
    nodePath: z
      .string()
      .describe(
        "Absolute node path (e.g., /root/MainMenu/VBoxContainer/StartButton)"
      ),
  },
  async ({ nodePath }) => {
    if (!godotClient) {
      return {
        content: [
          { type: "text" as const, text: "No Godot instance running. Use godot_launch first." },
        ],
        isError: true,
      };
    }
    const result = await godotClient.click(nodePath);
    return {
      content: [
        {
          type: "text" as const,
          text: result.success
            ? `Clicked node: ${nodePath}`
            : `Click failed: ${result.error}`,
        },
      ],
      isError: !result.success,
    };
  }
);

server.tool(
  "godot_screenshot",
  "Take a screenshot of the running Godot instance",
  {},
  async () => {
    if (!godotClient) {
      return {
        content: [
          { type: "text" as const, text: "No Godot instance running. Use godot_launch first." },
        ],
        isError: true,
      };
    }
    const screenshot = await godotClient.takeScreenshot();
    const base64 = Buffer.from(screenshot.pngData).toString("base64");
    return {
      content: [
        {
          type: "image" as const,
          data: base64,
          mimeType: "image/png",
        },
      ],
    };
  }
);

server.tool(
  "godot_shutdown",
  "Shut down the running Godot instance",
  {},
  async () => {
    if (godotClient) {
      try {
        await godotClient.shutdown();
      } catch {
        // Ignore shutdown errors
      }
      godotClient.close();
      godotClient = null;
    }
    if (godotProcess) {
      godotProcess.kill();
      godotProcess = null;
    }
    return {
      content: [{ type: "text" as const, text: "Godot instance shut down." }],
    };
  }
);

// --- Resources ---

server.resource(
  "scene-tree",
  "godot://scene-tree",
  { description: "Current Godot scene tree as structured JSON" },
  async () => {
    if (!godotClient) {
      return {
        contents: [
          {
            uri: "godot://scene-tree",
            text: "No Godot instance running.",
            mimeType: "text/plain",
          },
        ],
      };
    }
    const tree = await godotClient.getSceneTree();
    return {
      contents: [
        {
          uri: "godot://scene-tree",
          text: JSON.stringify(tree, null, 2),
          mimeType: "application/json",
        },
      ],
    };
  }
);

// --- Start ---

const transport = new StdioServerTransport();
await server.connect(transport);
```

**Step 6: Build the MCP server**

Run:
```bash
cd D:/ai/playgodot/src/godotplay-mcp
npm run build
```
Expected: Compiles to `dist/`.

**Step 7: Commit**

```bash
cd D:/ai/playgodot
git add src/godotplay-mcp/
git commit -m "feat: add MCP server wrapping GodotPlay gRPC API"
```

---

### Task 10: Documentation & Final Cleanup

**Files:**
- Create: `README.md`
- Create: `CLAUDE.md`

**Step 1: Create README.md**

File: `README.md`

```markdown
# GodotPlay

Playwright-like test automation framework for Godot 4.x.

## Components

- **GodotPlay.Plugin** — C# Godot addon that embeds a gRPC server
- **GodotPlay.Client** — .NET client library with Playwright-inspired API
- **godotplay-mcp** — MCP server for AI agent integration

## Quick Start

### 1. Add plugin to your Godot project

Copy `src/GodotPlay.Plugin/addons/godotplay/` into your project's `addons/` folder.
Enable the plugin in Project > Project Settings > Plugins.

### 2. Write a test

```csharp
var session = await GodotPlay.LaunchAsync(new LaunchOptions {
    ProjectPath = "../my-game",
    Headless = true
});

var button = session.Locator(className: "Button", namePattern: "Start*");
await button.ClickAsync();
await Expect.That(button).ToExistAsync();

await session.DisposeAsync();
```

### 3. Use with AI agents (MCP)

Add to your Claude Code MCP config:
```json
{
  "mcpServers": {
    "godotplay": {
      "command": "node",
      "args": ["path/to/godotplay-mcp/dist/index.js"]
    }
  }
}
```

## Development

```bash
dotnet build GodotPlay.sln          # Build .NET projects
dotnet test                          # Run unit tests
cd src/godotplay-mcp && npm run build  # Build MCP server
```

## Status

MVP — proof of concept. See [design document](docs/plans/2026-03-17-godotplay-design.md) for full vision.
```

**Step 2: Create CLAUDE.md**

File: `CLAUDE.md`

```markdown
# CLAUDE.md — Project context for Claude Code

## Project

GodotPlay — Playwright-like test automation framework for Godot 4.x with MCP server for AI agents.

## Structure

- `proto/` — Shared protobuf definitions (source of truth for gRPC API)
- `src/GodotPlay.Client/` — .NET client library (NuGet package)
- `src/GodotPlay.Plugin/` — Godot C# addon (gRPC server embedded in Godot)
- `src/GodotPlay.Tests/` — Unit + integration tests (NUnit)
- `src/godotplay-mcp/` — TypeScript MCP server
- `demo/` — Demo Godot project for testing

## Commands

- `dotnet build GodotPlay.sln` — Build all .NET projects
- `dotnet test src/GodotPlay.Tests --filter "Category!=Integration"` — Unit tests only
- `dotnet test src/GodotPlay.Tests --filter "Category=Integration"` — Integration tests (requires Godot)
- `cd src/godotplay-mcp && npm run build` — Build MCP server

## Key decisions

- gRPC for communication (protobuf schema-first)
- Headless input via direct signal emission (workaround for Godot bug #73557)
- MCP server in TypeScript (ecosystem standard), everything else in C#
- Plugin is an AutoLoad node that starts gRPC server on _Ready()
```

**Step 3: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "docs: add README and CLAUDE.md with project context"
```

---

## Summary

| Task | Description | Dependencies |
|------|-------------|-------------|
| 1 | Repo setup, proto definition, .NET projects | None |
| 2 | NodeLocator with query building | Task 1 |
| 3 | GodotPlaySession with gRPC client + mock tests | Task 2 |
| 4 | Expect API with auto-retry assertions | Task 3 |
| 5 | GodotPlay.Launch() process management | Task 3 |
| 6 | Godot plugin (gRPC server, inspector, input, screenshot) | Task 1 |
| 7 | Demo Godot project | Task 6 |
| 8 | End-to-end integration tests | Tasks 5, 7 |
| 9 | MCP server (TypeScript) | Task 1 |
| 10 | README, CLAUDE.md | All |

Tasks 2-5 (client library) and Task 6 (plugin) can be developed in parallel.
Tasks 7-8 require both client and plugin.
Task 9 (MCP) only depends on the proto definition (Task 1).
