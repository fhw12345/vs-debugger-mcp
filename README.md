# VS Debugger MCP Server

An MCP (Model Context Protocol) server that exposes Visual Studio debugging capabilities over HTTP/SSE, enabling AI assistants like Claude to programmatically control the Visual Studio debugger.

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
- .NET 8.0 SDK
- Visual Studio (must be running before starting the server)

## Getting Started

### Build and Run

```bash
dotnet build
dotnet run
```

The server starts at `http://localhost:5050` with SSE transport at `/sse`.

### Register with Claude Code

```bash
claude mcp add --transport sse vs-debugger http://localhost:5050/sse
```

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
| `DebugStartWithoutDebugging` | Start without debugging (Ctrl+F5) |
| `DebugStop` | Stop debugging (Shift+F5) |
| `DebugRestart` | Restart debugging |
| `DebugGetMode` | Get current debug mode (Design/Break/Run) |
| `DebugBreakAll` | Break all execution |
| `DebugListProcesses` | List available processes (with optional name filter) |
| `DebugAttachToProcess` | Attach to a process by PID |
| `DebugAttachToProcessByName` | Attach to a process by name |

### Breakpoint Tools

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
| `DebugGetCurrentLocation` | Get current file, line, function |
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

## License

[MIT](LICENSE)
