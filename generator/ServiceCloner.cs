using System.Diagnostics;

namespace OstrunGenerator;

// Shallow git clone of a service at a rev. Shared by the generate path and
// `ostrun validate --catalog`, so both clone the same way and fail the same way.
public static class ServiceCloner
{
    public static void Clone(string repository, string rev, string targetDir)
    {
        if (Directory.Exists(targetDir))
        {
            DeleteDirectory(targetDir);
        }

        RunGit(["clone", "--depth", "1", "--branch", rev, repository, targetDir]);
    }

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

    private static void RunGit(IEnumerable<string> arguments)
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
        // Read stderr before waiting: a full pipe would otherwise block git.
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new GenerationException($"git {string.Join(' ', arguments)} failed:\n{stderr}");
        }
    }
}
