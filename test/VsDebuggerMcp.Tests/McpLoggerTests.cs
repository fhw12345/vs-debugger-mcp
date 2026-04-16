using Xunit;

namespace VsDebuggerMcp.Tests;

public class McpLoggerTests
{
    [Fact]
    public void Log_WritesToStderr_NotStdout()
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        var stdoutWriter = new StringWriter();
        var stderrWriter = new StringWriter();

        try
        {
            Console.SetOut(stdoutWriter);
            Console.SetError(stderrWriter);

            McpLogger.Log("test message");

            var stdout = stdoutWriter.ToString();
            var stderr = stderrWriter.ToString();

            Assert.Empty(stdout);
            Assert.Contains("test message", stderr);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Log_ToolStep_FormatsCorrectly()
    {
        var originalErr = Console.Error;
        var writer = new StringWriter();

        try
        {
            Console.SetError(writer);

            McpLogger.Log("MyTool", "enter");

            var output = writer.ToString();
            Assert.Contains("[MyTool] enter", output);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Log_ToolStepDetail_FormatsCorrectly()
    {
        var originalErr = Console.Error;
        var writer = new StringWriter();

        try
        {
            Console.SetError(writer);

            McpLogger.Log("MyTool", "connect", "VS 2022");

            var output = writer.ToString();
            Assert.Contains("[MyTool] connect", output);
            Assert.Contains("VS 2022", output);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Log_IncludesTimestamp()
    {
        var originalErr = Console.Error;
        var writer = new StringWriter();

        try
        {
            Console.SetError(writer);

            McpLogger.Log("timestamp test");

            var output = writer.ToString();
            // Timestamp format: [HH:mm:ss.fff]
            Assert.Matches(@"\[\d{2}:\d{2}:\d{2}\.\d{3}\]", output);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }
}
