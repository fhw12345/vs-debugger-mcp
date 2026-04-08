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
        DteConnector.EnsureBreakMode(dte);

        try
        {
            dynamic frame = dte.Debugger.CurrentStackFrame;
            var sb = new StringBuilder();

            string header;
            try { header = $"Locals at {frame.FunctionName} (line {frame.LineNumber}):"; }
            catch { header = $"Locals at {frame.FunctionName}:"; }
            sb.AppendLine(header);

            foreach (Expression expr in frame.Locals)
            {
                try
                {
                    sb.AppendLine($"  {expr.Type} {expr.Name} = {expr.Value}");
                }
                catch
                {
                    sb.AppendLine($"  {expr.Name} = <unavailable>");
                }
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
        DteConnector.EnsureBreakMode(dte);

        var result = dte.Debugger.GetExpression(expression, false, 5000);
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
        DteConnector.EnsureBreakMode(dte);

        var sb = new StringBuilder();
        sb.AppendLine("Call Stack:");

        var thread = dte.Debugger.CurrentThread;
        int i = 0;
        foreach (StackFrame frame in thread.StackFrames)
        {
            var marker = i == 0 ? " -> " : "    ";
            try
            {
                dynamic df = frame;
                string funcName = df.FunctionName;
                string line;
                try { line = $" (line {df.LineNumber})"; }
                catch { line = ""; }
                sb.AppendLine($"{marker}[{i}] {funcName}{line}");
            }
            catch
            {
                sb.AppendLine($"{marker}[{i}] (unknown frame)");
            }

            i++;
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Get the current execution location (file, line, function)")]
    public static string DebugGetCurrentLocation()
    {
        var dte = DteConnector.GetDte();
        DteConnector.EnsureBreakMode(dte);

        dynamic frame = dte.Debugger.CurrentStackFrame;
        try
        {
            return $"Function: {frame.FunctionName}\nLine: {frame.LineNumber}\nModule: {frame.Module}";
        }
        catch
        {
            return $"Function: {frame.FunctionName}\nModule: {frame.Module}";
        }
    }

    [McpServerTool, Description("List all threads in the current process")]
    public static string DebugGetThreads()
    {
        var dte = DteConnector.GetDte();
        DteConnector.EnsureBreakMode(dte);

        var sb = new StringBuilder();
        var currentThread = dte.Debugger.CurrentThread;
        sb.AppendLine("Threads:");

        foreach (EnvDTE.Thread thread in dte.Debugger.CurrentProgram.Threads)
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
        DteConnector.EnsureBreakMode(dte);

        var result = dte.Debugger.GetExpression(variablePath, false, 5000);
        if (!result.IsValidValue)
        {
            return $"Could not inspect '{variablePath}': {result.Value}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{variablePath} = {result.Value} (Type: {result.Type})");

        if (result.DataMembers.Count > 0)
        {
            sb.AppendLine("Members:");
            foreach (Expression member in result.DataMembers)
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
        DteConnector.EnsureBreakMode(dte);

        foreach (EnvDTE.Thread thread in dte.Debugger.CurrentProgram.Threads)
        {
            if (thread.ID == threadId)
            {
                thread.Freeze();
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
        DteConnector.EnsureBreakMode(dte);

        foreach (EnvDTE.Thread thread in dte.Debugger.CurrentProgram.Threads)
        {
            if (thread.ID == threadId)
            {
                thread.Thaw();
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
        DteConnector.EnsureBreakMode(dte);

        foreach (EnvDTE.Thread thread in dte.Debugger.CurrentProgram.Threads)
        {
            if (thread.ID == threadId)
            {
                dte.Debugger.CurrentThread = thread;
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

}
