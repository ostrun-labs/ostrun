using System.Text.RegularExpressions;

namespace OstrunGenerator;

// Per-manifest rules: everything that can be checked on one
// ostrun.manifest.json alone, without an app.json. Used by the generate path
// (through Validation) and by `ostrun validate <service-dir>`.
public static partial class ManifestRules
{
    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalCase();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ServiceName();

    // Returns every violation, prefixed with `label`; empty when the manifest is valid.
    public static List<string> Check(ServiceManifest manifest, string label)
    {
        var errors = new List<string>();

        var required = new (string Field, string Value)[]
        {
            ("provider", manifest.Provider),
            ("service", manifest.Service),
            ("version", manifest.Version),
            ("displayName", manifest.DisplayName),
            ("description", manifest.Description),
        };
        foreach (var (field, value) in required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{label}: required field '{field}' is missing or empty.");
            }
        }

        if (manifest.Provider != "" && !PascalCase().IsMatch(manifest.Provider))
        {
            errors.Add($"{label}: provider '{manifest.Provider}' must be PascalCase.");
        }

        if (manifest.Service != "" && !ServiceName().IsMatch(manifest.Service))
        {
            errors.Add($"{label}: service '{manifest.Service}' must be lowercase (a-z, 0-9 and hyphens).");
        }

        if (manifest.Ports.Count != 1)
        {
            errors.Add($"{label}: manifest must declare exactly one ports[] entry (got {manifest.Ports.Count}) — multi-port services are unsupported by this generator.");
        }
        else if (manifest.Ports[0].Protocol != "http")
        {
            errors.Add($"{label}: ports[0].protocol must be 'http', got '{manifest.Ports[0].Protocol}'.");
        }

        if (manifest.HealthCheck.Method != "GET")
        {
            errors.Add($"{label}: healthCheck.method must be 'GET' (YARP active health checks are GET-only), got '{manifest.HealthCheck.Method}'.");
        }

        var envVars = new HashSet<string>();
        foreach (var env in manifest.Env)
        {
            if (env.Smtp is not (null or "host" or "port"))
            {
                errors.Add($"{label}: env entry '{env.Name}' has smtp '{env.Smtp}', expected 'host' or 'port'.");
            }

            var variable = Naming.EnvVar(manifest.Service, env.Name);
            if (!envVars.Add(variable))
            {
                errors.Add($"{label}: env entry '{env.Name}' maps to .env variable '{variable}', which another entry already uses.");
            }
        }

        // `exposure` (public | internal) isn't checked yet: the "Internal vs
        // public services at the gateway" epic hasn't landed. Add the rule there.

        return errors;
    }
}
