# GodotPlay Claude Code Plugin — Design Document

**Date:** 2026-03-18
**Status:** Approved

## Goal

Create a Claude Code plugin that teaches Claude how to effectively use GodotPlay's MCP server for interactive Godot project exploration and testing. The plugin bundles skills (workflow guides) with MCP server configuration so everything works out of the box.

## Plugin Structure

```
D:/ai/playgodot/claude-plugin/
├── .claude-plugin/
│   └── plugin.json              # Plugin metadata
├── package.json                 # NPM metadata
├── skills/
│   └── explore/
│       └── SKILL.md             # Explore skill — interactive Godot inspection
├── hooks/
│   ├── hooks.json               # SessionStart hook → check MCP setup
│   └── setup-mcp.cmd            # Script: verify MCP config & build status
└── README.md
```

## Components

### 1. Plugin Metadata

**`.claude-plugin/plugin.json`:**
```json
{
  "name": "godotplay",
  "description": "Playwright-like test automation for Godot 4.x — AI-powered game UI testing",
  "version": "0.1.0",
  "author": { "name": "GodotPlay" },
  "license": "MIT",
  "keywords": ["godot", "testing", "automation", "mcp", "game-testing"]
}
```

### 2. Explore Skill

**Purpose:** Guide Claude through interactive Godot project exploration using MCP tools.

**Workflow:**
1. Launch Godot with `godot_launch` (auto-start, project path detection)
2. Take screenshot with `godot_screenshot` to see current state
3. Inspect scene tree with `godot_inspect_tree`
4. Interact with `godot_click` to test UI flows
5. Repeat inspect/screenshot cycle after each interaction
6. Shutdown with `godot_shutdown` when done

**Content covers:**
- MCP tool reference with exact parameters and examples
- Godot-specific knowledge: scene tree structure, node paths, class names
- Interaction patterns: screenshot → inspect → click → verify
- Error handling: GODOT_PATH not set, port conflicts, build issues
- Project path detection from current working directory

### 3. SessionStart Hook

**Purpose:** On each session start, verify the MCP server is properly configured.

**Checks:**
1. Is `godotplay-mcp` built? (`dist/index.js` exists)
2. If not → run `npm run build` in the MCP server directory
3. Output a reminder if MCP server is not registered in Claude settings

### 4. MCP Server Registration

The user registers the MCP server once in their Claude Code MCP settings:
```json
{
  "godotplay": {
    "command": "node",
    "args": ["D:/ai/playgodot/src/godotplay-mcp/dist/index.js"]
  }
}
```

The hook verifies this is configured on each session start.

## Future Skills (not in this iteration)

- `/godotplay:test` — Generate tests for a scene
- `/godotplay:debug` — Reproduce and debug UI issues
- `/godotplay:coverage` — Analyze untested UI paths
