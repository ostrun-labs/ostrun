using System.Text.Json.Serialization;

namespace OstrunGenerator;

// Shape of the hand-written app.json input — the one file a human writes.
public sealed class AppConfig
{
    [JsonPropertyName("app")]
    public string App { get; init; } = "";

    [JsonPropertyName("gatewayPort")]
    public int GatewayPort { get; init; } = 8080;

    [JsonPropertyName("services")]
    public List<ServiceRef> Services { get; init; } = [];
}

public sealed class ServiceRef
{
    // "{provider}/{service}", e.g. "ostrun/auth" — must match the manifest's
    // own provider/service fields once cloned.
    [JsonPropertyName("ref")]
    public string Ref { get; init; } = "";

    [JsonPropertyName("repository")]
    public string Repository { get; init; } = "";

    [JsonPropertyName("rev")]
    public string Rev { get; init; } = "main";
}

// Shape of ostrun.manifest.json — see ostrun/contracts/service-manifest.md.
public sealed class ServiceManifest
{
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "";

    [JsonPropertyName("service")]
    public string Service { get; init; } = "";

    [JsonPropertyName("basePath")]
    public string? BasePath { get; init; }

    [JsonPropertyName("needsDb")]
    public bool NeedsDb { get; init; }

    [JsonPropertyName("events")]
    public EventsSection Events { get; init; } = new();

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; init; } = [];

    [JsonPropertyName("env")]
    public List<EnvVar> Env { get; init; } = [];

    [JsonPropertyName("ports")]
    public List<PortEntry> Ports { get; init; } = [];

    [JsonPropertyName("healthCheck")]
    public HealthCheck HealthCheck { get; init; } = new();
}

public sealed class EventsSection
{
    [JsonPropertyName("published")]
    public List<string> Published { get; init; } = [];

    [JsonPropertyName("consumed")]
    public List<string> Consumed { get; init; } = [];
}

public sealed class EnvVar
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("default")]
    public string? Default { get; init; }
}

public sealed class PortEntry
{
    [JsonPropertyName("containerPort")]
    public int ContainerPort { get; init; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = "http";
}

public sealed class HealthCheck
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "/health";

    [JsonPropertyName("method")]
    public string Method { get; init; } = "GET";
}

// A selected service, resolved: its ref-derived key plus its parsed manifest.
// "Key" is what every generated artifact keys off of — {provider}/{service}
// lower-cased and slashed, e.g. "ostrun/auth" -> compose service "auth-api".
public sealed record ResolvedService(string Key, ServiceRef Ref, ServiceManifest Manifest)
{
    // The manifest's own `service` field is the naming atom used everywhere
    // in generated artifacts (compose service/hostnames, env prefix, gateway
    // route prefix) — not the full "{provider}/{service}" ref, which is only
    // used to resolve `dependencies` entries.
    public string ServiceKey => Manifest.Service;
}
