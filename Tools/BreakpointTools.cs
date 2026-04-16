using System.ComponentModel;
using System.Text;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class BreakpointTools
{
    [McpServerTool, Description("Add a breakpoint at a specific file and line number. filePath may be absolute or relative to the open solution.")]
    public static string BreakpointAdd(string filePath, int lineNumber)
    {
        if (lineNumber <= 0)
            return "lineNumber must be a positive integer.";
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!TryResolveSourceFile(dte, filePath, out var resolvedPath, out var errorMessage))
            return errorMessage;

        DteConnector.ExecuteWithComRetry(() => dte.Debugger.Breakpoints.Add(File: resolvedPath, Line: lineNumber));
        return $"Breakpoint added at {resolvedPath}:{lineNumber}";
    }

    [McpServerTool, Description("Add a conditional breakpoint at a specific file and line. filePath may be absolute or relative to the open solution.")]
    public static string BreakpointAddConditional(string filePath, int lineNumber, string condition)
    {
        if (lineNumber <= 0)
            return "lineNumber must be a positive integer.";
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!TryResolveSourceFile(dte, filePath, out var resolvedPath, out var errorMessage))
            return errorMessage;

        DteConnector.ExecuteWithComRetry(() => dte.Debugger.Breakpoints.Add(
            File: resolvedPath,
            Line: lineNumber,
            Condition: condition,
            ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue));
        return $"Conditional breakpoint added at {resolvedPath}:{lineNumber} when '{condition}'";
    }

    [McpServerTool, Description("Remove a breakpoint at a specific file and line number. filePath may be absolute or relative to the open solution.")]
    public static string BreakpointRemove(string filePath, int lineNumber)
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var matches = FindMatchingBreakpoints(dte, filePath, lineNumber);
        if (matches.Count == 1)
        {
            var breakpoint = matches[0];
            DteConnector.ExecuteWithComRetry(() => breakpoint.Delete());
            return $"Breakpoint removed at {breakpoint.File}:{lineNumber}";
        }

        if (matches.Count > 1)
            return BuildAmbiguousBreakpointMessage(filePath, lineNumber, matches);

        return $"No breakpoint found at {filePath}:{lineNumber}";
    }

    [McpServerTool, Description("Toggle a breakpoint enabled/disabled at a specific file and line. filePath may be absolute or relative to the open solution.")]
    public static string BreakpointToggle(string filePath, int lineNumber)
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var matches = FindMatchingBreakpoints(dte, filePath, lineNumber);
        if (matches.Count == 1)
        {
            var breakpoint = matches[0];
            DteConnector.ExecuteWithComRetry(() => breakpoint.Enabled = !breakpoint.Enabled);
            var isEnabled = DteConnector.ExecuteWithComRetry(() => breakpoint.Enabled);
            return $"Breakpoint at {breakpoint.File}:{lineNumber} is now {(isEnabled ? "enabled" : "disabled")}";
        }

        if (matches.Count > 1)
            return BuildAmbiguousBreakpointMessage(filePath, lineNumber, matches);

        return $"No breakpoint found at {filePath}:{lineNumber}";
    }

    [McpServerTool, Description("List all breakpoints")]
    public static string BreakpointList()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var breakpoints = GetBreakpointSnapshot(dte);
        var sb = new StringBuilder();
        sb.AppendLine($"Total breakpoints: {breakpoints.Count}");

        foreach (var bp in breakpoints)
        {
            var status = bp.Enabled ? "enabled" : "disabled";
            var condition = !string.IsNullOrEmpty(bp.Condition) ? $" [when: {bp.Condition}]" : "";
            sb.AppendLine($"  {bp.File}:{bp.FileLine} ({status}){condition}");
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Remove all breakpoints")]
    public static string BreakpointRemoveAll()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var breakpoints = GetBreakpointSnapshot(dte);
        var count = breakpoints.Count;

        foreach (var breakpoint in breakpoints)
        {
            DteConnector.ExecuteWithComRetry(() => breakpoint.Delete());
        }

        return $"Removed {count} breakpoints.";
    }

    [McpServerTool, Description("Add a tracepoint that logs a message to debug output without breaking execution. filePath may be absolute or relative to the open solution. Message supports tokens: {expression}, $FUNCTION, $CALLER, $CALLSTACK, $THREAD, $PID")]
    public static string BreakpointAddTracepoint(string filePath, int lineNumber, string message)
    {
        if (lineNumber <= 0)
            return "lineNumber must be a positive integer.";
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!TryResolveSourceFile(dte, filePath, out var resolvedPath, out var errorMessage))
            return errorMessage;

        var bps = DteConnector.ExecuteWithComRetry(() => dte.Debugger.Breakpoints.Add(File: resolvedPath, Line: lineNumber));

        foreach (Breakpoint2 bp in bps)
        {
            // Message and BreakWhenHit setters may not be exposed in the interop type,
            // use dynamic to access the COM setter directly.
            dynamic dbp = bp;
            DteConnector.ExecuteWithComRetry(() => { dbp.Message = message; });
            DteConnector.ExecuteWithComRetry(() => { dbp.BreakWhenHit = false; });
        }

        return $"Tracepoint added at {resolvedPath}:{lineNumber} with message: \"{message}\"";
    }

    [McpServerTool, Description("Add a hit count breakpoint that only breaks after being hit a specified number of times. filePath may be absolute or relative to the open solution. hitCountType: 'equal' (break on Nth hit), 'gte' (break on Nth and every subsequent hit), 'multiple' (break on every Nth hit)")]
    public static string BreakpointAddHitCount(string filePath, int lineNumber, int hitCount, string hitCountType = "equal")
    {
        if (lineNumber <= 0)
            return "lineNumber must be a positive integer.";
        if (hitCount <= 0)
            return "hitCount must be a positive integer.";
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        if (!TryResolveSourceFile(dte, filePath, out var resolvedPath, out var errorMessage))
            return errorMessage;

        var bps = DteConnector.ExecuteWithComRetry(() => dte.Debugger.Breakpoints.Add(File: resolvedPath, Line: lineNumber));

        var type = hitCountType.ToLowerInvariant() switch
        {
            "equal" => dbgHitCountType.dbgHitCountTypeEqual,
            "greaterthanorequal" or "gte" => dbgHitCountType.dbgHitCountTypeGreaterOrEqual,
            "multiple" => dbgHitCountType.dbgHitCountTypeMultiple,
            _ => dbgHitCountType.dbgHitCountTypeEqual
        };

        foreach (Breakpoint2 bp in bps)
        {
            // HitCountType and HitCountTarget setters are not exposed in the interop type,
            // use dynamic to access the COM setter directly.
            dynamic dbp = bp;
            DteConnector.ExecuteWithComRetry(() => { dbp.HitCountType = type; });
            DteConnector.ExecuteWithComRetry(() => { dbp.HitCountTarget = hitCount; });
        }

        return $"Hit count breakpoint added at {resolvedPath}:{lineNumber} (breaks when hit count {hitCountType} {hitCount})";
    }

    private static bool TryResolveSourceFile(DTE2 dte, string filePath, out string resolvedPath, out string errorMessage)
    {
        resolvedPath = filePath;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "File path is required.";
            return false;
        }

        foreach (var candidate in GetCandidatePaths(dte, filePath))
        {
            if (File.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        var searchRoots = GetSearchRoots(dte);
        var matches = new List<string>();
        var fileName = Path.GetFileName(filePath);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var normalizedInput = NormalizePath(filePath);
            var hasDirectorySegments = normalizedInput.Contains('/');

            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (var candidate in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
                {
                    var normalizedCandidate = NormalizePath(candidate);
                    if (hasDirectorySegments)
                    {
                        if (!normalizedCandidate.EndsWith(normalizedInput, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    else if (!string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    matches.Add(Path.GetFullPath(candidate));
                }
            }
        }

        matches = matches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (matches.Count == 1)
        {
            resolvedPath = matches[0];
            return true;
        }

        if (matches.Count > 1)
        {
            errorMessage = $"File path '{filePath}' is ambiguous. Matches: {string.Join(", ", matches.Take(5))}";
            return false;
        }

        errorMessage = $"Could not resolve source file '{filePath}'. Provide an absolute path or a path relative to the open solution.";
        return false;
    }

    private static List<Breakpoint> FindMatchingBreakpoints(DTE2 dte, string filePath, int lineNumber)
    {
        var matches = new List<Breakpoint>();
        var normalizedInput = NormalizePath(filePath);

        TryResolveSourceFile(dte, filePath, out var resolvedPath, out _);
        var normalizedResolvedPath = NormalizePath(resolvedPath);

        foreach (var breakpoint in GetBreakpointSnapshot(dte))
        {
            if (breakpoint.FileLine != lineNumber || string.IsNullOrWhiteSpace(breakpoint.File))
                continue;

            var normalizedBreakpointPath = NormalizePath(breakpoint.File);
            if (string.Equals(normalizedBreakpointPath, normalizedResolvedPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedBreakpointPath, normalizedInput, StringComparison.OrdinalIgnoreCase) ||
                normalizedBreakpointPath.EndsWith($"/{normalizedInput}", StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(breakpoint);
            }
        }

        return matches;
    }

    private static List<Breakpoint> GetBreakpointSnapshot(DTE2 dte)
    {
        return DteConnector.ExecuteWithComRetry(() =>
        {
            var breakpoints = new List<Breakpoint>();
            foreach (Breakpoint breakpoint in dte.Debugger.Breakpoints)
            {
                breakpoints.Add(breakpoint);
            }

            return breakpoints;
        });
    }

    private static string BuildAmbiguousBreakpointMessage(string filePath, int lineNumber, List<Breakpoint> matches)
    {
        var locations = matches
            .Select(match => $"{match.File}:{match.FileLine}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5);

        return $"Multiple breakpoints match {filePath}:{lineNumber}. Matches: {string.Join(", ", locations)}";
    }

    private static IEnumerable<string> GetCandidatePaths(DTE2 dte, string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            yield return Path.GetFullPath(filePath);
            yield break;
        }

        foreach (var root in GetSearchRoots(dte))
        {
            yield return Path.GetFullPath(Path.Combine(root, filePath));
        }
    }

    private static List<string> GetSearchRoots(DTE2 dte)
    {
        var roots = new List<string>();

        try
        {
            var solutionFullName = dte.Solution?.FullName;
            if (!string.IsNullOrWhiteSpace(solutionFullName))
            {
                var solutionDirectory = Path.GetDirectoryName(solutionFullName);
                if (!string.IsNullOrWhiteSpace(solutionDirectory))
                    roots.Add(Path.GetFullPath(solutionDirectory));
            }
        }
        catch
        {
        }

        roots.Add(Path.GetFullPath(Directory.GetCurrentDirectory()));
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizePath(string filePath)
    {
        return filePath.Replace('\\', '/').Trim();
    }
}
