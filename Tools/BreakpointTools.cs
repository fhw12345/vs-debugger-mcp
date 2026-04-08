using System.ComponentModel;
using System.Text;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class BreakpointTools
{
    [McpServerTool, Description("Add a breakpoint at a specific file and line number")]
    public static string BreakpointAdd(string filePath, int lineNumber)
    {
        var dte = DteConnector.GetDte();
        dte.Debugger.Breakpoints.Add(File: filePath, Line: lineNumber);
        return $"Breakpoint added at {filePath}:{lineNumber}";
    }

    [McpServerTool, Description("Add a conditional breakpoint at a specific file and line")]
    public static string BreakpointAddConditional(string filePath, int lineNumber, string condition)
    {
        var dte = DteConnector.GetDte();
        dte.Debugger.Breakpoints.Add(
            File: filePath,
            Line: lineNumber,
            Condition: condition,
            ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue);
        return $"Conditional breakpoint added at {filePath}:{lineNumber} when '{condition}'";
    }

    [McpServerTool, Description("Remove a breakpoint at a specific file and line number")]
    public static string BreakpointRemove(string filePath, int lineNumber)
    {
        var dte = DteConnector.GetDte();
        foreach (Breakpoint bp in dte.Debugger.Breakpoints)
        {
            if (bp.File == filePath && bp.FileLine == lineNumber)
            {
                bp.Delete();
                return $"Breakpoint removed at {filePath}:{lineNumber}";
            }
        }

        return $"No breakpoint found at {filePath}:{lineNumber}";
    }

    [McpServerTool, Description("Toggle a breakpoint enabled/disabled at a specific file and line")]
    public static string BreakpointToggle(string filePath, int lineNumber)
    {
        var dte = DteConnector.GetDte();
        foreach (Breakpoint bp in dte.Debugger.Breakpoints)
        {
            if (bp.File == filePath && bp.FileLine == lineNumber)
            {
                bp.Enabled = !bp.Enabled;
                return $"Breakpoint at {filePath}:{lineNumber} is now {(bp.Enabled ? "enabled" : "disabled")}";
            }
        }

        return $"No breakpoint found at {filePath}:{lineNumber}";
    }

    [McpServerTool, Description("List all breakpoints")]
    public static string BreakpointList()
    {
        var dte = DteConnector.GetDte();
        var sb = new StringBuilder();
        sb.AppendLine($"Total breakpoints: {dte.Debugger.Breakpoints.Count}");

        foreach (Breakpoint bp in dte.Debugger.Breakpoints)
        {
            var status = bp.Enabled ? "enabled" : "disabled";
            var condition = !string.IsNullOrEmpty(bp.Condition) ? $" [when: {bp.Condition}]" : "";
            sb.AppendLine($"  {bp.File}:{bp.FileLine} ({status}){condition}");
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Remove all breakpoints")]
    public static string BreakpointRemoveAll()
    {
        var dte = DteConnector.GetDte();
        var count = dte.Debugger.Breakpoints.Count;

        foreach (Breakpoint bp in dte.Debugger.Breakpoints)
        {
            bp.Delete();
        }

        return $"Removed {count} breakpoints.";
    }

    [McpServerTool, Description("Add a tracepoint that logs a message to debug output without breaking execution. Message supports tokens: {expression}, $FUNCTION, $CALLER, $CALLSTACK, $THREAD, $PID")]
    public static string BreakpointAddTracepoint(string filePath, int lineNumber, string message)
    {
        var dte = DteConnector.GetDte();

        var bps = dte.Debugger.Breakpoints.Add(File: filePath, Line: lineNumber);

        foreach (Breakpoint2 bp in bps)
        {
            // Message and BreakWhenHit setters may not be exposed in the interop type,
            // use dynamic to access the COM setter directly.
            dynamic dbp = bp;
            dbp.Message = message;
            dbp.BreakWhenHit = false;
        }

        return $"Tracepoint added at {filePath}:{lineNumber} with message: \"{message}\"";
    }

    [McpServerTool, Description("Add a hit count breakpoint that only breaks after being hit a specified number of times. hitCountType: 'equal' (break on Nth hit), 'gte' (break on Nth and every subsequent hit), 'multiple' (break on every Nth hit)")]
    public static string BreakpointAddHitCount(string filePath, int lineNumber, int hitCount, string hitCountType = "equal")
    {
        var dte = DteConnector.GetDte();

        var bps = dte.Debugger.Breakpoints.Add(File: filePath, Line: lineNumber);

        var type = hitCountType.ToLowerInvariant() switch
        {
            "equal" => dbgHitCountType.dbgHitCountTypeEqual,
            "greaterthanorequal" or "gte" => dbgHitCountType.dbgHitCountTypeGreaterOrEqual,
            "multiple" => dbgHitCountType.dbgHitCountTypeMultiple,
            _ => dbgHitCountType.dbgHitCountTypeEqual
        };

        foreach (Breakpoint2 bp in bps)
        {
            // HitCountType and HitCountTarget setters are not exposed in the interop type,
            // use dynamic to access the COM setter directly.
            dynamic dbp = bp;
            dbp.HitCountType = type;
            dbp.HitCountTarget = hitCount;
        }

        return $"Hit count breakpoint added at {filePath}:{lineNumber} (breaks when hit count {hitCountType} {hitCount})";
    }
}
