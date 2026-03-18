# GodotPlay Features 1-7 (except 3) Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extend GodotPlay from MVP to production-ready with more interactions, skills, event streaming, visual regression, CI/CD integration, and addon packaging.

**Architecture:** New gRPC RPCs in proto → C# plugin handlers → MCP server tools → Skills. Each feature layer builds on the previous. Event streaming uses gRPC server-side streaming. Visual regression compares screenshots via perceptual hash. CI/CD wraps the test runner in a GitHub Actions workflow.

**Tech Stack:** .NET 8/9, Godot 4.x, gRPC (Grpc.Core), Protobuf, NUnit, TypeScript, @modelcontextprotocol/sdk, GitHub Actions

---

### Task 1: Proto — Add New RPCs (Type, GetProperty, SetProperty, WaitForNode, WaitForSignal, SubscribeEvents)

**Files:**
- Modify: `proto/godotplay.proto`

**Step 1: Add new messages and RPCs to proto**

Add these messages after the existing ones in `proto/godotplay.proto`:

```protobuf
message TypeRequest {
  string node_path = 1;
  string text = 2;
  bool clear_first = 3;  // clear existing text before typing
}

message SetPropertyRequest {
  string node_path = 1;
  string property_name = 2;
  string value = 3;
}

message WaitRequest {
  string node_path = 1;       // wait for this node to exist
  string class_name = 2;      // optional: also match class
  int32 timeout_ms = 3;       // max wait time (default 5000)
}

message SignalWaitRequest {
  string node_path = 1;
  string signal_name = 2;
  int32 timeout_ms = 3;
}

message SignalData {
  string signal_name = 1;
  string node_path = 2;
  repeated string args = 3;
}

message EventFilter {
  bool scene_changes = 1;
  bool signals = 2;
  bool errors = 3;
  repeated string watch_signals = 4;  // specific signal names
}

message GameEvent {
  string type = 1;           // "scene_changed", "signal", "error", "node_added", "node_removed"
  string detail = 2;         // JSON payload
  int64 timestamp_ms = 3;
}

message LoadSceneRequest {
  string scene_path = 1;     // e.g. "res://scenes/game.tscn"
}

message SceneInfo {
  string scene_path = 1;
  string root_node_path = 2;
  string root_class_name = 3;
}
```

Add these RPCs to the `GodotPlayService`:

```protobuf
  // New interactions
  rpc Type(TypeRequest) returns (ActionResult);
  rpc SetProperty(SetPropertyRequest) returns (ActionResult);
  rpc GetProperty(NodeRef) returns (PropertyMap);  // alias for GetNodeProperties

  // Navigation
  rpc LoadScene(LoadSceneRequest) returns (ActionResult);
  rpc GetCurrentScene(Empty) returns (SceneInfo);

  // Waiting
  rpc WaitForNode(WaitRequest) returns (NodeRef);
  rpc WaitForSignal(SignalWaitRequest) returns (SignalData);

  // Event streaming
  rpc SubscribeEvents(EventFilter) returns (stream GameEvent);
```

**Step 2: Rebuild all .NET projects to verify proto compiles**

Run: `dotnet build src/GodotPlay.Client && dotnet build src/GodotPlay.Tests`
Expected: Build succeeds (new RPCs generate stubs).

**Step 3: Commit**

```bash
git add proto/godotplay.proto
git commit -m "feat(proto): add Type, SetProperty, LoadScene, Wait, SubscribeEvents RPCs"
```

---

### Task 2: Plugin — Implement New Interactions (Type, SetProperty, LoadScene)

**Files:**
- Create: `src/GodotPlay.Plugin/addons/godotplay/Services/TextInput.cs`
- Modify: `src/GodotPlay.Plugin/addons/godotplay/Services/GodotPlayServiceImpl.cs`
- Modify: `src/GodotPlay.Plugin/addons/godotplay/Services/SceneTreeInspector.cs`

**Step 1: Create TextInput service**

