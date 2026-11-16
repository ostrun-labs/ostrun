using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

// Tests for the network-free catalog rules (CatalogRules): the structural
// checks and the provider collision of contracts/README.md §2.3.
public class CatalogRulesTests
{
    private static string Catalog(params string[] entries) => $$"""{ "services": [{{string.Join(",", entries)}}] }""";

    private static string Entry(string serviceRef, string repository = "https://github.com/acme/svc", string rev = "main") =>
        $$"""{ "ref": "{{serviceRef}}", "repository": "{{repository}}", "rev": "{{rev}}" }""";

    [Fact]
    public void Accepts_the_real_shape()
    {
        var errors = CatalogRules.CheckStructure(Catalog(Entry("ostrun/auth"), Entry("ostrun/crawler")));

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_invalid_json()
    {
        var errors = CatalogRules.CheckStructure("{ nope");

        Assert.Contains(errors, e => e.Contains("not valid JSON"));
    }

    [Fact]
    public void Rejects_a_missing_services_array()
    {
        var errors = CatalogRules.CheckStructure("{}");

        Assert.Contains(errors, e => e.Contains("'services' must be an array"));
    }

    [Theory]
    [InlineData("""{ "repository": "https://x/y", "rev": "main" }""")]
    [InlineData("""{ "ref": "a/b", "rev": "main" }""")]
    [InlineData("""{ "ref": "a/b", "repository": "https://x/y" }""")]
    [InlineData("""{ "ref": "a/b", "repository": "", "rev": "main" }""")]
    public void Rejects_an_entry_missing_a_field(string entry)
    {
        var errors = CatalogRules.CheckStructure(Catalog(entry));

        Assert.Contains(errors, e => e.Contains("missing ref, repository, or rev"));
    }

    [Theory]
    [InlineData("Acme/auth")]
    [InlineData("acme")]
    [InlineData("acme/auth/extra")]
    [InlineData("acme/Auth")]
    public void Rejects_a_malformed_ref(string serviceRef)
    {
        var errors = CatalogRules.CheckStructure(Catalog(Entry(serviceRef)));

        Assert.Contains(errors, e => e.Contains("must be lowercase"));
    }

    [Fact]
    public void Rejects_a_duplicate_ref()
    {
        var errors = CatalogRules.CheckStructure(Catalog(Entry("acme/auth"), Entry("acme/auth")));

        Assert.Contains(errors, e => e.Contains("duplicate ref 'acme/auth'"));
    }

    [Theory]
    [InlineData("http://github.com/acme/svc")]
    [InlineData("git@github.com:acme/svc.git")]
    public void Rejects_a_non_https_repository(string repository)
    {
        var errors = CatalogRules.CheckStructure(Catalog(Entry("acme/auth", repository)));

        Assert.Contains(errors, e => e.Contains("must be an https URL"));
    }

    [Fact]
    public void Allows_the_same_exact_provider_on_several_services()
    {
        var errors = CatalogRules.CheckProviderCollisions(
            [("ostrun/auth", "Ostrun"), ("ostrun/messaging", "Ostrun"), ("ostrun/crawler", "Ostrun")]);

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_providers_that_differ_only_by_case()
    {
        var errors = CatalogRules.CheckProviderCollisions(
            [("acme/auth", "Acme"), ("acme/billing", "Acme"), ("other/mailer", "ACME")]);

        var error = Assert.Single(errors);
        Assert.Contains("acme/auth 'Acme'", error);
        Assert.Contains("other/mailer 'ACME'", error);
        Assert.Contains("maintainer decides", error);
    }

    [Fact]
    public void Allows_different_providers()
    {
        var errors = CatalogRules.CheckProviderCollisions([("ostrun/auth", "Ostrun"), ("acme/auth", "Acme")]);

        Assert.Empty(errors);
    }
}
