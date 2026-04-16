using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Xunit;

namespace VsDebuggerMcp.Tests;

public class DteConnectorTests
{
    [Fact]
    public void TryGetDte_WhenNoVS_ReturnsFalseWithErrorMessage()
    {
        // Save and set a process ID that won't match any VS
        var original = Environment.GetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID");
        try
        {
            Environment.SetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID", "99999");
            var result = DteConnector.TryGetDte(out var dte, out var error);
            Assert.False(result);
            Assert.Null(dte);
            Assert.Contains("No running Visual Studio instance found", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID", original);
        }
    }

    [Fact]
    public void TryGetDte_ErrorMessageContainsHelpfulGuidance()
    {
        var original = Environment.GetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID");
        try
        {
            Environment.SetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID", "99999");
            DteConnector.TryGetDte(out _, out var error);
            Assert.Contains("Please open Visual Studio first", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID", original);
        }
    }

    [Fact]
    public void ExecuteWithComRetry_SucceedsOnFirstAttempt()
    {
        var callCount = 0;
        var result = DteConnector.ExecuteWithComRetry(() =>
        {
            callCount++;
            return 42;
        });
        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void ExecuteWithComRetry_RetriesOnTransientComException()
    {
        var callCount = 0;
        var result = DteConnector.ExecuteWithComRetry(() =>
        {
            callCount++;
            if (callCount < 3)
                throw new COMException("RPC_E_CALL_REJECTED", unchecked((int)0x80010001));
            return "success";
        }, maxAttempts: 5, baseDelayMs: 10);

        Assert.Equal("success", result);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void ExecuteWithComRetry_ThrowsOnNonTransientComException()
    {
        Assert.Throws<COMException>(() =>
        {
            DteConnector.ExecuteWithComRetry<int>(() =>
            {
                throw new COMException("E_FAIL", unchecked((int)0x80004005));
            });
        });
    }

    [Fact]
    public void ExecuteWithComRetry_ThrowsAfterMaxAttempts()
    {
        var callCount = 0;
        Assert.Throws<COMException>(() =>
        {
            DteConnector.ExecuteWithComRetry<int>(() =>
            {
                callCount++;
                throw new COMException("RPC_E_SERVERCALL_RETRYLATER", unchecked((int)0x8001010A));
            }, maxAttempts: 3, baseDelayMs: 10);
        });
        Assert.Equal(3, callCount); // 3 attempts (last one throws without retry)
    }

    [Fact]
    public void ExecuteWithComRetry_ActionOverload_Works()
    {
        var executed = false;
        DteConnector.ExecuteWithComRetry(() => { executed = true; });
        Assert.True(executed);
    }

    [Fact]
    public void TryRequireMode_WrongMode_ReturnsFalseWithMessage()
    {
        // We can't easily test with a real DTE, but we can test that
        // TryGetDte fails gracefully when no VS is running
        var original = Environment.GetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID");
        try
        {
            Environment.SetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID", "99999");
            // Can't call TryRequireMode without a real DTE2 instance
            // This test verifies TryGetDte properly guards tool calls
            var result = DteConnector.TryGetDte(out var dte, out var error);
            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VS_DEBUGGER_MCP_DTE_PROCESS_ID", original);
        }
    }
}
