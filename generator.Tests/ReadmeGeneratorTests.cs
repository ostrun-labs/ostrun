using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

public class ReadmeGeneratorTests
{
    private static readonly AppConfig App = new() { App = "demo", GatewayPort = 9000 };

    private static ResolvedService Service(string name, bool needsSmtp = false) => new(
        $"ostrun/{name}",
        new ServiceRef { Ref = $"ostrun/{name}" },
        new ServiceManifest
        {
            Provider = "Ostrun",
            Service = name,
            DisplayName = name.ToUpperInvariant(),
            NeedsSmtp = needsSmtp,
            Ports = [new PortEntry { ContainerPort = 8080 }],
        },
        $"https://github.com/ostrun-labs/ostrun-{name}-service");

    private static string Emit(bool prod, params ResolvedService[] services)
    {
        var values = DevValues.Resolve(App, services, eventBusNeeded: false, prod);
        return ReadmeGenerator.Generate(App, services, values, prod);
    }

    [Fact]
    public void Lists_each_service_with_prefix_health_and_repository()
    {
        var readme = Emit(false, Service("auth"), Service("crawler"));

        Assert.Contains("http://localhost:9000/auth/", readme);
        Assert.Contains("http://localhost:9000/auth/health", readme);
        Assert.Contains("http://localhost:9000/crawler/", readme);
        Assert.Contains("https://github.com/ostrun-labs/ostrun-auth-service", readme);
        Assert.Contains("docker compose up --build -d", readme);
    }

    [Fact]
    public void Mentions_mailpit_only_when_the_app_has_one()
    {
        Assert.Contains("http://localhost:8025", Emit(false, Service("messaging", needsSmtp: true)));
        Assert.DoesNotContain("Mailpit", Emit(false, Service("auth")));
        Assert.DoesNotContain("Mailpit", Emit(true, Service("messaging", needsSmtp: true)));
    }

    [Fact]
    public void Warns_about_dev_values_unless_generated_with_prod()
    {
        Assert.Contains("forge tokens", Emit(false, Service("auth")));
        Assert.DoesNotContain("forge tokens", Emit(true, Service("auth")));
    }
}
