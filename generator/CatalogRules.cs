using System.Text.Json;
using System.Text.RegularExpressions;

namespace OstrunGenerator;

// Rules on catalog.json that need no network: its structure (valid JSON, each
// entry has ref/repository/rev, ref format, no duplicate ref, https repository)
// and the provider collision across all manifests (contracts/README.md §2.3).
// The per-entry clone and manifest checks live in Program.cs, run at the rev the
// generator would use (rev "latest" resolved to the highest vX.Y.Z tag)
// (`ostrun validate --catalog <catalog.json> --contracts <dir>`).
public static partial class CatalogRules
{
    [GeneratedRegex("^[a-z0-9-]+/[a-z0-9-]+$")]
    private static partial Regex RefFormat();

    // Returns every structural violation; empty when the catalog is well formed.
    public static List<string> CheckStructure(string json)
    {
        var errors = new List<string>();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            errors.Add($"not valid JSON: {ex.Message}");
            return errors;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("services", out var services)
                || services.ValueKind != JsonValueKind.Array)
            {
                errors.Add("'services' must be an array.");
                return errors;
            }

            var seen = new HashSet<string>();
            var i = 0;
            foreach (var entry in services.EnumerateArray())
            {
                var at = $"services[{i++}]";
                var serviceRef = StringField(entry, "ref");
                var repository = StringField(entry, "repository");
                var rev = StringField(entry, "rev");

                if (string.IsNullOrEmpty(serviceRef) || string.IsNullOrEmpty(repository) || string.IsNullOrEmpty(rev))
                {
                    errors.Add($"{at} is missing ref, repository, or rev.");
                }

                if (!string.IsNullOrEmpty(serviceRef))
                {
                    if (!RefFormat().IsMatch(serviceRef))
                    {
                        errors.Add($"{at}.ref '{serviceRef}' must be lowercase '{{provider}}/{{service}}'.");
                    }
                    if (!seen.Add(serviceRef))
                    {
                        errors.Add($"duplicate ref '{serviceRef}'.");
                    }
                }

                if (!string.IsNullOrEmpty(repository) && !repository.StartsWith("https://", StringComparison.Ordinal))
                {
                    errors.Add($"{at}.repository '{repository}' must be an https URL.");
                }
            }
        }

        return errors;
    }

    // Two providers equal case-insensitively but not exactly (Acme vs ACME) collide.
    // The same exact provider on several services is fine (Ostrun on auth, messaging...).
    public static List<string> CheckProviderCollisions(IEnumerable<(string Label, string Provider)> manifests)
    {
        var errors = new List<string>();
        var groups = manifests
            .Where(m => m.Provider != "")
            .GroupBy(m => m.Provider, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var spellings = group.Select(m => m.Provider).Distinct().ToList();
            if (spellings.Count > 1)
            {
                var detail = string.Join(", ", group.Select(m => $"{m.Label} '{m.Provider}'"));
                errors.Add($"provider collision: {detail}. Providers are compared case-insensitively; the maintainer decides how to resolve it.");
            }
        }

        return errors;
    }

    private static string? StringField(JsonElement entry, string name) =>
        entry.ValueKind == JsonValueKind.Object
            && entry.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
}
