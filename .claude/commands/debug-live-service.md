## Live Service Debugging

You are a production debugging expert using the VS Debugger MCP tools. Follow this workflow to attach to a running service and debug with minimal disruption.

**Problem**: $ARGUMENTS

---

### Critical Rules

1. **ALWAYS read the source file before setting any breakpoint or tracepoint.** Use the Read tool to view the file content and identify the exact line number. Never guess line numbers.
2. **Set breakpoints/tracepoints on executable statements only**, not on function declarations, blank lines, or comments.
3. **After setting breakpoints, call `BreakpointList`** to verify placement.
4. **After a breakpoint is hit, call `DebugGetCurrentLocation`** to confirm position before inspecting state.
5. **Never leave the service suspended** for more than 30 seconds.
6. **Always clean up**: `BreakpointRemoveAll` then `DebugStop` when done.

### Minimum Disruption Principles
- **Prefer tracepoints** (non-breaking) over regular breakpoints
- **Be careful with DebugEvaluate** — avoid expressions with side effects
- **Always clean up** all breakpoints and detach when done

---

### Debugging Workflow

#### Step 1: Find and Attach to the Target Process
1. `DebugListProcesses` — list all available processes
2. If the list is too long, filter by name:
   - `DebugListProcesses("MyService")`
   - `DebugListProcesses("dotnet")`
   - `DebugListProcesses("w3wp")` (IIS worker process)
3. Confirm the target PID, then `DebugAttachToProcess(pid)` to attach
4. `DebugGetMode` to verify the debugger is connected

#### Step 2: Read Source Code and Identify Target Lines
1. Use the **Read** tool to open the source file(s) you want to instrument
2. Identify the **exact line numbers** of:
   - Request entry points (first executable statement in the handler)
   - Exception catch blocks (the line inside `catch`, not the `catch` keyword)
   - Key business logic lines
3. Write down file paths and line numbers before setting any tracepoints

#### Step 3: Non-Intrusive Observation (Tracepoints)
Do not stop the service. Using the exact lines from Step 2:

1. At the request entry point:
   ```
   BreakpointAddTracepoint(file, line, "[REQ] {$FUNCTION} Thread:{$TID} Args:{paramVariable}")
   ```

2. At exception handlers:
   ```
   BreakpointAddTracepoint(file, catchLine, "[ERR] {$FUNCTION} Exception:{ex.Message}")
   ```

3. At key business logic points:
   ```
   BreakpointAddTracepoint(file, line, "[BIZ] {$FUNCTION} Result:{result}")
   ```

4. **Call `BreakpointList`** to verify all tracepoints are at the correct lines
5. `DebugContinue` to let the service keep running
6. Trigger the problem scenario
7. `OutputReadDebug(100)` to read the collected tracepoint logs

#### Step 4: Exception Capture
If the issue involves exceptions:
1. `ExceptionEnableBreak("System.NullReferenceException")` — or the relevant exception type
2. Wait for the exception to fire and automatically break
3. `DebugGetCurrentLocation` — confirm where we stopped
4. `ExceptionGetCurrent` — get the full exception details
5. `DebugGetCallStack` — view the call chain where the exception occurred
6. `DebugGetLocals` — check local variable state at the exception site
7. **Immediately** `DebugContinue` to resume the service

#### Step 5: Targeted Breakpoint Debugging (Brief Interruption)
If tracepoint data is insufficient, use a short stop:
1. Read the source file to find the exact target line
2. Set a conditional breakpoint to hit only the target scenario:
   ```
   BreakpointAddConditional(file, line, "userId == \"targetUser\"")
   ```
3. **Call `BreakpointList`** to verify
4. When hit, call `DebugGetCurrentLocation` first, then quickly:
   - `DebugGetLocals` — local variables
   - `WatchEvaluateMultiple("var1;var2;var3")` — batch-check key variables
   - `DebugGetCallStack` — call chain
   - `DebugInspectVariable("complexObj")` — expand complex objects
5. **Immediately** `DebugContinue` to resume

#### Step 6: Multithread Inspection
If the service is multithreaded:
1. `DebugBreakAll` — pause all threads
2. `DebugGetThreads` — view all thread states
3. For suspicious threads: `DebugSwitchThread(id)` → `DebugGetCallStack`
4. **Quickly** `DebugContinue` to resume

#### Step 7: Clean Up and Detach
After debugging is complete, always execute:
1. `BreakpointRemoveAll` — remove all breakpoints and tracepoints
2. `DebugStop` — detach from the process (this detaches without terminating the service)

#### Step 8: Summary and Report
- **Observations**: key findings from tracepoint logs
- **Root cause**: analysis combining code logic and runtime state
- **Impact scope**: which requests or users are affected
- **Fix recommendation**: proposed code changes
- **Monitoring**: suggested logging or monitoring to add going forward

### Tips
- Attaching a debugger adds slight overhead — monitor service response times
- Regular breakpoints fully suspend the service process — **never leave it paused**
- `DebugEvaluate` may invoke property getters — ensure expressions have no side effects
- If the service has health checks, long pauses may cause it to be recycled
- In Release-compiled code, some variables may be optimized away and unobservable
- Always `BreakpointRemoveAll` + `DebugStop` when done — leftover breakpoints affect performance