File: `src/GodotPlay.Plugin/addons/godotplay/Services/TextInput.cs`

```csharp
using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class TextInput
{
    private readonly SceneTree _sceneTree;

    public TextInput(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ActionResult Type(TypeRequest request)
    {
        var node = _sceneTree.Root.GetNodeOrNull(request.NodePath);
        if (node == null)
            return new ActionResult { Success = false, Error = $"Node not found: {request.NodePath}" };

        if (node is LineEdit lineEdit)
        {
            if (request.ClearFirst) lineEdit.Text = "";
            lineEdit.Text += request.Text;
            lineEdit.EmitSignal(LineEdit.SignalName.TextChanged, lineEdit.Text);
            return new ActionResult { Success = true };
        }

        if (node is TextEdit textEdit)
        {
            if (request.ClearFirst) textEdit.Text = "";
            textEdit.Text += request.Text;
            textEdit.EmitSignal(TextEdit.SignalName.TextChanged);
            return new ActionResult { Success = true };
        }

        return new ActionResult { Success = false, Error = $"Node {request.NodePath} is not a text input (LineEdit/TextEdit)." };
    }

    public ActionResult SetProperty(SetPropertyRequest request)
    {
        var node = _sceneTree.Root.GetNodeOrNull(request.NodePath);
        if (node == null)
            return new ActionResult { Success = false, Error = $"Node not found: {request.NodePath}" };

        var variant = ParseVariant(request.Value);
        node.Set(request.PropertyName, variant);
        return new ActionResult { Success = true };
    }

    public ActionResult LoadScene(LoadSceneRequest request)
    {
        var error = _sceneTree.ChangeSceneToFile(request.ScenePath);
        if (error != Error.Ok)
            return new ActionResult { Success = false, Error = $"Failed to load scene: {request.ScenePath} ({error})" };
        return new ActionResult { Success = true };
    }

    public SceneInfo GetCurrentScene()
    {
        var scene = _sceneTree.CurrentScene;
        return new SceneInfo
        {
            ScenePath = scene?.SceneFilePath ?? "",
            RootNodePath = scene?.GetPath() ?? "",
            RootClassName = scene?.GetClass() ?? ""
        };
    }

    private static Variant ParseVariant(string value)
    {
        if (bool.TryParse(value, out var b)) return b;
        if (int.TryParse(value, out var i)) return i;
        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)) return f;
        return value;
    }
}
```

**Step 2: Add Type/SetProperty/LoadScene/GetCurrentScene to GodotPlayServiceImpl**

Add to `GodotPlayServiceImpl.cs`:

```csharp
    public override Task<ActionResult> Type(TypeRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TextInput.Type(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> SetProperty(SetPropertyRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TextInput.SetProperty(request));
        return Task.FromResult(result);
    }

    public override Task<ActionResult> LoadScene(LoadSceneRequest request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TextInput.LoadScene(request));
        return Task.FromResult(result);
    }

    public override Task<SceneInfo> GetCurrentScene(Empty request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.TextInput.GetCurrentScene());
        return Task.FromResult(result);
    }

    public override Task<PropertyMap> GetProperty(NodeRef request, ServerCallContext context)
    {
        var result = _server.RunOnMainThread(() => _server.Inspector.GetNodeProperties(request));
        return Task.FromResult(result);
    }
```

**Step 3: Add TextInput to GodotPlayServer.cs**

Add field, property, and initialization alongside the existing services:

```csharp
private TextInput? _textInput;
public TextInput TextInput => _textInput!;

// In _Ready():
_textInput = new TextInput(GetTree());
```

**Step 4: Copy plugin files to demo project and rebuild both**

```bash
cp -r src/GodotPlay.Plugin/addons/godotplay/* demo/addons/godotplay/
cd demo && dotnet build
```

**Step 5: Commit**

```bash
git add src/GodotPlay.Plugin/ demo/addons/godotplay/
git commit -m "feat(plugin): add Type, SetProperty, LoadScene, GetCurrentScene RPCs"
```

