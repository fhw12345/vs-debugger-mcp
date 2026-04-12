using System.ComponentModel;
using System.Text;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class WatchTools
{
    [McpServerTool, Description("Evaluate a watch expression and expand its members in the current debug context (requires Break mode)")]
    public static string WatchEvaluate(string expression)
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Watch evaluation requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var result = DteConnector.ExecuteWithComRetry(() => dte.Debugger.GetExpression(expression, false, 5000));
        if (!result.IsValidValue)
            return $"Could not evaluate '{expression}': {result.Value}";

        var sb = new StringBuilder();
        var value = DteConnector.ExecuteWithComRetry(() => result.Value);
        var type = DteConnector.ExecuteWithComRetry(() => result.Type);
        sb.AppendLine($"{expression} = {value} (Type: {type})");

        var members = DteConnector.ExecuteWithComRetry(() =>
        {
            var snapshot = new List<(string Type, string Name, string Value)>();
            foreach (Expression member in result.DataMembers)
            {
                snapshot.Add((
                    DteConnector.ExecuteWithComRetry(() => member.Type),
                    DteConnector.ExecuteWithComRetry(() => member.Name),
                    DteConnector.ExecuteWithComRetry(() => member.Value)));
            }

            return snapshot;
        });

        if (members.Count > 0)
        {
            sb.AppendLine("Members:");
            int count = 0;
            foreach (var member in members)
            {
                sb.AppendLine($"  {member.Type} {member.Name} = {member.Value}");
                count++;
                if (count >= 50)
                {
                    sb.AppendLine($"  ... and {members.Count - 50} more members");
                    break;
                }
            }
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Evaluate multiple watch expressions at once (requires Break mode). Pass expressions separated by semicolons, e.g. 'x;y;obj.Name'")]
    public static string WatchEvaluateMultiple(string expressions)
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Watch evaluation requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var sb = new StringBuilder();
        var exprList = expressions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        sb.AppendLine($"Watch results ({exprList.Length} expressions):");

        foreach (var expr in exprList)
        {
            try
            {
                var result = DteConnector.ExecuteWithComRetry(() => dte.Debugger.GetExpression(expr, false, 5000));
                if (result.IsValidValue)
                {
                    var value = DteConnector.ExecuteWithComRetry(() => result.Value);
                    var type = DteConnector.ExecuteWithComRetry(() => result.Type);
                    sb.AppendLine($"  {expr} = {value} ({type})");
                }
                else
                {
                    var errorValue = DteConnector.ExecuteWithComRetry(() => result.Value);
                    sb.AppendLine($"  {expr} = <error: {errorValue}>");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  {expr} = <error: {ex.Message}>");
            }
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Add a watch expression to Visual Studio's Watch window")]
    public static string WatchAdd(string expression)
    {
        var dte = DteConnector.GetDte();

        try
        {
            DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Debug.AddWatch", expression));
            return $"Added '{expression}' to Watch window.";
        }
        catch (Exception ex)
        {
            return $"Failed to add watch expression: {ex.Message}";
        }
    }

    [McpServerTool, Description("Remove all watch expressions from Watch Window 1")]
    public static string WatchClearAll()
    {
        var dte = DteConnector.GetDte();

        try
        {
            DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Debug.Watch1"));
            DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Edit.SelectAll"));
            DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Edit.Delete"));
            return "All watch expressions cleared from Watch 1.";
        }
        catch (Exception ex)
        {
            return $"Failed to clear watch expressions: {ex.Message}";
        }
    }

    private static bool TryRequireBreakMode(DTE2 dte, string userMessage, out string message)
    {
        var currentMode = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentMode);
        if (currentMode == dbgDebugMode.dbgBreakMode)
        {
            message = string.Empty;
            return true;
        }

        message = $"{userMessage} Current mode: {currentMode}.";
        return false;
    }
}
