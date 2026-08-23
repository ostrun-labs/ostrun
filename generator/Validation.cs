namespace OstrunGenerator;

public static class Validation
{
    // Fails loudly and stops generation rather than emitting a broken app —
    // per contracts/service-manifest.md, the generator's contract is the
    // manifest; anything a service needs that isn't declared there is a
    // manifest gap, not something to silently work around.
    public static void Validate(IReadOnlyList<ResolvedService> services)
    {
        var errors = new List<string>();
        var keys = new HashSet<string>();

        foreach (var svc in services)
        {
            var label = svc.Ref.Ref;

            if (!keys.Add(svc.Ref.Ref))
            {
                errors.Add($"{label}: duplicate service ref selected twice.");
            }

            if (svc.Manifest.Provider.ToLowerInvariant() != svc.Ref.Ref.Split('/')[0])
            {
                errors.Add($"{label}: manifest provider '{svc.Manifest.Provider}' does not match app.json ref.");
            }

            if (svc.Manifest.Ports.Count != 1)
            {
                errors.Add($"{label}: manifest must declare exactly one ports[] entry (got {svc.Manifest.Ports.Count}) — multi-port services are unsupported by this generator.");
            }
            else if (svc.Manifest.Ports[0].Protocol != "http")
            {
                errors.Add($"{label}: ports[0].protocol must be 'http', got '{svc.Manifest.Ports[0].Protocol}'.");
            }

            if (svc.Manifest.HealthCheck.Method != "GET")
            {
                errors.Add($"{label}: healthCheck.method must be 'GET' (YARP active health checks are GET-only), got '{svc.Manifest.HealthCheck.Method}'.");
            }

            foreach (var dep in svc.Manifest.Dependencies)
            {
                if (!services.Any(s => s.Ref.Ref == dep))
                {
                    errors.Add($"{label}: depends on '{dep}', which is not among the selected services.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new GenerationException("Manifest validation failed:\n" + string.Join('\n', errors.Select(e => $"  - {e}")));
        }
    }
}

public sealed class GenerationException(string message) : Exception(message);
