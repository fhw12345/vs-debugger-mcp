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
        var dte = DteConnector.GetDte();
        dte.Debugger.Go(false);
        return $"Debugging started. Mode: {dte.Debugger.CurrentMode}";
    }

    [McpServerTool, Description("Start without debugging (Ctrl+F5)")]
    public static string DebugStartWithoutDebugging()
    {
        var dte = DteConnector.GetDte();
        dte.ExecuteCommand("Debug.StartWithoutDebugging");
        return "Started without debugging.";
    }

    [McpServerTool, Description("Stop debugging (Shift+F5)")]
    public static string DebugStop()
    {
        var dte = DteConnector.GetDte();
        dte.Debugger.Stop(false);
        return "Debugging stopped.";
    }

    [McpServerTool, Description("Restart debugging")]
    public static string DebugRestart()
    {
        var dte = DteConnector.GetDte();
        dte.ExecuteCommand("Debug.Restart");
        return "Debug restarted.";
    }

    [McpServerTool, Description("Get current debug mode (Design/Break/Run)")]
    public static string DebugGetMode()
    {
        var dte = DteConnector.GetDte();
        var mode = dte.Debugger.CurrentMode;
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
        dte.Debugger.Break(false);
        return "Execution paused.";
    }

    [McpServerTool, Description("List available processes that can be attached to for debugging. Optionally filter by name.")]
    public static string DebugListProcesses(string? nameFilter = null)
    {
        var dte = DteConnector.GetDte();
        var sb = new StringBuilder();
        var processes = dte.Debugger.LocalProcesses;
        int shown = 0;

        foreach (EnvDTE.Process process in processes)
        {
            try
            {
                if (nameFilter == null ||
                    process.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"  [{process.ProcessID}] {process.Name}");
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

        foreach (EnvDTE.Process process in dte.Debugger.LocalProcesses)
        {
            if (process.ProcessID == processId)
            {
                process.Attach();
                return $"Attached to process [{processId}] {process.Name}. Mode: {dte.Debugger.CurrentMode}";
            }
        }

        return $"Process with ID {processId} not found. Use DebugListProcesses to see available processes.";
    }

    [McpServerTool, Description("Attach debugger to a process by name (attaches to first match). Use DebugListProcesses to find available processes.")]
    public static string DebugAttachToProcessByName(string processName)
    {
        var dte = DteConnector.GetDte();

        foreach (EnvDTE.Process process in dte.Debugger.LocalProcesses)
        {
            try
            {
                if (process.Name.Contains(processName, StringComparison.OrdinalIgnoreCase))
                {
                    process.Attach();
                    return $"Attached to process [{process.ProcessID}] {process.Name}. Mode: {dte.Debugger.CurrentMode}";
                }
            }
            catch
            {
                // Skip inaccessible processes
            }
        }

        return $"No process matching '{processName}' found. Use DebugListProcesses to see available processes.";
    }
}
