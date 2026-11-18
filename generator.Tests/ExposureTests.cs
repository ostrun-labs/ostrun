using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

// Exposure: an internal service gets no gateway route and isn't awaited by the gateway.
public class ExposureTests
{
    private static ResolvedService Service(string name, string? exposure) => new(
        $"ostrun/{name}",
        new ServiceRef { Ref = $"ostrun/{name}" },
        new ServiceManifest
        {
            Provider = "Ostrun",
            Service = name,
            Version = "1.0.0",
            DisplayName = name,
            Description = name,
            Exposure = exposure,
            Ports = [new PortEntry { ContainerPort = 8082 }],
        });

    private static readonly ResolvedService[] Both = [Service("auth", "public"), Service("crawler", "internal")];

    [Fact]
    public void Routes_skip_internal_services()
    {
        var routes = RoutesGenerator.Generate(Both);

        Assert.Contains("auth-route", routes);
        Assert.Contains("auth-health", routes);
        Assert.DoesNotContain("crawler", routes);
    }

    [Fact]
    public void Missing_exposure_is_routed_like_public()
    {
        Assert.Contains("auth-route", RoutesGenerator.Generate([Service("auth", null)]));
    }

    [Fact]
    public void Gateway_depends_only_on_public_services()
    {
        var app = new AppConfig { App = "demo" };
        var values = DevValues.Resolve(app, Both, eventBusNeeded: false, prod: false);
        var compose = ComposeGenerator.Generate(app, Both, false, values);

        Assert.Contains("  crawler-api:", compose);
        Assert.Contains("      - auth-api", compose);
        Assert.DoesNotContain("      - crawler-api", compose);
    }

    [Fact]
    public void Gateway_has_no_depends_on_when_every_service_is_internal()
    {
        var only = new[] { Service("crawler", "internal") };
        var app = new AppConfig { App = "demo" };
        var values = DevValues.Resolve(app, only, eventBusNeeded: false, prod: false);
        var compose = ComposeGenerator.Generate(app, only, false, values);

        Assert.DoesNotContain("depends_on", compose);
    }

    [Fact]
    public void Unknown_exposure_is_a_validation_error()
    {
        var errors = ManifestRules.Check(Service("auth", "private").Manifest, "ostrun/auth");

        Assert.Contains(errors, e => e.Contains("exposure 'private'"));
        Assert.DoesNotContain(ManifestRules.Check(Service("auth", "internal").Manifest, "x"), e => e.Contains("exposure"));
        Assert.DoesNotContain(ManifestRules.Check(Service("auth", null).Manifest, "x"), e => e.Contains("exposure"));
    }

    [Fact]
    public void Readme_gives_the_in_app_address_of_internal_services()
    {
        var app = new AppConfig { App = "demo" };
        var values = DevValues.Resolve(app, Both, eventBusNeeded: false, prod: false);
        var readme = ReadmeGenerator.Generate(app, Both, values, prod: false);

        Assert.Contains("http://crawler-api:8082", readme);
        Assert.DoesNotContain("localhost:8080/crawler", readme);
        Assert.Contains("localhost:8080/auth/", readme);
    }
}
