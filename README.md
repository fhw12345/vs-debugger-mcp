# VS Debugger MCP Server

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![MCP Protocol](https://img.shields.io/badge/MCP-compatible-green.svg)](https://modelcontextprotocol.io)

Control Visual Studio's debugger from any AI coding agent — Claude Code, Cursor, GitHub Copilot, Cline, Continue, Windsurf, or any MCP client.

```
AI Agent  →  MCP (SSE or stdio)  →  VsDebuggerMcp.exe  →  Visual Studio DTE2 COM API
```

## Quickstart

```bash
# Option A: Claude Code plugin (auto-starts the server)
claude plugin marketplace add fhw12345/vs-debugger-mcp
claude plugin install vs-debugger-mcp
claude plugin enable vs-debugger-mcp

# Option B: Any MCP client — stdio (agent launches the server)
# Add to your agent's MCP config:
#   command: "C:\path\to\VsDebuggerMcp.exe"
#   args: ["--stdio"]

# Option C: Any MCP client — SSE (connect to a running server)
VsDebuggerMcp.exe                # starts on http://localhost:5050/sse
```

## Requirements

- Windows (COM interop)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or SDK to build from source)
- Visual Studio (must be running for debugger features)

## Features

| Category | Capabilities |
|----------|-------------|
| **Build** | Build/rebuild/clean solution or project, list projects, query build status |
| **Debug Lifecycle** | Open/close solutions, start/stop/restart, attach by PID or name, query debug mode |
| **Breakpoints** | Add/remove/toggle, conditional, tracepoints, hit count |
| **Stepping** | Step over/into/out, continue, run to cursor, set next statement |
| **Inspection** | Locals, evaluate expressions, call stack, threads, variable members |
| **Thread Control** | Freeze/thaw threads, switch debugger context |
| **Exceptions** | Current exception details, first-chance breaking config |
| **Output & Watch** | Read output panes, watch expressions, Immediate window |

## Installation

### Claude Code (Plugin)

```bash
claude plugin marketplace add fhw12345/vs-debugger-mcp
claude plugin install vs-debugger-mcp
claude plugin enable vs-debugger-mcp
```

The server auto-starts on every session. All 52 MCP tools are available immediately.

To update to the latest version:

```bash
claude plugins update vs-debugger-mcp@vs-debugger-marketplace
```

<details>
<summary>Alternative: manual settings.json</summary>

```json
{
  "extraKnownMarketplaces": {
    "vs-debugger-marketplace": {
      "source": { "source": "github", "repo": "fhw12345/vs-debugger-mcp" }
    }
  },
  "enabledPlugins": {
    "vs-debugger-mcp@vs-debugger-marketplace": true
  }
}
```
</details>

<details>
<summary>Alternative: manual MCP registration (no plugin)</summary>

```bash
dotnet run                        # or: dist/net8.0-windows/VsDebuggerMcp.exe
claude mcp add --transport sse vs-debugger http://localhost:5050/sse
```
</details>

### Cursor / Cline / Continue / Windsurf / GitHub Copilot

All these agents support MCP. Pick **stdio** (recommended — agent auto-launches the server) or **SSE** (connect to a running server).

#### stdio (recommended)

The agent launches `VsDebuggerMcp.exe --stdio` as a child process. No manual server management needed.

| Agent | Config file | Config |
|-------|------------|--------|
| **Cursor** | `.cursor/mcp.json` | `{ "mcpServers": { "vs-debugger": { "command": "C:\\path\\to\\VsDebuggerMcp.exe", "args": ["--stdio"] } } }` |
| **GitHub Copilot** | `.vscode/mcp.json` | `{ "servers": { "vs-debugger": { "type": "stdio", "command": "C:\\path\\to\\VsDebuggerMcp.exe", "args": ["--stdio"] } } }` |
| **Cline** | MCP Servers panel | `{ "mcpServers": { "vs-debugger": { "command": "C:\\path\\to\\VsDebuggerMcp.exe", "args": ["--stdio"] } } }` |
| **Continue** | `.continue/mcpServers/mcp.json` | `{ "mcpServers": { "vs-debugger": { "command": "C:\\path\\to\\VsDebuggerMcp.exe", "args": ["--stdio"] } } }` |
| **Windsurf** | `~/.codeium/windsurf/mcp_config.json` | `{ "mcpServers": { "vs-debugger": { "command": "C:\\path\\to\\VsDebuggerMcp.exe", "args": ["--stdio"] } } }` |