---

### Task 3: Plugin — Implement WaitForNode and WaitForSignal

**Files:**
- Create: `src/GodotPlay.Plugin/addons/godotplay/Services/Waiter.cs`
- Modify: `src/GodotPlay.Plugin/addons/godotplay/Services/GodotPlayServiceImpl.cs`
- Modify: `src/GodotPlay.Plugin/addons/godotplay/GodotPlayServer.cs`

**Step 1: Create Waiter service**

File: `src/GodotPlay.Plugin/addons/godotplay/Services/Waiter.cs`

```csharp
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

            // Connect to the signal once
            Callable callback = default;
            callback = Callable.From(() =>
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
        {
            throw new Grpc.Core.RpcException(new Grpc.Core.Status(
                Grpc.Core.StatusCode.DeadlineExceeded,
                $"Signal {request.SignalName} on {request.NodePath} not received within {timeout}ms"));
        }

        return await tcs.Task;
    }
}
```

**Step 2: Wire into GodotPlayServiceImpl and GodotPlayServer**

GodotPlayServer: Add `Waiter` field and init.
GodotPlayServiceImpl:

```csharp
    public override async Task<NodeRef> WaitForNode(WaitRequest request, ServerCallContext context)
    {
        return await _server.Waiter.WaitForNode(request, _server);
    }

    public override async Task<SignalData> WaitForSignal(SignalWaitRequest request, ServerCallContext context)
    {
        return await _server.Waiter.WaitForSignal(request, _server);
    }
```

**Step 3: Copy to demo, rebuild, commit**

```bash
cp -r src/GodotPlay.Plugin/addons/godotplay/* demo/addons/godotplay/
cd demo && dotnet build
git add src/GodotPlay.Plugin/ demo/addons/godotplay/
git commit -m "feat(plugin): add WaitForNode and WaitForSignal RPCs"
```

---

### Task 4: Plugin — Implement Event Streaming (SubscribeEvents)

**Files:**
- Create: `src/GodotPlay.Plugin/addons/godotplay/Services/EventStreamer.cs`
- Modify: `src/GodotPlay.Plugin/addons/godotplay/Services/GodotPlayServiceImpl.cs`
- Modify: `src/GodotPlay.Plugin/addons/godotplay/GodotPlayServer.cs`

**Step 1: Create EventStreamer**

File: `src/GodotPlay.Plugin/addons/godotplay/Services/EventStreamer.cs`

```csharp
using System.Collections.Concurrent;
using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class EventStreamer
{
    private readonly SceneTree _sceneTree;
    private readonly ConcurrentQueue<GameEvent> _eventQueue = new();
    private string _lastScenePath = "";

    public EventStreamer(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
        _lastScenePath = sceneTree.CurrentScene?.SceneFilePath ?? "";
    }

    /// <summary>Call from _Process on main thread to detect scene changes</summary>
    public void Poll()
    {
        var currentScene = _sceneTree.CurrentScene?.SceneFilePath ?? "";
        if (currentScene != _lastScenePath)
        {
            _eventQueue.Enqueue(new GameEvent
            {
                Type = "scene_changed",
                Detail = $"{{\"from\":\"{_lastScenePath}\",\"to\":\"{currentScene}\"}}",
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            _lastScenePath = currentScene;
        }
    }

    public void PushEvent(string type, string detail)
    {
        _eventQueue.Enqueue(new GameEvent
        {
            Type = type,
            Detail = detail,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    public bool TryDequeue(out GameEvent? evt)
    {
        return _eventQueue.TryDequeue(out evt);
    }
}
```

**Step 2: Wire SubscribeEvents into GodotPlayServiceImpl**

