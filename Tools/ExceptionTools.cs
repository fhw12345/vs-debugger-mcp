using System.ComponentModel;
using System.Text;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class ExceptionTools
{
    [McpServerTool, Description("Get current exception information when stopped at an exception (requires Break mode). Shows exception type, message, stack trace, and inner exception.")]
    public static string ExceptionGetCurrent()
    {
        var dte = DteConnector.GetDte();
        DteConnector.EnsureBreakMode(dte);

        var sb = new StringBuilder();

        var result = dte.Debugger.GetExpression("$exception", false, 5000);
        if (result.IsValidValue)
        {
            sb.AppendLine($"Exception: {result.Value}");
            sb.AppendLine($"Type: {result.Type}");

            var msg = dte.Debugger.GetExpression("$exception.Message", false, 5000);
            if (msg.IsValidValue)
                sb.AppendLine($"Message: {msg.Value}");

            var stack = dte.Debugger.GetExpression("$exception.StackTrace", false, 5000);
            if (stack.IsValidValue)
                sb.AppendLine($"StackTrace: {stack.Value}");

            var inner = dte.Debugger.GetExpression("$exception.InnerException", false, 5000);
            if (inner.IsValidValue && inner.Value != "null")
                sb.AppendLine($"InnerException: {inner.Value}");
        }
        else
        {
            sb.AppendLine("No exception in current context.");
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Enable first-chance exception breaking for a specific CLR exception type (e.g. 'System.NullReferenceException'). This is best-effort and may not work on all VS versions.")]
    public static string ExceptionEnableBreak(string exceptionType)
    {
        var dte = DteConnector.GetDte();

        // Try multiple approaches since ExceptionGroups API availability varies by VS version
        try
        {
            // Approach 1: ExceptionGroups via dynamic (Debugger5+)
            dynamic debugger = dte.Debugger;
            dynamic exGroups = debugger.ExceptionGroups;

            foreach (dynamic group in exGroups)
            {
                string groupName = group.Name;
                if (groupName == "Common Language Runtime Exceptions")
                {
                    try { group.NewException(exceptionType, 0); } catch { }
                    group.SetBreakWhenThrown(true, exGroups.Item("Common Language Runtime Exceptions"));
                    return $"First-chance break enabled for CLR exceptions (including {exceptionType}).";
                }
            }

            return "CLR Exception group not found in exception settings.";
        }
        catch
        {
            // Approach 2: Try via ExecuteCommand
            try
            {
                dte.ExecuteCommand("Debug.Exceptions");
                return $"Opened Exception Settings dialog. Please enable '{exceptionType}' manually in the UI.";
            }
            catch
            {
                return $"Could not configure exception breaking automatically. Please enable '{exceptionType}' via Debug > Windows > Exception Settings in Visual Studio.";
            }
        }
    }

    [McpServerTool, Description("List exception groups and their settings")]
    public static string ExceptionListSettings()
    {
        var dte = DteConnector.GetDte();
        var sb = new StringBuilder();

        try
        {
            dynamic debugger = dte.Debugger;
            dynamic exGroups = debugger.ExceptionGroups;
            int count = exGroups.Count;

            sb.AppendLine($"Exception Groups ({count}):");
            foreach (dynamic group in exGroups)
            {
                sb.AppendLine($"  - {group.Name}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Could not enumerate exception groups: {ex.Message}");
        }

        return sb.ToString();
    }
}
