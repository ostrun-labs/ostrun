# ostrun/generator

MVP script that automates what an earlier manual spike did by hand: clone the selected services, validate their manifests, and generate `docker-compose.yml`, `gateway/routes.generated.json`, and `.env.example`.

It packs as a .NET tool named `ostrun`. The tool isn't on nuget.org yet, so for now you either run it from this repo with `dotnet run` or build and install it locally.

## Usage

Prerequisites: the .NET 8 SDK, Docker (with Compose) and Git. To run from source, clone this repo first:

```bash
git clone https://github.com/ostrun-labs/ostrun.git && cd ostrun
```

Then, from the repo root:

```bash
cat > app.json <<'EOF'
{
  "app": "my-app",
  "gatewayPort": 8080,
  "services": [
    { "ref": "ostrun/auth" },
    { "ref": "ostrun/messaging" }
  ]
}
EOF

dotnet run --project generator -- app.json ./my-app
# or, once installed as a tool (see below):
ostrun app.json ./my-app

cd my-app
docker compose up --build -d
```

On Windows, a full file path is capped at 260 characters by default. The Auth service has paths up to 115 characters, cloned under `{output}/services/ostrun/auth/`, so a deep output folder makes the clone fail. Use a short output path or run `git config --global core.longpaths true`.

A bare `ref` is enough for a service listed in [`../catalog.json`](../catalog.json). The generator resolves `repository`/`rev` from there. For a personal service not yet catalogued, giving `repository` (and optionally `rev`) directly in the `app.json` entry skips the catalog lookup.

## Validate a service manifest

```bash
dotnet run --project generator -- validate ../ostrun-auth-service
# or: ostrun validate ../ostrun-auth-service
```

Checks the `ostrun.manifest.json` at the root of a service directory, with no `app.json` and no clone. It lists every error and exits 1, or prints `OK` and exits 0. The rules are the per-manifest ones the generator also applies: required fields present, `provider` PascalCase, `service` lowercase, exactly one `http` port, `healthCheck.method == GET`, no two `env` entries mapping to the same `.env` variable. Rules that need the whole `app.json` (duplicate refs, manifest vs ref, dependencies) only run when generating.

Add `--contracts <dir>` to also check the manifest's events against an ostrun checkout's `contracts/` folder:

```bash
dotnet run --project generator -- validate ../ostrun-messaging-service --contracts contracts
```

For every name in `events.published` and `events.consumed`: the name follows §2.1 (structure), §2.5 (command verbs, error) and §2.4 (past participle, warning only), and a contract file `{provider}/{service}/{Entity}.v*.json` exists under `<dir>`. For `events.published` only (§2.2): the `Provider` segment equals the manifest's `provider` exactly, and the `Service` segment equals its `service` case-insensitively. Without `--contracts`, only the manifest rules run, so a contributor needs no ostrun checkout.

## Validate event contracts

```bash
dotnet run --project generator -- validate --contracts contracts
```

Walks `<dir>/**/*.v*.json` (skipping `_verbs/`) and checks each file against the rules in [`../contracts/README.md`](../contracts/README.md) that need only the contract: valid JSON, `title` matches the directory, file name and version (§2.6), the 3-segment PascalCase structure (§2.1) and the command-verb blocklist (§2.5) as errors (exit 1), and the past-participle heuristic (§2.4) as a warning (exit stays 0). With `GITHUB_ACTIONS=true`, findings print as `::warning`/`::error` annotations. This is what the `validate-contracts` CI job runs.

## Install as a local tool

```bash
dotnet pack generator -c Release -o ./nupkg
dotnet tool install -g Ostrun.Cli --add-source ./nupkg
ostrun --help
```

The tool needs the .NET 8 SDK or newer. It runs from any folder because the gateway skeleton and `catalog.json` ship inside the package. The bundled catalog is a copy taken at build time, so a service added to the catalog afterwards needs a new build of the tool. To use another catalog file, pass `--catalog <path>`.

To pick up a new local build, run `dotnet tool uninstall -g Ostrun.Cli` first, or bump `<Version>` in `OstrunGenerator.csproj`.

## What it does

1. Resolves each `ref` in `app.json`. It uses `repository`/`rev` when the entry gives them, otherwise it looks the `ref` up in the catalog (the bundled `catalog.json`, or the file passed with `--catalog`).
2. Clones each service (`git clone --depth 1`) into `services/{ref}/`.
3. Reads and validates each `ostrun.manifest.json` (per-manifest rules as in `validate`, plus no duplicate refs, manifest matching its ref, dependencies resolved within the selection). Fails loudly rather than generating a broken app.
4. Copies the gateway skeleton (`gateway/`, bundled with the tool) as-is.
5. Generates `docker-compose.yml`: one `{key}-api` service per selected service, a `{key}-db` if `needsDb`, a `rabbitmq` if at least one service publishes or consumes events. No host port published besides the gateway's.
6. Generates `gateway/routes.generated.json`: a catch-all route plus a dedicated health route per service (the health check always lives at an absolute path on the container, independent of `basePath`).
7. Generates `.env.example`: the union of each manifest's `env[]`, prefixed per service, excluding anything derivable (connection strings, `ASPNETCORE_URLS`, RabbitMQ config).

Both files carry the same dev values, computed once per generation (`DevValues.cs`), so `docker compose up` works with no `.env`. Every variable in `docker-compose.yml` is written as `${NAME:-value}`, and a value in `.env` overrides it. The value of an `env` entry is, in order: its `generate` kind (`password`, `rsa-private-key`, `app-name`), its `default`, `change-me` if required, otherwise empty. Postgres and RabbitMQ passwords are random. Nothing is hardcoded, so two generated apps never share a secret.

## Known limits (MVP)

- One HTTP port per service (a `ports[]` with more than one entry fails validation).
- No "ready-to-use" mode (`registry`): the generator always clones from `repository`.
- No transitive dependency resolution: if `app.json` selects Messaging without Auth, validation fails instead of adding Auth automatically.
- `catalog.json` is hand-maintained for now. No CI checks that it stays in sync with the real manifests of the services it lists, only its own structural validity (see `.github/workflows/ci.yml`).
