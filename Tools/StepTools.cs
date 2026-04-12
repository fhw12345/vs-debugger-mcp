using System.ComponentModel;
using System.Text;
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
        if (!TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Step over requires break mode. Pause at a breakpoint first.", out var message))
            return message;

        DteConnector.ExecuteWithComRetry(() => dte.Debugger.StepOver(false));
        return GetCurrentLocation(dte);
    }

    [McpServerTool, Description("Step into (F11) - step into the function call")]
    public static string DebugStepInto()
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Step into requires break mode. Pause at a breakpoint first.", out var message))
            return message;

        DteConnector.ExecuteWithComRetry(() => dte.Debugger.StepInto(false));
        return GetCurrentLocation(dte);
    }

    [McpServerTool, Description("Step out (Shift+F11) - step out of current function")]
    public static string DebugStepOut()
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Step out requires break mode. Pause at a breakpoint first.", out var message))
            return message;

        DteConnector.ExecuteWithComRetry(() => dte.Debugger.StepOut(false));
        return GetCurrentLocation(dte);
    }

    [McpServerTool, Description("Continue execution (F5)")]
    public static string DebugContinue()
    {
        var dte = DteConnector.GetDte();
        var mode = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentMode);
        if (mode == dbgDebugMode.dbgRunMode)
            return "Already running.";

        if (mode != dbgDebugMode.dbgBreakMode)
            return $"Continue requires break mode. Current mode: {mode}.";

        DteConnector.ExecuteWithComRetry(() => dte.Debugger.Go(false));
        return $"Continued. Mode: {DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentMode)}";
    }

    [McpServerTool, Description("Run to cursor position")]
    public static string DebugRunToCursor()
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Run to cursor requires break mode. Pause at a breakpoint first.", out var message))
            return message;

        DteConnector.ExecuteWithComRetry(() => dte.Debugger.RunToCursor(false));
        return GetCurrentLocation(dte);
    }

    internal static string GetCurrentLocation(DTE2 dte)
    {
        return DescribeCurrentLocation(dte);
    }

    internal static string DescribeCurrentLocation(DTE2 dte, int contextLines = 2)
    {
        try
        {
            var mode = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentMode);
            if (mode == dbgDebugMode.dbgBreakMode)
            {
                dynamic frame = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentStackFrame);
                var functionName = TryGetFrameText(() => (string)frame.FunctionName) ?? "(unknown)";
                var moduleName = TryGetFrameText(() => (string)frame.Module);
                var fileName = TryGetFrameText(() => (string)frame.FileName);
                var lineNumber = TryGetFrameNumber(() => (int)frame.LineNumber);
                (fileName, lineNumber) = TryGetEditorLocation(dte, fileName, lineNumber);

                var sb = new StringBuilder();
                sb.AppendLine($"Function: {functionName}");

                if (!string.IsNullOrWhiteSpace(fileName))
                    sb.AppendLine($"File: {fileName}");

                if (lineNumber != null)
                    sb.AppendLine($"Line: {lineNumber.Value}");

                if (!string.IsNullOrWhiteSpace(moduleName))
                    sb.AppendLine($"Module: {moduleName}");

                var sourcePreview = TryReadSourcePreview(fileName, lineNumber, contextLines);
                if (!string.IsNullOrEmpty(sourcePreview))
                {
                    sb.AppendLine("Source:");
                    sb.Append(sourcePreview);
                }

                return sb.ToString().TrimEnd();
            }

            return $"Mode: {mode}";
        }
        catch
        {
            try
            {
                return $"Mode: {DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentMode)}";
            }
            catch
            {
                return "Mode unavailable.";
            }
        }
    }

    private static bool TryRequireMode(DTE2 dte, dbgDebugMode requiredMode, string userMessage, out string message)
    {
        var currentMode = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentMode);
        if (currentMode == requiredMode)
        {
            message = string.Empty;
            return true;
        }

        message = $"{userMessage} Current mode: {currentMode}.";
        return false;
    }

    [McpServerTool, Description("Set next statement - move the execution pointer to a specific line in the current file (requires Break mode)")]
    public static string DebugSetNextStatement(int lineNumber)
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Set next statement requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        try
        {
            var doc = DteConnector.ExecuteWithComRetry(() => dte.ActiveDocument);
            if (doc == null)
                return "No active document found.";

            var textSelection = DteConnector.ExecuteWithComRetry(() => (EnvDTE.TextSelection)doc.Selection);
            DteConnector.ExecuteWithComRetry(() => textSelection.GotoLine(lineNumber, true));
            DteConnector.ExecuteWithComRetry(() => dte.Debugger.SetNextStatement());

            return GetCurrentLocation(dte);
        }
        catch (Exception ex)
        {
            return $"Failed to set next statement to line {lineNumber}: {ex.Message}";
        }
    }

    private static string? TryGetFrameText(Func<string> getter)
    {
        try
        {
            return DteConnector.ExecuteWithComRetry(getter);
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetFrameNumber(Func<int> getter)
    {
        try
        {
            return DteConnector.ExecuteWithComRetry(getter);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadSourcePreview(string? fileName, int? lineNumber, int contextLines)
    {
        if (string.IsNullOrWhiteSpace(fileName) || lineNumber == null || lineNumber.Value <= 0 || !File.Exists(fileName))
            return null;

        var lines = File.ReadAllLines(fileName);
        if (lines.Length == 0 || lineNumber.Value > lines.Length)
            return null;

        var startLine = Math.Max(1, lineNumber.Value - Math.Max(0, contextLines));
        var endLine = Math.Min(lines.Length, lineNumber.Value + Math.Max(0, contextLines));
        var width = endLine.ToString().Length;
        var sb = new StringBuilder();

        for (var currentLine = startLine; currentLine <= endLine; currentLine++)
        {
            var marker = currentLine == lineNumber.Value ? ">" : " ";
            sb.AppendLine($"{marker} {currentLine.ToString().PadLeft(width)}: {lines[currentLine - 1]}");
        }

        return sb.ToString().TrimEnd();
    }

    private static (string? FileName, int? LineNumber) TryGetEditorLocation(DTE2 dte, string? fileName, int? lineNumber)
    {
        try
        {
            var activeDocument = DteConnector.ExecuteWithComRetry(() => dte.ActiveDocument);
            if (activeDocument == null)
                return (fileName, lineNumber);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                var activeFile = DteConnector.ExecuteWithComRetry(() => activeDocument.FullName);
                if (!string.IsNullOrWhiteSpace(activeFile))
                    fileName = activeFile;
            }

            if (lineNumber == null)
            {
                var selection = DteConnector.ExecuteWithComRetry(() => activeDocument.Selection as TextSelection);
                if (selection != null)
                    lineNumber = DteConnector.ExecuteWithComRetry(() => selection.CurrentLine);
            }
        }
        catch
        {
        }

        return (fileName, lineNumber);
    }
}
