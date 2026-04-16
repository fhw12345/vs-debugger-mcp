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
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        lastNLines = NormalizeLineCount(lastNLines);

        try
        {
            var debugPane = GetPaneByName(dte, "Debug");

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
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var panes = GetPaneSnapshot(dte);
        var sb = new StringBuilder();

        sb.AppendLine($"Output window panes ({panes.Count}):");

        foreach (var pane in panes)
        {
            sb.AppendLine($"  - {DteConnector.ExecuteWithComRetry(() => pane.Name)}");
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Read content from a specific output window pane by name (e.g. 'Build', 'Debug'). Returns the last N lines (default 50).")]
    public static string OutputReadPane(string paneName, int lastNLines = 50)
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        lastNLines = NormalizeLineCount(lastNLines);

        try
        {
            var pane = GetPaneByName(dte, paneName);
            if (pane == null)
                return $"Pane '{paneName}' not found. Use OutputListPanes to see available panes.";

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
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Immediate execution requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        try
        {
            var result = DteConnector.ExecuteWithComRetry(() => dte.Debugger.GetExpression(command, false, 10000));
            if (result.IsValidValue)
            {
                var value = DteConnector.ExecuteWithComRetry(() => result.Value);
                var type = DteConnector.ExecuteWithComRetry(() => result.Type);
                return $"> {command}\n{value} (Type: {type})";
            }

            var errorValue = DteConnector.ExecuteWithComRetry(() => result.Value);
            return $"> {command}\nCould not evaluate: {errorValue}";
        }
        catch (Exception ex)
        {
            return $"Failed to execute '{command}': {ex.Message}";
        }
    }

    private static string ReadPane(OutputWindowPane pane, int lastNLines)
    {
        var paneName = DteConnector.ExecuteWithComRetry(() => pane.Name);
        var textDoc = DteConnector.ExecuteWithComRetry(() => pane.TextDocument);
        var endPoint = DteConnector.ExecuteWithComRetry(() => textDoc.EndPoint);
        var totalLines = DteConnector.ExecuteWithComRetry(() => endPoint.Line);

        if (totalLines <= 1)
            return $"Output pane '{paneName}' is empty.";

        var startLine = Math.Max(1, totalLines - lastNLines + 1);
        var relevantText = DteConnector.ExecuteWithComRetry(() =>
        {
            var editPoint = textDoc.StartPoint.CreateEditPoint();
            return editPoint.GetLines(startLine, totalLines + 1);
        });

        if (string.IsNullOrWhiteSpace(relevantText))
            return $"Output pane '{paneName}' is empty.";

        if (startLine == 1)
            return relevantText;

        var shownLines = totalLines - startLine + 1;
        return $"(Showing last {shownLines} of {totalLines} lines)\n{relevantText}";
    }

    private static OutputWindowPane? GetPaneByName(DTE2 dte, string paneName)
    {
        var aliases = GetPaneAliases(paneName);
        return GetPaneSnapshot(dte)
            .FirstOrDefault(pane => aliases.Contains(NormalizePaneName(DteConnector.ExecuteWithComRetry(() => pane.Name))));
    }

    private static List<OutputWindowPane> GetPaneSnapshot(DTE2 dte)
    {
        return DteConnector.ExecuteWithComRetry(() =>
        {
            var panes = new List<OutputWindowPane>();
            var outputWindow = dte.ToolWindows.OutputWindow;
            foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
            {
                panes.Add(pane);
            }

            return panes;
        });
    }

    private static int NormalizeLineCount(int lastNLines)
    {
        return Math.Clamp(lastNLines, 1, 500);
    }

    private static HashSet<string> GetPaneAliases(string paneName)
    {
        var normalizedName = NormalizePaneName(paneName);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            normalizedName
        };

        switch (normalizedName)
        {
            case "debug":
                aliases.Add("调试");
                break;
            case "build":
                aliases.Add("生成");
                break;
            case "build order":
            case "buildorder":
                aliases.Add("生成顺序");
                break;
        }

        return aliases;
    }

    private static string NormalizePaneName(string paneName)
    {
        return paneName.Trim().ToLowerInvariant();
    }
}
