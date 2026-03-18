# GodotPlay — Claude Code Plugin

Claude Code plugin for [GodotPlay](../README.md), a Playwright-like test automation framework for Godot 4.x.

## Skills

- `/godotplay:explore` — Launch and interactively explore a Godot project via MCP

## Setup

### 1. Register the MCP server

Add to your Claude Code MCP settings (via `/mcp`):

```json
{
  "godotplay": {
    "command": "node",
    "args": ["D:/ai/playgodot/src/godotplay-mcp/dist/index.js"]
  }
}
```

### 2. Enable the plugin

Register this plugin directory in Claude Code's plugin settings.

### 3. Set Godot path

Set the `GODOT_PATH` environment variable to your Godot executable, or ensure `godot` is on PATH.

## Usage

In Claude Code, invoke the explore skill:

```
/godotplay:explore
```

Or ask Claude to explore your Godot project — the skill triggers automatically when relevant.
