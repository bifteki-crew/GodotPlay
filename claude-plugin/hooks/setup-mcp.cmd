@echo off
setlocal

REM GodotPlay MCP Server Setup Check
REM Runs on every Claude Code session start

set "MCP_DIR=%~dp0..\..\src\godotplay-mcp"
set "MCP_DIST=%MCP_DIR%\dist\index.js"

REM Check if MCP server is built
if not exist "%MCP_DIST%" (
    echo [GodotPlay] MCP server not built. Building...
    cd /d "%MCP_DIR%"
    call npm run build >nul 2>&1
    if exist "%MCP_DIST%" (
        echo [GodotPlay] MCP server built successfully.
    ) else (
        echo [GodotPlay] WARNING: Failed to build MCP server. Run: cd src/godotplay-mcp ^&^& npm run build
    )
) else (
    echo [GodotPlay] MCP server ready.
)

endlocal
