using System.Text.Json;
using OstrunGenerator;

const string usage = """
    Usage: ostrun <app.json> <output-dir> [--catalog <catalog.json>] [--prod]
           ostrun validate <service-dir> [--contracts <contracts-dir>]
           ostrun validate --contracts <contracts-dir>
           ostrun validate --catalog <catalog.json> --contracts <contracts-dir>
    """;

var positional = new List<string>();
string? catalogPath = null;
string? contractsDir = null;
var prod = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-h" or "--help":
            Console.WriteLine(usage);
            return 0;
        case "--catalog" when i + 1 < args.Length:
            catalogPath = args[++i];
            break;
        case "--prod":
            prod = true;
            break;
        case "--contracts" when i + 1 < args.Length:
            contractsDir = args[++i];
            break;
        default:
            positional.Add(args[i]);
            break;
    }
}

if (catalogPath is not null && contractsDir is not null && positional is ["validate"])
{
    return ValidateCatalog(catalogPath, contractsDir);
}

if (contractsDir is not null && positional is ["validate"])
{
    return ValidateContracts(contractsDir);
}

if (positional.Count == 2 && positional[0] == "validate")
{
    return ValidateService(positional[1], contractsDir);
}

if (positional.Count != 2)
{
    Console.Error.WriteLine(usage);
    return 1;
}

var appJsonPath = positional[0];
var outputDir = positional[1];

