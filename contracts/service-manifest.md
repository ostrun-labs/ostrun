# Service manifest, specification

Every service in the Ostrun catalog (core or contributed) ships a manifest. The generator reads it to validate the service, wire the gateway, and generate `docker-compose.yml` and `.env.example`.

## Location

At the root of the service's own repo:

```
ostrun.manifest.json
```

In a generated app, the generator clones each service into `services/{provider}/{service}/`, so the auth manifest ends up at `services/ostrun/auth/ostrun.manifest.json`.

## Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `provider` | string | yes | Declarative identifier for the contributor/maintainer, PascalCase. Must match the `Provider` segment of published events. |
| `service` | string | yes | Short service name, lowercase, matching the second half of the catalog ref (`auth`, `messaging`). A compound name uses hyphens (`order-fulfillment`), only `a-z`, `0-9` and `-`, the same characters the catalog CI allows in a ref. Event names stay PascalCase: the `Service` segment is the `service` value with hyphens dropped, compared case-insensitively (`order-fulfillment` ↔ `Ostrun.OrderFulfillment.OrderShipped`), the same way `provider` is compared. |
| `version` | string | yes | Version of the service itself (semver, e.g. `1.0.0`). It must equal the service's git tag `v{version}`, see "Versioning and `rev`" below. |
| `displayName` | string | yes | Human-readable name of the service. |
| `description` | string | yes | Short description shown in the catalog. |
| `basePath` | string | optional (default `/`) | Internal prefix under which the service mounts its routes (e.g. `/auth`). Without this field, the gateway assumes the service serves its routes at the root. If that's not the case, it strips the app prefix and breaks routing (`/auth/register` -> `/register` on the service side -> 404). |
| `exposure` | string | optional (default `public`) | Whether the gateway routes the service from outside. See "Exposure" below. |
| `events.published` | string[] | yes (can be empty) | Full names of published events (3 segments), referencing files in `/contracts`. |
| `events.consumed` | string[] | yes (can be empty) | Full names of consumed events (3 segments). Routing to this service happens by subscribing to a RabbitMQ exchange named after the event (no HTTP endpoint to declare), see "Event bus" below. |
| `env` | object[] | yes (can be empty) | Expected environment variables. Each entry: `name`, `description`, `required` (bool), `default` (optional, a string), `generate` (optional, see "Generated env values"), `smtp` (optional, see "SMTP"). The generator writes `default` into `.env.example` and uses it in `docker-compose.yml` when the variable is empty or missing from `.env`. Declare a `default` on every optional variable that isn't plain text, such as a number or a boolean. Compose passes an empty variable as `""`, and a .NET service fails at startup when it binds `""` to an `int` or `bool`. |
| `ports` | object[] | yes | Ports the service exposes in its container. Each entry: `containerPort` (number), `protocol` (optional, e.g. `http`), `description` (optional). An array rather than a single port, for services that expose more than one port. |
| `needsDb` | boolean | yes | Whether the service needs its own database. If `true`, the generator provisions a dedicated DB container in `docker-compose.yml` and adds the connection variables to `.env.example`. Never a DB shared between services. |
| `needsSmtp` | boolean | optional (default `false`) | Whether the service sends email and needs an SMTP server. If `true`, the generator adds a Mailpit container in dev mode and fills the env entries marked with `smtp` (see "SMTP"). Like `needsDb`, the service declares a need and the generator provides the infrastructure. |
| `dependencies` | string[] | yes (can be empty) | Required services (`{provider}/{service}`) that must be selected alongside this one. |
| `healthCheck.path` | string | yes | Path of the health check endpoint (e.g. `/health`). |
| `healthCheck.method` | string | yes | HTTP method to use for the health check call (e.g. `GET`). |
| `repository` | string (URL) | optional | Link to the service's Git repo, used by the generator to clone the source code (MVP "starter to tinker with" mode). |
| `registry` | string (URL/image) | optional | Reference to an already-built Docker image, used for the "ready-to-use" mode (post-MVP). Lets the generator skip the clone and just reference the image in `docker-compose.yml`. |

## Versioning and `rev`

A service is published by tagging its repo. The catalog entry (and an `app.json` entry that gives a `repository`) says which revision to clone with `rev`:

