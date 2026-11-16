using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

// Tests for Validation.Validate, the manifest rules the generator enforces
// before emitting anything.
public class ValidationTests
{
    private static ResolvedService Service(ServiceManifest manifest) =>
        new("ostrun/auth", new ServiceRef { Ref = "ostrun/auth" }, manifest);

    private static ServiceManifest Manifest(params PortEntry[] ports) => new()
    {
        Provider = "ostrun",
        Service = "auth",
        Ports = [.. ports],
    };

    [Fact]
    public void Validate_accepts_a_single_http_port()
    {
        Validation.Validate([Service(Manifest(new PortEntry { ContainerPort = 8080 }))]);
    }

    [Fact]
    public void Validate_rejects_two_ports()
    {
        var manifest = Manifest(
            new PortEntry { ContainerPort = 8080 },
            new PortEntry { ContainerPort = 9090 });

        var ex = Assert.Throws<GenerationException>(() => Validation.Validate([Service(manifest)]));

        Assert.Contains("exactly one ports[] entry", ex.Message);
    }
}
