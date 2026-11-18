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

app.MapGet("/gateway/health", () => Results.Ok(new { status = "Healthy" }));
app.MapReverseProxy();

app.Run();
