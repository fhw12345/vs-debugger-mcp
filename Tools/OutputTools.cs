using System.ComponentModel;
using System.Text;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class OutputTools
{
    [McpServerTool, Description("Read content from the Debug output window pane. Returns the last N lines (default 50).")]
    public static string OutputReadDebug(int lastNLines = 50)
    {
        var dte = DteConnector.GetDte();

        try
        {
            var outputWindow = dte.ToolWindows.OutputWindow;
            OutputWindowPane? debugPane = null;

            foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
            {
                if (pane.Name == "Debug")
                {
                    debugPane = pane;
                    break;
                }
            }

            if (debugPane == null)
                return "Debug output pane not found.";

            return ReadPane(debugPane, lastNLines);
        }
        catch (Exception ex)
        {
            return $"Failed to read debug output: {ex.Message}";
        }
    }

    [McpServerTool, Description("List all output window panes")]
    public static string OutputListPanes()
    {
        var dte = DteConnector.GetDte();
        var sb = new StringBuilder();

        var outputWindow = dte.ToolWindows.OutputWindow;
        sb.AppendLine($"Output window panes ({outputWindow.OutputWindowPanes.Count}):");

        foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
        {
            sb.AppendLine($"  - {pane.Name}");
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Read content from a specific output window pane by name (e.g. 'Build', 'Debug'). Returns the last N lines (default 50).")]
    public static string OutputReadPane(string paneName, int lastNLines = 50)
    {
        var dte = DteConnector.GetDte();

        try
        {
            var pane = dte.ToolWindows.OutputWindow.OutputWindowPanes.Item(paneName);
            return ReadPane(pane, lastNLines);
        }
        catch (Exception ex)
        {
            return $"Failed to read pane '{paneName}': {ex.Message}. Use OutputListPanes to see available panes.";
        }
    }

    [McpServerTool, Description("Execute a command in the Immediate window context and return the result (requires Break mode). Evaluates expressions, calls methods, or inspects values.")]
    public static string OutputImmediateExecute(string command)
    {
        var dte = DteConnector.GetDte();
        DteConnector.EnsureBreakMode(dte);

        try
        {
            var result = dte.Debugger.GetExpression(command, false, 10000);
            if (result.IsValidValue)
            {
                return $"> {command}\n{result.Value} (Type: {result.Type})";
            }

            return $"> {command}\nCould not evaluate: {result.Value}";
        }
        catch (Exception ex)
        {
            return $"Failed to execute '{command}': {ex.Message}";
        }
    }

    private static string ReadPane(OutputWindowPane pane, int lastNLines)
    {
        var textDoc = pane.TextDocument;
        var editPoint = textDoc.StartPoint.CreateEditPoint();
        var allText = editPoint.GetText(textDoc.EndPoint);

        if (string.IsNullOrEmpty(allText))
            return $"Output pane '{pane.Name}' is empty.";

        var lines = allText.Split('\n');
        if (lines.Length <= lastNLines)
            return allText;

        var relevantLines = lines.Skip(lines.Length - lastNLines);
        return $"(Showing last {lastNLines} of {lines.Length} lines)\n" +
               string.Join("\n", relevantLines);
    }
}
