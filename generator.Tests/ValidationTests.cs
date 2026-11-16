using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

// Tests for the manifest rules (ManifestRules.Check) and the selection rules
// (Validation.Validate) the generator enforces before emitting anything.
public class ValidationTests
{
    private static ServiceManifest Valid(
        string provider = "Ostrun",
        string service = "auth",
        string version = "1.0.0",
        string displayName = "Ostrun Auth",
        string description = "Auth service.",
        List<PortEntry>? ports = null,
        HealthCheck? healthCheck = null,
        List<EnvVar>? env = null,
        List<string>? dependencies = null) => new()
    {
        Provider = provider,
        Service = service,
        Version = version,
        DisplayName = displayName,
        Description = description,
        Ports = ports ?? [new PortEntry { ContainerPort = 8080 }],
        HealthCheck = healthCheck ?? new HealthCheck(),
        Env = env ?? [],
        Dependencies = dependencies ?? [],
    };

    private static ResolvedService Service(ServiceManifest manifest, string key = "ostrun/auth") =>
        new(key, new ServiceRef { Ref = key }, manifest);

    private static List<string> Check(ServiceManifest manifest) => ManifestRules.Check(manifest, "svc");

    [Fact]
    public void Check_accepts_a_valid_manifest()
    {
        Assert.Empty(Check(Valid()));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("service")]
    [InlineData("version")]
    [InlineData("displayName")]
    [InlineData("description")]
    public void Check_rejects_a_missing_required_field(string field)
    {
        var manifest = field switch
        {
            "provider" => Valid(provider: ""),
            "service" => Valid(service: ""),
            "version" => Valid(version: ""),
            "displayName" => Valid(displayName: ""),
            _ => Valid(description: ""),
        };

        Assert.Contains(Check(manifest), e => e.Contains($"required field '{field}'"));
    }

    [Fact]
    public void Check_rejects_a_provider_that_is_not_PascalCase()
    {
        Assert.Contains(Check(Valid(provider: "ostrun")), e => e.Contains("must be PascalCase"));
    }

    [Theory]
    [InlineData("Auth")]
    [InlineData("order_fulfillment")]
    public void Check_rejects_a_service_that_is_not_lowercase(string service)
    {
        Assert.Contains(Check(Valid(service: service)), e => e.Contains("must be lowercase"));
    }

    [Fact]
    public void Check_accepts_a_hyphenated_service()
    {
        Assert.Empty(Check(Valid(service: "order-fulfillment")));
    }

    [Fact]
    public void Check_rejects_two_ports()
    {
        var ports = new List<PortEntry>
        {
            new() { ContainerPort = 8080 },
            new() { ContainerPort = 9090 },
        };

        Assert.Contains(Check(Valid(ports: ports)), e => e.Contains("exactly one ports[] entry"));
    }

    [Fact]
    public void Check_rejects_a_non_http_port()
    {
        var ports = new List<PortEntry> { new() { ContainerPort = 8080, Protocol = "grpc" } };

        Assert.Contains(Check(Valid(ports: ports)), e => e.Contains("protocol must be 'http'"));
    }

    [Fact]
    public void Check_rejects_a_non_GET_health_check()
    {
        var healthCheck = new HealthCheck { Method = "POST" };

        Assert.Contains(Check(Valid(healthCheck: healthCheck)), e => e.Contains("healthCheck.method must be 'GET'"));
    }

    [Fact]
    public void Check_rejects_env_entries_that_map_to_the_same_variable()
    {
        var env = new List<EnvVar>
        {
            new() { Name = "Jwt__Issuer" },
            new() { Name = "AUTH_JWT_ISSUER" },
        };

        Assert.Contains(Check(Valid(env: env)), e => e.Contains("which another entry already uses"));
    }

    [Fact]
    public void Check_lists_every_error()
    {
        var errors = Check(Valid(provider: "ostrun", healthCheck: new HealthCheck { Method = "POST" }));

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Validate_accepts_a_valid_selection()
    {
        Validation.Validate([Service(Valid())]);
    }

    [Fact]
    public void Validate_rejects_a_duplicate_ref()
    {
        var ex = Assert.Throws<GenerationException>(() => Validation.Validate([Service(Valid()), Service(Valid())]));

        Assert.Contains("duplicate service ref", ex.Message);
    }

    [Fact]
    public void Validate_rejects_a_provider_that_does_not_match_the_ref()
    {
        var ex = Assert.Throws<GenerationException>(() => Validation.Validate([Service(Valid(provider: "Other"))]));

        Assert.Contains("manifest provider 'Other' does not match", ex.Message);
    }

    [Fact]
    public void Validate_rejects_a_service_that_does_not_match_the_ref()
    {
        var ex = Assert.Throws<GenerationException>(() => Validation.Validate([Service(Valid(service: "other"))]));

        Assert.Contains("manifest service 'other' does not match", ex.Message);
    }

    [Fact]
    public void Validate_rejects_a_missing_dependency()
    {
        var manifest = Valid(dependencies: ["ostrun/messaging"]);

        var ex = Assert.Throws<GenerationException>(() => Validation.Validate([Service(manifest)]));

        Assert.Contains("which is not among the selected services", ex.Message);
    }
}
