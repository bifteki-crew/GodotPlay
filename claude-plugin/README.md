# GodotPlay — Claude Code Plugin

Claude Code plugin for [GodotPlay](../README.md), a Playwright-like test automation framework for Godot 4.x.

## Skills (MCP Prompts)

Skills are delivered as MCP prompts via the `godotplay-mcp` server:

- `godot_explore` — Launch and interactively explore a Godot project
- `godot_test` — Write and run tests for a Godot project
- `godot_debug` — Debug a failing Godot scene or script
- `godot_coverage` — Analyze test coverage for a Godot project

## Setup

### 1. Register the MCP server

Add to your Claude Code MCP settings (via `/mcp`):

```json
{
  "godotplay": {
    "command": "npx",
    "args": ["-y", "godotplay-mcp"]
  }
}
```

### 2. Enable the plugin

Register this plugin directory in Claude Code's plugin settings.

### 3. Set Godot path

Set the `GODOT_PATH` environment variable to your Godot executable, or ensure `godot` is on PATH.

## Usage

Skills are available as MCP prompts. Ask Claude to explore, test, debug, or analyze coverage of your Godot project and the relevant prompt will be used automatically.
