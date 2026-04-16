#!/bin/bash
# E2E test for VsDebuggerMcp via stdio transport
# Requires: VS running with a solution open, and the MCP server binary
#
# Usage:
#   bash test/e2e-test.sh                              # uses default binary path
#   bash test/e2e-test.sh --exe dist/net8.0-windows/VsDebuggerMcp.exe
#   bash test/e2e-test.sh --exe bin/test-build/VsDebuggerMcp.exe
#   bash test/e2e-test.sh --solution path/to/TestDebugApp.csproj  # opens & closes VS automatically

set -euo pipefail

EXE=""
SOLUTION=""
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
RESULTS_FILE=""
PASS=0
FAIL=0
SKIP=0
FAILED_IDS=""

# Parse args
while [[ $# -gt 0 ]]; do
    case $1 in
        --exe) EXE="$2"; shift 2 ;;
        --solution) SOLUTION="$2"; shift 2 ;;
        *) echo "Unknown arg: $1"; exit 2 ;;
    esac
done

# Find binary
if [ -z "$EXE" ]; then
    for candidate in \
        "$REPO_DIR/bin/test-build/VsDebuggerMcp.exe" \
        "$REPO_DIR/dist/net8.0-windows/VsDebuggerMcp.exe" \
        "$REPO_DIR/bin/Release/net8.0-windows/VsDebuggerMcp.exe" \
        "$REPO_DIR/bin/Debug/net8.0-windows/VsDebuggerMcp.exe"; do
        if [ -f "$candidate" ]; then
            EXE="$candidate"
            break
        fi
    done
fi

if [ -z "$EXE" ] || [ ! -f "$EXE" ]; then
    echo "ERROR: VsDebuggerMcp.exe not found. Build first: dotnet build -o bin/test-build"
    exit 2
fi

echo "=== VsDebuggerMcp E2E Test ==="
echo "Binary: $EXE"

# Default solution
if [ -z "$SOLUTION" ]; then
    SOLUTION="$REPO_DIR/test/TestDebugApp/TestDebugApp.csproj"
fi
SOLUTION_ESC=$(echo "$SOLUTION" | sed 's/\\/\\\\/g')
echo "Solution: $SOLUTION"

RESULTS_FILE=$(mktemp)