```csharp
    public override async Task SubscribeEvents(EventFilter request,
        Grpc.Core.IServerStreamWriter<GameEvent> responseStream,
        ServerCallContext context)
    {
        while (!context.CancellationToken.IsCancellationRequested)
        {
            while (_server.EventStreamer.TryDequeue(out var evt))
            {
                if (evt != null)
                    await responseStream.WriteAsync(evt);
            }
            await Task.Delay(100, context.CancellationToken);
        }
    }
```

**Step 3: Add EventStreamer to GodotPlayServer, call Poll() in _Process**

```csharp
private EventStreamer? _eventStreamer;
public EventStreamer EventStreamer => _eventStreamer!;

// In _Ready():
_eventStreamer = new EventStreamer(GetTree());

// In _Process(), after the work queue processing:
_eventStreamer?.Poll();
```

**Step 4: Copy to demo, rebuild, commit**

```bash
cp -r src/GodotPlay.Plugin/addons/godotplay/* demo/addons/godotplay/
cd demo && dotnet build
git add src/GodotPlay.Plugin/ demo/addons/godotplay/
git commit -m "feat(plugin): add event streaming (scene changes)"
```

---

### Task 5: MCP Server — Add New Tools (type, get_property, set_property, wait, load_scene, events)

**Files:**
- Modify: `src/godotplay-mcp/src/godot-client.ts`
- Modify: `src/godotplay-mcp/src/index.ts`

**Step 1: Add new gRPC client methods to godot-client.ts**

Add these methods to the `GodotPlayClient` class:

```typescript
  type(nodePath: string, text: string, clearFirst: boolean = false): Promise<{ success: boolean; error: string }> {
    return this.callUnary("type", { nodePath, text, clearFirst });
  }

  getProperty(nodePath: string): Promise<{ properties: Record<string, string> }> {
    return this.callUnary("getProperty", { path: nodePath });
  }

  setProperty(nodePath: string, propertyName: string, value: string): Promise<{ success: boolean; error: string }> {
    return this.callUnary("setProperty", { nodePath, propertyName, value });
  }

  loadScene(scenePath: string): Promise<{ success: boolean; error: string }> {
    return this.callUnary("loadScene", { scenePath });
  }

  getCurrentScene(): Promise<{ scenePath: string; rootNodePath: string; rootClassName: string }> {
    return this.callUnary("getCurrentScene", {});
  }

  waitForNode(nodePath: string, className?: string, timeoutMs: number = 5000): Promise<{ path: string }> {
    return this.callUnary("waitForNode", { nodePath, className: className || "", timeoutMs });
  }

  waitForSignal(nodePath: string, signalName: string, timeoutMs: number = 5000): Promise<{ signalName: string; nodePath: string; args: string[] }> {
    return this.callUnary("waitForSignal", { nodePath, signalName, timeoutMs });
  }
```

**Step 2: Add new MCP tools to index.ts**

Add after existing tools:

```typescript
server.tool(
  "godot_type",
  "Type text into a LineEdit or TextEdit node",
  {
    nodePath: z.string().describe("Absolute path to the text input node"),
    text: z.string().describe("Text to type"),
    clearFirst: z.boolean().default(false).describe("Clear existing text before typing"),
  },
  async ({ nodePath, text, clearFirst }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.type(nodePath, text, clearFirst);
    return { content: [{ type: "text" as const, text: result.success ? `Typed "${text}" into ${nodePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_get_property",
  "Get all properties of a specific node",
  {
    nodePath: z.string().describe("Absolute node path"),
  },
  async ({ nodePath }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const props = await godotClient.getProperty(nodePath);
    return { content: [{ type: "text" as const, text: JSON.stringify(props.properties, null, 2) }] };
  }
);

