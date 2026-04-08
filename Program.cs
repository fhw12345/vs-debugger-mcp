using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using VsDebuggerMcp.Tools;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5050");

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<BuildTools>()
    .WithTools<DebugLifecycleTools>()
    .WithTools<BreakpointTools>()
    .WithTools<StepTools>()
    .WithTools<InspectTools>()
    .WithTools<ExceptionTools>()
    .WithTools<OutputTools>()
    .WithTools<WatchTools>();

var app = builder.Build();
app.MapMcp();

Console.WriteLine("VS Debugger MCP Server running on http://localhost:5050/sse");
Console.WriteLine("Register with: claude mcp add --transport sse vs-debugger http://localhost:5050/sse");
Console.WriteLine("Press Ctrl+C to stop.");

await app.RunAsync();
