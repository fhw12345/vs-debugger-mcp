using System.ComponentModel;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class StepTools
{
    [McpServerTool, Description("Step over (F10) - execute current line and move to next")]
    public static string DebugStepOver()
    {
        var dte = DteConnector.GetDte();
        dte.Debugger.StepOver(false);
        return GetCurrentLocation(dte);
    }

    [McpServerTool, Description("Step into (F11) - step into the function call")]
    public static string DebugStepInto()
    {
        var dte = DteConnector.GetDte();
        dte.Debugger.StepInto(false);
        return GetCurrentLocation(dte);
    }

    [McpServerTool, Description("Step out (Shift+F11) - step out of current function")]
    public static string DebugStepOut()
    {
        var dte = DteConnector.GetDte();
        dte.Debugger.StepOut(false);
        return GetCurrentLocation(dte);
    }

    [McpServerTool, Description("Continue execution (F5)")]
    public static string DebugContinue()
    {
        var dte = DteConnector.GetDte();
        dte.Debugger.Go(false);
        return $"Continued. Mode: {dte.Debugger.CurrentMode}";
    }

    [McpServerTool, Description("Run to cursor position")]
    public static string DebugRunToCursor()
    {
        var dte = DteConnector.GetDte();
        dte.Debugger.RunToCursor(false);
        return GetCurrentLocation(dte);
    }

    internal static string GetCurrentLocation(DTE2 dte)
    {
        try
        {
            if (dte.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                dynamic frame = dte.Debugger.CurrentStackFrame;
                return $"Stopped at {frame.FunctionName} ({frame.Module}:{frame.LineNumber})";
            }

            return $"Mode: {dte.Debugger.CurrentMode}";
        }
        catch
        {
            return $"Mode: {dte.Debugger.CurrentMode}";
        }
    }

    [McpServerTool, Description("Set next statement - move the execution pointer to a specific line in the current file (requires Break mode)")]
    public static string DebugSetNextStatement(int lineNumber)
    {
        var dte = DteConnector.GetDte();
        DteConnector.EnsureBreakMode(dte);

        try
        {
            var doc = dte.ActiveDocument;
            if (doc == null)
                return "No active document found.";

            var textSelection = (EnvDTE.TextSelection)doc.Selection;
            textSelection.GotoLine(lineNumber, true);
            dte.Debugger.SetNextStatement();

            return GetCurrentLocation(dte);
        }
        catch (Exception ex)
        {
            return $"Failed to set next statement to line {lineNumber}: {ex.Message}";
        }
    }
}