# Run MCP commands via stdio
send_requests() {
    echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"e2e-test","version":"1.0.0"}}}'
    echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
    sleep 1

    # Open solution
    echo "{\"jsonrpc\":\"2.0\",\"id\":20,\"method\":\"tools/call\",\"params\":{\"name\":\"open_solution\",\"arguments\":{\"solutionPath\":\"$SOLUTION_ESC\"}}}"
    sleep 12

    # Verify open
    echo '{"jsonrpc":"2.0","id":30,"method":"tools/call","params":{"name":"list_projects","arguments":{}}}'
    sleep 2

    # Build
    echo '{"jsonrpc":"2.0","id":40,"method":"tools/call","params":{"name":"get_build_status","arguments":{}}}'
    sleep 2

    # Add breakpoint (line 7 = first line of Main)
    local bp_path
    bp_path=$(echo "$SOLUTION" | sed 's|[/\\][^/\\]*$||')/Program.cs
    bp_path=$(echo "$bp_path" | sed 's/\\/\\\\/g')
    echo "{\"jsonrpc\":\"2.0\",\"id\":50,\"method\":\"tools/call\",\"params\":{\"name\":\"breakpoint_add\",\"arguments\":{\"filePath\":\"$bp_path\",\"lineNumber\":7}}}"
    sleep 2

    # Start debug
    echo '{"jsonrpc":"2.0","id":51,"method":"tools/call","params":{"name":"debug_start","arguments":{}}}'
    sleep 12

    # Inspect (serial with delays)
    echo '{"jsonrpc":"2.0","id":60,"method":"tools/call","params":{"name":"debug_get_mode","arguments":{}}}'
    sleep 2
    echo '{"jsonrpc":"2.0","id":61,"method":"tools/call","params":{"name":"debug_get_current_location","arguments":{}}}'
    sleep 2
    echo '{"jsonrpc":"2.0","id":62,"method":"tools/call","params":{"name":"debug_get_locals","arguments":{}}}'
    sleep 2
    echo '{"jsonrpc":"2.0","id":63,"method":"tools/call","params":{"name":"debug_evaluate","arguments":{"expression":"args.Length"}}}'
    sleep 2
    echo '{"jsonrpc":"2.0","id":64,"method":"tools/call","params":{"name":"debug_get_call_stack","arguments":{}}}'
    sleep 2
    echo '{"jsonrpc":"2.0","id":65,"method":"tools/call","params":{"name":"debug_get_threads","arguments":{}}}'
    sleep 2
    echo '{"jsonrpc":"2.0","id":66,"method":"tools/call","params":{"name":"debug_inspect_variable","arguments":{"variablePath":"args"}}}'
    sleep 2

    # Step
    echo '{"jsonrpc":"2.0","id":70,"method":"tools/call","params":{"name":"debug_step_over","arguments":{}}}'
    sleep 3
    echo '{"jsonrpc":"2.0","id":71,"method":"tools/call","params":{"name":"debug_get_current_location","arguments":{}}}'
    sleep 2

    # Watch
    echo '{"jsonrpc":"2.0","id":72,"method":"tools/call","params":{"name":"watch_evaluate","arguments":{"expression":"args"}}}'
    sleep 2
    echo '{"jsonrpc":"2.0","id":73,"method":"tools/call","params":{"name":"watch_evaluate_multiple","arguments":{"expressions":"args;args.Length"}}}'
    sleep 2

    # Output
    echo '{"jsonrpc":"2.0","id":80,"method":"tools/call","params":{"name":"output_list_panes","arguments":{}}}'
    sleep 2
    echo '{"jsonrpc":"2.0","id":81,"method":"tools/call","params":{"name":"output_read_debug","arguments":{"lastNLines":5}}}'
    sleep 2

    # Continue + Stop
    echo '{"jsonrpc":"2.0","id":82,"method":"tools/call","params":{"name":"debug_continue","arguments":{}}}'
    sleep 3
    echo '{"jsonrpc":"2.0","id":90,"method":"tools/call","params":{"name":"debug_stop","arguments":{}}}'
    sleep 3

    # Cleanup
    echo '{"jsonrpc":"2.0","id":91,"method":"tools/call","params":{"name":"breakpoint_remove_all","arguments":{}}}'
    sleep 2

    # Close
    echo '{"jsonrpc":"2.0","id":100,"method":"tools/call","params":{"name":"close_solution","arguments":{"quitVS":false}}}'
    sleep 3

    # Verify closed
    echo '{"jsonrpc":"2.0","id":110,"method":"tools/call","params":{"name":"list_projects","arguments":{}}}'
    sleep 2

    # Input validation
    echo '{"jsonrpc":"2.0","id":130,"method":"tools/call","params":{"name":"breakpoint_add","arguments":{"filePath":"Program.cs","lineNumber":-1}}}'
    sleep 1
    echo '{"jsonrpc":"2.0","id":131,"method":"tools/call","params":{"name":"open_solution","arguments":{"solutionPath":""}}}'
    sleep 1
    echo '{"jsonrpc":"2.0","id":132,"method":"tools/call","params":{"name":"open_solution","arguments":{"solutionPath":"C:\\\\nonexistent.sln"}}}'
    sleep 1
}

echo ""
echo "Running E2E tests..."
send_requests | "$EXE" --stdio 2>/dev/null > "$RESULTS_FILE"

echo ""
echo "=== Results ==="

# Parse results
while IFS= read -r line; do
    id=$(echo "$line" | grep -o '"id":[0-9]*' | head -1 | grep -o '[0-9]*' || true)
    [ -z "$id" ] && continue
    [ "$id" = "1" ] && continue  # skip init

    isError=$(echo "$line" | grep -o '"isError":true' || true)
    text=$(echo "$line" | grep -o '"text":"[^"]*"' | head -1 | sed 's/"text":"//;s/"$//' | head -c 120 || true)

    if [ -n "$isError" ]; then
        echo "  FAIL [id=$id]: $text"
        FAIL=$((FAIL + 1))
        FAILED_IDS="$FAILED_IDS $id"
    else
        echo "  PASS [id=$id]: $text"
        PASS=$((PASS + 1))
    fi
done < "$RESULTS_FILE"

TOTAL=$((PASS + FAIL))
echo ""
echo "=== Summary ==="
echo "Total: $TOTAL | Pass: $PASS | Fail: $FAIL"

if [ $FAIL -gt 0 ]; then
    echo "Failed IDs:$FAILED_IDS"
fi

rm -f "$RESULTS_FILE"

if [ $FAIL -gt 0 ]; then
    exit 1
fi

if [ $PASS -eq 0 ]; then
    echo "WARNING: No test results captured"
    exit 1
fi

echo "All E2E tests passed!"
exit 0