try
{
    Run(appJsonPath, outputDir, catalogPath, prod);
    return 0;
}
catch (GenerationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int ValidateContracts(string contractsDir)
{
    try
    {
        var result = ContractRules.CheckDirectory(contractsDir);
        var inActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

        // Annotations show on the PR in GitHub Actions; plain lines elsewhere.
        foreach (var w in result.Warnings)
        {
            Console.WriteLine(inActions ? $"::warning file={w.File}::{w.Message}" : $"warning: {w.File}: {w.Message}");
        }
        foreach (var e in result.Errors)
        {
            Console.Error.WriteLine(inActions ? $"::error file={e.File}::{e.Message}" : $"error: {e.File}: {e.Message}");
        }

        if (result.Errors.Count > 0)
        {
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }
    catch (GenerationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int ValidateService(string serviceDir, string? contractsDir)
{
    try
    {
        var manifest = ManifestLoader.Load(Path.Combine(serviceDir, "ostrun.manifest.json"), serviceDir);
        var errors = ManifestRules.Check(manifest, serviceDir);
        if (contractsDir is not null)
        {
            var events = ManifestEvents.Check(manifest, contractsDir, serviceDir);
            errors.AddRange(events.Errors);
            foreach (var warning in events.Warnings)
            {
                Console.WriteLine($"  warning: {warning}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }
    catch (GenerationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int ValidateCatalog(string catalogPath, string contractsDir)
{
    var inActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    var errors = new List<string>();

    try
    {
        if (!File.Exists(catalogPath))
        {
            throw new GenerationException($"{catalogPath}: catalog not found.");
        }

        var json = File.ReadAllText(catalogPath);
        errors.AddRange(CatalogRules.CheckStructure(json));

        // Entries are only cloned once the catalog is well formed.
        if (errors.Count == 0)
        {
            var catalog = JsonSerializer.Deserialize<Catalog>(json, ManifestLoader.JsonOptions)!;
            var providers = new List<(string Label, string Provider)>();
            var workDir = Path.Combine(Path.GetTempPath(), "ostrun-catalog-" + Path.GetRandomFileName());
            try
            {
                foreach (var entry in catalog.Services)
                {
                    ValidateCatalogEntry(entry, Path.Combine(workDir, entry.Ref), contractsDir, errors, providers);
                }
            }
            finally
            {
                if (Directory.Exists(workDir))
                {
                    ServiceCloner.DeleteDirectory(workDir);
                }
            }

            errors.AddRange(CatalogRules.CheckProviderCollisions(providers));
        }
    }
    catch (GenerationException ex)
    {
        errors.Add(ex.Message);
    }

    foreach (var error in errors)
    {
        // Annotations show on the PR in GitHub Actions; plain lines elsewhere.
        Console.Error.WriteLine(inActions ? $"::error file={catalogPath}::{error.ReplaceLineEndings(" ")}" : $"error: {error}");
    }

    if (errors.Count > 0)
    {
        return 1;
    }

    Console.WriteLine("OK");
    return 0;
}

static void ValidateCatalogEntry(
    CatalogEntry entry, string cloneDir, string contractsDir, List<string> errors, List<(string Label, string Provider)> providers)
{
    Console.WriteLine($"Validating {entry.Ref} ({entry.Repository} @ {entry.Rev})...");
    try
    {
        ServiceCloner.Clone(entry.Repository, entry.Rev, cloneDir);
        var manifest = ManifestLoader.Load(Path.Combine(cloneDir, "ostrun.manifest.json"), entry.Ref);

        errors.AddRange(ManifestRules.Check(manifest, entry.Ref));
        var events = ManifestEvents.Check(manifest, contractsDir, entry.Ref);
        errors.AddRange(events.Errors);
        foreach (var warning in events.Warnings)
        {
            Console.WriteLine($"  warning: {warning}");
        }

        // Same comparison as Validation.Validate does for an app.json ref.
        var refParts = entry.Ref.Split('/');
        if (manifest.Provider.ToLowerInvariant() != refParts[0])
        {
            errors.Add($"{entry.Ref}: manifest provider '{manifest.Provider}' does not match the catalog ref.");
        }
        if (manifest.Service != refParts[1])
        {
            errors.Add($"{entry.Ref}: manifest service '{manifest.Service}' does not match the catalog ref.");
        }

        providers.Add((entry.Ref, manifest.Provider));
    }
    catch (GenerationException ex)
    {
        errors.Add(ex.Message);
    }
}

static void Run(string appJsonPath, string outputDir, string? catalogPath, bool prod)
{
    // The gateway skeleton and catalog.json are copied next to the binary at build time (see OstrunGenerator.csproj).
    var bundleDir = AppContext.BaseDirectory;

    var jsonOptions = ManifestLoader.JsonOptions;

    var app = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(appJsonPath), jsonOptions)
        ?? throw new GenerationException($"{appJsonPath}: could not parse app config.");

    var catalog = LoadCatalog(catalogPath ?? Path.Combine(bundleDir, "catalog.json"), jsonOptions);

    Directory.CreateDirectory(outputDir);
    var servicesDir = Path.Combine(outputDir, "services");
    Directory.CreateDirectory(servicesDir);

    var resolved = new List<ResolvedService>();
    foreach (var serviceRef in app.Services)
    {
        var (repository, rev) = ResolveRepository(serviceRef, catalog);

        var cloneDir = Path.Combine(servicesDir, serviceRef.Ref);
        Console.WriteLine($"Cloning {serviceRef.Ref} from {repository} ({rev})...");
        ServiceCloner.Clone(repository, rev, cloneDir);

        var manifest = ManifestLoader.Load(Path.Combine(cloneDir, "ostrun.manifest.json"), serviceRef.Ref);

        resolved.Add(new ResolvedService(serviceRef.Ref, serviceRef, manifest, repository));
    }

    Console.WriteLine("Validating manifests...");
    Validation.Validate(resolved);

    var eventBusNeeded = resolved.Any(s => s.Manifest.Events.Published.Count > 0 || s.Manifest.Events.Consumed.Count > 0);

    var gatewaySkeletonDir = Path.Combine(bundleDir, "gateway");
    Console.WriteLine($"Copying gateway skeleton from {gatewaySkeletonDir}...");
    CopyGatewaySkeleton(gatewaySkeletonDir, Path.Combine(outputDir, "gateway"));

    var values = DevValues.Resolve(app, resolved, eventBusNeeded, prod);

    Console.WriteLine("Generating docker-compose.yml...");
    File.WriteAllText(Path.Combine(outputDir, "docker-compose.yml"), ComposeGenerator.Generate(app, resolved, eventBusNeeded, values));

    Console.WriteLine("Generating gateway/routes.generated.json...");
    File.WriteAllText(Path.Combine(outputDir, "gateway", "routes.generated.json"), RoutesGenerator.Generate(resolved));

    Console.WriteLine("Generating .env.example...");
    File.WriteAllText(Path.Combine(outputDir, ".env.example"), EnvExampleGenerator.Generate(app, resolved, eventBusNeeded, values, prod));

    var readmePath = Path.Combine(outputDir, "README.md");
    if (File.Exists(readmePath))
    {
        Console.WriteLine("Warning: README.md already exists in the output folder, leaving it untouched.");
    }
    else
    {
        Console.WriteLine("Generating README.md...");
        File.WriteAllText(readmePath, ReadmeGenerator.Generate(app, resolved, values, prod));
    }

    File.WriteAllText(Path.Combine(outputDir, ".gitignore"), ".env\nservices/\n");

    Console.WriteLine();
    Console.WriteLine($"Done. Next steps in {outputDir}:");
    if (prod)
    {
        Console.WriteLine("  cp .env.example .env   # then replace every change-me");
    }
    Console.WriteLine("  docker compose up --build -d");
}

static Catalog LoadCatalog(string catalogPath, JsonSerializerOptions jsonOptions)
{
    if (!File.Exists(catalogPath))
    {
        throw new GenerationException($"{catalogPath}: catalog not found.");
    }

    return JsonSerializer.Deserialize<Catalog>(File.ReadAllText(catalogPath), jsonOptions)
        ?? throw new GenerationException($"{catalogPath}: could not parse catalog.");
}

static (string Repository, string Rev) ResolveRepository(ServiceRef serviceRef, Catalog catalog)
{
    // app.json may fully specify repository/rev itself. That's the
    // "starter" path for a personal/uncatalogued service. Otherwise
    // resolve from catalog.json.
    if (serviceRef.Repository is not null)
    {
        return (serviceRef.Repository, serviceRef.Rev ?? "main");
    }

    var entry = catalog.Services.FirstOrDefault(e => e.Ref == serviceRef.Ref)
        ?? throw new GenerationException($"{serviceRef.Ref}: not in catalog.json and no repository given in app.json. Either add it to the catalog or specify \"repository\" directly.");

    return (entry.Repository, serviceRef.Rev ?? entry.Rev);
}

static void CopyGatewaySkeleton(string sourceDir, string targetDir)
{
    Directory.CreateDirectory(targetDir);

    foreach (var file in Directory.GetFiles(sourceDir))
    {
        var name = Path.GetFileName(file);
        if (name is "routes.generated.json")
        {
            continue; // this app's own copy is generated fresh, not copied
        }
        File.Copy(file, Path.Combine(targetDir, name), overwrite: true);
    }

    var propertiesDir = Path.Combine(sourceDir, "Properties");
    if (Directory.Exists(propertiesDir))
    {
        var targetProperties = Path.Combine(targetDir, "Properties");
        Directory.CreateDirectory(targetProperties);
        foreach (var file in Directory.GetFiles(propertiesDir))
        {
            File.Copy(file, Path.Combine(targetProperties, Path.GetFileName(file)), overwrite: true);
        }
    }
}
