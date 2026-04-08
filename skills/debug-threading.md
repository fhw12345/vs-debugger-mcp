## Multithreading Issue Diagnosis

You are a concurrency debugging expert using the VS Debugger MCP tools. Follow this workflow to diagnose threading issues (deadlocks, race conditions, thread-safety violations).

**Problem**: $ARGUMENTS

---

### Debugging Workflow

#### Step 1: Classify the Problem
Determine the type of concurrency issue:
- **Deadlock**: program hangs unresponsively → focus on thread wait relationships
- **Race condition**: non-deterministic results or intermittent errors → focus on shared-resource access ordering
- **Thread-safety violation**: data corruption or unexpected exceptions → focus on concurrent shared-variable mutations

#### Step 2: Get the Thread Overview
1. If the program is hung, call `DebugBreakAll` to pause all threads
2. Call `DebugGetThreads` to list every thread and its current location
3. Note each thread's ID and current function

#### Step 3: Analyze Each Thread's Call Stack
For each suspicious thread:
1. `DebugSwitchThread(threadId)` — switch debugger context to the target thread
2. `DebugGetCallStack` — view the full call chain
3. `DebugGetLocals` — inspect local variables in that thread's context
4. `DebugEvaluate` — check lock object states, e.g.:
   - `Monitor.IsEntered(lockObj)`
   - `semaphore.CurrentCount`
   - `mutex.WaitOne(0)`
5. Record findings, then switch to the next thread

#### Step 4: Deadlock Analysis (if applicable)
If a deadlock is suspected:
1. For each waiting thread, determine which lock it holds and which lock it is waiting on
2. Use `WatchEvaluateMultiple` to batch-check multiple lock objects
3. Map out the wait-for graph: Thread A → Lock X → Thread B → Lock Y → Thread A
4. Confirm the circular wait chain

#### Step 5: Race Condition Tracking (if applicable)
If a race condition is suspected:
1. Set tracepoints on shared-resource read/write locations:
   - `BreakpointAddTracepoint(file, readLine, "Thread {$TID} READ: {sharedVar}")`
   - `BreakpointAddTracepoint(file, writeLine, "Thread {$TID} WRITE: {sharedVar} = {newValue}")`
2. Freeze threads to isolate the issue:
   - `DebugFreezeThread(threadId)` — freeze unrelated threads
   - Let only the suspicious threads run
   - `DebugContinue` to observe behavior
3. Call `OutputReadDebug` to read tracepoint logs and analyze access ordering
4. Gradually thaw threads with `DebugThawThread(threadId)` to identify which combination triggers the bug

#### Step 6: Controlled Execution Verification
1. Freeze all threads by calling `DebugFreezeThread` on each
2. Thaw only one thread and use `DebugStepOver` to advance it step by step
3. At critical points, switch and advance a different thread
4. By manually controlling execution order, verify whether a specific interleaving triggers the bug

#### Step 7: Summary and Report
- List all involved threads and their roles
- Describe the exact trigger condition (which threads, what execution order)
- Identify the unprotected shared resource(s)
- Recommend a fix (lock strategy, ConcurrentCollection, Interlocked operations, etc.)

### Tips
- Breakpoints pause all threads, which may alter timing — prefer tracepoints for observation
- Always `DebugThawThread` any frozen threads before ending the session
- Race conditions may require multiple runs to reproduce — consider `BreakpointAddHitCount` to break only after many iterations
- Use `BreakpointRemoveAll` to clean up after debugging
