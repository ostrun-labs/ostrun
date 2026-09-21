using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// The composed app's routes/clusters live outside the image, so one gateway
// image serves every generated app. Missing or malformed file is a startup
// crash, not a gateway that boots and silently proxies nothing.
builder.Configuration.AddJsonFile("routes.generated.json", optional: false, reloadOnChange: true);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console(new CompactJsonFormatter()));

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
});

// Event-sink endpoints (a service's HTTP binding for a consumed event) are
// internal by construction: reachable only service-to-service on the compose
// network, never from outside the app. Blocking here, ahead of YARP routing,
// means the response is a plain 404 instead of a YARP passthrough that would
// reveal the destination exists. Prefixes come from the generated routes file
// (Ostrun:InternalPathPrefixes), derived from each manifest's events.consumed.
var internalPrefixes = app.Configuration
    .GetSection("Ostrun:InternalPathPrefixes")
    .Get<string[]>() ?? [];

var guardLogger = app.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("OstrunGateway.InternalPathGuard");

app.Use(async (context, next) =>
{
    foreach (var prefix in internalPrefixes)
    {
        if (context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
        {
            guardLogger.LogWarning("Blocked external request to internal path {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }

    await next();
});

app.MapGet("/gateway/health", () => Results.Ok(new { status = "Healthy" }));
app.MapReverseProxy();

app.Run();
