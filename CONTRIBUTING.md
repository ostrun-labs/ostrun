# Contributing to Ostrun

Thanks for your interest in Ostrun! This project is designed from the start to accept external contributions, with a few rules to keep it coherent and maintainable.

## Before contributing

### Project license

Ostrun is distributed under the **Apache-2.0** license. By contributing, you agree that your contribution will be distributed under this license.

## Architecture principles to respect

Before proposing a new service, keep the project's non-negotiable rules in mind:

- **Strict independence**: your service has its own database and schema. Never a shared DB, never a direct call into another service's code.
- **Communication only via API (synchronous) or events (asynchronous)**.
- **Polyglot allowed**: write your service in whatever language you choose (Node, Python, etc.), as long as it honors the contract (API + events) and provides a `Dockerfile` and a `/health` endpoint.
- **No runtime coupling**: no dependency on a "central" service at startup, aside from the event bus and the gateway.

## Adding a new service

1. Choose a **provider**: a declarative identifier (free-form, not necessarily your GitHub handle) that identifies you as a contributor or maintainer. CI automatically checks it doesn't collide with a provider already used by someone else.
2. Generate the skeleton with the official template (`dotnet new ostrun-service` or the equivalent for your stack, see `/scaffold`). Your service goes under `/services/{provider}/{service}/`. A single provider can host multiple services.
3. Fill in the **service manifest** (`services/{provider}/{service}/ostrun.manifest.json`). Full spec and examples: [`/contracts/service-manifest.md`](./contracts/service-manifest.md).
4. Make sure your service exposes `/health` and starts standalone via its own `Dockerfile`.
5. **No hardcoded secrets** in code or the image. Use environment variables, loaded via `.env` locally, and provide a `.env.example` documenting the expected keys with no real values.
6. Add your service to the project's `docker-compose.yml`.

For now, adding a service to the **official catalog** (the one exposed in the generation UI) is reviewed case by case by the project maintainer. This will be revisited once the MVP is public.

## Adding or changing an event

This is the most sensitive part of the project. Please follow these rules to the letter:

1. **Strict 3-segment naming convention**: `{Provider}.{Service}.{Entity}{PastParticiple}`, e.g. `Ostrun.Auth.UserRegistered`. `Provider` avoids collisions between two same-named services written by different contributors.
2. The last segment ends in a **past participle** describing a completed fact (`UserRegistered`, never `CreateUser`). Validation stays **loose**: no closed list of verbs, since the service catalog is meant to grow and a strict list would require a PR for every new business verb.
3. Any change to an existing event creates a **new version** (`v2`, etc.) rather than breaking the existing one.
4. The schema (JSON Schema or Protobuf) goes in `/contracts/{provider}/{service}/{Entity}{PastParticiple}.v1.json`, versioned.
5. Your pull request must pass **contract validation CI**: name uniqueness, consistency between the `provider` declared in the manifest and the one used in published events, schema validity. A PR that fails this validation will not be merged.

There is no central event governance service. Consistency happens through PR review, not through a single execution point.

> Full validation spec (regex, verb heuristic, test cases): see [`/contracts/README.md`](./contracts/README.md).

## Pull request process

1. Fork the repo, create a branch from `main`.
2. Make sure tests and CI pass (build, tests, contract validation if you touch `/contracts`).
3. Clearly describe in the PR what the service or change does, and any events published or consumed.
4. A maintainer reviews and merges.

## Reporting a bug or proposing an idea

Open an issue with as much context as possible: expected vs. observed behavior, logs, reproduction steps. For an architecture proposal or a new service, a discussion in an issue before the PR is welcome to avoid wasted work.

## Maintenance and turnaround

This project is maintained in my personal time, so feedback on issues and PRs may take a while. Thanks for your patience.
