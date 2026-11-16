namespace OstrunGenerator;

// Rules that connect a manifest to /contracts (contracts/README.md §2.1,
// §2.2, §2.4, §2.5): every event in events.published and events.consumed
// follows the name rules and has a contract file, and a published event
// belongs to the manifest's own provider and service. Used by
// `ostrun validate <service-dir> --contracts <dir>`.
public static class ManifestEvents
{
    public sealed record Result(List<string> Errors, List<string> Warnings);

    public static Result Check(ServiceManifest manifest, string contractsDir, string label)
    {
        if (!Directory.Exists(contractsDir))
        {
            throw new GenerationException($"{contractsDir}: contracts directory not found.");
        }

        var irregular = ContractRules.LoadIrregularParticiples(contractsDir);
        var result = new Result([], []);

        foreach (var name in manifest.Events.Published.Distinct())
        {
            if (!CheckName(name, "events.published", contractsDir, irregular, label, result))
            {
                continue;
            }

            var segments = name.Split('.');
            if (segments[0] != manifest.Provider)
            {
                result.Errors.Add($"{label}: published event '{name}' has provider '{segments[0]}', but the manifest's provider is '{manifest.Provider}'.");
            }
            if (!segments[1].Equals(manifest.Service, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"{label}: published event '{name}' has service '{segments[1]}', but the manifest's service is '{manifest.Service}'. A service can't publish under another service's name.");
            }
        }

        foreach (var name in manifest.Events.Consumed.Distinct())
        {
            CheckName(name, "events.consumed", contractsDir, irregular, label, result);
        }

        return result;
    }

    // Name rules plus the contract file lookup. Returns false when the name is
    // malformed, so the caller skips the checks that need 3 segments.
    private static bool CheckName(
        string name, string field, string contractsDir, IReadOnlySet<string> irregular, string label, Result result)
    {
        var (errors, warnings, wellFormed) = ContractRules.CheckEventName(name, irregular);
        result.Errors.AddRange(errors.Select(e => $"{label}: {field}: {e}."));
        result.Warnings.AddRange(warnings.Select(w => $"{label}: {field}: {w}."));
        if (!wellFormed)
        {
            return false;
        }

        // Contract directories are lowercase: {provider}/{service}/{Entity}.v{N}.json
        var segments = name.Split('.');
        var providerDir = segments[0].ToLowerInvariant();
        var serviceDir = segments[1].ToLowerInvariant();
        var dir = Path.Combine(contractsDir, providerDir, serviceDir);
        var hasContract = Directory.Exists(dir)
            && Directory.EnumerateFiles(dir, $"{segments[2]}.v*.json").Any();
        if (!hasContract)
        {
            result.Errors.Add($"{label}: {field}: no contract for '{name}' (expected {providerDir}/{serviceDir}/{segments[2]}.v*.json under {contractsDir}).");
        }

        return true;
    }
}
