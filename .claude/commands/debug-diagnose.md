## Systematic Bug Diagnosis

You are a debugging expert using the VS Debugger MCP tools. Follow this structured workflow to locate the root cause of the bug described below.

**Problem**: $ARGUMENTS

---

### Debugging Workflow

Execute each step in order. Report findings before moving to the next step.

#### Step 1: Verify Environment and Reproduction Path
1. Call `DebugGetMode` to confirm current debugger state
2. Call `ListProjects` to understand the solution structure
3. Call `BuildSolution` to ensure code compiles — review any build errors
4. Based on the problem description, identify likely entry-point files and line numbers

#### Step 2: Set Up Breakpoint Array
Place breakpoints along the critical path:
1. Use `BreakpointAdd` at the suspected failing line(s)
2. If trigger conditions are known, use `BreakpointAddConditional` (e.g. `item == null`, `count > 100`)
3. At key function entries use `BreakpointAddTracepoint` to log execution flow without stopping — use `$FUNCTION` and `{variableName}` tokens in the message
4. Call `BreakpointList` to confirm the breakpoint setup

#### Step 3: Start Debugging and Trigger the Issue
1. Call `DebugStart` to launch the debugger
2. Wait for a breakpoint hit
3. Call `DebugGetCurrentLocation` to confirm the stop position

#### Step 4: Deep State Inspection
At each breakpoint, run through these checks:
1. `DebugGetLocals` — view all local variable values
2. `DebugGetCallStack` — understand how execution reached this point
3. `DebugEvaluate` — evaluate suspicious expressions (e.g. `obj.Property`, `list.Count`, `str?.Length`)
4. `DebugInspectVariable` — expand complex objects to inspect members
5. `WatchEvaluateMultiple` — batch-monitor multiple critical variables (semicolon-separated)

#### Step 5: Check Exception Information
1. If stopped at an exception, call `ExceptionGetCurrent` for full exception details
2. Examine the Message, StackTrace, and InnerException
3. If needed, call `ExceptionEnableBreak` to enable first-chance exception breaking for the relevant type

#### Step 6: Trace Execution Flow
1. Use `DebugStepOver` to execute line-by-line, observing variable changes
2. At suspicious function calls, use `DebugStepInto` to go deeper
3. Once confirmed, use `DebugStepOut` to return to the caller
4. Use `DebugSetNextStatement` to skip or re-execute code sections
5. Call `OutputReadDebug` to review tracepoint log output

#### Step 7: Summary and Report
Compile your findings:
- **Root cause**: the exact line and condition that causes the bug
- **Data flow**: how bad data propagates to the failure point
- **Fix recommendation**: a concrete code change to resolve the issue

### Tips
- Always confirm Break mode before inspecting variables
- If breakpoints are not hit, verify conditions and code path coverage
- For intermittent bugs, prefer tracepoints over stopping breakpoints to collect data across multiple executions
- Call `BreakpointRemoveAll` to clean up after debugging
