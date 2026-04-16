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
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Get locals requires break mode. Pause at a breakpoint first.", out var modeMessage))
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
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Evaluate requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var result = DteConnector.ExecuteWithComRetry(() => dte.Debugger.GetExpression(expression, false, 5000));
        var isValid = DteConnector.ExecuteWithComRetry(() => result.IsValidValue);
        if (isValid)
        {
            var value = DteConnector.ExecuteWithComRetry(() => result.Value);
            var type = DteConnector.ExecuteWithComRetry(() => result.Type);
            return $"{expression} = {value} (Type: {type})";
        }

        var errorValue = DteConnector.ExecuteWithComRetry(() => result.Value);
        return $"Could not evaluate '{expression}': {errorValue}";
    }

    [McpServerTool, Description("Get the current call stack")]
    public static string DebugGetCallStack()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Get call stack requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var sb = new StringBuilder();
        sb.AppendLine("Call Stack:");

        var thread = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentThread);
        var frames = DteConnector.ExecuteWithComRetry(() =>
        {
            var list = new List<StackFrame>();
            foreach (StackFrame f in thread.StackFrames) list.Add(f);
            return list;
        });

        int i = 0;
        foreach (var frame in frames)
        {
            try
            {
                var marker = i == 0 ? " -> " : "    ";
                var funcName = DteConnector.ExecuteWithComRetry(() => frame.FunctionName);
                string lineInfo;
                try
                {
                    int lineNum = DteConnector.ExecuteWithComRetry(() => ((dynamic)frame).LineNumber);
                    lineInfo = $" (line {lineNum})";
                }
                catch
                {
                    lineInfo = "";
                }

                sb.AppendLine($"{marker}[{i}] {funcName}{lineInfo}");
            }
            catch
            {
                // Skip frames that can't be inspected
            }
            i++;
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Get the current execution location (file, line, function) with a small source preview when available")]
    public static string DebugGetCurrentLocation()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        return StepTools.DescribeCurrentLocation(dte);
    }

    [McpServerTool, Description("List all threads in the current process")]
    public static string DebugGetThreads()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Get threads requires break mode. Pause at a breakpoint first.", out var modeMessage))
            return modeMessage;

        var sb = new StringBuilder();
        var currentThread = DteConnector.ExecuteWithComRetry(() => dte.Debugger.CurrentThread);
        sb.AppendLine("Threads:");

        foreach (var thread in GetThreadSnapshot(dte))
        {
            try
            {
                var threadId = DteConnector.ExecuteWithComRetry(() => thread.ID);
                var currentId = DteConnector.ExecuteWithComRetry(() => currentThread.ID);
                var marker = threadId == currentId ? " -> " : "    ";
                var threadName = DteConnector.ExecuteWithComRetry(() => thread.Name);
                var name = !string.IsNullOrEmpty(threadName) ? threadName : $"Thread {threadId}";
                string location;
                try
                {
                    dynamic topFrame = DteConnector.ExecuteWithComRetry(() => thread.StackFrames.Item(1));
                    location = $"{topFrame.FunctionName}";
                }
                catch
                {
                    location = "(unknown)";
                }

                sb.AppendLine($"{marker}[{threadId}] {name} - {location}");
            }
            catch
            {
                // Skip threads that can't be inspected
            }
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Expand a variable to see its properties/fields")]
    public static string DebugInspectVariable(string variablePath)
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Inspect variable requires break mode. Pause at a breakpoint first.", out var modeMessage))
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
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Freeze thread requires break mode. Pause at a breakpoint first.", out var modeMessage))
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
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Thaw thread requires break mode. Pause at a breakpoint first.", out var modeMessage))
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
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!DteConnector.TryRequireMode(dte, dbgDebugMode.dbgBreakMode, "Switch thread requires break mode. Pause at a breakpoint first.", out var modeMessage))
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
