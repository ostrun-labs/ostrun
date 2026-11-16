using System.Security.Cryptography;
using System.Text;

namespace OstrunGenerator;

// Resolved variable values plus the names that must come from .env (prod).
public sealed record Values(IReadOnlyDictionary<string, string> Items, IReadOnlySet<string> MustSet);

// Dev values for every variable the generated app reads from .env, computed
// once per generation so docker-compose.yml (as `${NAME:-value}` defaults) and
// .env.example show exactly the same thing. Everything comes from the
// manifests: no service is special-cased by name, and no secret is hardcoded,
// so two generated apps never share one.
//
// With prod, nothing is generated: a variable without a manifest `default`
// gets "change-me" in .env.example and lands in MustSet, which the compose
// file writes as `${NAME:?...}` so Compose refuses to start without it.
public static class DevValues
{
    public const string Placeholder = "change-me";

    // Same order for env entries in every manifest:
    // `generate`, then `default`, then "change-me" if required, else empty.
    public static Values Resolve(
        AppConfig app, IReadOnlyList<ResolvedService> services, bool eventBusNeeded, bool prod = false)
    {
        var values = new Dictionary<string, string>();
        var mustSet = new HashSet<string>();

        void Unset(string name)
        {
            values[name] = Placeholder;
            mustSet.Add(name);
        }

        values["GATEWAY_PORT"] = app.GatewayPort.ToString();

        if (eventBusNeeded)
        {
            if (prod)
            {
                Unset("RABBITMQ_USER");
                Unset("RABBITMQ_PASSWORD");
            }
            else
            {
                values["RABBITMQ_USER"] = "ostrun";
                values["RABBITMQ_PASSWORD"] = RandomPassword();
            }
        }

        foreach (var svc in services)
        {
            var key = svc.ServiceKey;
            var prefix = Naming.EnvPrefix(key);

            if (svc.Manifest.NeedsDb)
            {
                if (prod)
                {
                    Unset($"{prefix}POSTGRES_USER");
                    Unset($"{prefix}POSTGRES_PASSWORD");
                    Unset($"{prefix}POSTGRES_DB");
                }
                else
                {
                    values[$"{prefix}POSTGRES_USER"] = $"ostrun_{key}";
                    values[$"{prefix}POSTGRES_PASSWORD"] = RandomPassword();
                    values[$"{prefix}POSTGRES_DB"] = $"ostrun_{key}";
                }
            }

            foreach (var env in svc.Manifest.Env)
            {
                if (IsDerived(env.Name))
                {
                    continue;
                }

                var name = Naming.EnvVar(key, env.Name);
                if (!prod)
                {
                    values[name] = ResolveEntry(env, app);
                }
                else if (env.Default is not null)
                {
                    values[name] = env.Default;
                }
                else
                {
                    Unset(name);
                }
            }
        }

        return new Values(values, mustSet);
    }

    // Topology the generator inlines in docker-compose.yml, never a variable.
    public static bool IsDerived(string envName) =>
        envName is "ASPNETCORE_URLS"
        || envName.StartsWith("ConnectionStrings__", StringComparison.Ordinal)
        || envName.StartsWith("RabbitMq__", StringComparison.Ordinal);

    public static string ResolveEntry(EnvVar env, AppConfig app) =>
        env.Generate switch
        {
            "password" => RandomPassword(),
            "rsa-private-key" => RsaPrivateKeyBase64(),
            "app-name" => app.App,
            _ => env.Default ?? (env.Required ? Placeholder : ""),
        };

    private static string RandomPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    // PEM text, base64-encoded: what Auth's Jwt__SigningKey expects.
    private static string RsaPrivateKeyBase64()
    {
        using var rsa = RSA.Create(2048);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem()));
    }
}
