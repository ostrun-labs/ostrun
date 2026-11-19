using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

// Tests for resolving the catalog rev "latest" to the highest vX.Y.Z tag.
public class ServiceClonerTests
{
    private static string Ls(params string[] tags) =>
        string.Join('\n', tags.Select(t => $"0123456789abcdef0123456789abcdef01234567\trefs/tags/{t}")) + "\n";

    [Fact]
    public void Picks_highest_semver_numerically()
    {
        Assert.Equal("v1.10.0", ServiceCloner.HighestSemverTag(Ls("v1.2.0", "v1.10.0", "v1.9.9", "v0.99.0")));
    }

    [Fact]
    public void Ignores_non_matching_and_prerelease_tags()
    {
        Assert.Equal("v1.0.0", ServiceCloner.HighestSemverTag(Ls("v1.0.0", "v2.0.0-rc.1", "release-3", "1.5.0", "v1.0")));
    }

    [Fact]
    public void Handles_annotated_tag_peeled_entries()
    {
        Assert.Equal("v0.2.0", ServiceCloner.HighestSemverTag(Ls("v0.1.0", "v0.1.0^{}", "v0.2.0", "v0.2.0^{}")));
    }

    [Fact]
    public void Returns_null_without_tags()
    {
        Assert.Null(ServiceCloner.HighestSemverTag(""));
        Assert.Null(ServiceCloner.HighestSemverTag(Ls("nightly")));
    }

    [Fact]
    public void Exact_rev_is_returned_unchanged_without_calling_git()
    {
        Assert.Equal("main", ServiceCloner.ResolveRev("https://invalid.example/none.git", "main"));
        Assert.Equal("v1.2.3", ServiceCloner.ResolveRev("https://invalid.example/none.git", "v1.2.3"));
    }

    [Fact]
    public void Latest_without_tag_fails_with_clear_message()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ostrun-cloner-" + Guid.NewGuid().ToString("N"));
        try
        {
            Git(dir, "init", "-q");
            var ex = Assert.Throws<GenerationException>(() => ServiceCloner.ResolveRev(dir, "latest"));
            Assert.Contains("no vX.Y.Z tag", ex.Message);
        }
        finally
        {
            if (Directory.Exists(dir)) ServiceCloner.DeleteDirectory(dir);
        }
    }

    private static void Git(string dir, params string[] args)
    {
        Directory.CreateDirectory(dir);
        var psi = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit();
        Assert.Equal(0, p.ExitCode);
    }
}
