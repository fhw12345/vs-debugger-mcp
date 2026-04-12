using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;
using EnvDTE80;

namespace VsDebuggerMcp;

/// <summary>
/// Connects to a running Visual Studio instance via COM/DTE automation.
/// </summary>
public static class DteConnector
{
    private const string ProcessIdSelectorEnv = "VS_DEBUGGER_MCP_DTE_PROCESS_ID";
    private const string SolutionHintSelectorEnv = "VS_DEBUGGER_MCP_DTE_SOLUTION_HINT";
    private const int RpcEServerCallRetryLater = unchecked((int)0x8001010A);
    private const int RpcECallRejected = unchecked((int)0x80010001);

    private static DTE2? _dte;

    public static DTE2 GetDte()
    {
        if (_dte != null)
        {
            try
            {
                _ = ExecuteWithComRetry(() => _dte.Version);
                return _dte;
            }
            catch (COMException)
            {
                _dte = null;
            }
        }

        _dte = ConnectToVisualStudio();
        return _dte;
    }

    private static DTE2 ConnectToVisualStudio()
    {
        // Enumerate Running Object Table to find VS instances
        var dte = GetDteFromRunningObjectTable();
        if (dte != null)
        {
            var version = ExecuteWithComRetry(() => dte.Version);
            Console.WriteLine($"Connected to Visual Studio (Version: {version})");
            return dte;
        }

        throw new InvalidOperationException(
            $"No running Visual Studio instance found matching selectors. " +
            $"Optional selectors: {ProcessIdSelectorEnv}=<pid>, {SolutionHintSelectorEnv}=<substring>. " +
            "Please open Visual Studio first or clear selectors.");
    }

    private static DTE2? GetDteFromRunningObjectTable()
    {
        var processIdSelector = TryGetProcessIdSelector();
        var solutionHintSelector = TryGetSelectorValue(SolutionHintSelectorEnv);

        DTE2? firstMatch = null;

        IRunningObjectTable? rot = null;
        IEnumMoniker? enumMoniker = null;

        try
        {
            Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out rot));
            rot.EnumRunning(out enumMoniker);

            var monikers = new IMoniker[1];
            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                IBindCtx? bindCtx = null;
                try
                {
                    Marshal.ThrowExceptionForHR(CreateBindCtx(0, out bindCtx));
                    monikers[0].GetDisplayName(bindCtx, null, out var displayName);

                    if (!displayName.StartsWith("!VisualStudio.DTE", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    rot.GetObject(monikers[0], out var obj);
                    if (obj is not DTE2 dte)
                    {
                        continue;
                    }

                    firstMatch ??= dte;

                    if (MatchesSelectors(dte, displayName, processIdSelector, solutionHintSelector))
                    {
                        return dte;
                    }
                }
                finally
                {
                    if (bindCtx != null) Marshal.ReleaseComObject(bindCtx);
                }
            }
        }
        finally
        {
            if (enumMoniker != null) Marshal.ReleaseComObject(enumMoniker);
            if (rot != null) Marshal.ReleaseComObject(rot);
        }

        if (processIdSelector == null && solutionHintSelector == null)
        {
            return firstMatch;
        }

        return null;
    }

    private static bool MatchesSelectors(DTE2 dte, string rotDisplayName, int? processIdSelector, string? solutionHintSelector)
    {
        if (processIdSelector != null)
        {
            if (!rotDisplayName.EndsWith($":{processIdSelector.Value}", StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (solutionHintSelector != null)
        {
            try
            {
                var fullName = ExecuteWithComRetry(() => dte.Solution?.FullName, maxAttempts: 3, baseDelayMs: 50);
                if (string.IsNullOrWhiteSpace(fullName) ||
                    fullName.IndexOf(solutionHintSelector, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }
            catch (COMException)
            {
                return false;
            }
        }

        return true;
    }

    private static int? TryGetProcessIdSelector()
    {
        var value = TryGetSelectorValue(ProcessIdSelectorEnv);
        if (value == null)
        {
            return null;
        }

        if (int.TryParse(value, out var pid) && pid > 0)
        {
            return pid;
        }

        throw new InvalidOperationException(
            $"Invalid {ProcessIdSelectorEnv} value '{value}'. Expected a positive integer process id.");
    }

    private static string? TryGetSelectorValue(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

    public static void EnsureBreakMode(DTE2 dte)
    {
        if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
        {
            throw new InvalidOperationException(
                $"Debugger must be in Break mode. Current mode: {dte.Debugger.CurrentMode}");
        }
    }

    public static void ExecuteWithComRetry(Action action, int maxAttempts = 5, int baseDelayMs = 75)
    {
        ExecuteWithComRetry(() =>
        {
            action();
            return true;
        }, maxAttempts, baseDelayMs);
    }

    public static T ExecuteWithComRetry<T>(Func<T> action, int maxAttempts = 5, int baseDelayMs = 75)
    {
        COMException? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return action();
            }
            catch (COMException ex) when (IsTransientBusyComException(ex) && attempt < maxAttempts)
            {
                lastException = ex;
                System.Threading.Thread.Sleep(baseDelayMs * attempt);
            }
        }

        if (lastException != null)
            throw lastException;

        return action();
    }

    private static bool IsTransientBusyComException(COMException ex)
    {
        return ex.ErrorCode == RpcEServerCallRetryLater || ex.ErrorCode == RpcECallRejected;
    }
}
