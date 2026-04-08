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
    private static DTE2? _dte;

    public static DTE2 GetDte()
    {
        if (_dte != null)
        {
            try
            {
                _ = _dte.Version;
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
            Console.WriteLine($"Connected to Visual Studio (Version: {dte.Version})");
            return dte;
        }

        throw new InvalidOperationException(
            "No running Visual Studio instance found. Please open Visual Studio first.");
    }

    private static DTE2? GetDteFromRunningObjectTable()
    {
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

                    if (displayName.StartsWith("!VisualStudio.DTE"))
                    {
                        rot.GetObject(monikers[0], out var obj);
                        if (obj is DTE2 dte)
                        {
                            return dte;
                        }
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

        return null;
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
}