- `"rev": "latest"`: the generator runs `git ls-remote --tags` and clones the highest `vX.Y.Z` tag. Other tags, including pre-releases such as `v1.0.0-rc.1`, are ignored. If the repo has no `vX.Y.Z` tag, generation fails with an error instead of falling back to `main`.
- Any other `rev` (a branch, a tag, e.g. `main` or `v1.2.0`) is accepted and used as-is.

The tag must match the manifest: the annotated tag `vX.Y.Z` carries the same `version` as `ostrun.manifest.json` at that commit (`v1.2.0` ↔ `"version": "1.2.0"`). A release is a PR that bumps `version`, then the tag on the merged commit. See [`CONTRIBUTING.md`](../CONTRIBUTING.md#versioning-a-service). The generator doesn't check this match for you.

## Example: Auth service

Abridged from the real manifest in [`ostrun-auth-service`](https://github.com/ostrun-labs/ostrun-auth-service): the `env` list is a subset, and the optional `generate` field (see "Generated env values") isn't shown.

```json
{
  "provider": "Ostrun",
  "service": "auth",
  "version": "0.1.0",
  "displayName": "Ostrun Auth",
  "description": "Ostrun reference authentication service: email/password and Google sign-in, cookie sessions, RS256 JWTs with a JWKS endpoint.",
  "repository": "https://github.com/ostrun-labs/ostrun-auth-service",
  "registry": null,
  "needsDb": true,
  "basePath": "/auth",
  "exposure": "public",
  "events": {
    "published": [
      "Ostrun.Auth.UserRegistered",
      "Ostrun.Auth.UserLoggedIn"
    ],
    "consumed": []
  },
  "dependencies": [],
  "env": [
    {
      "name": "ASPNETCORE_URLS",
      "required": true,
      "description": "Kestrel listen URL(s), e.g. http://+:8080"
    },
    {
      "name": "ConnectionStrings__Auth",
      "required": true,
      "description": "PostgreSQL connection string (Npgsql)"
    },
    {
      "name": "Jwt__SigningKey",
      "required": true,
      "description": "RSA private key (2048+ bits) as base64-encoded PEM, generate one per instance. Its public half is served at /auth/.well-known/jwks.json"
    },
    {
      "name": "Jwt__Issuer",
      "required": true,
      "description": "JWT issuer (iss)"
    },
    {
      "name": "Jwt__Audience",
      "required": true,
      "description": "JWT audience (aud)"
    },
    {
      "name": "Jwt__ExpirationMinutes",
      "required": false,
      "default": "60",
      "description": "Token validity in minutes"
    }
  ],
  "ports": [
    {
      "containerPort": 8080,
      "protocol": "http",
      "description": "HTTP API"
    }
  ],
  "healthCheck": {
    "path": "/health",
    "method": "GET"
  }
}
```

## Example: service with a dependency (Messaging)

Abridged from the real manifest in [`ostrun-messaging-service`](https://github.com/ostrun-labs/ostrun-messaging-service): the `env` list is a subset. The manifest declares no `basePath`, so the service serves its routes at the root, and it is `internal`: the gateway has no route to it. It doesn't use `needsSmtp` or `smtp` yet (see "SMTP").

```json
{
  "provider": "Ostrun",
  "service": "messaging",
  "version": "0.1.0",
  "displayName": "Ostrun Messaging",
  "description": "Ostrun reference messaging service: sends emails triggered by other services' events (e.g. user registration).",
  "repository": "https://github.com/ostrun-labs/ostrun-messaging-service",
  "registry": null,
  "needsDb": true,
  "exposure": "internal",
  "events": {
    "published": [
      "Ostrun.Messaging.EmailSent"
    ],
    "consumed": [
      "Ostrun.Auth.UserRegistered"
    ]
  },
  "dependencies": ["ostrun/auth"],
  "env": [
    {
      "name": "ASPNETCORE_URLS",
      "required": true,
      "description": "Kestrel listen URL(s), e.g. http://+:8080"
    },
    {
      "name": "ConnectionStrings__Messaging",
      "required": true,
      "description": "PostgreSQL connection string (Npgsql)"
    },
    {
      "name": "Smtp__Host",
      "required": true,
      "description": "SMTP server host used for sending"
    },
    {
      "name": "Smtp__Port",
      "required": false,
      "default": "587",
      "description": "SMTP port"
    }
  ],
  "ports": [
    {
      "containerPort": 8080,
      "protocol": "http",
      "description": "HTTP API"
    }
  ],
  "healthCheck": {
    "path": "/health",
    "method": "GET"
  }
}
```

## Exposure

`exposure` says whether the gateway routes the service from outside the app.

| `exposure` | Meaning |
|---|---|
| `"public"` | The gateway routes the service, so anyone who can reach the gateway can call it. Use it for services that clients call directly, such as Auth. |
| `"internal"` | The gateway has no route to the service, not even for its health check. Other services in the app reach it on the Compose network at `http://{service}-api:{containerPort}`, for example `http://messaging-api:8080`. Use it for services that only work on behalf of other services. |

The granularity is the whole service. A service can't expose some routes and keep others internal.

When `exposure` is missing, the service is treated as `public` and the generator prints a warning naming it. Existing manifests and third-party services keep working, and nothing is silently dropped from the gateway. This is a transition: catalogued services will be required to declare `exposure`, enforced by `ostrun validate` in CI.

## Generated env values

An `env` entry can declare `generate` so the generator fills in a value instead of leaving `change-me`:

| `generate` | Value |
|---|---|
| `"password"` | A random value, generated once per app. Every entry with this kind in the same app gets its own value. |
| `"rsa-private-key"` | A 2048-bit RSA private key, PEM, base64-encoded (what Auth's `Jwt__SigningKey` expects). |
| `"app-name"` | The `app` value from `app.json`. |

A generated value is a dev value. It lands in `docker-compose.yml` as a default and in `.env.example`, so treat it as public. `--prod` never fills it in: the variable has to be set in `.env`. If an entry has both `default` and `generate`, `generate` wins in dev mode.

## SMTP

With `needsSmtp: true`, the generator runs a Mailpit container in dev mode so emails can be read without a real server. It never adds Mailpit with `--prod`.

The service marks the entries the generator fills in with a per-entry `smtp` field, so a non-.NET service isn't forced into .NET names like `Smtp__Host`. The value is one of:

| `smtp` | Value in dev mode |
|---|---|
| `"host"` | The Mailpit container's hostname. |
| `"port"` | Mailpit's SMTP port. |

Entries without `smtp` are handled like any other entry. Mailpit takes no credentials, so the service doesn't need username or password entries in dev mode. With `--prod`, every `smtp` entry has to be set in `.env`, like any other variable.

## Event bus

Services publish and consume via RabbitMQ + MassTransit. The exchange name is forced to the event's full name as declared in `/contracts` (`cfg.Message<T>(m => m.SetEntityName("Ostrun.Auth.UserRegistered"))` on the MassTransit side), not derived from the CLR type name. Each service defines its own local message type, no shared contract package is required, and a future service written in another language follows the same exchange naming convention with no C# dependency. The generator adds a single `rabbitmq` container to the app as soon as at least one selected service publishes or consumes an event.

Without `RabbitMq__Host` set in configuration, each service falls back to MassTransit's in-memory transport. No broker is required to run a single service standalone, in its own `docker-compose.yml`.

## Status

Fields `basePath`, `events.published`/`events.consumed`, `ports[]`, `needsDb`, and `healthCheck.method` were aligned (2026-08-21) with the real implementation and the gateway's needs, identified during the first generated app's spike. Manifests shipped before that date still used `events.publishes`/`events.consumes`, `port`, `needs_db` (snake_case), and `healthCheck.format`.

`events.consumed` was simplified to an array of strings (2026-08-24): delivery now goes through RabbitMQ/MassTransit instead of one HTTP endpoint per consumer, so `path`/`method` no longer make sense in the manifest.

`generate` on `env[]` entries and the top-level `needsSmtp` (with `smtp` on `env[]` entries) were added (2026-10). Both are optional: manifests without them keep working, with `change-me` defaults and SMTP configured by hand.

The manifest spec was aligned with the real manifests (2026-10): `service` is lowercase, and the Auth and Messaging examples now match the manifests on each service's `main`.

`exposure` was added (2026-11). It is optional and defaults to `public`, so manifests without it keep working.

There is no formal JSON schema for the manifest yet. For now the generator checks the fields it depends on when it runs (see [`generator/README.md`](../generator/README.md)).
