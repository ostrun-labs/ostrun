# Ostrun

Ostrun is a service catalog and one-shot generator for building microservice backends. You pick services from [`catalog.json`](./catalog.json), the generator (`generator/`) clones them, validates their manifests, and emits a standalone repo, `docker-compose.yml`, gateway routes, `.env.example`, with no runtime dependency back on Ostrun.

This repo itself ships no service code. Services live in their own repos (e.g. `ostrun-auth-service`, `ostrun-mailer-service`). Ostrun only holds the gateway skeleton, the generator, and the event/manifest contracts that keep those independent services interoperable.

## What makes Ostrun special

### 1. Strict service independence

Each service owns its own database and repo, and talks to others only via synchronous API calls or async events on the bus (RabbitMQ + MassTransit). Never a direct call into another service's code, never a shared DB. This is the non-negotiable rule of the project. Guard it in every review.

### 2. Polyglot by contract, not by convention

A contributed service can be written in any language. What makes it a valid Ostrun service is the manifest (`ostrun.manifest.json`) and the contract it upholds: exposes `/health`, ships its own `Dockerfile`, publishes/consumes events per the naming convention. Don't assume C#/.NET idioms apply outside `gateway/` and `generator/`, which are the only first-party runtime code here.

### 3. No runtime governance service

There is deliberately no central "EventManager" enforcing event contracts at runtime. Consistency comes from PR review and CI validation against `/contracts`, not from a live coordination point. Don't propose one.

### 4. Generate once, then hands-off

Generated apps have zero coupling back to Ostrun after generation. No polling, no shared registry, no "phone home". Anything you add to the generator should preserve that: it emits files and exits.

## A note from the maintainer

This is a technical showcase for a production-shaped distributed architecture, built to eventually accept external contributions. Favor the smallest change that keeps the architecture honest over anything that looks impressive. Don't preserve complexity that isn't earning its keep, and don't add governance machinery "just in case".

The rest of this document is meant to help you navigate the repo and make changes effectively. Treat it as good defaults, not hard rules. The developer's direct instructions win.

## A small glossary

- **you** means the agent reading this file and changing Ostrun.
- **provider** means a contributor identifier (declarative, not necessarily a GitHub handle) used as the first segment of a service `ref` and of event names, to avoid collisions between two services of the same name from different contributors.
- **service** means one independently deployable unit with its own DB, repo, `Dockerfile`, and manifest.
- **manifest** means `ostrun.manifest.json`, the spec a service must satisfy to be catalogable, see [`contracts/service-manifest.md`](./contracts/service-manifest.md).
- **catalog** means [`catalog.json`](./catalog.json), the list of `{provider}/{service}` refs the generator can resolve without an explicit `repository`.
- **generated app** means the output of running the generator against an `app.json`: a standalone repo, independent of this one from that point on.
- **event** means a fact published on the bus, named `{Provider}.{Service}.{Entity}{PastParticiple}` and schema'd under `/contracts`.
- **gateway** means the YARP skeleton in `gateway/`, copied as-is into every generated app; its routes are generated per app, not hand-maintained per service.

## Where code lives

- `generator/` is the .NET console app (`dotnet run --project generator -- app.json ./output`). Resolves refs, clones services, validates manifests, emits compose + gateway routes + `.env.example`. See [`generator/README.md`](./generator/README.md) for the full pipeline and known MVP limits.
- `gateway/` is a YARP skeleton, copied verbatim into generated apps. Not a running service in this repo; it has no business logic of its own, only routing config that the generator overwrites (`routes.generated.json`).
- `contracts/` holds event schemas (versioned, `/contracts/{provider}/{service}/{Entity}{PastParticiple}.v1.json`) and the service manifest spec. This is the most sensitive part of the repo, see naming rules below.
- `catalog.json` is a hand-maintained list of catalogable services (`ref` → `repository`/`rev`). No CI currently checks it stays in sync with the manifests of the services it lists, only its own structural validity.
- `brainstorm/` is the maintainer's local folder for design notes, PRDs and roadmap. It isn't committed, so it only exists in the maintainer's checkout. If it's there, read it for rationale, but don't treat it as current-state documentation; it can be stale relative to the code.
- `.github/workflows/ci.yml` runs contract and catalog validation.

## Event and manifest rules

These are enforced by CI and reviewed strictly. Don't loosen them without maintainer sign-off:

1. Event names are exactly 3 segments: `{Provider}.{Service}.{Entity}{PastParticiple}` (e.g. `Ostrun.Auth.UserRegistered`). Past participle, never an imperative (`UserRegistered`, not `CreateUser`).
2. A breaking change to an existing event creates a new version (`v2`, ...) rather than mutating `v1`'s schema in place.
3. A service's manifest `provider` must match the provider it publishes events under.
4. Full spec, regex, and test cases: [`contracts/README.md`](./contracts/README.md).

## Working on the generator

- Single HTTP port per service is a hard MVP constraint (`ports[]` with more than one entry fails validation). Don't quietly relax this without updating the manifest spec too.
- No transitive dependency resolution: if `app.json` selects a service without its declared dependencies, validation fails rather than auto-adding them. Keep failing loud; don't make this silently permissive.
- The generator clones every time (no local "already have it" fast path). That's intentional for now, not an oversight to fix opportunistically.
- Changes to what the generator emits (compose, routes, `.env.example`) should stay in sync with `generator/README.md`'s description of the pipeline.

## Verifying

- `dotnet build` for `gateway/` and `generator/` individually; there's no top-level solution orchestrating both.
- To exercise the generator end-to-end, run it against a real `app.json` selecting one or two catalog services and confirm `docker-compose.yml`, `gateway/routes.generated.json`, and `.env.example` come out correct. This repo has no automated integration test for that path yet, so a manual run is the proof.
- CI (`.github/workflows/ci.yml`) validates `/contracts` and `catalog.json` structure. Don't bypass it; if a change needs CI relaxed, say so explicitly rather than routing around it.
- No live dev server or database in this repo to protect. Unlike a running product, there's nothing here that stays up between sessions. The generator is a one-shot CLI; run it freely.

## Pull requests

- Never open a PR unless the developer explicitly asks.
- Conventional commit titles, plain language.
- One concern per PR: a new event schema, a generator fix, and a docs update are three PRs, not one.
- Contract/manifest changes need to point at the CI validation they pass, not just describe intent.

## Plans and work artifacts

- Design rationale goes in `brainstorm/` (local, not committed) when the maintainer asks for it. Don't invent a committed `docs/` or `.plans/` tree.
- Don't commit agent scratch files or throwaway research notes outside `brainstorm/`.

## Taste

- Complexity belongs in the generator and contract validation, not in the gateway skeleton or in a runtime coordination service that doesn't exist.
- No secrets in code or images. Env vars via `.env`, documented (no real values) in `.env.example`.
- If a rule here fights the task in front of you, say so and get sign-off before breaking it.
