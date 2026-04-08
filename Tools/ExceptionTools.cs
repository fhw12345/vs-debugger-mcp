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

        try
        {
            // ExceptionGroups is available on Debugger5 and later, use dynamic to avoid
            // compile-time dependency on interfaces not present in the envdte80 interop package.
            dynamic debugger = dte.Debugger;
            dynamic exGroups = debugger.ExceptionGroups;

            foreach (dynamic group in exGroups)
            {
                string groupName = group.Name;
                if (groupName == "Common Language Runtime Exceptions")
                {
                    try
                    {
                        group.NewException(exceptionType, 0);
                    }
                    catch
                    {
                        // Exception type may already exist in the list
                    }

                    group.SetBreakWhenThrown(true, exGroups.Item("Common Language Runtime Exceptions"));
                    return $"First-chance break enabled for CLR exceptions (including {exceptionType}).";
                }
            }

            return "CLR Exception group not found in exception settings.";
        }
        catch (Exception ex)
        {
            return $"Failed to configure exception breaking: {ex.Message}. Try using Visual Studio UI: Debug > Windows > Exception Settings.";
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
