# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repo-Specific Execution Notes

This repository inherits the root agent-team workflow from `D:\repo\CLAUDE.md`.
This repository explicitly opts into the root file's optional autonomous in-session workflow.

Repo-specific overrides:
- This is a Windows-only `.NET 8` project targeting `net8.0-windows`.
- Work should be tailored to a Visual Studio DTE2 / COM integration context.
- For non-trivial tasks, this repo uses an autonomous in-session improvement loop once work begins, unless an escalation condition is hit.
- The main agent should still coordinate through an agent team, but repo-specific agents should reason carefully about COM state, running Visual Studio instances, and debugger mode transitions.
- The dedicated C# project under `test/` is the primary feature-validation project for this repository.
- Before any meaningful debugger-feature test, rely on the repo-local SessionStart hook to attempt server startup, then verify SSE reachability and MCP discoverability state before continuing.
- Validation should use the `test/` C# project as the debug target and exercise debugger features through that project.
- When testing or reviewing changes, distinguish between code correctness, build success, MCP server availability, and actual debugger behavior inside a running Visual Studio instance.
- Current autonomy is session-driven by instructions, agent coordination, and repo-local automation hooks in `.claude/settings.json`; it is not durable background automation across session restarts.
- Autonomous execution should continue until the product agent accepts the current PRD wave and the latest validation cycle score is greater than 95/100 under the repo scoring rubric, except when the harness is in plan mode, a risky or destructive action would be required, an external prerequisite is missing, or an issue exceeds the retry budget defined below.
- Spawned agents in this repository must report progress proactively to the main agent at minimum on initial assessment, first material finding or runnable slice, midpoint, blocker, and completion. Long silent execution is not acceptable.
- The repo-local `CLAUDE.md` may be revised during autonomous work when doing so is necessary to remove contradictions, encode validated workflow rules, add missing readiness checks, or keep the autonomous loop accurate for this repository.
- Repo-local self-improvement is limited to workflow rules, validation flow, retry logic, fixture support, and repo-local automation behavior for this repository. It must not autonomously expand product scope, perform unrelated refactors, introduce speculative architecture changes, or make permanent external environment changes without explicit approval.
- After every completed test or validation cycle, the team must shut down the active test agent and stop `vs-debugger-mcp` before starting the next iteration.

Repo-specific prompt tailoring examples:

