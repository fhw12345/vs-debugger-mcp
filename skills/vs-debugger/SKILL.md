---
name: vs-debugger
description: Use when the user wants to debug, build, inspect, or control Visual Studio through Claude Code. Covers all VS debugger MCP tools — build, debug lifecycle, breakpoints, stepping, inspection, exceptions, output, and watch.
---

# Visual Studio Debugger MCP — Usage Guide

You have access to a running Visual Studio instance through the `vs-debugger` MCP server. This skill teaches you how to use these tools effectively.

## Before You Start

1. **Check debug mode first.** Always call `DebugGetMode` before any action to know what's possible:
   - `Design` mode → you can build and start debugging
   - `Break` mode → you can inspect, step, evaluate, and manage breakpoints
   - `Run` mode → you can break all, stop, or wait for a breakpoint hit
2. **Read the source file** before setting breakpoints. You need accurate line numbers.
3. **One step at a time.** After each step (StepOver/StepInto/StepOut), call `DebugGetCurrentLocation` to see where you are and what's around you.

## Common Workflows

### Build and Start Debugging

```
1. BuildSolution or BuildProject (check build errors with GetBuildStatus)
2. BreakpointAdd at the line you want to investigate
3. DebugStart or DebugStartWithBuild
4. Wait for breakpoint to hit — use DebugGetMode to confirm Break mode
```

### Investigate a Bug

```
1. DebugGetMode → confirm Break mode
2. DebugGetCurrentLocation → see where execution stopped
3. DebugGetLocals → see all local variables
4. DebugEvaluate("suspiciousExpression") → test hypotheses
5. DebugInspectVariable("obj") → drill into object members
6. DebugGetCallStack → understand how you got here
7. DebugStepOver / DebugStepInto → trace execution
8. Repeat 2-7 until root cause is found
```

### Inspect an Exception

```
1. ExceptionGetCurrent → get type, message, stack trace, inner exception
2. DebugGetLocals → see state when exception was thrown
3. DebugEvaluate("$exception.InnerException") → dig deeper
4. DebugGetCallStack → see the full call chain
```

### Attach to a Running Process

```
1. DebugListProcesses("myapp") → find the process
2. DebugAttachToProcess(pid) or DebugAttachToProcessByName("myapp")
3. Set breakpoints, then wait or BreakAll
```

### Non-Intrusive Logging with Tracepoints

```
1. BreakpointAddTracepoint("file.cs", 42, "x={x}, y={y}")
   → Logs without stopping execution
   → Supports: {expression}, $FUNCTION, $CALLER, $CALLSTACK, $THREAD, $PID
2. DebugStart
3. OutputReadDebug(100) → read the logged messages
```

## Critical Rules

- **Never guess line numbers.** Read the file with the Read tool first, then set breakpoints at verified lines.
- **Always verify breakpoint placement.** After adding a breakpoint, call `BreakpointList` to confirm it was set correctly. The debugger may adjust the line.
- **Check mode before acting.** Stepping and inspection only work in Break mode. Building and starting only work in Design mode.
- **Clean up breakpoints.** Remove breakpoints you no longer need with `BreakpointRemove` or `BreakpointRemoveAll`.
- **Don't spam steps.** Each step changes state. Check location and locals after each step before deciding the next action.
- **Use Watch for multiple values.** `WatchEvaluateMultiple("a;b;c.Name")` is more efficient than three separate evaluations.

## Tool Quick Reference

| Category | Key Tools | Requires |
|----------|-----------|----------|
| Build | `BuildSolution`, `BuildProject`, `ListProjects`, `GetBuildStatus` | Design mode |
| Lifecycle | `DebugStart`, `DebugStop`, `DebugRestart`, `DebugGetMode`, `DebugBreakAll` | Varies |
| Breakpoints | `BreakpointAdd`, `BreakpointAddConditional`, `BreakpointAddTracepoint`, `BreakpointList` | Any mode |
| Stepping | `DebugStepOver`, `DebugStepInto`, `DebugStepOut`, `DebugContinue` | Break mode |
| Inspection | `DebugGetLocals`, `DebugEvaluate`, `DebugGetCallStack`, `DebugInspectVariable` | Break mode |
| Threads | `DebugGetThreads`, `DebugSwitchThread`, `DebugFreezeThread`, `DebugThawThread` | Break mode |
| Exceptions | `ExceptionGetCurrent`, `ExceptionEnableBreak` | Break mode |
| Output | `OutputReadDebug`, `OutputReadPane`, `OutputImmediateExecute` | Any / Break |
| Watch | `WatchEvaluate`, `WatchEvaluateMultiple`, `WatchAdd` | Break mode |

## File Paths

Breakpoint file paths can be:
- **Absolute:** `C:\Users\me\project\Program.cs`
- **Relative to solution:** `src/Program.cs`

Always prefer the path format that matches what the user provides or what `DebugGetCurrentLocation` returns.