> **Note:** GitHub Copilot uses `"servers"` as the top-level key; all others use `"mcpServers"`.

<details>
<summary>Full JSON examples</summary>

**Cursor** (`.cursor/mcp.json` or `~/.cursor/mcp.json`):
```json
{
  "mcpServers": {
    "vs-debugger": {
      "command": "C:\\path\\to\\VsDebuggerMcp.exe",
      "args": ["--stdio"]
    }
  }
}
```

**GitHub Copilot** (`.vscode/mcp.json`):
```json
{
  "servers": {
    "vs-debugger": {
      "type": "stdio",
      "command": "C:\\path\\to\\VsDebuggerMcp.exe",
      "args": ["--stdio"]
    }
  }
}
```

**Cline** (`%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json`):
```json
{
  "mcpServers": {
    "vs-debugger": {
      "command": "C:\\path\\to\\VsDebuggerMcp.exe",
      "args": ["--stdio"],
      "disabled": false,
      "autoApprove": []
    }
  }
}
```

**Continue** (`.continue/mcpServers/mcp.json`):
```json
{
  "mcpServers": {
    "vs-debugger": {
      "command": "C:\\path\\to\\VsDebuggerMcp.exe",
      "args": ["--stdio"]
    }
  }
}
```

**Windsurf** (`~/.codeium/windsurf/mcp_config.json`):
```json
{
  "mcpServers": {
    "vs-debugger": {
      "command": "C:\\path\\to\\VsDebuggerMcp.exe",
      "args": ["--stdio"]
    }
  }
}
```
</details>

#### SSE (manual server)

Start the server first, then point your agent at `http://localhost:5050/sse`:

```bash
VsDebuggerMcp.exe                # or: dotnet run
```

| Agent | Config file | SSE URL field |
|-------|------------|---------------|
| **Cursor** | `.cursor/mcp.json` | `"url": "http://localhost:5050/sse"` |
| **GitHub Copilot** | `.vscode/mcp.json` | `"type": "sse", "url": "http://localhost:5050/sse"` |
| **Cline** | MCP Servers panel | `"type": "sse", "url": "http://localhost:5050/sse"` |
| **Continue** | `.continue/mcpServers/mcp.json` | `"type": "sse", "url": "http://localhost:5050/sse"` |
| **Windsurf** | `~/.codeium/windsurf/mcp_config.json` | `"serverUrl": "http://localhost:5050/sse"` |

> **Windsurf note:** Use `"serverUrl"` instead of `"url"`.

#### Any other MCP client

- **stdio:** Launch `VsDebuggerMcp.exe --stdio` as a subprocess
- **SSE:** Connect to `http://localhost:5050/sse`

Ready-to-copy config templates are in the [`editors/`](editors/) directory.

## Tool Reference

<details>
<summary><strong>Build Tools</strong> (6 tools)</summary>

| Tool | Description |
|------|-------------|
| `BuildSolution` | Build the entire solution |
| `RebuildSolution` | Rebuild the entire solution |
| `BuildProject` | Build a specific project (supports fuzzy name matching) |
| `ListProjects` | List all projects with their unique names |
| `GetBuildStatus` | Get current build state |
| `CleanSolution` | Clean the solution |
</details>

<details>
<summary><strong>Debug Lifecycle Tools</strong> (12 tools)</summary>