### Explore Agent Override
```text
Focus on `Program.cs`, `DteConnector.cs`, and `Tools/*.cs`.
Trace how MCP requests flow into DTE2 calls, and identify any COM-state assumptions, debugger-mode preconditions, or inconsistent tool outputs.
Report progress to the main agent after the initial code-path map, after the first material constraint or risk is confirmed, and after the final issue list.
```

### Plan Agent Override
```text
Prefer the smallest possible change that preserves the current static tool pattern and attribute-based MCP registration model.
Call out any dependence on Visual Studio state, break mode requirements, ROT lookup behavior, or dynamic COM access.
Report progress to the main agent when the draft PRD is ready, when the frozen PRD is stable, and on any scope or risk change.
```

### Implement Agent Override
```text
Preserve the existing structure centered on `Program.cs`, `DteConnector.cs`, and `Tools/*.cs`.
Avoid speculative abstractions.
Be explicit around COM failure behavior and debugger state assumptions.
Report progress to the main agent after code changes begin, after the first runnable slice, after the main edit set is complete, and on any blocker.
```

### Test Project Update Agent Override
```text
When a new debugger feature needs new validation coverage, update the dedicated C# project under `test/` so it exposes the right debugging scenario.
Add or adjust test code to make the feature observable through Visual Studio debugging and MCP.
Coordinate with the feature coding agent so the new test scenario matches the intended behavior.
If test or review findings show that the current fixture does not support adequate validation, treat that as a direct loop-back trigger to the test-project-update agent.
Report progress to the main agent when the needed scenario is identified, after the first fixture change lands, and when the `test/` project update is complete.
```

### Test Agent Override
```text
Use `dotnet build` as the baseline validation step.
Before debugger-feature testing, verify the repo-local SessionStart hook result first; if the server is not ready, start `vs-debugger-mcp` manually, then re-check SSE reachability and MCP discoverability.
Use the dedicated C# project under `test/` as the main debug target for feature validation.
Validate debugger behavior by debugging the `test/` project through MCP and exercising the affected feature there.
If a change depends on a running Visual Studio instance or active debug session, say so explicitly in the results.
Report progress to the main agent when validation starts, when the MCP server is confirmed available, on the first blocking or first clean scenario result, and when final results are available.
```
### Review Agent Override
```text
Review for COM safety, DTE2 state correctness, MCP result clarity, and regressions in debugger workflows.
Pay special attention to whether the change behaves correctly when no Visual Studio instance is available, when the debugger is not in break mode, when the MCP server is not running, or when COM calls fail.
Check whether the test flow correctly uses the dedicated `test/` C# project as the debugger validation target.
Report progress to the main agent at review start, on the first blocking finding if any, and at final verdict.
```

## Autonomous Workflow for This Repository

For `vs-debugger-mcp`, once a non-trivial task starts, the team should keep moving through the full development loop without waiting for more user input unless an escalation condition is hit.

Default autonomous loop for this repo:
1. Intake and requirement generation
2. Discovery and PRD drafting
3. PRD freeze for the current implementation wave
4. Parallel coding and test-project updates when needed
5. Environment bootstrap and readiness checks
6. Validation through MCP against `test/TestDebugApp`
7. Review and issue routing back to the responsible coding or fixture agent
8. Test-cycle shutdown: stop `vs-debugger-mcp` and shut down the active test agent
9. Integration of approved change streams
10. Product-agent acceptance and score check
11. Repeat for remaining in-scope backlog items or stop

PRD freeze ownership and execution control:
- The main orchestrator owns backlog priority, PRD freeze, and PRD unfreeze decisions for the current implementation wave.
- The product agent is the acceptance authority for whether the current PRD wave is satisfied.
- After discovery and PRD drafting, the team should freeze the wave scope before implementation or fixture changes begin.
- If the harness is in plan mode because Claude Code requires plan approval before execution, pause after the draft PRD and frozen-wave proposal so the user can approve the plan before implementation starts.
- After freeze, agents should stay within the frozen scope unless the main orchestrator explicitly unfreezes it because of a blocking finding, accepted requirement correction, or user instruction.
- A new implementation or fix iteration may begin only after the previous cycle has recorded the confirmed outcome level, calculated the cycle score, assigned a primary failure class when applicable, chosen the next action (`continue`, `retry`, `stop`, or `escalate`), and completed the required shutdown of both the active test agent and `vs-debugger-mcp`.
- PRD unfreeze is allowed only for requirement correction, blocking fixture gaps, or external prerequisite mismatches that prevent valid end-to-end evaluation.

Retry limits:
- Before retrying, assign one primary failure class for the cycle: `implementation defect`, `fixture gap`, `bootstrap/readiness failure`, `MCP discovery/server availability failure`, `Visual Studio state/precondition failure`, `review-only issue`, or `external prerequisite`.
- Allow at most 2 autonomous fix attempts for the same blocking issue in the current wave.
- If the second attempt still fails, or if the same class of failure reappears after handoff, escalate instead of continuing to loop.
- A retry counts when code, fixture, or validation changes are made in response to that blocking issue and re-validated.
- `implementation defect`, `fixture gap`, and `review-only issue` may retry autonomously within the retry budget.
- `bootstrap/readiness failure`, `MCP discovery/server availability failure`, and `Visual Studio state/precondition failure` require a fresh bootstrap pass before any retry.
- `external prerequisite` should escalate immediately unless the missing prerequisite can be satisfied safely inside the current session.
- A new iteration may begin only after the previous test or validation cycle has shut down both the active test agent and `vs-debugger-mcp`.

## Validation Cycle Record Artifact (Required)

Each completed validation cycle must produce one lightweight record artifact before a new iteration starts.

Artifact rules:
- Default path: `D:\repo\vs-debugger-mcp\.claude\artifacts\validation-cycles\<cycle_id>.md`.
- One file per cycle; do not overwrite prior cycle records.
- Required before deciding `continue`, `retry`, `stop`, or `escalate`.
- Required before starting the next implementation or validation iteration.

Required schema/template:

```markdown
# Validation Cycle Record

- cycle_id: <YYYYMMDD-HHMMSS>-<short-scope>
- timestamp: <ISO-8601 local timestamp>
- scenario_target:
  - feature_scope: <what was validated>
  - debug_target: D:\repo\vs-debugger-mcp\test\TestDebugApp\TestDebugApp.csproj
  - scenario_steps: <brief scenario exercised>
- readiness:
  - build_baseline: <green|red|skipped>
  - sessionstart_hook: <green|red|skipped>
  - sse_reachable: <green|red>
  - mcp_discoverable: <green|red>
  - visual_studio_ready: <green|red>
  - debug_target_ready: <green|red>
  - scenario_executed: <green|red>
  - highest_confirmed_level: <build-only|server-ready|MCP-ready|end-to-end debugger verified>
- result:
  - pass: <true|false>
  - primary_failure_class: <none|implementation defect|fixture gap|bootstrap/readiness failure|MCP discovery/server availability failure|Visual Studio state/precondition failure|review-only issue|external prerequisite>
  - retry_counter: <0|1|2>
  - next_action: <continue|retry|stop|escalate>
- score:
  - mcp_operability: <0-20>
  - debugger_behavior: <0-45>
  - tool_quality: <0-20>
  - workflow_rigor: <0-15>
  - total: <0-100>
  - threshold: >95 required for autonomous stop
- shutdown_confirmation:
  - test_agent_shutdown: <true|false>
  - vs_debugger_mcp_stopped: <true|false>
```

Short example record:

```markdown
# Validation Cycle Record

- cycle_id: 20260411-142530-debug-lifecycle-restart
- timestamp: 2026-04-11T14:25:30+08:00
- scenario_target:
  - feature_scope: restart debugging lifecycle flow
  - debug_target: D:\repo\vs-debugger-mcp\test\TestDebugApp\TestDebugApp.csproj
  - scenario_steps: build -> attach -> restart -> verify debug mode transitions
- readiness:
  - build_baseline: green
  - sessionstart_hook: green
  - sse_reachable: green
  - mcp_discoverable: green
  - visual_studio_ready: green
  - debug_target_ready: green
  - scenario_executed: red
  - highest_confirmed_level: MCP-ready
- result:
  - pass: false
  - primary_failure_class: Visual Studio state/precondition failure
  - retry_counter: 1
  - next_action: retry
- score:
  - mcp_operability: 20
  - debugger_behavior: 0
  - tool_quality: 10
  - workflow_rigor: 15
  - total: 45
  - threshold: >95 required for autonomous stop
- shutdown_confirmation:
  - test_agent_shutdown: true
  - vs_debugger_mcp_stopped: true
```

## Project Score Rubric (Required for Autonomous Stop)

The latest validation cycle must include a debugger-MCP quality score from 0 to 100 using these buckets:
- `mcp_operability` (0-20): `sse_reachable` green = 5, `mcp_discoverable` green = 5, `visual_studio_ready` green = 5, `debug_target_ready` green = 5.
- `debugger_behavior` (0-45): `scenario_executed` green against `test/TestDebugApp` = 15, the affected debugger workflow works end-to-end through MCP against the target scenario = 15, and the break-mode-sensitive capabilities relevant to the current wave (for example stepping, breakpoints, inspect/watch, exceptions, output, or debug lifecycle) behave correctly and consistently = 15.
- `tool_quality` (0-20): no blocking findings from product, test, or review agents = 10, and no unresolved regression, COM-state correctness, tool-output clarity, or user-facing debugger workflow issue in the final review = 10.
- `workflow_rigor` (0-15): validation cycle record complete = 5, shutdown confirmation complete = 5, and proactive progress reporting obligations met by all spawned agents = 5.

Scoring rules:
- Autonomous work does not stop successfully until the total score is greater than 95/100.
- Build success, SessionStart success, and fixture-only checks are prerequisites for validation but do not contribute score by themselves.
- If any code file changed and the highest confirmed level is below `end-to-end debugger verified`, cap the total score at 70.
- If MCP is not discoverable, Visual Studio is not ready, or the affected debugger workflow cannot be exercised end-to-end because of an external prerequisite, cap the total score at 60 and escalate instead of looping indefinitely.
- Any blocking finding from product, test, or review caps `tool_quality` at 5 and prevents completion.
- A score of 95 or below requires `continue` or `retry` unless an escalation condition applies.

Stop conditions:
- The product agent explicitly accepts the current frozen PRD wave.
- The latest validation cycle score is greater than 95/100 for successful completion, or any unmet threshold has already been escalated as an external prerequisite.
- No blocking implementation, test, review, or fixture issues remain for that wave.
- Required validation for the wave has completed to the repo completion gate level, or any remaining unmet check has already been escalated as an external prerequisite.
- No higher-priority in-scope item remains open inside the current frozen wave.

Escalation conditions:
- Requirements are ambiguous, contradictory, or would materially change the frozen PRD wave.
- The harness is in plan mode and implementation approval is still required.
- A risky action would be required, including destructive file or git operations, irreversible environment changes, secret or credential handling, or actions that could disrupt the user's active Visual Studio/debugging state without approval.
- A required external prerequisite is missing, such as a running Visual Studio instance, MCP discovery, a usable debug target, or a required server/process that the agents cannot safely provide.
- The same blocking issue has reached the retry limit, or repeated failures suggest the current approach is no longer justified.
- The next step would require unauthorized external actions or permissions the session does not have.

## Environment Bootstrap and Readiness

Before repo-specific validation work, run bootstrap in this order and fail fast on the first red stage:
1. Run the optional repository build baseline check.
2. Allow the repo-local SessionStart hook to perform server process check/start for `vs-debugger-mcp`.
3. Confirm the HTTP/SSE endpoint is reachable at `http://localhost:5050/sse`.
4. Run a best-effort MCP discoverability check for `vs-debugger`; if not discovered, use fallback guidance: `claude mcp add --transport sse vs-debugger http://localhost:5050/sse` only with explicit user approval because it may modify persistent/global CLI MCP configuration (manual, not automatic).
5. Ensure a running Visual Studio instance is available.
6. Ensure `D:\repo\vs-debugger-mcp\test\TestDebugApp\TestDebugApp.csproj` is the debug target.
7. Run debugger validation against the target scenario.

Bootstrap rules:
- Do not continue to a later stage if an earlier stage is red.
- Treat bootstrap and build checks as gates for attempting debugger validation, not as score contributors.
- Record the highest confirmed readiness level reached in the current cycle.
- Use these outcome levels consistently: `build-only`, `server-ready`, `MCP-ready`, `end-to-end debugger verified`.
- If bootstrap fails after the server was started, stop `vs-debugger-mcp` before retrying or escalating.
- If a retry is needed for readiness or discovery reasons, rerun bootstrap from the earliest failed stage rather than skipping ahead.

## Fixture-Driven Validation Policy

`test/TestDebugApp` is a debugger fixture project, not a formal `dotnet test` suite.
Use it to expose and exercise real debugger scenarios through MCP and Visual Studio.
If a feature cannot be meaningfully observed with the current fixture scenarios, update the fixture before final validation.

## Repo Completion Gate

A change or iteration is not complete until all of the following are true:
- Build status is acceptable for the scope of the change.
- Build status and fixture readiness are entry gates for validation, not score sources.
- The highest confirmed outcome level for the cycle has been recorded as one of: `build-only`, `server-ready`, `MCP-ready`, `end-to-end debugger verified`.
- `vs-debugger-mcp` is running and discoverable through MCP when end-to-end validation is in progress.
- The `test/TestDebugApp` fixture is ready for the target scenario.
- If any code file changed in the iteration, end-to-end debugger behavior validation in Visual Studio through MCP is mandatory before scoring or completion.
- The relevant debugger behavior works end-to-end in Visual Studio through MCP for successful end-to-end completion.
- Product, test, and review agents no longer report blocking findings for the current in-scope wave.
- The latest validation cycle score is greater than 95/100 for successful autonomous completion.
- Proactive progress reporting obligations for spawned agents were met for the cycle and reflected in the workflow rigor score.
- If the cycle did not succeed end-to-end, the primary failure class and next action (`continue`, `retry`, `stop`, or `escalate`) have been recorded.
- If the score is 95 or below, the iteration remains incomplete unless the outcome is an accepted escalation.
- The active test agent has been shut down and `vs-debugger-mcp` has been stopped before any next iteration begins.

## Project Overview

VsDebuggerMcp is an MCP (Model Context Protocol) server that exposes Visual Studio debugging capabilities over HTTP/SSE. It connects to a running Visual Studio instance via COM interop (DTE2) and provides programmatic control of building, debugging, breakpoints, stepping, and inspection. Windows-only (`net8.0-windows`).

## Build and Run

```bash
dotnet build
dotnet run
```

The server starts on `http://localhost:5050` with SSE transport at `/sse`.

Register with Claude: `claude mcp add --transport sse vs-debugger http://localhost:5050/sse`

## Test Workflow

The repository includes a dedicated C# project under `test/` for feature validation.

Recommended test flow:
1. Let the repo-local SessionStart hook attempt to start `vs-debugger-mcp` automatically.
2. Confirm `http://localhost:5050/sse` is reachable, then run the best-effort MCP discoverability check for `vs-debugger`.
3. If discoverability is missing, run fallback guidance manually only with explicit user approval: `claude mcp add --transport sse vs-debugger http://localhost:5050/sse` (it may modify persistent/global CLI MCP configuration).
4. Open and debug the C# project under `test/` in Visual Studio.
5. Exercise the relevant debugger features through MCP against that test project.
6. Record whether the result reflects build success only, MCP connectivity success, or actual end-to-end debugger behavior.

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
