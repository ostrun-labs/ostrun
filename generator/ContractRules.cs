using System.Text.Json;
using System.Text.RegularExpressions;

namespace OstrunGenerator;

// Rules for the event contract files under /contracts (contracts/README.md
// §2.1, §2.4, §2.5, §2.6): the ones that need only the contract itself, not
// a manifest. Used by `ostrun validate --contracts <dir>`.
public static partial class ContractRules
{
    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$")]
    private static partial Regex EventName();

    [GeneratedRegex("(Create|Update|Delete|Add|Remove|Set|Get|Send|Make|Register|Cancel|Renew|Pay|Login|Logout)$")]
    private static partial Regex Command();

    [GeneratedRegex(@"^(?<entity>.+)\.v(?<version>\d+)\.json$")]
    private static partial Regex FileName();

    public sealed record Finding(string File, string Message);

    public sealed record Result(List<Finding> Errors, List<Finding> Warnings);

    // Checks every {provider}/{service}/{Entity}{PastParticiple}.v{N}.json under
    // `contractsDir`. `_verbs/` holds the irregular participle list, not contracts.
    public static Result CheckDirectory(string contractsDir)
    {
        if (!Directory.Exists(contractsDir))
        {
            throw new GenerationException($"{contractsDir}: contracts directory not found.");
        }

        var irregularPath = Path.Combine(contractsDir, "_verbs", "irregular-participles.txt");
        var irregular = File.Exists(irregularPath)
            ? File.ReadAllLines(irregularPath).Select(l => l.Trim()).Where(l => l != "").ToHashSet()
            : [];

        var result = new Result([], []);
        var files = Directory.EnumerateFiles(contractsDir, "*.v*.json", SearchOption.AllDirectories)
            .Select(f => (Full: f, Relative: Path.GetRelativePath(contractsDir, f).Replace('\\', '/')))
            .Where(f => !f.Relative.StartsWith("_verbs/"))
            .OrderBy(f => f.Relative, StringComparer.Ordinal);

        foreach (var (full, relative) in files)
        {
            var (errors, warnings) = CheckFile(relative, File.ReadAllText(full), irregular);
            var display = Path.Combine(contractsDir, relative).Replace('\\', '/');
            result.Errors.AddRange(errors.Select(e => new Finding(display, e)));
            result.Warnings.AddRange(warnings.Select(w => new Finding(display, w)));
        }

        return result;
    }

    // `relativePath` is the file's path under the contracts dir, '/'-separated.
    public static (List<string> Errors, List<string> Warnings) CheckFile(
        string relativePath, string json, IReadOnlySet<string> irregularParticiples)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException)
        {
            errors.Add("not valid JSON");
            return (errors, warnings);
        }

        var title = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString() ?? "" : "";
        var version = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("version", out var v)
            ? v.ToString() : "";
        if (title == "" || version == "")
        {
            errors.Add("missing title or version field");
            return (errors, warnings);
        }

        var parts = relativePath.Split('/');
        var fileMatch = FileName().Match(parts[^1]);
        if (parts.Length != 3 || !fileMatch.Success)
        {
            errors.Add($"path '{relativePath}' must be {{provider}}/{{service}}/{{Entity}}{{PastParticiple}}.v{{N}}.json");
            return (errors, warnings);
        }

        if (fileMatch.Groups["version"].Value != version)
        {
            errors.Add($"filename version 'v{fileMatch.Groups["version"].Value}' does not match schema version '{version}'");
        }

        // §2.1: past this point the title has exactly 3 segments.
        if (!EventName().IsMatch(title))
        {
            errors.Add($"title '{title}' must be {{Provider}}.{{Service}}.{{Entity}}{{PastParticiple}}, 3 PascalCase segments");
            return (errors, warnings);
        }

        var segments = title.Split('.');
        if (!segments[0].Equals(parts[0], StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"title provider '{segments[0]}' does not match directory '{parts[0]}'");
        }
        if (!segments[1].Equals(parts[1], StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"title service '{segments[1]}' does not match directory '{parts[1]}'");
        }
        if (segments[2] != fileMatch.Groups["entity"].Value)
        {
            errors.Add($"title entity '{segments[2]}' does not match filename '{fileMatch.Groups["entity"].Value}'");
        }

        var last = segments[2];
        var command = Command().Match(last);
        if (command.Success)
        {
            errors.Add($"'{title}' ends with the verb '{command.Value}', which reads as a command; name the fact in the past participle");
        }
        else if (!last.EndsWith("ed") && !irregularParticiples.Any(p => last.EndsWith(p)))
        {
            warnings.Add($"'{last}' doesn't look like a known past participle; if it is one, add it to contracts/_verbs/irregular-participles.txt");
        }

        return (errors, warnings);
    }
}
