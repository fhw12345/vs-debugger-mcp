# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VsDebuggerMcp is an MCP (Model Context Protocol) server that exposes Visual Studio debugging capabilities over HTTP/SSE. It connects to a running Visual Studio instance via COM interop (DTE2) and provides programmatic control of building, debugging, breakpoints, stepping, and inspection. Windows-only (`net8.0-windows`).

## Build and Run

```bash
dotnet build
dotnet run
```

The server starts on `http://localhost:5050` with SSE transport at `/sse`.

Register with Claude: `claude mcp add --transport sse vs-debugger http://localhost:5050/sse`

No tests exist in this repository.

## Architecture

```
HTTP/SSE Client → MCP Server (Program.cs :5050) → Tool Classes → DteConnector → Visual Studio DTE2 COM API
```

**Program.cs** — Entry point. Configures ASP.NET Core + MCP server, registers all 8 tool groups.

**DteConnector.cs** — Singleton-style COM bridge. Uses Windows Running Object Table (ROT) to discover running Visual Studio instances. Caches the DTE2 object and validates it before reuse. All tool classes depend on this. Also provides shared `EnsureBreakMode()` helper.

**Tools/** — Eight tool classes, each decorated with `[McpServerToolType]`, with individual methods marked `[McpServerTool]`:

- **BuildTools.cs** — Build/rebuild/clean solution or specific projects, list projects, query build status. `BuildProject` supports fuzzy name matching.
- **DebugLifecycleTools.cs** — Start/stop/restart debugging, break all, query debug mode, list processes, attach to process by PID or name
- **BreakpointTools.cs** — Add/remove/toggle/list breakpoints (conditional, tracepoint, hit count)
- **StepTools.cs** — Step over/into/out, continue, run to cursor, set next statement
- **InspectTools.cs** — Get locals, evaluate expressions, call stack, threads, inspect variable members, freeze/thaw/switch threads
- **ExceptionTools.cs** — Get current exception details ($exception), configure first-chance exception breaking, list exception groups
- **OutputTools.cs** — Read debug/build output window panes, list panes, execute in Immediate window context
- **WatchTools.cs** — Evaluate single/multiple watch expressions with member expansion, add/clear watch window items

## Key Design Patterns

- All tool methods are **static** — state lives in Visual Studio, not in the server
- DTE2 connection is **lazy** with cached singleton and validation on each access
- COM interop uses P/Invoke (`GetRunningObjectTable`, `CreateBindCtx`) to enumerate running VS instances
- MCP tool registration is attribute-driven (`[McpServerToolType]`, `[McpServerTool]`, `[Description]`)
- Some DTE APIs (ExceptionGroups, HitCountType) lack proper interop type definitions — use `dynamic` to access COM properties at runtime

## Dependencies

- `ModelContextProtocol.AspNetCore` (0.8.0-preview.1) — MCP server framework
- `envdte` / `envdte80` (17.13.40008) — Visual Studio DTE COM interop
