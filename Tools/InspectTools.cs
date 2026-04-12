using System.ComponentModel;
using System.Text;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class InspectTools
{
    [McpServerTool, Description("Get local variables in the current stack frame")]
    public static string DebugGetLocals()
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Get locals requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        try
        {
            var frame = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentStackFrame);
            var sb = new StringBuilder();
            string funcName = frame.FunctionName;
            string lineInfo;
            try { lineInfo = $" (line {((dynamic)frame).LineNumber})"; }
            catch { lineInfo = ""; }
            sb.AppendLine($"Locals at {funcName}{lineInfo}:");

            var locals = DteConnector.ExecuteWithComRetry(() =>
            {
                var snapshot = new List<(string Type, string Name, string Value)>();
                foreach (Expression expr in frame.Locals)
                {
                    snapshot.Add((
                        DteConnector.ExecuteWithComRetry(() => expr.Type),
                        DteConnector.ExecuteWithComRetry(() => expr.Name),
                        DteConnector.ExecuteWithComRetry(() => expr.Value)));
                }

                return snapshot;
            });

            foreach (var local in locals)
            {
                sb.AppendLine($"  {local.Type} {local.Name} = {local.Value}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to get locals: {ex.Message}";
        }
    }

    [McpServerTool, Description("Evaluate an expression in the current debug context")]
    public static string DebugEvaluate(string expression)
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Evaluate requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var result = DteConnector.ExecuteWithComRetry(() => dte.Debugger.GetExpression(expression, false, 5000));
        if (result.IsValidValue)
        {
            return $"{expression} = {result.Value} (Type: {result.Type})";
        }

        return $"Could not evaluate '{expression}': {result.Value}";
    }

    [McpServerTool, Description("Get the current call stack")]
    public static string DebugGetCallStack()
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Get call stack requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var sb = new StringBuilder();
        sb.AppendLine("Call Stack:");

        var thread = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentThread);
        int i = 0;
        foreach (StackFrame frame in thread.StackFrames)
        {
            var marker = i == 0 ? " -> " : "    ";
            string funcName = frame.FunctionName;
            string lineInfo;
            try
            {
                int lineNum = ((dynamic)frame).LineNumber;
                lineInfo = $" (line {lineNum})";
            }
            catch
            {
                lineInfo = "";
            }

            sb.AppendLine($"{marker}[{i}] {funcName}{lineInfo}");
            i++;
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Get the current execution location (file, line, function) with a small source preview when available")]
    public static string DebugGetCurrentLocation()
    {
        var dte = DteConnector.GetDte();
        return StepTools.DescribeCurrentLocation(dte);
    }

    [McpServerTool, Description("List all threads in the current process")]
    public static string DebugGetThreads()
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Get threads requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var sb = new StringBuilder();
        var currentThread = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentThread);
        sb.AppendLine("Threads:");

        foreach (var thread in GetThreadSnapshot(dte))
        {
            var marker = thread.ID == currentThread.ID ? " -> " : "    ";
            var name = !string.IsNullOrEmpty(thread.Name) ? thread.Name : $"Thread {thread.ID}";
            string location;
            try
            {
                dynamic topFrame = thread.StackFrames.Item(1);
                location = $"{topFrame.FunctionName}";
            }
            catch
            {
                location = "(unknown)";
            }

            sb.AppendLine($"{marker}[{thread.ID}] {name} - {location}");
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Expand a variable to see its properties/fields")]
    public static string DebugInspectVariable(string variablePath)
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Inspect variable requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var result = DteConnector.ExecuteWithComRetry(() => dte.Debugger.GetExpression(variablePath, false, 5000));
        if (!result.IsValidValue)
        {
            return $"Could not inspect '{variablePath}': {result.Value}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{variablePath} = {result.Value} (Type: {result.Type})");

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
            foreach (var member in members)
            {
                sb.AppendLine($"  {member.Type} {member.Name} = {member.Value}");
            }
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Freeze a thread by ID to prevent it from executing (requires Break mode). Use DebugGetThreads to find thread IDs.")]
    public static string DebugFreezeThread(int threadId)
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Freeze thread requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        foreach (var thread in GetThreadSnapshot(dte))
        {
            if (thread.ID == threadId)
            {
                DteConnector.ExecuteWithComRetry(() => thread.Freeze());
                var name = !string.IsNullOrEmpty(thread.Name) ? thread.Name : $"Thread {thread.ID}";
                return $"Thread [{threadId}] {name} frozen.";
            }
        }

        return $"Thread with ID {threadId} not found.";
    }

    [McpServerTool, Description("Thaw (unfreeze) a thread by ID to allow it to execute again (requires Break mode). Use DebugGetThreads to find thread IDs.")]
    public static string DebugThawThread(int threadId)
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Thaw thread requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        foreach (var thread in GetThreadSnapshot(dte))
        {
            if (thread.ID == threadId)
            {
                DteConnector.ExecuteWithComRetry(() => thread.Thaw());
                var name = !string.IsNullOrEmpty(thread.Name) ? thread.Name : $"Thread {thread.ID}";
                return $"Thread [{threadId}] {name} thawed.";
            }
        }

        return $"Thread with ID {threadId} not found.";
    }

    [McpServerTool, Description("Switch the debugger context to a specific thread by ID (requires Break mode). Use DebugGetThreads to find thread IDs.")]
    public static string DebugSwitchThread(int threadId)
    {
        var dte = DteConnector.GetDte();
        if (!TryRequireBreakMode(dte, "Switch thread requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        foreach (var thread in GetThreadSnapshot(dte))
        {
            if (thread.ID == threadId)
            {
                DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentThread = thread);
                var name = !string.IsNullOrEmpty(thread.Name) ? thread.Name : $"Thread {thread.ID}";

                string location;
                try
                {
                    dynamic topFrame = thread.StackFrames.Item(1);
                    location = $" at {topFrame.FunctionName} (line {topFrame.LineNumber})";
                }
                catch
                {
                    location = "";
                }

                return $"Switched to thread [{threadId}] {name}{location}";
            }
        }

        return $"Thread with ID {threadId} not found.";
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

    private static List<EnvDTE.Thread> GetThreadSnapshot(DTE2 dte)
    {
        return DteConnector.ExecuteWithComRetry(() =>
        {
            var threads = new List<EnvDTE.Thread>();
            foreach (EnvDTE.Thread thread in dte.Debugger.CurrentProgram.Threads)
            {
                threads.Add(thread);
            }

            return threads;
        });
    }

}
