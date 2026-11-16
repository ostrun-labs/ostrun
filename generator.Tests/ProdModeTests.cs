using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

// Tests for the values and files the generator emits with and without --prod.
public class ProdModeTests
{
    private static readonly AppConfig App = new() { App = "demo", GatewayPort = 9000 };

    private static ResolvedService Auth() => new(
        "ostrun/auth",
        new ServiceRef { Ref = "ostrun/auth" },
        new ServiceManifest
        {
            Provider = "Ostrun",
            Service = "auth",
            NeedsDb = true,
            Ports = [new PortEntry { ContainerPort = 8080 }],
            Env =
            [
                new EnvVar { Name = "Jwt__SigningKey", Required = true, Generate = "rsa-private-key" },
                new EnvVar { Name = "Jwt__Lifetime", Default = "15" },
                new EnvVar { Name = "ConnectionStrings__Default", Required = true },
            ],
        });

    private static (string Compose, string Env) Emit(bool prod)
    {
        var services = new List<ResolvedService> { Auth() };
        var values = DevValues.Resolve(App, services, eventBusNeeded: true, prod);
        return (
            ComposeGenerator.Generate(App, services, true, values),
            EnvExampleGenerator.Generate(App, services, true, values, prod));
    }

    [Fact]
    public void Prod_requires_every_variable_without_a_manifest_default()
    {
        var (compose, _) = Emit(prod: true);

        Assert.Contains("${AUTH_JWT_SIGNING_KEY:?AUTH_JWT_SIGNING_KEY must be set in .env}", compose);
        Assert.Contains("${AUTH_POSTGRES_PASSWORD:?AUTH_POSTGRES_PASSWORD must be set in .env}", compose);
        Assert.Contains("${RABBITMQ_PASSWORD:?RABBITMQ_PASSWORD must be set in .env}", compose);
    }

    [Fact]
    public void Prod_keeps_manifest_defaults()
    {
        var (compose, _) = Emit(prod: true);

        Assert.Contains(":-15}", compose);
        Assert.Contains("${GATEWAY_PORT:-9000}", compose);
    }

    [Fact]
    public void Prod_env_example_uses_change_me_and_generates_nothing()
    {
        var (_, env) = Emit(prod: true);

        Assert.Contains("RABBITMQ_PASSWORD=change-me", env);
        Assert.Contains("AUTH_JWT_SIGNING_KEY=change-me", env);
        Assert.Contains("AUTH_JWT_LIFETIME=15", env);
    }

    [Fact]
    public void Dev_has_no_required_variable()
    {
        var (compose, env) = Emit(prod: false);

        Assert.DoesNotContain(":?", compose);
        Assert.DoesNotContain("change-me", env);
    }

    private static ResolvedService Messaging() => new(
        "ostrun/messaging",
        new ServiceRef { Ref = "ostrun/messaging" },
        new ServiceManifest
        {
            Provider = "Ostrun",
            Service = "messaging",
            NeedsSmtp = true,
            Ports = [new PortEntry { ContainerPort = 8080 }],
            Env =
            [
                new EnvVar { Name = "Smtp__Host", Required = true, Smtp = "host" },
                new EnvVar { Name = "Smtp__Port", Required = true, Smtp = "port" },
            ],
        });

    private static (string Compose, string Env) EmitWith(ResolvedService svc, bool prod)
    {
        var services = new List<ResolvedService> { svc };
        var values = DevValues.Resolve(App, services, eventBusNeeded: false, prod);
        return (
            ComposeGenerator.Generate(App, services, false, values),
            EnvExampleGenerator.Generate(App, services, false, values, prod));
    }

    [Fact]
    public void Dev_adds_mailpit_for_needsSmtp_and_points_the_entries_at_it()
    {
        var (compose, env) = EmitWith(Messaging(), prod: false);

        Assert.Contains("  mailpit:", compose);
        Assert.Contains("image: axllent/mailpit", compose);
        Assert.Contains("${MAILPIT_PORT:-8025}:8025", compose);
        Assert.Contains("MESSAGING_SMTP_HOST:-mailpit}", compose);
        Assert.Contains("MESSAGING_SMTP_PORT:-1025}", compose);
        Assert.Contains("MAILPIT_PORT=8025", env);
    }

    [Fact]
    public void Prod_has_no_mailpit_and_requires_the_smtp_entries()
    {
        var (compose, _) = EmitWith(Messaging(), prod: true);

        Assert.DoesNotContain("mailpit", compose);
        Assert.Contains("MESSAGING_SMTP_HOST:?", compose);
    }

    [Fact]
    public void No_mailpit_without_needsSmtp()
    {
        var (compose, env) = Emit(prod: false);

        Assert.DoesNotContain("mailpit", compose);
        Assert.DoesNotContain("MAILPIT_PORT", env);
    }
}
