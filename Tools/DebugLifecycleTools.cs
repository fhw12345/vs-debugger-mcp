using System.ComponentModel;
using System.Text;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class DebugLifecycleTools
{
    [McpServerTool, Description("Start debugging (F5)")]
    public static string DebugStart()
    {
        McpLogger.Log("DebugStart", "enter");
        var dte = DteConnector.GetDte();
        McpLogger.Log("DebugStart", "got DTE");
        var mode = GetCurrentMode(dte);
        McpLogger.Log("DebugStart", "current mode", mode.ToString());

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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromMinutes(8))
        {
            System.Threading.Thread.Sleep(3000);
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
    public static string DebugStartWithBuild(string? projectName = null, string configuration = "Debug")
    {
        var dte = DteConnector.GetDte();
        var mode = GetCurrentMode(dte);

        if (mode != dbgDebugMode.dbgDesignMode)
            return $"Start with build requires design mode. Current mode: {mode}. Stop the current debug session first.";

        BuildTools.BuildInvocationResult build;
        try
        {
            build = BuildTools.ExecuteBuild(dte, projectName, configuration);
        }
        catch (Exception ex)
        {
            return $"Build failed before debugging could start: {ex.Message}";
        }

        if (!build.Succeeded)
            return $"Build failed for '{build.TargetName}'. Debugging not started. State: {build.State}, Failed projects: {build.FailedProjects}{build.Errors}";

        DteConnector.ExecuteWithComRetry(() => dte.Debugger.Go(false));
        return $"Build succeeded for '{build.TargetName}'. Started debugging the current startup project. Mode: {GetCurrentMode(dte)}";
    }

    [McpServerTool, Description("Start without debugging (Ctrl+F5)")]
    public static string DebugStartWithoutDebugging()
    {
        var dte = DteConnector.GetDte();
        var mode = GetCurrentMode(dte);

        if (mode != dbgDebugMode.dbgDesignMode)
            return $"Start without debugging requires design mode. Current mode: {mode}. Stop the current debug session first.";

        DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Debug.StartWithoutDebugging"));
        return "Started without debugging.";
    }

    [McpServerTool, Description("Stop debugging (Shift+F5)")]
    public static string DebugStop()
    {
        var dte = DteConnector.GetDte();
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
        var dte = DteConnector.GetDte();
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
        var dte = DteConnector.GetDte();
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
        var dte = DteConnector.GetDte();
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
        var dte = DteConnector.GetDte();
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
        var dte = DteConnector.GetDte();

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
        var dte = DteConnector.GetDte();

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
}