| Tool | Description |
|------|-------------|
| `OpenSolution` | Open a .sln or .csproj in Visual Studio (launches VS if not running) |
| `CloseSolution` | Close the current solution, optionally quit VS |
| `DebugStart` | Start debugging (F5) |
| `DebugStartWithBuild` | Build then start debugging the startup project |
| `DebugStartWithoutDebugging` | Start without debugging (Ctrl+F5) |
| `DebugStop` | Stop debugging (Shift+F5) |
| `DebugRestart` | Restart debugging |
| `DebugGetMode` | Get current debug mode (Design/Break/Run) |
| `DebugBreakAll` | Break all execution |
| `DebugListProcesses` | List available processes (optional name filter) |
| `DebugAttachToProcess` | Attach to a process by PID |
| `DebugAttachToProcessByName` | Attach to a process by name |
</details>

<details>
<summary><strong>Breakpoint Tools</strong> (8 tools)</summary>

File paths may be absolute or relative to the open solution.

| Tool | Description |
|------|-------------|
| `BreakpointAdd` | Add a breakpoint at file:line |
| `BreakpointAddConditional` | Add a conditional breakpoint |
| `BreakpointAddTracepoint` | Add a tracepoint (logs without breaking) |
| `BreakpointAddHitCount` | Add a hit count breakpoint (equal/gte/multiple) |
| `BreakpointRemove` | Remove a breakpoint |
| `BreakpointToggle` | Toggle a breakpoint enabled/disabled |
| `BreakpointList` | List all breakpoints |
| `BreakpointRemoveAll` | Remove all breakpoints |
</details>

<details>
<summary><strong>Step Tools</strong> (6 tools)</summary>

Responses include a source preview when Visual Studio exposes file and line info.

| Tool | Description |
|------|-------------|
| `DebugStepOver` | Step over (F10) |
| `DebugStepInto` | Step into (F11) |
| `DebugStepOut` | Step out (Shift+F11) |
| `DebugContinue` | Continue execution (F5) |
| `DebugRunToCursor` | Run to cursor position |
| `DebugSetNextStatement` | Move execution pointer to a specific line |
</details>

<details>
<summary><strong>Inspect Tools</strong> (9 tools)</summary>

| Tool | Description |
|------|-------------|
| `DebugGetLocals` | Get local variables in current stack frame |
| `DebugEvaluate` | Evaluate an expression |
| `DebugGetCallStack` | Get the current call stack |
| `DebugGetCurrentLocation` | Get current file, line, function, and nearby source |
| `DebugGetThreads` | List all threads |
| `DebugInspectVariable` | Expand a variable to see members |
| `DebugFreezeThread` | Freeze a thread |
| `DebugThawThread` | Thaw (unfreeze) a thread |
| `DebugSwitchThread` | Switch debugger to a specific thread |
</details>

<details>
<summary><strong>Exception Tools</strong> (3 tools)</summary>

| Tool | Description |
|------|-------------|
| `ExceptionGetCurrent` | Get current exception details (type, message, stack trace) |
| `ExceptionEnableBreak` | Enable first-chance breaking for a CLR exception type |
| `ExceptionListSettings` | List exception groups |
</details>

<details>
<summary><strong>Output Tools</strong> (4 tools)</summary>

| Tool | Description |
|------|-------------|
| `OutputReadDebug` | Read the Debug output pane (last N lines) |
| `OutputListPanes` | List all output window panes |
| `OutputReadPane` | Read a specific output pane by name |
| `OutputImmediateExecute` | Evaluate in Immediate window context |
</details>

<details>
<summary><strong>Watch Tools</strong> (4 tools)</summary>

| Tool | Description |
|------|-------------|
| `WatchEvaluate` | Evaluate a watch expression with member expansion |
| `WatchEvaluateMultiple` | Evaluate multiple expressions at once (semicolon-separated) |
| `WatchAdd` | Add a watch expression to the Watch window |
| `WatchClearAll` | Clear all watch expressions |
</details>

## Building from Source

```bash
dotnet build                      # build
dotnet run                        # run (SSE mode, port 5050)
dotnet run -- --stdio             # run (stdio mode)
dotnet publish -c Release -o dist/net8.0-windows --no-self-contained  # publish
```

## License

[MIT](LICENSE)
