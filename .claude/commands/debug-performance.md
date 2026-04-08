## Performance Issue Investigation

You are a performance tuning expert using the VS Debugger MCP tools. Follow this workflow to identify and analyze performance bottlenecks.

**Problem**: $ARGUMENTS

---

### Debugging Workflow

#### Step 1: Characterize the Performance Issue
Confirm the type of problem:
- **Slow response**: a specific operation takes too long → locate the hot function
- **High CPU**: sustained CPU usage → find tight loops or compute-intensive code
- **Memory growth**: memory keeps increasing → find object leaks
- **Excessive GC**: frequent garbage collections → find unnecessary object allocations

#### Step 2: Set Up Performance Observation Points
Place tracepoints along the suspected critical path for timing:

1. At function entry — record start time:
   ```
   BreakpointAddTracepoint(file, entryLine, "[PERF] ENTER {$FUNCTION} Thread:{$TID} Tick:{System.Environment.TickCount}")
   ```

2. At function exit — record end time:
   ```
   BreakpointAddTracepoint(file, exitLine, "[PERF] EXIT {$FUNCTION} Thread:{$TID} Tick:{System.Environment.TickCount}")
   ```

3. Inside loop bodies — use hit-count breakpoints to measure iteration count:
   ```
   BreakpointAddHitCount(file, loopBodyLine, 1000, "multiple")
   ```
   This breaks every 1000 iterations to check progress without stopping on every pass.

#### Step 3: Run and Collect Data
1. `DebugStart` to launch the application
2. Trigger the slow operation
3. Wait for a hit-count breakpoint or manually call `DebugBreakAll`
4. `OutputReadDebug(100)` to read the tracepoint timing log

#### Step 4: Analyze Hot Spots
At the breakpoint:
1. `DebugGetCallStack` — verify execution is on the expected code path
2. `DebugEvaluate` — check key metrics:
   - Collection sizes: `list.Count`, `dict.Count`
   - Loop counters: `i`, `index`
   - String lengths: `str.Length`
   - Result set sizes
3. `WatchEvaluateMultiple` — batch-check multiple performance indicators
4. `DebugGetLocals` — look for unexpectedly large objects

#### Step 5: Loop and Algorithm Analysis
If a loop is the suspected bottleneck:
1. Use `BreakpointAddConditional` to observe specific iterations:
   - `i == 0` (first iteration — check initial state)
   - `i == count - 1` (last iteration — check final state)
2. Use `DebugStepOver` to walk through the loop body, evaluating expressions at each step
3. Assess algorithm complexity: observe the relationship between data size and iteration count

#### Step 6: Memory Analysis (if applicable)
1. Set tracepoints at object creation sites:
   ```
   BreakpointAddTracepoint(file, newObjLine, "[MEM] new {$FUNCTION} Type:{objectType}")
   ```
2. Use hit-count breakpoints to measure allocation frequency
3. `DebugEvaluate("GC.GetTotalMemory(false)")` — check current memory usage
4. `DebugEvaluate("GC.CollectionCount(0)")` — check Gen0 GC count

#### Step 7: Comparative Analysis
If you have fast/slow scenarios to compare:
1. Run each scenario, collecting tracepoint logs
2. `OutputReadDebug` to read and compare timing logs from both runs
3. Identify the function with the largest time difference

#### Step 8: Summary and Report
- **Bottleneck location**: specific function and code line
- **Performance data**: iteration counts, elapsed time, memory usage
- **Root cause**: why it's slow (O(n²) algorithm, redundant computation, excessive allocations, etc.)
- **Optimization recommendations**: caching, algorithm improvement, object pooling, async I/O, etc.

### Tips
- Tracepoints have slight overhead — too many can skew timing measurements
- In loops, prefer hit-count breakpoints over per-iteration stops
- `DebugEvaluate` may trigger property getters or method calls — avoid side-effecting expressions
- Performance problems in Debug builds can be much worse than Release — keep this in mind
- Call `BreakpointRemoveAll` to clean up after debugging
