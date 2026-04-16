using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using VsDebuggerMcp.Tools;

if (args.Contains("--stdio"))
{
    // Stdio transport: server runs as a child process, communicates via stdin/stdout
    var builder = Host.CreateApplicationBuilder(args);
    RegisterTools(builder.Services.AddMcpServer().WithStdioServerTransport());
    await builder.Build().RunAsync();
}
else
{
    // HTTP/SSE transport: standalone server on port 5050
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseUrls("http://localhost:5050");
    RegisterTools(builder.Services.AddMcpServer().WithHttpTransport());

    var app = builder.Build();
    app.MapMcp();

    Console.WriteLine("VS Debugger MCP Server running on http://localhost:5050/sse");
    Console.WriteLine("Register with: claude mcp add --transport sse vs-debugger http://localhost:5050/sse");
    Console.WriteLine("Press Ctrl+C to stop.");

    await app.RunAsync();
}

static void RegisterTools(IMcpServerBuilder builder) => builder
    .WithTools<BuildTools>()
    .WithTools<DebugLifecycleTools>()
    .WithTools<BreakpointTools>()
    .WithTools<StepTools>()
    .WithTools<InspectTools>()
    .WithTools<ExceptionTools>()
    .WithTools<OutputTools>()
    .WithTools<WatchTools>();
