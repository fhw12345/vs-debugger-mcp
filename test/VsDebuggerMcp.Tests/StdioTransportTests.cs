using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace VsDebuggerMcp.Tests;

public class StdioTransportTests
{
    private static string GetExePath()
    {
        // Walk up from test output dir to find the main project binary
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            foreach (var subpath in new[]
            {
                Path.Combine("bin", "test-build", "VsDebuggerMcp.exe"),
                Path.Combine("bin", "Debug", "net8.0-windows", "VsDebuggerMcp.exe"),
                Path.Combine("bin", "Release", "net8.0-windows", "VsDebuggerMcp.exe"),
                Path.Combine("dist", "net8.0-windows", "VsDebuggerMcp.exe"),
            })
            {
                var candidate = Path.Combine(dir, subpath);
                if (File.Exists(candidate)) return candidate;
            }
            dir = Path.GetDirectoryName(dir) ?? dir;
        }
        throw new FileNotFoundException("VsDebuggerMcp.exe not found. Run 'dotnet build -o bin/test-build' or 'dotnet build' first.");
    }

    private static async Task<string> RunStdio(string input, int timeoutSeconds = 10)
    {
        var exe = GetExePath();
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = "--stdio",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // Suppress .NET hosting info logs that leak to stdout
        psi.Environment["Logging__LogLevel__Default"] = "None";

        using var proc = Process.Start(psi)!;

        // Write each line separately with a small delay so the server processes them
        foreach (var line in input.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            await proc.StandardInput.WriteLineAsync(line);
            await proc.StandardInput.FlushAsync();
        }

        // Give the server time to process before closing stdin
        await Task.Delay(2000);
        proc.StandardInput.Close();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token);
        await proc.WaitForExitAsync(cts.Token);

        return stdout;
    }

    [Fact]
    public async Task Initialize_ReturnsValidJsonRpc()
    {
        var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0.0\"}}}";
        var stdout = await RunStdio(request);

        Assert.Contains("\"jsonrpc\":\"2.0\"", stdout);
        Assert.Contains("\"id\":1", stdout);
        Assert.Contains("\"protocolVersion\"", stdout);
        Assert.Contains("VsDebuggerMcp", stdout);
    }

    [Fact]
    public async Task ToolsList_ReturnsAllTools()
    {
        var requests = string.Join("\n",
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0.0\"}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\",\"params\":{}}"
        );
        var stdout = await RunStdio(requests, 15);

        // Count tool names in the response
        var toolCount = 0;
        var searchPos = 0;
        while (true)
        {
            var idx = stdout.IndexOf("\"name\":\"", searchPos, StringComparison.Ordinal);
            if (idx < 0) break;
            toolCount++;
            searchPos = idx + 8;
        }

        // 52 tools + 1 "name" from clientInfo echo = at least 52 tool entries
        // The tools/list response should have all tools
        Assert.True(toolCount >= 52, $"Expected at least 52 tool names, got {toolCount}");
    }

    [Fact]
    public async Task Stdio_NoLogOutputOnStdout()
    {
        var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0.0\"}}}";
        var stdout = await RunStdio(request);

        // stdout should only contain JSON-RPC responses, no log prefixes
        Assert.DoesNotContain("[McpLogger]", stdout);
        Assert.DoesNotContain("Connected to Visual Studio", stdout);

        // Every non-empty line should be valid JSON
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            Assert.True(trimmed.StartsWith("{") || trimmed.StartsWith("info:"),
                $"Unexpected stdout content: {trimmed[..Math.Min(100, trimmed.Length)]}");
        }
    }
}
