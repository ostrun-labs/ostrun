using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

// Tests for the contract-file rules (ContractRules), one per contract-only
// row of the table in contracts/README.md §4.
public class ContractRulesTests
{
    private static readonly HashSet<string> Irregular = ["Sent", "Paid", "Done"];

    private static (List<string> Errors, List<string> Warnings) Check(string path, string title, string version = "1") =>
        ContractRules.CheckFile(path, $$"""{ "title": "{{title}}", "version": {{version}} }""", Irregular);

    [Theory]
    [InlineData("ostrun/auth/UserRegistered.v1.json", "Ostrun.Auth.UserRegistered")]
    [InlineData("acme/billing/InvoiceCreated.v1.json", "Acme.Billing.InvoiceCreated")]
    [InlineData("acme/mailer/EmailSent.v1.json", "Acme.Mailer.EmailSent")]
    public void Accepts_a_valid_contract_without_warning(string path, string title)
    {
        var (errors, warnings) = Check(path, title);

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Rejects_a_title_with_two_segments()
    {
        var (errors, _) = Check("ostrun/auth/UserRegistered.v1.json", "Auth.UserRegistered");

        Assert.Contains(errors, e => e.Contains("3 PascalCase segments"));
    }

    [Fact]
    public void Rejects_a_lowercase_provider()
    {
        var (errors, _) = Check("ostrun/auth/UserRegistered.v1.json", "ostrun.Auth.UserRegistered");

        Assert.Contains(errors, e => e.Contains("3 PascalCase segments"));
    }

    [Fact]
    public void Rejects_a_command_ending_in_Create()
    {
        var (errors, _) = Check("ostrun/auth/UserCreate.v1.json", "Ostrun.Auth.UserCreate");

        Assert.Contains(errors, e => e.Contains("verb 'Create'"));
    }

    [Fact]
    public void Rejects_a_command_ending_in_Login()
    {
        var (errors, _) = Check("ostrun/auth/UserLogin.v1.json", "Ostrun.Auth.UserLogin");

        Assert.Contains(errors, e => e.Contains("verb 'Login'"));
    }

    [Fact]
    public void Warns_on_an_unknown_participle_without_failing()
    {
        var (errors, warnings) = Check("ostrun/auth/UserWoken.v1.json", "Ostrun.Auth.UserWoken");

        Assert.Empty(errors);
        Assert.Single(warnings);
    }

    [Fact]
    public void Rejects_a_version_mismatch()
    {
        var (errors, _) = Check("ostrun/auth/UserRegistered.v2.json", "Ostrun.Auth.UserRegistered");

        Assert.Contains(errors, e => e.Contains("does not match schema version"));
    }

    [Fact]
    public void Rejects_a_title_that_does_not_match_the_directory_or_file()
    {
        var (errors, _) = Check("ostrun/auth/UserRegistered.v1.json", "Acme.Billing.UserDeleted");

        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void Rejects_invalid_json()
    {
        var (errors, _) = ContractRules.CheckFile("ostrun/auth/UserRegistered.v1.json", "{", Irregular);

        Assert.Equal(["not valid JSON"], errors);
    }

    [Fact]
    public void CheckDirectory_skips_the_verbs_folder_and_reads_the_irregular_list()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "_verbs"));
            Directory.CreateDirectory(Path.Combine(dir, "acme", "mailer"));
            File.WriteAllText(Path.Combine(dir, "_verbs", "irregular-participles.txt"), "Sent\n");
            File.WriteAllText(Path.Combine(dir, "_verbs", "junk.v1.json"), "{");
            File.WriteAllText(Path.Combine(dir, "acme", "mailer", "EmailSent.v1.json"), """{ "title": "Acme.Mailer.EmailSent", "version": 1 }""");

            var result = ContractRules.CheckDirectory(dir);

            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
