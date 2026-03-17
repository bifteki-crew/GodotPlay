# GodotPlay Design Document

**Date:** 2026-03-17
**Status:** Approved
**Author:** Design collaboration (User + Claude)

## Problem Statement

There is no Playwright-equivalent for Godot that works with unmodified engine builds. Existing tools (GUT, GdUnit4, Chickensoft) are in-process only. PlayGodot requires an engine fork. No testing-focused MCP server exists for AI-agent integration.

## Vision

GodotPlay is a three-part ecosystem for automated UI testing of Godot 4.x projects:

1. **Godot Plugin** (C#) — gRPC server embedded in the Godot process
2. **Test Runner** (.NET) — External process, Playwright-inspired API
3. **MCP Server** (TypeScript) — Wraps gRPC API for AI agents + LLM skills

## Architecture

```
┌─────────────────────┐     gRPC      ┌──────────────────────┐
│  Godot Instance      │◄────────────►│  GodotPlay           │
│  + GodotPlay Plugin  │  (bidirect.) │  Test Runner (.NET)  │
│  (gRPC Server)       │              │  (gRPC Client)       │
└─────────────────────┘              └──────────────────────┘
                                              ▲
                                              │ uses
                                              ▼
                                     ┌──────────────────────┐
                                     │  MCP Server           │
                                     │  (wraps gRPC API)     │
                                     │  + LLM Skills/Agents  │
                                     └──────────────────────┘
```

### Data Flow

1. Test runner starts Godot instance (headless or visual) with the plugin loaded
2. Plugin starts gRPC server on localhost
3. Test runner connects as gRPC client
4. Tests send commands (find node, click, assert), plugin executes
5. Plugin streams events back (signals, errors, scene changes)
6. MCP server wraps the same gRPC API for AI agents

### Key Design Decisions

- **gRPC over WebSocket**: Typed schemas (protobuf), bidirectional streaming, auto-generated stubs in any language, HTTP/2 multiplexing
- **Plugin-based (not engine fork)**: Works with stock Godot 4.x, easy to install as addon
- **Godot 4.x with both C# and GDScript support**: Plugin is C#, but can inspect/test GDScript projects too
- **Localhost-only by default**: Security constraint, no remote access without explicit opt-in

## Component Details

### 1. Godot Plugin (gRPC Server)

**Location:** `addons/godotplay/` in the target Godot project

**Files:**
- `GodotPlayServer.cs` — AutoLoad node, starts/stops gRPC server
- `SceneTreeInspector.cs` — Traverses and serializes the scene tree
- `InputSimulator.cs` — Simulates input with headless workaround
- `SignalWatcher.cs` — Connects to signals, forwards to gRPC stream
- `ScreenshotCapture.cs` — Viewport capture to PNG

**Headless Input Strategy (3-tier):**
1. **Preferred:** Direct node methods (`button.EmitSignal("pressed")`, `lineEdit.Text = "..."`)
2. **Fallback:** `Input.ParseInputEvent()` when not headless
3. **CI:** Xvfb/Virtual Display for full input simulation

**Configuration:**
```ini
[godotplay]
port = 50051
auto_start = true
allowed_hosts = ["127.0.0.1"]
```

### 2. gRPC Protocol

```protobuf
service GodotPlayService {
  // Scene Tree
  rpc GetSceneTree(Empty) returns (SceneTreeResponse);
  rpc FindNodes(NodeQuery) returns (NodeList);
  rpc GetNodeProperties(NodeRef) returns (PropertyMap);
  rpc SetNodeProperty(SetPropertyRequest) returns (Empty);

  // Interaction
  rpc Click(NodeRef) returns (ActionResult);
  rpc Type(TypeRequest) returns (ActionResult);
  rpc DragDrop(DragDropRequest) returns (ActionResult);
  rpc SimulateInput(InputEventRequest) returns (ActionResult);

  // Navigation
  rpc LoadScene(SceneRequest) returns (ActionResult);
  rpc GetCurrentScene(Empty) returns (SceneInfo);

  // Wait & Assertions
  rpc WaitForNode(WaitRequest) returns (NodeRef);
  rpc WaitForSignal(SignalWaitRequest) returns (SignalData);
  rpc TakeScreenshot(ScreenshotRequest) returns (ScreenshotData);

  // Events (bidirectional stream)
  rpc SubscribeEvents(EventFilter) returns (stream GameEvent);

  // Lifecycle
  rpc Ping(Empty) returns (PingResponse);
  rpc Shutdown(Empty) returns (Empty);
}
```

**NodeQuery system:** Nodes are located by path, class name, group, or property filters. Queries are lazy and re-evaluated on each action (like Playwright Locators).

### 3. Test Runner (.NET)

**NuGet packages:** `GodotPlay` (client library) + `GodotPlay.NUnit` / `GodotPlay.XUnit`

**API Example:**
```csharp
[TestFixture]
public class MainMenuTests
{
    private IGodotPlaySession _session;

    [SetUp]
    public async Task Setup()
    {
        _session = await GodotPlay.Launch(new LaunchOptions {
            ProjectPath = "../my-godot-game",
            Headless = true,
            Scene = "res://scenes/main_menu.tscn"
        });
    }

    [Test]
    public async Task StartButton_NavigatesToGame()
    {
        var startButton = _session.Locator("Button", text: "Start Game");
        await startButton.ClickAsync();
        await Expect(_session.CurrentScene).ToHavePathAsync("res://scenes/game.tscn");
    }

    [TearDown]
    public async Task Teardown()
    {
        await _session.DisposeAsync();
    }
}
```

**Core features:**
- `GodotPlay.Launch()` — Starts Godot process, waits for gRPC ready
- `NodeLocator` — Lazy, re-evaluating, with filters (path, class, text, group, properties)
- `Expect()` — Auto-retry assertions (timeout-based, like Playwright)
- Godot process lifecycle management (start, stop, crash detection)
- Locator chaining: `_session.Locator("VBoxContainer").Locator("Button", text: "Settings").First.ClickAsync()`

### 4. MCP Server (TypeScript)

**MCP Tools:**

| Tool | Description |
|------|-------------|
| `godot_launch` | Starts a Godot instance with a project |
| `godot_inspect_tree` | Shows the current scene tree |
| `godot_find_nodes` | Finds nodes by criteria |
| `godot_click` | Clicks a node |
| `godot_type_text` | Types text into an input field |
| `godot_get_property` | Reads a node property |
| `godot_set_property` | Sets a node property |
| `godot_load_scene` | Loads a scene |
| `godot_screenshot` | Takes a screenshot |
| `godot_wait_signal` | Waits for a signal |
| `godot_run_test` | Runs a single test |
| `godot_shutdown` | Shuts down the Godot instance |

**MCP Resources:**
- `godot://scene-tree` — Current scene tree as structured data
- `godot://project-settings` — Project configuration
- `godot://console-log` — Godot console output

**LLM Skills (future, post-MVP):**
- `/godotplay:test` — Generate tests for a scene based on its structure
- `/godotplay:explore` — Start Godot, interactively inspect the UI
- `/godotplay:debug` — Reproduce and debug a UI problem
- `/godotplay:coverage` — Analyze which UI paths are not yet tested

## MVP Scope (Thin Slice)

End-to-end proof that all three components work together.

### In MVP

**Godot Plugin:**
- gRPC server start/stop
- `GetSceneTree`, `FindNodes`, `GetNodeProperties`
- `Click` (direct signal emission)
- `TakeScreenshot`
- `Ping` / lifecycle

**Test Runner:**
- `GodotPlay.Launch()` — start Godot process with plugin
- `NodeLocator` — basic (path + class)
- `ClickAsync()` — click a node
- `Expect().ToExistAsync()` — one assertion with auto-retry
- `Screenshot()` — screenshot for debugging
- One example test against a demo scene

**MCP Server:**
- `godot_launch`, `godot_inspect_tree`, `godot_click`, `godot_screenshot`, `godot_shutdown`

**Demo Project:**
- Simple Godot scene: MainMenu with Start button -> GameScene
- Example test: "clicking Start button navigates to GameScene"
- MCP demo: agent inspects UI and clicks button

### NOT in MVP
- TypeScript test API
- Input simulation (only direct signal emission)
- Event streaming
- Visual regression testing
- Parallel test execution
- CI/CD integration
- LLM Skills (only MCP tools)

## Repository Structure

```
godotplay/
├── proto/                    # Shared .proto definitions
│   └── godotplay.proto
├── src/
│   ├── GodotPlay.Plugin/     # Godot C# Plugin (gRPC Server)
│   ├── GodotPlay.Client/     # .NET Client Library (NuGet)
│   ├── GodotPlay.Tests/      # Test Runner + example tests
│   └── godotplay-mcp/        # MCP Server (TypeScript/Node.js)
├── demo/                     # Demo Godot project
└── docs/
    └── plans/
```

## Competitive Landscape

| Tool | Language | External Runner | Stock Godot | AI Integration |
|------|----------|----------------|-------------|----------------|
| GUT | GDScript | No | Yes | No |
| GdUnit4 | GDScript + C# | No | Yes | No |
| Chickensoft | C# | No | Yes | No |
| PlayGodot | Python | Yes | No (fork) | No |
| **GodotPlay** | **C# + TS** | **Yes** | **Yes** | **Yes (MCP)** |

## Known Risks

1. **Headless input bug** (Godot #73557): `Input.parse_input_event()` doesn't work headless. Mitigated by direct signal emission strategy.
2. **gRPC in Godot**: No official gRPC NuGet for Godot. May need `Grpc.Net.Client` / `Grpc.Core` compatibility testing.
3. **Godot process management on Windows**: Godot process lifecycle may behave differently across OS. Need cross-platform testing.
4. **Scene tree serialization performance**: Large scene trees may be slow to serialize. May need pagination or filtering.
