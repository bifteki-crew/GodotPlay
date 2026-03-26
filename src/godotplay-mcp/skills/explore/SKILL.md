---
name: explore
description: Use when you need to inspect, navigate, or interact with a running Godot project. Launches Godot automatically, explores the scene tree, takes screenshots, and clicks UI elements via the GodotPlay MCP server.
---

# Explore a Godot Project

Launch a Godot project and interactively explore its UI using GodotPlay's MCP tools.

## Prerequisites

- GodotPlay MCP server registered in Claude Code (`godotplay-mcp`)
- Godot 4.x installed (set `GODOT_PATH` env var or ensure `godot` is on PATH)
- The target Godot project must have the GodotPlay addon installed under `addons/godotplay/`

## Workflow

Follow this sequence every time:

### 1. Launch Godot

```
godot_launch(projectPath: "/path/to/godot/project", headless: false)
```

- Use the **current working directory** as `projectPath` if it contains a `project.godot` file
- Set `headless: true` for CI or when no display is available
- Default port is 50051; change if another instance is running
- Wait for "gRPC ready" confirmation before proceeding

### 2. Take a Screenshot

```
godot_screenshot()
```

Always screenshot first to see what's currently on screen. This gives you visual context before inspecting the tree.

### 3. Inspect the Scene Tree

```
godot_inspect_tree()
```

Returns the full node hierarchy as JSON. Key things to look for:
- **Node paths** like `/root/MainMenu/VBoxContainer/StartButton` — use these for clicking
- **Class names** like `Button`, `Label`, `Control`, `VBoxContainer`
- **Node names** — match the names visible in the Godot editor

### 4. Interact

```
godot_click(nodePath: "/root/MainMenu/VBoxContainer/StartButton")
```

- Use the **exact node path** from the scene tree inspection
- After clicking, always take another screenshot to verify the result
- For buttons: the `pressed` signal fires automatically
- For scene changes: the scene tree will update — inspect again

### 5. Iterate

After each interaction:
1. `godot_screenshot()` — see the new state
2. `godot_inspect_tree()` — if the scene changed, get the new tree
3. Repeat interactions as needed

### 6. Shutdown

```
godot_shutdown()
```

Always shut down when done to free the Godot process.

## MCP Tool Reference

| Tool | Purpose | Key Parameters |
|------|---------|---------------|
| `godot_launch` | Start Godot with GodotPlay plugin | `projectPath` (required), `headless`, `scene`, `port`, `godotPath` |
| `godot_inspect_tree` | Get scene tree as JSON | none |
| `godot_click` | Click a node | `nodePath` (absolute path from tree) |
| `godot_screenshot` | Capture current viewport | none |
| `godot_shutdown` | Stop Godot instance | none |

## Godot Scene Tree Basics

- The root node is always `/root` (a `Window`)
- The current scene is a child of `/root`, e.g., `/root/MainMenu`
- Autoloads (singletons) are also children of `/root`
- Node paths use `/` separator: `/root/MainMenu/VBoxContainer/Button`
- Common UI nodes: `Control`, `Button`, `Label`, `LineEdit`, `VBoxContainer`, `HBoxContainer`, `Panel`

## Common Patterns

### Exploring a menu flow
1. Launch → Screenshot → Inspect tree
2. Find the button node path
3. Click it → Screenshot to see result
4. If scene changed → Inspect new tree → Continue

### Finding a specific control
1. Inspect tree
2. Search the JSON for the class name or node name
3. Use the `path` field as the click target

### Verifying text content
1. Inspect tree
2. Look at Label nodes' properties
3. Or take a screenshot and read the text visually

## Error Handling

| Error | Cause | Fix |
|-------|-------|-----|
| "Failed to connect" | Godot didn't start | Check `godotPath`, verify project has `addons/godotplay/` |
| "Node not found" | Wrong path | Re-inspect tree, use exact path from JSON |
| "Port in use" | Another Godot instance | Use different `port` parameter or shutdown the other instance |
| Screenshot is black | Headless mode | Use `headless: false` or accept that headless has no rendering |

## Important Notes

- All Godot operations run on the main thread (thread-safe)
- The gRPC server uses `Grpc.Core` on port 50051 by default
- Screenshots are returned as PNG images
- Scene tree inspection includes the full hierarchy up to 10 levels deep
- Click works on `Button` (via signal) and `Control` (via input event)
