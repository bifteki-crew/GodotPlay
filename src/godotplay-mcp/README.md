# godotplay-mcp

MCP server for AI-powered Godot 4.x game testing. Gives AI agents (Claude Code, Cursor, etc.) the ability to launch, inspect, interact with, and test Godot games — like Playwright, but for game UIs.

## Setup

Add to your `.mcp.json` or Claude Code MCP settings:

```json
{
  "mcpServers": {
    "godotplay": {
      "command": "npx",
      "args": ["godotplay-mcp"]
    }
  }
}
```

### Prerequisites

- **Node.js** 18+
- **Godot 4.x** with the [GodotPlay.Plugin](https://www.nuget.org/packages/GodotPlay.Plugin) addon installed in your project
- `GODOT_PATH` environment variable (or `godot` on PATH)

### Installing the Godot addon

```bash
cd your-godot-project
dotnet add package GodotPlay.Plugin
dotnet build
```

This automatically copies the addon files to `addons/godotplay/`, registers the autoload, and pulls in gRPC dependencies.

## What it does

The MCP server connects to the GodotPlay gRPC plugin running inside Godot. It exposes tools that let AI agents interact with a running game just like a human tester would — clicking buttons, typing text, taking screenshots, inspecting the scene tree, and verifying properties.

## Tools

### Lifecycle
| Tool | Description |
|------|-------------|
| `godot_launch` | Start Godot with the GodotPlay plugin |
| `godot_shutdown` | Gracefully close the running Godot instance |
| `godot_wait` | Wait for a node or signal |

### Inspection
| Tool | Description |
|------|-------------|
| `godot_inspect_tree` | Query the scene tree (nodes, properties, hierarchy) |
| `godot_get_property` | Read properties of a node |
| `godot_set_property` | Set a property on a node |
| `godot_get_scene` | Get current scene info |
| `godot_screenshot` | Capture a screenshot (full viewport or specific node) |
| `godot_visual_compare` | Compare screenshot against a saved baseline |

### High-Level Interaction
| Tool | Description |
|------|-------------|
| `godot_click` | Click a node by path |
| `godot_type` | Type text into a node |
| `godot_hover` | Hover over a node |
| `godot_drag` | Drag from one node to another |
| `godot_scroll` | Scroll a node |
| `godot_load_scene` | Load a scene by path |

### Low-Level Input
| Tool | Description |
|------|-------------|
| `godot_mouse` | Raw mouse events (move, click, wheel) |
| `godot_key` | Keyboard events (press, release, type) |
| `godot_touch` | Touch events (tap, drag) |
| `godot_gesture` | Gesture events (pinch, pan, magnify) |
| `godot_gamepad` | Gamepad button and axis events |
| `godot_action` | Godot Input Map actions |

### Knowledge
| Tool | Description |
|------|-------------|
| `godot_learn` | Save discovered screens, buttons, and navigation to a game map |
| `godot_recall` | Recall saved game knowledge |

## Prompts

The server includes guided workflows as MCP prompts:

| Prompt | Description |
|--------|-------------|
| `godot_explore` | Launch and interactively explore a Godot project |
| `godot_test` | Systematically test UI with verification and reporting |
| `godot_debug` | Reproduce and diagnose UI problems |
| `godot_coverage` | Analyze test coverage and identify gaps |

## How it works

```
AI Agent  ←—stdio—→  godotplay-mcp  ←—gRPC :50051—→  Godot (GodotPlay Plugin)
```

1. The AI agent communicates with the MCP server via stdio (standard MCP transport)
2. The MCP server translates tool calls into gRPC requests
3. The GodotPlay plugin inside Godot executes them on the main thread and returns results

## License

MIT