server.tool(
  "godot_set_property",
  "Set a property on a node",
  {
    nodePath: z.string().describe("Absolute node path"),
    property: z.string().describe("Property name (e.g. 'text', 'visible', 'modulate')"),
    value: z.string().describe("Value as string (auto-parsed to bool/int/float/string)"),
  },
  async ({ nodePath, property, value }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.setProperty(nodePath, property, value);
    return { content: [{ type: "text" as const, text: result.success ? `Set ${property}=${value} on ${nodePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_load_scene",
  "Navigate to a different scene",
  {
    scenePath: z.string().describe("Scene resource path (e.g. res://scenes/game.tscn)"),
  },
  async ({ scenePath }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.loadScene(scenePath);
    return { content: [{ type: "text" as const, text: result.success ? `Loaded scene: ${scenePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_wait",
  "Wait for a node to appear in the scene tree, or for a signal to fire",
  {
    nodePath: z.string().describe("Node path to wait for"),
    signal: z.string().optional().describe("If set, wait for this signal on the node instead of just existence"),
    timeout: z.number().default(5000).describe("Timeout in milliseconds"),
  },
  async ({ nodePath, signal, timeout }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    try {
      if (signal) {
        const result = await godotClient.waitForSignal(nodePath, signal, timeout);
        return { content: [{ type: "text" as const, text: `Signal "${result.signalName}" received from ${result.nodePath}` }] };
      } else {
        const result = await godotClient.waitForNode(nodePath, undefined, timeout);
        return { content: [{ type: "text" as const, text: `Node found: ${result.path}` }] };
      }
    } catch (err: any) {
      return { content: [{ type: "text" as const, text: `Timeout: ${err.message || err}` }], isError: true };
    }
  }
);
```

**Step 3: Build MCP server**

```bash
cd src/godotplay-mcp && npm run build
```

**Step 4: Commit**

```bash
git add src/godotplay-mcp/
git commit -m "feat(mcp): add type, get_property, set_property, load_scene, wait tools"
```

---

### Task 6: Client Library — Add New Methods and Tests

**Files:**
- Modify: `src/GodotPlay.Client/GodotPlaySession.cs`
- Modify: `src/GodotPlay.Client/NodeLocator.cs`
- Modify: `src/GodotPlay.Client/Expect.cs`
- Modify: `src/GodotPlay.Tests/SessionTests.cs` (add mock implementations)
- Create: `src/GodotPlay.Tests/TypeAndPropertyTests.cs`
- Create: `src/GodotPlay.Tests/WaitTests.cs`

**Step 1: Add new methods to GodotPlaySession**

```csharp
    public async Task<ActionResult> TypeAsync(TypeRequest request, CancellationToken ct = default)
    {
        return await _client.TypeAsync(request, cancellationToken: ct);
    }

    public async Task<ActionResult> SetPropertyAsync(SetPropertyRequest request, CancellationToken ct = default)
    {
        return await _client.SetPropertyAsync(request, cancellationToken: ct);
    }

    public async Task<ActionResult> LoadSceneAsync(string scenePath, CancellationToken ct = default)
    {
        return await _client.LoadSceneAsync(new LoadSceneRequest { ScenePath = scenePath }, cancellationToken: ct);
    }

    public async Task<SceneInfo> GetCurrentSceneAsync(CancellationToken ct = default)
    {
        return await _client.GetCurrentSceneAsync(new Empty(), cancellationToken: ct);
    }

    public async Task<NodeRef> WaitForNodeAsync(WaitRequest request, CancellationToken ct = default)
    {
        return await _client.WaitForNodeAsync(request, cancellationToken: ct);
    }

    public async Task<SignalData> WaitForSignalAsync(SignalWaitRequest request, CancellationToken ct = default)
    {
        return await _client.WaitForSignalAsync(request, cancellationToken: ct);
    }
```

**Step 2: Add TypeAsync to NodeLocator**

```csharp
    public async Task<ActionResult> TypeAsync(string text, bool clearFirst = false, CancellationToken ct = default)
    {
        if (_session == null)
            throw new InvalidOperationException("NodeLocator is not bound to a session.");

        string path = _path ?? (await ResolveAsync(ct))[0].Path;
        return await _session.TypeAsync(new TypeRequest { NodePath = path, Text = text, ClearFirst = clearFirst }, ct);
    }
```

**Step 3: Add ToHavePropertyAsync to Expect**

```csharp
    public async Task ToHavePropertyAsync(string propertyName, string expectedValue, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        string lastValue = "";
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var nodes = await _locator.ResolveAsync(ct);
            if (nodes.Count > 0 && nodes[0].Properties.TryGetValue(propertyName, out var val))
            {
                lastValue = val;
                if (val == expectedValue) return;
            }
            await Task.Delay(PollInterval, ct);
        }
        throw new TimeoutException(
            $"Expected property '{propertyName}' to be '{expectedValue}', but was '{lastValue}' within {timeout ?? DefaultTimeout}.");
    }
```

**Step 4: Add mock implementations to MockGodotPlayService in SessionTests.cs**

Add `Type`, `SetProperty`, `LoadScene`, `GetCurrentScene`, `WaitForNode`, `WaitForSignal` overrides.

**Step 5: Write tests**

File: `src/GodotPlay.Tests/TypeAndPropertyTests.cs` — 3 tests (type text, set property, get property).
File: `src/GodotPlay.Tests/WaitTests.cs` — 2 tests (wait for node success, wait timeout).

**Step 6: Run all tests, commit**

```bash
dotnet test src/GodotPlay.Tests --filter "Category!=Integration" -v n
git add src/
git commit -m "feat(client): add Type, SetProperty, LoadScene, Wait methods and tests"
```

---

### Task 7: Skills — Create /godotplay-test and /godotplay-debug Commands

**Files:**
- Create: `claude-plugin/skills/test/SKILL.md`
- Create: `claude-plugin/skills/debug/SKILL.md`
- Copy to: `D:/ai/project-starfall2/.claude/commands/godotplay-test.md`
- Copy to: `D:/ai/project-starfall2/.claude/commands/godotplay-debug.md`

**Step 1: Create test skill**

File: `claude-plugin/skills/test/SKILL.md`

The test skill should instruct Claude to:
1. `godot_recall()` to load known screens
2. `godot_launch()` to start the project
3. For each known screen: navigate there, inspect, screenshot, verify buttons work
4. Generate a test report with pass/fail per screen
5. `godot_learn()` with any new discoveries
6. Suggest .NET NUnit tests based on what was found

**Step 2: Create debug skill**

File: `claude-plugin/skills/debug/SKILL.md`

The debug skill should instruct Claude to:
1. Ask user what the problem is
2. `godot_launch()` and navigate to the relevant screen
3. `godot_inspect_tree()` deeply on the problem area
4. `godot_get_property()` on suspect nodes
5. `godot_screenshot()` for visual evidence
6. Analyze and suggest fixes with file:line references

**Step 3: Copy to project-starfall2**

```bash
cp claude-plugin/skills/test/SKILL.md D:/ai/project-starfall2/.claude/commands/godotplay-test.md
cp claude-plugin/skills/debug/SKILL.md D:/ai/project-starfall2/.claude/commands/godotplay-debug.md
```

(Update frontmatter to use `description:` format for commands)

**Step 4: Commit**

```bash
git add claude-plugin/skills/
git commit -m "feat(skills): add /godotplay-test and /godotplay-debug skills"
```

---

### Task 8: Visual Regression — Screenshot Comparison

**Files:**
- Create: `src/GodotPlay.Client/VisualRegression.cs`
- Create: `src/GodotPlay.Tests/VisualRegressionTests.cs`
- Add MCP tool: `godot_visual_compare` in `src/godotplay-mcp/src/index.ts`

**Step 1: Implement perceptual hash comparison in C#**

File: `src/GodotPlay.Client/VisualRegression.cs`

Simple approach: resize both images to 8x8 grayscale, compute average hash, compare hamming distance.

```csharp
namespace GodotPlay;

public static class VisualRegression
{
    public static ulong ComputeHash(byte[] pngData)
    {
        // Decode PNG, resize to 8x8, grayscale, compute average hash
        // Use System.Drawing or SkiaSharp — keep it simple
    }

    public static double Compare(ulong hash1, ulong hash2)
    {
        // Hamming distance as percentage (0 = identical, 1 = completely different)
        var xor = hash1 ^ hash2;
        var bits = 0;
        while (xor != 0) { bits += (int)(xor & 1); xor >>= 1; }
        return bits / 64.0;
    }

    public static async Task SaveBaseline(string name, byte[] pngData, string baselineDir)
    {
        Directory.CreateDirectory(baselineDir);
        await File.WriteAllBytesAsync(Path.Combine(baselineDir, $"{name}.png"), pngData);
    }

    public static async Task<byte[]?> LoadBaseline(string name, string baselineDir)
    {
        var path = Path.Combine(baselineDir, $"{name}.png");
        return File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
    }
}
```

**Step 2: Add MCP tool godot_visual_compare**

Saves screenshots as baselines and compares against previous runs. Returns diff percentage.

**Step 3: Write tests, commit**

```bash
git add src/
git commit -m "feat: add visual regression with perceptual hash comparison"
```

---

### Task 9: CI/CD — GitHub Actions Workflow

**Files:**
- Create: `.github/workflows/godotplay-tests.yml`

**Step 1: Create workflow**

```yaml
name: GodotPlay Tests
on: [push, pull_request]
jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
      - run: dotnet test src/GodotPlay.Tests --filter "Category!=Integration" -v n

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
      - uses: chickensoft-games/setup-godot@v2
        with:
          version: 4.6.1
          use-dotnet: true
      - run: cd demo && dotnet build
      - run: |
          GODOT_PATH=godot dotnet test src/GodotPlay.Tests \
            --filter "Category=Integration" -v n
```

**Step 2: Commit**

```bash
git add .github/
git commit -m "ci: add GitHub Actions workflow for unit and integration tests"
```

---

### Task 10: Addon Packaging — NuGet + npm Publish Config

**Files:**
- Modify: `src/GodotPlay.Client/GodotPlay.Client.csproj` (NuGet metadata)
- Modify: `src/godotplay-mcp/package.json` (npm publish config)
- Create: `src/GodotPlay.Plugin/README.md` (addon installation guide)

**Step 1: Add NuGet metadata to Client .csproj**

```xml
<PropertyGroup>
  <PackageId>GodotPlay</PackageId>
  <Version>0.2.0</Version>
  <Authors>GodotPlay</Authors>
  <Description>Playwright-like test automation for Godot 4.x</Description>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageProjectUrl>https://github.com/your-org/godotplay</PackageProjectUrl>
  <PackageTags>godot;testing;automation;playwright;grpc</PackageTags>
</PropertyGroup>
```

**Step 2: Add npm publish config**

```json
{
  "publishConfig": { "access": "public" },
  "files": ["dist/", "README.md"]
}
```

**Step 3: Create addon installation README**

Document: copy addons/godotplay/, add NuGet packages, add autoload.

**Step 4: Commit**

```bash
git add src/ .github/
git commit -m "feat: add NuGet/npm packaging config and addon installation docs"
```

---

## Task Dependency Summary

| Task | Description | Depends On |
|------|-------------|-----------|
| 1 | Proto: new RPCs | None |
| 2 | Plugin: Type, SetProperty, LoadScene | Task 1 |
| 3 | Plugin: WaitForNode, WaitForSignal | Task 1 |
| 4 | Plugin: Event Streaming | Task 1 |
| 5 | MCP Server: new tools | Tasks 2, 3 |
| 6 | Client Library: new methods + tests | Tasks 2, 3 |
| 7 | Skills: test + debug | Task 5 |
| 8 | Visual Regression | Task 5 |
| 9 | CI/CD | Task 6 |
| 10 | Packaging | All |

Tasks 2, 3, 4 can run in parallel after Task 1.
Tasks 5, 6 can run in parallel after Tasks 2+3.
Tasks 7, 8 can run in parallel after Task 5.
