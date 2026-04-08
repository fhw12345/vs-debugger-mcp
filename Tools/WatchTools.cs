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
        DteConnector.EnsureBreakMode(dte);

        var result = dte.Debugger.GetExpression(expression, false, 5000);
        if (!result.IsValidValue)
            return $"Could not evaluate '{expression}': {result.Value}";

        var sb = new StringBuilder();
        sb.AppendLine($"{expression} = {result.Value} (Type: {result.Type})");

        if (result.DataMembers.Count > 0)
        {
            sb.AppendLine("Members:");
            int count = 0;
            foreach (Expression member in result.DataMembers)
            {
                sb.AppendLine($"  {member.Type} {member.Name} = {member.Value}");
                count++;
                if (count >= 50)
                {
                    sb.AppendLine($"  ... and {result.DataMembers.Count - 50} more members");
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
        DteConnector.EnsureBreakMode(dte);

        var sb = new StringBuilder();
        var exprList = expressions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        sb.AppendLine($"Watch results ({exprList.Length} expressions):");

        foreach (var expr in exprList)
        {
            var result = dte.Debugger.GetExpression(expr, false, 5000);
            if (result.IsValidValue)
            {
                sb.AppendLine($"  {expr} = {result.Value} ({result.Type})");
            }
            else
            {
                sb.AppendLine($"  {expr} = <error: {result.Value}>");
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
            dte.ExecuteCommand("Debug.AddWatch", expression);
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
            dte.ExecuteCommand("Debug.Watch1");
            dte.ExecuteCommand("Edit.SelectAll");
            dte.ExecuteCommand("Edit.Delete");
            return "All watch expressions cleared from Watch 1.";
        }
        catch (Exception ex)
        {
            return $"Failed to clear watch expressions: {ex.Message}";
        }
    }
}
