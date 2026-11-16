namespace OstrunGenerator;

// Validation of a whole app selection before generation. Runs the per-manifest
// rules (ManifestRules) on each service, then the selection rules that need
// the full app.json: duplicate refs, manifest vs ref, dependencies present.
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

            errors.AddRange(ManifestRules.Check(svc.Manifest, label));

            if (!keys.Add(svc.Ref.Ref))
            {
                errors.Add($"{label}: duplicate service ref selected twice.");
            }

            var refParts = svc.Ref.Ref.Split('/');
            if (svc.Manifest.Provider.ToLowerInvariant() != refParts[0])
            {
                errors.Add($"{label}: manifest provider '{svc.Manifest.Provider}' does not match app.json ref.");
            }

            if (refParts.Length < 2 || svc.Manifest.Service != refParts[1])
            {
                errors.Add($"{label}: manifest service '{svc.Manifest.Service}' does not match app.json ref.");
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
