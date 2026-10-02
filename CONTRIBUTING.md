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

1. Choose a **provider**: a declarative identifier (free-form, not necessarily your GitHub handle) that identifies you as a contributor or maintainer. It must not collide with a provider someone else already uses. The maintainer checks this when reviewing your catalog PR.
2. Create your service in **its own repo**. This repo holds no service code. There's no official template yet: [`ostrun-auth-service`](https://github.com/ostrun-labs/ostrun-auth-service) and [`ostrun-notification-service`](https://github.com/ostrun-labs/ostrun-notification-service) are the working examples to copy from.
3. Add an `ostrun.manifest.json` at the root of your repo. Full spec and examples: [`/contracts/service-manifest.md`](./contracts/service-manifest.md).
4. Make sure your service exposes `/health` and starts standalone via its own `Dockerfile`.
5. **No hardcoded secrets** in code or the image. Use environment variables, loaded via `.env` locally, and provide a `.env.example` documenting the expected keys with no real values.
6. Try it with the generator before asking for a catalog entry. An `app.json` entry can point straight at your repo with `repository` (and optionally `rev`), no catalog needed. See [`generator/README.md`](./generator/README.md).
7. To get into the **official catalog**, open a PR here that adds your service to [`catalog.json`](./catalog.json). The maintainer reviews each one case by case for now.

### Service stack versions (.NET services)

Every .NET service in the catalog targets the versions below. Use them as-is when you create a new service. A service written in another language only has to honor the contract (API + events), so this table doesn't apply to it.

| Area | Package | Version |
|---|---|---|
| Runtime | .NET (`net8.0`), Docker images `mcr.microsoft.com/dotnet/sdk:8.0` and `aspnet:8.0` | 8.0 |
| Messaging | `MassTransit.RabbitMQ` | 8.4.1 |
| Messaging | `MassTransit.EntityFrameworkCore` (transactional outbox) | 8.4.1 |
| Data | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` | 8.0.11 |
| Data | `Npgsql.EntityFrameworkCore.PostgreSQL` | 8.0.11 |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.11 |
| Auth | `Microsoft.Extensions.Identity.Core` | 8.0.11 |
| Auth | `System.IdentityModel.Tokens.Jwt` | 8.1.2 |
| Config | `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Options.DataAnnotations` | 8.0.0 |
| Health | `AspNetCore.HealthChecks.NpgSql` | 8.0.2 |
| Tests | `xunit`, `xunit.runner.visualstudio` | 2.5.3 |
| Tests | `Microsoft.NET.Test.Sdk` | 17.8.0 |
| Tests | `NSubstitute` | 5.1.0 |
| Tests | `coverlet.collector` | 6.0.0 |

MassTransit stays on 8.4.1 on purpose. The 8.5 EF Core integration requires EF Core 9, and the services run on EF Core 8. Don't bump MassTransit or EF Core on its own in a single service. A version change to anything in this table goes through one PR per service repo, landed together, plus an update to this table.

Libraries specific to one service (MailKit for the notification service, for example) are up to that service.

## Adding or changing an event

This is the most sensitive part of the project. Please follow these rules to the letter:

1. **Strict 3-segment naming convention**: `{Provider}.{Service}.{Entity}{PastParticiple}`, e.g. `Ostrun.Auth.UserRegistered`. `Provider` avoids collisions between two same-named services written by different contributors.
2. The last segment ends in a **past participle** describing a completed fact (`UserRegistered`, never `CreateUser`). Validation stays **loose**: no closed list of verbs, since the service catalog is meant to grow and a strict list would require a PR for every new business verb.
3. Any change to an existing event creates a **new version** (`v2`, etc.) rather than breaking the existing one.
4. The schema (JSON Schema or Protobuf) goes in `/contracts/{provider}/{service}/{Entity}{PastParticiple}.v1.json`, versioned.
5. Your pull request must pass **contract validation CI**. Today CI checks that the file name, the schema `title`, and the `version` agree with each other and with the directory. The maintainer checks the other naming rules by hand in review until CI covers them: provider consistency with the manifest, past participle, blocklisted verbs.

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
