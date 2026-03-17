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

- `dotnet build GodotPlay.sln` — Build all .NET projects (Plugin project will have errors without Godot SDK)
- `dotnet test src/GodotPlay.Tests --filter "Category!=Integration"` — Unit tests only (16 tests)
- `GODOT_PATH=/path/to/godot dotnet test src/GodotPlay.Tests --filter "Category=Integration"` — Integration tests (5 tests, requires Godot)
- `cd demo && dotnet build` — Build demo Godot project
- `cd src/godotplay-mcp && npm run build` — Build MCP server

## Key decisions

- **Grpc.Core** (not Grpc.AspNetCore) for the Godot plugin — ASP.NET Core runtime is not available in Godot's .NET host
- Main-thread dispatcher in GodotPlayServer — all gRPC handlers dispatch work to Godot's main thread via `RunOnMainThread<T>()`
- Headless input via direct signal emission (workaround for Godot bug #73557)
- MCP server in TypeScript (ecosystem standard), everything else in C#
- Plugin is an AutoLoad node that starts gRPC server on _Ready()
- EditorPlugin (.cs with `#if TOOLS`) should NOT be included in game builds — use autoload directly in project.godot
- `CopyLocalLockFileAssemblies=true` required in Godot .csproj for NuGet dependencies
- GODOT_PATH env var configures Godot executable path for integration tests
