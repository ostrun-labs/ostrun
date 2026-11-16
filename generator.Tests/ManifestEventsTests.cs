using OstrunGenerator;
using Xunit;

namespace OstrunGenerator.Tests;

// Tests for the manifest-vs-contracts rules (ManifestEvents): contracts/README.md §2.2
// plus the name and contract-file checks applied to events.published/consumed.
public class ManifestEventsTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ManifestEventsTests()
    {
        Contract("ostrun", "auth", "UserRegistered");
        Contract("acme", "auth", "UserRegistered");
        Contract("ostrun", "messaging", "EmailSent");
        Directory.CreateDirectory(Path.Combine(dir, "_verbs"));
        File.WriteAllText(Path.Combine(dir, "_verbs", "irregular-participles.txt"), "Sent\n");
    }

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private void Contract(string provider, string service, string entity)
    {
        Directory.CreateDirectory(Path.Combine(dir, provider, service));
        File.WriteAllText(Path.Combine(dir, provider, service, $"{entity}.v1.json"), "{}");
    }

    private ManifestEvents.Result Check(string provider, string service, string[] published, string[] consumed) =>
        ManifestEvents.Check(new ServiceManifest
        {
            Provider = provider,
            Service = service,
            Events = new EventsSection { Published = [.. published], Consumed = [.. consumed] },
        }, dir, "svc");

    [Fact]
    public void Accepts_a_manifest_with_no_events()
    {
        var result = Check("Ostrun", "crawler", [], []);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Accepts_published_and_consumed_events_that_have_contracts()
    {
        var result = Check("Ostrun", "messaging", ["Ostrun.Messaging.EmailSent"], ["Ostrun.Auth.UserRegistered"]);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Rejects_a_published_event_with_another_provider()
    {
        var result = Check("Globex", "auth", ["Acme.Auth.UserRegistered"], []);

        Assert.Single(result.Errors, e => e.Contains("provider 'Acme'"));
    }

    [Fact]
    public void Compares_the_provider_case_sensitively()
    {
        var result = Check("OSTRUN", "auth", ["Ostrun.Auth.UserRegistered"], []);

        Assert.Single(result.Errors, e => e.Contains("provider 'Ostrun'"));
    }

    [Fact]
    public void Rejects_publishing_under_another_services_name()
    {
        var result = Check("Ostrun", "messaging", ["Ostrun.Auth.UserRegistered"], []);

        Assert.Single(result.Errors, e => e.Contains("service 'Auth'"));
    }

    [Fact]
    public void Rejects_a_consumed_event_with_no_contract()
    {
        var result = Check("Ostrun", "messaging", [], ["Ostrun.Auth.UserDeleted"]);

        Assert.Single(result.Errors, e => e.Contains("no contract for 'Ostrun.Auth.UserDeleted'"));
    }

    [Fact]
    public void Does_not_check_the_provider_of_a_consumed_event()
    {
        var result = Check("Ostrun", "messaging", [], ["Acme.Auth.UserRegistered"]);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Applies_the_name_rules_to_both_lists()
    {
        var result = Check("Ostrun", "auth", ["Auth.UserRegistered"], ["Ostrun.Auth.UserCreate"]);

        Assert.Contains(result.Errors, e => e.Contains("events.published") && e.Contains("3 PascalCase segments"));
        Assert.Contains(result.Errors, e => e.Contains("events.consumed") && e.Contains("verb 'Create'"));
    }

    [Fact]
    public void Warns_on_an_unknown_participle_without_failing()
    {
        Contract("ostrun", "auth", "UserWoken");

        var result = Check("Ostrun", "auth", ["Ostrun.Auth.UserWoken"], []);

        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Fails_when_the_contracts_directory_does_not_exist()
    {
        Assert.Throws<GenerationException>(() =>
            ManifestEvents.Check(new ServiceManifest(), Path.Combine(dir, "missing"), "svc"));
    }
}
