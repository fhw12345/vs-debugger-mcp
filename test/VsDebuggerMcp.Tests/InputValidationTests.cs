using VsDebuggerMcp.Tools;
using Xunit;

namespace VsDebuggerMcp.Tests;

public class InputValidationTests
{
    // BreakpointTools
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void BreakpointAdd_InvalidLineNumber_ReturnsError(int lineNumber)
    {
        var result = BreakpointTools.BreakpointAdd("test.cs", lineNumber);
        Assert.Equal("lineNumber must be a positive integer.", result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BreakpointAddConditional_InvalidLineNumber_ReturnsError(int lineNumber)
    {
        var result = BreakpointTools.BreakpointAddConditional("test.cs", lineNumber, "x > 0");
        Assert.Equal("lineNumber must be a positive integer.", result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BreakpointAddTracepoint_InvalidLineNumber_ReturnsError(int lineNumber)
    {
        var result = BreakpointTools.BreakpointAddTracepoint("test.cs", lineNumber, "msg");
        Assert.Equal("lineNumber must be a positive integer.", result);
    }

    [Fact]
    public void BreakpointAddHitCount_InvalidLineNumber_ReturnsError()
    {
        var result = BreakpointTools.BreakpointAddHitCount("test.cs", -1, 5);
        Assert.Equal("lineNumber must be a positive integer.", result);
    }

    [Fact]
    public void BreakpointAddHitCount_InvalidHitCount_ReturnsError()
    {
        var result = BreakpointTools.BreakpointAddHitCount("test.cs", 10, 0);
        Assert.Equal("hitCount must be a positive integer.", result);
    }

    // StepTools
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DebugSetNextStatement_InvalidLineNumber_ReturnsError(int lineNumber)
    {
        var result = StepTools.DebugSetNextStatement(lineNumber);
        Assert.Equal("lineNumber must be a positive integer.", result);
    }

    // DebugLifecycleTools
    [Fact]
    public void DebugAttachToProcess_InvalidProcessId_ReturnsError()
    {
        var result = DebugLifecycleTools.DebugAttachToProcess(-1);
        Assert.Equal("processId must be a positive integer.", result);
    }

    [Fact]
    public void DebugAttachToProcess_ZeroProcessId_ReturnsError()
    {
        var result = DebugLifecycleTools.DebugAttachToProcess(0);
        Assert.Equal("processId must be a positive integer.", result);
    }

    // OpenSolution
    [Fact]
    public async Task OpenSolution_EmptyPath_ReturnsError()
    {
        var result = await DebugLifecycleTools.OpenSolution("");
        Assert.Equal("solutionPath is required. Provide an absolute path to a .sln or .csproj file.", result);
    }

    [Fact]
    public async Task OpenSolution_NonexistentFile_ReturnsError()
    {
        var result = await DebugLifecycleTools.OpenSolution(@"C:\nonexistent\foo.sln");
        Assert.Contains("File not found", result);
    }

    [Fact]
    public async Task OpenSolution_WrongExtension_ReturnsError()
    {
        // Use a file that exists but isn't .sln/.csproj
        var readme = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "README.md");
        if (!File.Exists(readme))
        {
            // Fallback: create a temp file
            var tmp = Path.GetTempFileName() + ".txt";
            File.WriteAllText(tmp, "test");
            try
            {
                var result = await DebugLifecycleTools.OpenSolution(tmp);
                Assert.Contains("Unsupported file type", result);
            }
            finally
            {
                File.Delete(tmp);
            }
            return;
        }
        var r = await DebugLifecycleTools.OpenSolution(readme);
        Assert.Contains("Unsupported file type", r);
    }

    // CloseSolution without VS — should return DTE error
    [Fact]
    public void CloseSolution_NoVS_ReturnsDteError()
    {
        // This will try to get DTE; if no VS running, returns error message
        // If VS is running, it might actually close — so we just verify it doesn't crash
        var result = DebugLifecycleTools.CloseSolution(false);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
