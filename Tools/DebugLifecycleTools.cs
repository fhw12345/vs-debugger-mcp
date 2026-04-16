using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class DebugLifecycleTools
{
    [McpServerTool, Description("Start debugging (F5)")]
    public static async Task<string> DebugStart()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var mode = GetCurrentMode(dte);

        if (mode == dbgDebugMode.dbgRunMode)
            return "Debugging is already running.";

        if (mode == dbgDebugMode.dbgBreakMode)
        {
            DteConnector.ExecuteWithComRetry(() => dte.Debugger.Go(false));
            return $"Continued from break mode. Mode: {GetCurrentMode(dte)}";
        }

        // ExecuteCommand dispatches asynchronously on VS's UI thread and returns quickly
        McpLogger.Log("DebugStart", "calling ExecuteCommand(Debug.Start)");
        DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Debug.Start"));
        McpLogger.Log("DebugStart", "ExecuteCommand returned, polling for mode change");

        // Poll until VS enters Run/Break mode or timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(3000, cts.Token).ConfigureAwait(false);
            try
            {
                var currentMode = GetCurrentMode(dte);
                McpLogger.Log("DebugStart", "poll", $"mode={currentMode} elapsed={sw.Elapsed.TotalSeconds:0}s");
                if (currentMode == dbgDebugMode.dbgRunMode)
                    return $"Debugging started (after {sw.Elapsed.TotalSeconds:0}s). Mode: Run";
                if (currentMode == dbgDebugMode.dbgBreakMode)
                    return $"Debugging started and hit breakpoint (after {sw.Elapsed.TotalSeconds:0}s). Mode: Break";
            }
            catch (Exception ex)
            {
                McpLogger.Log("DebugStart", "poll error (VS busy)", ex.Message);
            }
        }

        McpLogger.Log("DebugStart", "TIMEOUT after 8 minutes");
        return "Debug start command issued but VS hasn't entered Run/Break mode within 8 minutes. Check VS manually.";
    }

    [McpServerTool, Description("Build the solution or a specific project, then start debugging the current startup project. If projectName is omitted, the whole solution is built first.")]
    public static async Task<string> DebugStartWithBuild(string? projectName = null, string configuration = "Debug")
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var mode = GetCurrentMode(dte);

        if (mode != dbgDebugMode.dbgDesignMode)
            return $"Start with build requires design mode. Current mode: {mode}. Stop the current debug session first.";

        BuildTools.BuildInvocationResult build;
        try
        {
            build = await BuildTools.ExecuteBuildAsync(dte, projectName, configuration);
        }
        catch (Exception ex)
        {
            return $"Build failed before debugging could start: {ex.Message}";
        }

        if (!build.Succeeded)
            return $"Build failed for '{build.TargetName}'. Debugging not started. State: {build.State}, Failed projects: {build.FailedProjects}{build.Errors}";

        DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Debug.Start"));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(1000, cts.Token).ConfigureAwait(false);
            try
            {
                var currentMode = GetCurrentMode(dte);
                if (currentMode == dbgDebugMode.dbgRunMode || currentMode == dbgDebugMode.dbgBreakMode)
                    return $"Build succeeded for '{build.TargetName}'. Started debugging. Mode: {currentMode}";
            }
            catch
            {
                // VS busy, keep polling
            }
        }

        return $"Build succeeded for '{build.TargetName}'. Debug start command issued but VS hasn't entered Run/Break mode. Check VS manually.";
    }

    [McpServerTool, Description("Start without debugging (Ctrl+F5)")]
    public static string DebugStartWithoutDebugging()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var mode = GetCurrentMode(dte);

        if (mode != dbgDebugMode.dbgDesignMode)
            return $"Start without debugging requires design mode. Current mode: {mode}. Stop the current debug session first.";

        DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Debug.StartWithoutDebugging"));
        return "Started without debugging.";
    }

    [McpServerTool, Description("Stop debugging (Shift+F5)")]
    public static string DebugStop()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var mode = GetCurrentMode(dte);

        if (mode == dbgDebugMode.dbgRunMode || mode == dbgDebugMode.dbgBreakMode)
        {
            DteConnector.ExecuteWithComRetry(() => dte.Debugger.Stop(false));
            return "Debugging stopped.";
        }

        if (mode == dbgDebugMode.dbgDesignMode)
        {
            return "Not debugging; nothing to stop.";
        }

        return $"Stop skipped. Current mode: {mode}.";
    }

    [McpServerTool, Description("Restart debugging")]
    public static string DebugRestart()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var mode = GetCurrentMode(dte);

        if (mode == dbgDebugMode.dbgRunMode || mode == dbgDebugMode.dbgBreakMode)
        {
            DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Debug.Restart"));
            return "Debug restarted.";
        }

        if (mode == dbgDebugMode.dbgDesignMode)
        {
            return "Not debugging. Start debugging first.";
        }

        return $"Restart skipped. Current mode: {mode}.";
    }

    [McpServerTool, Description("Get current debug mode (Design/Break/Run)")]
    public static string DebugGetMode()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var mode = GetCurrentMode(dte);
        return mode switch
        {
            dbgDebugMode.dbgDesignMode => "Design (not debugging)",
            dbgDebugMode.dbgBreakMode => "Break (paused at breakpoint)",
            dbgDebugMode.dbgRunMode => "Run (executing)",
            _ => $"Unknown: {mode}"
        };
    }

    [McpServerTool, Description("Break all execution (Ctrl+Alt+Break)")]
    public static string DebugBreakAll()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var mode = GetCurrentMode(dte);

        if (mode == dbgDebugMode.dbgRunMode)
        {
            DteConnector.ExecuteWithComRetry(() => dte.Debugger.Break(false));
            return "Execution paused.";
        }

        if (mode == dbgDebugMode.dbgBreakMode)
        {
            return "Already paused in break mode.";
        }

        if (mode == dbgDebugMode.dbgDesignMode)
        {
            return "Not debugging; break all is unavailable.";
        }

        return $"Break all skipped. Current mode: {mode}.";
    }

    [McpServerTool, Description("List available processes that can be attached to for debugging. Optionally filter by name.")]
    public static string DebugListProcesses(string? nameFilter = null)
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var sb = new StringBuilder();
        var processes = GetProcessSnapshot(dte);
        int shown = 0;

        foreach (var process in processes)
        {
            try
            {
                var processName = DteConnector.ExecuteWithComRetry(() => process.Name);
                if (nameFilter == null ||
                    processName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    var processId = DteConnector.ExecuteWithComRetry(() => process.ProcessID);
                    sb.AppendLine($"  [{processId}] {processName}");
                    shown++;
                }
            }
            catch
            {
                // Skip processes that can't be inspected
            }
        }

        return $"Processes ({shown} shown):\n{sb}";
    }

    [McpServerTool, Description("Attach debugger to a process by process ID (PID). Use DebugListProcesses to find available PIDs.")]
    public static string DebugAttachToProcess(int processId)
    {
        if (processId <= 0)
            return "processId must be a positive integer.";
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;

        foreach (var process in GetProcessSnapshot(dte))
        {
            if (DteConnector.ExecuteWithComRetry(() => process.ProcessID) == processId)
            {
                DteConnector.ExecuteWithComRetry(() => process.Attach());
                var processName = DteConnector.ExecuteWithComRetry(() => process.Name);
                return $"Attached to process [{processId}] {processName}. Mode: {GetCurrentMode(dte)}";
            }
        }

        return $"Process with ID {processId} not found. Use DebugListProcesses to see available processes.";
    }

    [McpServerTool, Description("Attach debugger to a process by name (attaches to first match). Use DebugListProcesses to find available processes.")]
    public static string DebugAttachToProcessByName(string processName)
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;

        foreach (var process in GetProcessSnapshot(dte))
        {
            try
            {
                var candidateName = DteConnector.ExecuteWithComRetry(() => process.Name);
                if (candidateName.Contains(processName, StringComparison.OrdinalIgnoreCase))
                {
                    DteConnector.ExecuteWithComRetry(() => process.Attach());
                    var processId = DteConnector.ExecuteWithComRetry(() => process.ProcessID);
                    return $"Attached to process [{processId}] {candidateName}. Mode: {GetCurrentMode(dte)}";
                }
            }
            catch
            {
                // Skip inaccessible processes
            }
        }

        return $"No process matching '{processName}' found. Use DebugListProcesses to see available processes.";
    }

    private static dbgDebugMode GetCurrentMode(DTE2 dte)
    {
        return DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentMode);
    }

    private static List<EnvDTE.Process> GetProcessSnapshot(DTE2 dte)
    {
        return DteConnector.ExecuteWithComRetry(() =>
        {
            var processes = new List<EnvDTE.Process>();
            foreach (EnvDTE.Process process in dte.Debugger.LocalProcesses)
            {
                processes.Add(process);
            }

            return processes;
        });
    }

    [McpServerTool, Description("Open a solution in Visual Studio. Launches VS if not running, or opens the solution in an existing instance. Waits until VS is ready for debugging. solutionPath must be an absolute path to a .sln or .csproj file.")]
    public static async Task<string> OpenSolution(string solutionPath)
    {
        McpLogger.Log("OpenSolution", "enter", solutionPath);

        if (string.IsNullOrWhiteSpace(solutionPath))
            return "solutionPath is required. Provide an absolute path to a .sln or .csproj file.";

        if (!File.Exists(solutionPath))
            return $"File not found: {solutionPath}";

        var ext = Path.GetExtension(solutionPath).ToLowerInvariant();
        if (ext != ".sln" && ext != ".csproj")
            return $"Unsupported file type '{ext}'. Provide a .sln or .csproj file.";

        // Check if VS already has this solution/project open
        if (DteConnector.TryGetDte(out var existingDte, out _))
        {
            try
            {
                var currentSolution = DteConnector.ExecuteWithComRetry(() => existingDte.Solution?.FullName);
                McpLogger.Log("OpenSolution", "current solution", currentSolution ?? "(empty)");

                var requestedFull = Path.GetFullPath(solutionPath);
                var requestedName = Path.GetFileNameWithoutExtension(solutionPath);

                // Match by solution path
                if (!string.IsNullOrWhiteSpace(currentSolution))
                {
                    var currentFull = Path.GetFullPath(currentSolution);
                    if (string.Equals(currentFull, requestedFull, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetDirectoryName(currentFull), Path.GetDirectoryName(requestedFull), StringComparison.OrdinalIgnoreCase))
                    {
                        McpLogger.Log("OpenSolution", "already open (solution match)", currentSolution);
                        return $"Solution already open in Visual Studio: {currentSolution}";
                    }
                }

                // Fallback: when VS opens a .csproj, Solution.FullName is empty — check project list
                var projects = DteConnector.ExecuteWithComRetry(() => existingDte.Solution.Projects);
                foreach (EnvDTE.Project project in projects)
                {
                    try
                    {
                        var projectName = DteConnector.ExecuteWithComRetry(() => project.Name);
                        if (string.Equals(projectName, requestedName, StringComparison.OrdinalIgnoreCase))
                        {
                            McpLogger.Log("OpenSolution", "already open (project match)", projectName);
                            return $"Project already open in Visual Studio: {projectName}";
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                // VS instance exists but can't query solution — continue with open
            }
        }

        // If VS is already running, open the solution in the existing instance
        if (DteConnector.TryGetDte(out var runningDte, out _))
        {
            McpLogger.Log("OpenSolution", "opening in existing VS", solutionPath);
            var fullPath = Path.GetFullPath(solutionPath);

            if (ext == ".sln")
                DteConnector.ExecuteWithComRetry(() => runningDte.Solution.Open(fullPath));
            else
                DteConnector.ExecuteWithComRetry(() => runningDte.ExecuteCommand("File.OpenProject", $"\"{fullPath}\""));

            // Wait briefly for the solution to load
            using var existingCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!existingCts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, existingCts.Token).ConfigureAwait(false);
                try
                {
                    var projects = DteConnector.ExecuteWithComRetry(() => runningDte.Solution.Projects);
                    if (DteConnector.ExecuteWithComRetry(() => projects.Count) > 0)
                    {
                        McpLogger.Log("OpenSolution", "loaded in existing VS");
                        return $"Solution opened in existing Visual Studio: {solutionPath}";
                    }
                }
                catch { }
            }

            return $"Solution open command sent to Visual Studio. It may still be loading: {solutionPath}";
        }

        // No VS running — launch devenv
        McpLogger.Log("OpenSolution", "launching devenv", solutionPath);
        var devenvPath = FindDevenv();
        if (devenvPath == null)
            return "Could not find devenv.exe. Ensure Visual Studio is installed.";

        System.Diagnostics.Process.Start(new ProcessStartInfo
        {
            FileName = devenvPath,
            Arguments = $"\"{Path.GetFullPath(solutionPath)}\"",
            UseShellExecute = false
        });

        // Poll until VS is ready and the solution is loaded
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var sw = Stopwatch.StartNew();
        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(3000, cts.Token).ConfigureAwait(false);
            try
            {
                if (DteConnector.TryGetDte(out var dte, out _))
                {
                    var loadedSolution = DteConnector.ExecuteWithComRetry(() => dte.Solution?.FullName);
                    if (!string.IsNullOrWhiteSpace(loadedSolution) &&
                        (loadedSolution.IndexOf(Path.GetFileNameWithoutExtension(solutionPath), StringComparison.OrdinalIgnoreCase) >= 0 ||
                         string.Equals(Path.GetDirectoryName(Path.GetFullPath(loadedSolution)), Path.GetDirectoryName(Path.GetFullPath(solutionPath)), StringComparison.OrdinalIgnoreCase)))
                    {
                        McpLogger.Log("OpenSolution", "ready", $"{sw.Elapsed.TotalSeconds:0}s");
                        return $"Visual Studio is ready with {loadedSolution} (took {sw.Elapsed.TotalSeconds:0}s).";
                    }
                }
            }
            catch
            {
                // VS still starting, keep polling
            }
        }

        return $"Visual Studio launched but solution not fully loaded after 3 minutes. Check VS manually.";
    }

    [McpServerTool, Description("Close the current solution and optionally quit Visual Studio. Use after debugging/testing is complete to clean up. Set quitVS to true to also exit the Visual Studio process.")]
    public static string CloseSolution(bool quitVS = false)
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;

        var mode = GetCurrentMode(dte);
        if (mode != dbgDebugMode.dbgDesignMode)
        {
            DteConnector.ExecuteWithComRetry(() => dte.Debugger.Stop(false));
            McpLogger.Log("CloseSolution", "stopped active debug session");
        }

        var solutionName = "";
        try
        {
            solutionName = DteConnector.ExecuteWithComRetry(() => dte.Solution?.FullName) ?? "";
            if (string.IsNullOrWhiteSpace(solutionName))
            {
                // csproj mode — get project name instead
                var projects = DteConnector.ExecuteWithComRetry(() => dte.Solution.Projects);
                foreach (EnvDTE.Project p in projects)
                {
                    try { solutionName = DteConnector.ExecuteWithComRetry(() => p.Name); break; } catch { }
                }
            }
        }
        catch { }

        if (quitVS)
        {
            McpLogger.Log("CloseSolution", "quitting VS", solutionName);
            DteConnector.ExecuteWithComRetry(() => dte.Quit());
            return $"Visual Studio closed (was: {solutionName}).";
        }

        McpLogger.Log("CloseSolution", "closing solution", solutionName);
        DteConnector.ExecuteWithComRetry(() => dte.Solution.Close(false));
        return $"Solution closed: {solutionName}. Visual Studio is still running.";
    }

    private static string? FindDevenv()
    {
        // Try vswhere first (standard VS installation discovery)
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var vswhere = Path.Combine(programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");

        if (File.Exists(vswhere))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = vswhere,
                    Arguments = "-latest -property productPath",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    var path = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(5000);
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        return path;
                }
            }
            catch
            {
                // vswhere failed, fall through
            }
        }

        // Fallback: common paths
        var commonPaths = new[]
        {
            Path.Combine(programFiles, "Microsoft Visual Studio", "2022", "Enterprise", "Common7", "IDE", "devenv.exe"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2022", "Professional", "Common7", "IDE", "devenv.exe"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2022", "Community", "Common7", "IDE", "devenv.exe"),
        };

        // Also check Program Files (not x86) for newer VS
        var programFiles64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var commonPaths64 = new[]
        {
            Path.Combine(programFiles64, "Microsoft Visual Studio", "2022", "Enterprise", "Common7", "IDE", "devenv.exe"),
            Path.Combine(programFiles64, "Microsoft Visual Studio", "2022", "Professional", "Common7", "IDE", "devenv.exe"),
            Path.Combine(programFiles64, "Microsoft Visual Studio", "2022", "Community", "Common7", "IDE", "devenv.exe"),
        };

        foreach (var path in commonPaths.Concat(commonPaths64))
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
