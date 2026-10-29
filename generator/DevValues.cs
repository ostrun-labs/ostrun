using System.Security.Cryptography;
using System.Text;

namespace OstrunGenerator;

// Dev values for every variable the generated app reads from .env, computed
// once per generation so docker-compose.yml (as `${NAME:-value}` defaults) and
// .env.example show exactly the same thing. Everything comes from the
// manifests: no service is special-cased by name, and no secret is hardcoded,
// so two generated apps never share one.
public static class DevValues
{
    public const string Placeholder = "change-me";

    // Same order for env entries in every manifest:
    // `generate`, then `default`, then "change-me" if required, else empty.
    public static IReadOnlyDictionary<string, string> Resolve(AppConfig app, IReadOnlyList<ResolvedService> services, bool eventBusNeeded)
    {
        var values = new Dictionary<string, string>();

        values["GATEWAY_PORT"] = app.GatewayPort.ToString();

        if (eventBusNeeded)
        {
            values["RABBITMQ_USER"] = "ostrun";
            values["RABBITMQ_PASSWORD"] = RandomPassword();
        }

        foreach (var svc in services)
        {
            var key = svc.ServiceKey;
            var prefix = Naming.EnvPrefix(key);

            if (svc.Manifest.NeedsDb)
            {
                values[$"{prefix}POSTGRES_USER"] = $"ostrun_{key}";
                values[$"{prefix}POSTGRES_PASSWORD"] = RandomPassword();
                values[$"{prefix}POSTGRES_DB"] = $"ostrun_{key}";
            }

            foreach (var env in svc.Manifest.Env)
            {
                if (IsDerived(env.Name))
                {
                    continue;
                }

                values[Naming.EnvVar(key, env.Name)] = ResolveEntry(env, app);
            }
        }

        return values;
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
