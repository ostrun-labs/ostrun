using System.Diagnostics;
using System.Text.Json;
using OstrunGenerator;

if (args.Length < 2 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: dotnet run --project ostrun/generator -- <app.json> <output-dir>");
    return 1;
}

var appJsonPath = args[0];
var outputDir = args[1];

try
{
    Run(appJsonPath, outputDir);
    return 0;
}
catch (GenerationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void Run(string appJsonPath, string outputDir)
{
    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    var app = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(appJsonPath), jsonOptions)
        ?? throw new GenerationException($"{appJsonPath}: could not parse app config.");

    Directory.CreateDirectory(outputDir);
    var servicesDir = Path.Combine(outputDir, "services");
    Directory.CreateDirectory(servicesDir);

    var resolved = new List<ResolvedService>();
    foreach (var serviceRef in app.Services)
    {
        var cloneDir = Path.Combine(servicesDir, serviceRef.Ref);
        Console.WriteLine($"Cloning {serviceRef.Ref} from {serviceRef.Repository} ({serviceRef.Rev})...");
        CloneService(serviceRef, cloneDir);

        var manifestPath = Path.Combine(cloneDir, "ostrun.manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new GenerationException($"{serviceRef.Ref}: no ostrun.manifest.json found at {manifestPath} after clone.");
        }

        var manifest = JsonSerializer.Deserialize<ServiceManifest>(File.ReadAllText(manifestPath), jsonOptions)
            ?? throw new GenerationException($"{serviceRef.Ref}: could not parse manifest.");

        resolved.Add(new ResolvedService(serviceRef.Ref, serviceRef, manifest));
    }

    Console.WriteLine("Validating manifests...");
    Validation.Validate(resolved);

    var eventBusNeeded = resolved.Any(s => s.Manifest.Events.Published.Count > 0 || s.Manifest.Events.Consumed.Count > 0);

    var gatewaySkeletonDir = FindGatewaySkeleton();
    Console.WriteLine($"Copying gateway skeleton from {gatewaySkeletonDir}...");
    CopyGatewaySkeleton(gatewaySkeletonDir, Path.Combine(outputDir, "gateway"));

    Console.WriteLine("Generating docker-compose.yml...");
    File.WriteAllText(Path.Combine(outputDir, "docker-compose.yml"), ComposeGenerator.Generate(app, resolved, eventBusNeeded));

    Console.WriteLine("Generating gateway/routes.generated.json...");
    File.WriteAllText(Path.Combine(outputDir, "gateway", "routes.generated.json"), RoutesGenerator.Generate(resolved));

    Console.WriteLine("Generating .env.example...");
    File.WriteAllText(Path.Combine(outputDir, ".env.example"), EnvExampleGenerator.Generate(app, resolved, eventBusNeeded));

    File.WriteAllText(Path.Combine(outputDir, ".gitignore"), ".env\nservices/\n");

    Console.WriteLine();
    Console.WriteLine($"Done. Next steps in {outputDir}:");
    Console.WriteLine("  cp .env.example .env   # fill in every \"change-me\"");
    Console.WriteLine("  docker compose up --build -d");
}

static void CloneService(ServiceRef serviceRef, string targetDir)
{
    if (Directory.Exists(targetDir))
    {
        DeleteDirectory(targetDir);
    }

    RunGit(["clone", "--depth", "1", "--branch", serviceRef.Rev, serviceRef.Repository, targetDir]);
}

static void DeleteDirectory(string path)
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

static void RunGit(IEnumerable<string> arguments)
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
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new GenerationException($"git {string.Join(' ', arguments)} failed:\n{process.StandardError.ReadToEnd()}");
    }
}

static string FindGatewaySkeleton()
{
    // ostrun/generator/bin/Debug/net8.0 -> walk up to the ostrun repo root,
    // then into gateway/. Works whether run via `dotnet run` (cwd =
    // generator/) or the built exe (cwd = bin output).
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OstrunGateway.csproj")))
    {
        dir = dir.Parent;
    }

    if (dir is not null)
    {
        return dir.FullName;
    }

    var fromCwd = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "gateway"));
    if (File.Exists(Path.Combine(fromCwd, "OstrunGateway.csproj")))
    {
        return fromCwd;
    }

    throw new GenerationException("Could not locate the ostrun/gateway skeleton (OstrunGateway.csproj not found).");
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
