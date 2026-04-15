namespace VsDebuggerMcp;

/// <summary>
/// Simple file logger for debugging MCP tool calls.
/// Writes timestamped entries to a log file alongside the executable.
/// </summary>
public static class McpLogger
{
    private static readonly string LogPath = Path.Combine(
        AppContext.BaseDirectory, "vs-debugger-mcp.log");

    private static readonly object Lock = new();

    public static void Log(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        Console.WriteLine(entry);
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogPath, entry + Environment.NewLine);
            }
        }
        catch
        {
            // Don't let logging failures break tool calls
        }
    }

    public static void Log(string tool, string step, string detail = "")
    {
        var msg = string.IsNullOrEmpty(detail)
            ? $"[{tool}] {step}"
            : $"[{tool}] {step} — {detail}";
        Log(msg);
    }

    public static string GetLogPath() => LogPath;
}
