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
| `service` | string | yes | Short service name, PascalCase if compound (`Auth`, `Notification`, `OrderFulfillment`). |
| `version` | string | yes | Version of the service itself (semver, e.g. `1.0.0`). |
| `displayName` | string | yes | Human-readable name of the service. |
| `description` | string | yes | Short description shown in the catalog. |
| `basePath` | string | optional (default `/`) | Internal prefix under which the service mounts its routes (e.g. `/auth`). Without this field, the gateway assumes the service serves its routes at the root. If that's not the case, it strips the app prefix and breaks routing (`/auth/register` -> `/register` on the service side -> 404). |
| `events.published` | string[] | yes (can be empty) | Full names of published events (3 segments), referencing files in `/contracts`. |
| `events.consumed` | string[] | yes (can be empty) | Full names of consumed events (3 segments). Routing to this service happens by subscribing to a RabbitMQ exchange named after the event (no HTTP endpoint to declare), see "Event bus" below. |
| `env` | object[] | yes (can be empty) | Expected environment variables. Each entry: `name`, `description`, `required` (bool), `default` (optional, a string). The generator writes `default` into `.env.example` and uses it in `docker-compose.yml` when the variable is empty or missing from `.env`. Declare a `default` on every optional variable that isn't plain text, such as a number or a boolean. Compose passes an empty variable as `""`, and a .NET service fails at startup when it binds `""` to an `int` or `bool`. |
| `ports` | object[] | yes | Ports the service exposes in its container. Each entry: `containerPort` (number), `protocol` (optional, e.g. `http`), `description` (optional). An array rather than a single port, for services that expose more than one port. |
| `needsDb` | boolean | yes | Whether the service needs its own database. If `true`, the generator provisions a dedicated DB container in `docker-compose.yml` and adds the connection variables to `.env.example`. Never a DB shared between services. |
| `dependencies` | string[] | yes (can be empty) | Required services (`{provider}/{service}`) that must be selected alongside this one. |
| `healthCheck.path` | string | yes | Path of the health check endpoint (e.g. `/health`). |
| `healthCheck.method` | string | yes | HTTP method to use for the health check call (e.g. `GET`). |
| `repository` | string (URL) | optional | Link to the service's Git repo, used by the generator to clone the source code (MVP "starter to tinker with" mode). |
| `registry` | string (URL/image) | optional | Reference to an already-built Docker image, used for the "ready-to-use" mode (post-MVP). Lets the generator skip the clone and just reference the image in `docker-compose.yml`. |

## Example: Auth service

```json
{
  "provider": "Ostrun",
  "service": "auth",
  "version": "1.0.0",
  "displayName": "Auth",
  "description": "Accounts, sessions, and JWT out of the box.",
  "basePath": "/auth",
  "events": {
    "published": [
      "Ostrun.Auth.UserRegistered",
      "Ostrun.Auth.UserLoggedIn"
    ],
    "consumed": []
  },
  "env": [
    {
      "name": "JWT_SIGNING_KEY",
      "description": "Signing key for JWTs issued by the service.",
      "required": true
    },
    {
      "name": "JWT_EXPIRATION_MINUTES",
      "description": "Validity duration of issued tokens, in minutes.",
      "required": false,
      "default": "60"
    }
  ],
  "ports": [
    { "containerPort": 8080, "protocol": "http", "description": "HTTP API" }
  ],
  "needsDb": true,
  "dependencies": [],
  "healthCheck": {
    "path": "/health",
    "method": "GET"
  },
  "repository": "https://github.com/ostrun-labs/ostrun-auth-service",
  "registry": null
}
```

## Example: service with a dependency (Notification)

```json
{
  "provider": "Ostrun",
  "service": "notification",
  "version": "1.0.0",
  "displayName": "Notification",
  "description": "Sends emails triggered by other services' events.",
  "events": {
    "published": ["Ostrun.Notification.EmailSent"],
    "consumed": ["Ostrun.Auth.UserRegistered"]
  },
  "env": [
    {
      "name": "SMTP_HOST",
      "description": "SMTP server host used for sending.",
      "required": true
    }
  ],
  "ports": [
    { "containerPort": 8081, "protocol": "http", "description": "HTTP API" }
  ],
  "needsDb": false,
  "dependencies": ["ostrun/auth"],
  "healthCheck": {
    "path": "/health",
    "method": "GET"
  },
  "repository": "https://github.com/ostrun-labs/ostrun-notification-service",
  "registry": null
}
```

## Event bus

Services publish and consume via RabbitMQ + MassTransit. The exchange name is forced to the event's full name as declared in `/contracts` (`cfg.Message<T>(m => m.SetEntityName("Ostrun.Auth.UserRegistered"))` on the MassTransit side), not derived from the CLR type name. Each service defines its own local message type, no shared contract package is required, and a future service written in another language follows the same exchange naming convention with no C# dependency. The generator adds a single `rabbitmq` container to the app as soon as at least one selected service publishes or consumes an event.

Without `RabbitMq__Host` set in configuration, each service falls back to MassTransit's in-memory transport. No broker is required to run a single service standalone, in its own `docker-compose.yml`.

## Status

Fields `basePath`, `events.published`/`events.consumed`, `ports[]`, `needsDb`, and `healthCheck.method` were aligned (2026-08-21) with the real implementation and the gateway's needs, identified during the first generated app's spike. Manifests shipped before that date still used `events.publishes`/`events.consumes`, `port`, `needs_db` (snake_case), and `healthCheck.format`.

`events.consumed` was simplified to an array of strings (2026-08-24): delivery now goes through RabbitMQ/MassTransit instead of one HTTP endpoint per consumer, so `path`/`method` no longer make sense in the manifest.

There is no formal JSON schema for the manifest yet. For now the generator checks the fields it depends on when it runs (see [`generator/README.md`](../generator/README.md)).
