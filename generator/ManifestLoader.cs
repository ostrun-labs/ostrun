using System.Text.Json;

namespace OstrunGenerator;

// Reads and deserializes one ostrun.manifest.json. Shared by the generate
// path (after cloning a service) and `ostrun validate` (a service on disk),
// so both fail the same way on a missing or unparseable file.
public static class ManifestLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ServiceManifest Load(string manifestPath, string label)
    {
        if (!File.Exists(manifestPath))
        {
            throw new GenerationException($"{label}: no ostrun.manifest.json found at {manifestPath}.");
        }

        try
        {
            return JsonSerializer.Deserialize<ServiceManifest>(File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new GenerationException($"{label}: could not parse manifest.");
        }
        catch (JsonException ex)
        {
            throw new GenerationException($"{label}: could not parse manifest: {ex.Message}");
        }
    }
}
