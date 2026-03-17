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
