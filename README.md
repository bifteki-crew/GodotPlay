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
var session = await GodotPlayLauncher.LaunchAsync(new LaunchOptions {
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
