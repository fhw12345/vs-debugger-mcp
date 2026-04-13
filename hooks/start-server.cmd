@echo off
REM Start vs-debugger-mcp server if not already running
REM Called by Claude Code SessionStart hook

setlocal

REM Check if the server is already running on port 5050
netstat -ano 2>nul | findstr /C:":5050 " | findstr /C:"LISTENING" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    REM Server already running, output status for Claude Code
    echo {"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"vs-debugger-mcp server is already running on http://localhost:5050/sse"},"suppressOutput":true}
    exit /b 0
)

REM Find the plugin root (where this script lives)
set "SCRIPT_DIR=%~dp0"
set "EXE_PATH=%SCRIPT_DIR%..\dist\net8.0-windows\VsDebuggerMcp.exe"

REM Check if the exe exists
if not exist "%EXE_PATH%" (
    echo {"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"vs-debugger-mcp: dist/net8.0-windows/VsDebuggerMcp.exe not found. Run 'dotnet publish' to build."},"suppressOutput":true}
    exit /b 0
)

REM Start the server in background
start "" /B "%EXE_PATH%" >nul 2>&1

REM Wait briefly for startup
timeout /t 2 /nobreak >nul 2>&1

REM Verify it started
netstat -ano 2>nul | findstr /C:":5050 " | findstr /C:"LISTENING" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo {"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"vs-debugger-mcp server started successfully on http://localhost:5050/sse"},"suppressOutput":true}
) else (
    echo {"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"vs-debugger-mcp server failed to start. Check if .NET 8 runtime is installed."},"suppressOutput":true}
)

exit /b 0
