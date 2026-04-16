# VS Debugger MCP Server

An MCP (Model Context Protocol) server that exposes Visual Studio debugging capabilities over HTTP/SSE, enabling AI assistants like Claude to programmatically control the Visual Studio debugger.

Available as a **Claude Code plugin** — install it once and the server auto-starts with every Claude Code session. Also works with **Cursor**, **GitHub Copilot**, **Cline**, **Continue**, **Windsurf**, and any other MCP-compatible client.

## Features

- **Build Management** — Build/rebuild/clean solutions and projects, list projects, query build status with error details
- **Debug Lifecycle** — Start/stop/restart debugging, attach to processes by PID or name, query debug mode
- **Breakpoints** — Add/remove/toggle breakpoints, conditional breakpoints, tracepoints (log without breaking), hit count breakpoints
- **Stepping** — Step over/into/out, continue, run to cursor, set next statement (move execution pointer)
- **Inspection** — Get locals, evaluate expressions, call stack, threads, inspect variable members
- **Thread Control** — Freeze/thaw threads, switch debugger context between threads
- **Exception Handling** — View current exception details, configure first-chance exception breaking
- **Output & Watch** — Read output/debug window panes, evaluate watch expressions, Immediate window execution

## Requirements

- Windows (COM interop required)
- .NET 8.0 Runtime (or SDK if building from source)
- Visual Studio (must be running for debugger features to work)

## Installation

### Option 1: Claude Code Plugin (Recommended)

Install as a Claude Code plugin for automatic server startup on every session.

```bash
# Step 1 — Add the marketplace
claude plugin marketplace add fhw12345/vs-debugger-mcp

# Step 2 — Install the plugin
claude plugin install vs-debugger-mcp

# Step 3 — Enable the plugin
claude plugin enable vs-debugger-mcp
```

Restart Claude Code. The server will auto-start on port 5050 and the `vs-debugger` MCP tools will be available immediately.

#### Alternative: Manual settings.json Setup

If you prefer editing settings directly, add to `~/.claude/settings.json`:

```json
{
  "extraKnownMarketplaces": {
    "vs-debugger-marketplace": {
      "source": {
        "source": "github",
        "repo": "fhw12345/vs-debugger-mcp"
      }
    }
  },
  "enabledPlugins": {
    "vs-debugger-mcp@vs-debugger-marketplace": true
  }
}
```

#### Local Installation (for development)

```bash
claude plugin marketplace add /path/to/VsDebuggerMcp
claude plugin install vs-debugger-mcp
claude plugin enable vs-debugger-mcp
```

### Option 2: Manual MCP Registration

If you prefer not to use the plugin system:

```bash
# Build and run the server
dotnet build
dotnet run

# In another terminal, register with Claude Code
claude mcp add --transport sse vs-debugger http://localhost:5050/sse
```

### Option 3: Run from Pre-built Binary

```bash
# Run the published binary directly
dist/net8.0-windows/VsDebuggerMcp.exe

# Register with Claude Code
claude mcp add --transport sse vs-debugger http://localhost:5050/sse
```

### Plugin Management

```bash
claude plugin list                    # List installed plugins
claude plugin disable vs-debugger-mcp # Disable without uninstalling
claude plugin uninstall vs-debugger-mcp # Uninstall the plugin
claude plugin marketplace update      # Update all marketplaces
```

## How It Works

```
AI Agent  →  MCP (SSE or stdio)  →  VsDebuggerMcp.exe  →  Visual Studio DTE2 COM API
```

The server supports two transport modes:

- **SSE (default)** — Standalone HTTP server on port 5050. Start the server, then point your agent at `http://localhost:5050/sse`.
- **stdio** — The agent launches the server as a child process and communicates via stdin/stdout. Use the `--stdio` flag.

The server discovers running Visual Studio instances via the Windows Running Object Table (ROT) and controls them through the DTE2 COM automation interface. All 8 tool groups (build, debug lifecycle, breakpoints, stepping, inspection, exceptions, output, watch) are registered as MCP tools.

## Multi-Agent Setup

### Cursor

Copy `editors/cursor/mcp.json` to your project's `.cursor/` directory, or add to `~/.cursor/mcp.json` globally.

**stdio (recommended)** — Cursor launches the server automatically:
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

**SSE** — Connect to a running server:
```json
{
  "mcpServers": {
    "vs-debugger": {
      "url": "http://localhost:5050/sse"
    }
  }
}
```

### GitHub Copilot (VS Code)

Add to `.vscode/mcp.json` in your workspace, or open **MCP: Open User Configuration** for global setup.

> Note: Copilot uses `"servers"` as the top-level key, not `"mcpServers"`.

**stdio (recommended):**
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

**SSE:**
```json
{
  "servers": {
    "vs-debugger": {
      "type": "sse",
      "url": "http://localhost:5050/sse"
    }
  }
}
```

