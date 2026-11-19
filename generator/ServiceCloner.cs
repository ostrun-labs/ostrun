using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OstrunGenerator;

// Shallow git clone of a service at a rev. Shared by the generate path and
// `ostrun validate --catalog`, so both clone the same way and fail the same way.
// The rev "latest" is resolved first to the highest `vX.Y.Z` tag of the
// repository (pre-release tags like `v1.0.0-rc.1` are ignored); any other rev
// is a branch or tag passed to git as-is.
public static class ServiceCloner
{
    public const string LatestRev = "latest";

    public static void Clone(string repository, string rev, string targetDir)
    {
        if (Directory.Exists(targetDir))
        {
            DeleteDirectory(targetDir);
        }

        RunGit(["clone", "--depth", "1", "--branch", ResolveRev(repository, rev), repository, targetDir]);
    }

    public static string ResolveRev(string repository, string rev)
    {
        if (rev != LatestRev)
        {
            return rev;
        }

        var tag = HighestSemverTag(RunGit(["ls-remote", "--tags", repository]));
        return tag ?? throw new GenerationException(
            $"Cannot resolve rev \"latest\" for {repository}: no vX.Y.Z tag found. Tag the service (e.g. v0.1.0) or pin an explicit rev.");
    }

    // Parses `git ls-remote --tags` output and returns the highest `vX.Y.Z`
    // tag, or null if there is none.
    public static string? HighestSemverTag(string lsRemoteOutput)
    {
        string? best = null;
        (int Major, int Minor, int Patch) bestVersion = default;
        foreach (var line in lsRemoteOutput.Split("\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length != 2 || !parts[1].StartsWith("refs/tags/", StringComparison.Ordinal))
            {
                continue;
            }

            // Annotated tags are listed twice; the peeled entry ends in ^{}.
            var tag = parts[1]["refs/tags/".Length..].Replace("^{}", "");
            var match = SemverTag.Match(tag);
            if (!match.Success)
            {
                continue;
            }

            var version = (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
            if (best is null || version.CompareTo(bestVersion) > 0)
            {
                best = tag;
                bestVersion = version;
            }
        }

        return best;
    }

    private static readonly Regex SemverTag = new(@"^v(\d+)\.(\d+)\.(\d+)$", RegexOptions.Compiled);

    public static void DeleteDirectory(string path)
    {
        // git's pack files are read-only on Windows; Directory.Delete throws
        // UnauthorizedAccessException on them unless the attribute is cleared
        // first.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(path, recursive: true);
    }

    private static string RunGit(IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new GenerationException("Could not start git.");
        // Drain both pipes concurrently: a full pipe would otherwise block git.
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = stderrTask.GetAwaiter().GetResult();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new GenerationException($"git {string.Join(' ', arguments)} failed:\n{stderr}");
        }

        return stdout;
    }
}
