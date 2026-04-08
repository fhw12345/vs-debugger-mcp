## Multithreading Issue Diagnosis

You are a concurrency debugging expert using the VS Debugger MCP tools. Follow this workflow to diagnose threading issues (deadlocks, race conditions, thread-safety violations).

**Problem**: $ARGUMENTS

---

### Critical Rules

1. **ALWAYS read the source file before setting any breakpoint.** Use the Read tool to view the file content and identify the exact line number. Never guess line numbers.
2. **Never set breakpoints on function/lambda declaration lines.** Target executable statements inside the body.
3. **For thread debugging, set breakpoints AFTER thread start calls**, not before. If you set a breakpoint before `Thread.Start()`, threads won't be running when you hit it.
4. **After setting breakpoints, call `BreakpointList`** to verify placement.
5. **Always thaw all frozen threads** before ending the session or continuing normally.

---

### Debugging Workflow

#### Step 1: Classify the Problem
Determine the type of concurrency issue:
- **Deadlock**: program hangs unresponsively → focus on thread wait relationships
- **Race condition**: non-deterministic results or intermittent errors → focus on shared-resource access ordering
- **Thread-safety violation**: data corruption or unexpected exceptions → focus on concurrent shared-variable mutations

#### Step 2: Read Source Code and Identify Threading Code
1. Use the **Read** tool to open the source file(s) containing threading code
2. Identify the exact line numbers of:
   - Thread creation and start calls (`new Thread(...)`, `.Start()`)
   - Shared variable access points (read and write)
   - Lock acquisitions and releases
   - The join/wait points
3. Note which lines are inside thread lambdas/delegates vs. the main thread

#### Step 3: Set Up Observation Points
1. For race conditions — set tracepoints (not breakpoints) on shared-resource access:
   - Read the file first to find the exact line of the shared variable access
   - `BreakpointAddTracepoint(file, line, "Thread {$TID} access: {sharedVar}")`
2. For deadlocks — use `DebugStart` then trigger the hang, then `DebugBreakAll`
3. For general threading — set a breakpoint AFTER the point where all threads have started (e.g. on the join line or a line after all `.Start()` calls)
4. **Call `BreakpointList`** to verify all breakpoints/tracepoints are correct

#### Step 4: Get the Thread Overview
1. Once in Break mode (either from breakpoint or `DebugBreakAll`):
2. Call `DebugGetThreads` to list every thread and its current location
3. Note each thread's ID and current function

#### Step 5: Analyze Each Thread's Call Stack
For each suspicious thread:
1. `DebugSwitchThread(threadId)` — switch debugger context to the target thread
2. `DebugGetCallStack` — view the full call chain
3. `DebugGetLocals` — inspect local variables in that thread's context
4. `DebugEvaluate` — check lock object states, e.g.:
   - `Monitor.IsEntered(lockObj)`
   - `semaphore.CurrentCount`
5. Record findings, then switch to the next thread

#### Step 6: Deadlock Analysis (if applicable)
1. For each waiting thread, determine which lock it holds and which lock it is waiting on
2. Use `WatchEvaluateMultiple` to batch-check multiple lock objects
3. Map out the wait-for graph: Thread A → Lock X → Thread B → Lock Y → Thread A
4. Confirm the circular wait chain

#### Step 7: Race Condition Isolation (if applicable)
1. Freeze non-essential threads with `DebugFreezeThread(threadId)`
2. Let only the suspicious threads run
3. `DebugContinue` to observe behavior
4. Call `OutputReadDebug` to read tracepoint logs and analyze access ordering
5. Gradually thaw threads with `DebugThawThread(threadId)` to identify which combination triggers the bug

#### Step 8: Controlled Execution Verification
1. Freeze all threads by calling `DebugFreezeThread` on each
2. Thaw only one thread and use `DebugStepOver` to advance it step by step
3. At critical points, switch and advance a different thread
4. By manually controlling execution order, verify whether a specific interleaving triggers the bug

#### Step 9: Summary and Report
- List all involved threads and their roles
- Describe the exact trigger condition (which threads, what execution order)
- Identify the unprotected shared resource(s)
- Recommend a fix (lock strategy, ConcurrentCollection, Interlocked operations, etc.)

### Tips
- Breakpoints pause all threads, which may alter timing — prefer tracepoints for observation
- Always `DebugThawThread` any frozen threads before ending the session
- Race conditions may require multiple runs to reproduce — consider `BreakpointAddHitCount` to break only after many iterations
- Use `BreakpointRemoveAll` to clean up after debugging