### Cline

Open **Cline → MCP Servers** in the VS Code sidebar and add the server, or edit the settings file directly:
- Windows: `%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json`

**stdio (recommended):**
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

**SSE:**
```json
{
  "mcpServers": {
    "vs-debugger": {
      "type": "sse",
      "url": "http://localhost:5050/sse",
      "disabled": false,
      "autoApprove": []
    }
  }
}
```

### Continue

Place in `.continue/mcpServers/mcp.json` in your workspace.

**stdio (recommended):**
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

**SSE:**
```json
{
  "mcpServers": {
    "vs-debugger": {
      "type": "sse",
      "url": "http://localhost:5050/sse"
    }
  }
}
```

### Windsurf

Add to `~/.codeium/windsurf/mcp_config.json` (global only).

**stdio (recommended):**
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

**SSE:**
```json
{
  "mcpServers": {
    "vs-debugger": {
      "serverUrl": "http://localhost:5050/sse"
    }
  }
}
```

### Any MCP Client

The server uses standard MCP protocol. For any unlisted client:

- **stdio:** Run `VsDebuggerMcp.exe --stdio` as a child process
- **SSE:** Start the server (`dotnet run` or `VsDebuggerMcp.exe`), connect to `http://localhost:5050/sse`

### Quick Reference

| Agent | Config File | Top-Level Key | stdio | SSE |
|-------|------------|---------------|-------|-----|
| Claude Code | Plugin auto-config | — | — | Auto |
| Cursor | `.cursor/mcp.json` | `mcpServers` | Yes | Yes |
| GitHub Copilot | `.vscode/mcp.json` | `servers` | Yes | Yes |
| Cline | Cline MCP settings | `mcpServers` | Yes | Yes |
| Continue | `.continue/mcpServers/*.json` | `mcpServers` | Yes | Yes |
| Windsurf | `~/.codeium/windsurf/mcp_config.json` | `mcpServers` | Yes | Yes |

## Tool Reference

### Build Tools

| Tool | Description |
|------|-------------|
| `BuildSolution` | Build the entire solution |
| `RebuildSolution` | Rebuild the entire solution |
| `BuildProject` | Build a specific project by name (supports fuzzy matching) |
| `ListProjects` | List all projects with their unique names |
| `GetBuildStatus` | Get current build state |
| `CleanSolution` | Clean the solution |

### Debug Lifecycle Tools

| Tool | Description |
|------|-------------|
| `DebugStart` | Start debugging (F5) |
| `DebugStartWithBuild` | Build first, then start debugging the current startup project |
| `DebugStartWithoutDebugging` | Start without debugging (Ctrl+F5) |
| `DebugStop` | Stop debugging (Shift+F5) |
| `DebugRestart` | Restart debugging |
| `DebugGetMode` | Get current debug mode (Design/Break/Run) |
| `DebugBreakAll` | Break all execution |
| `DebugListProcesses` | List available processes (with optional name filter) |
| `DebugAttachToProcess` | Attach to a process by PID |
| `DebugAttachToProcessByName` | Attach to a process by name |

### Breakpoint Tools

Breakpoint file paths may be absolute paths or paths relative to the open solution.

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

### Step Tools

Step and current-location responses include a small source preview when Visual Studio exposes file and line information.

| Tool | Description |
|------|-------------|
| `DebugStepOver` | Step over (F10) |
| `DebugStepInto` | Step into (F11) |
| `DebugStepOut` | Step out (Shift+F11) |
| `DebugContinue` | Continue execution (F5) |
| `DebugRunToCursor` | Run to cursor position |
| `DebugSetNextStatement` | Move execution pointer to a specific line |

### Inspect Tools

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

### Exception Tools

| Tool | Description |
|------|-------------|
| `ExceptionGetCurrent` | Get current exception details (type, message, stack trace) |
| `ExceptionEnableBreak` | Enable first-chance exception breaking for a CLR exception type |
| `ExceptionListSettings` | List exception groups |

### Output Tools

| Tool | Description |
|------|-------------|
| `OutputReadDebug` | Read the Debug output pane (last N lines) |
| `OutputListPanes` | List all output window panes |
| `OutputReadPane` | Read a specific output pane by name |
| `OutputImmediateExecute` | Evaluate an expression in Immediate window context |

### Watch Tools

| Tool | Description |
|------|-------------|
| `WatchEvaluate` | Evaluate a watch expression with member expansion |
| `WatchEvaluateMultiple` | Evaluate multiple expressions at once (semicolon-separated) |
| `WatchAdd` | Add a watch expression to the Watch window |
| `WatchClearAll` | Clear all watch expressions |

## Building from Source

To rebuild the `dist/` binaries after making code changes:

```bash
dotnet publish -c Release -o dist/net8.0-windows --no-self-contained
```

## License

[MIT](LICENSE)
