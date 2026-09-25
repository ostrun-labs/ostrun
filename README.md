# Ostrun

**Service catalog + one-shot generator** for building microservice backends. Pick services, get an independent repo ready to clone. No sync, no runtime dependency on Ostrun after generation.

Technical showcase built to demonstrate a production-shaped distributed architecture: event-driven decoupling, strict service independence, governance designed from the start to accept external code.

> Private repo for now. MVP being finalized.

## Quickstart

Available services are listed in [`catalog.json`](./catalog.json). No need to know their repo URL, just their `ref` (`{provider}/{service}`):

```bash
cat > app.json <<'EOF'
{
  "app": "my-app",
  "gatewayPort": 8080,
  "services": [
    { "ref": "ostrun/auth" },
    { "ref": "ostrun/mailer" }
  ]
}
EOF

dotnet run --project generator -- app.json ./my-app

cd my-app
cp .env.example .env   # fill in every "change-me"
docker compose up --build -d
```

What the generator does and its known limits: [`generator/README.md`](./generator/README.md).

## Principles

- **Core in C#/.NET** (YARP gateway, reference Auth service); contributed services can be written in any language as long as they honor the contract (API + events).
- **Strict independence per service**: its own database, its own repo, its own `Dockerfile`. Communication only via API (synchronous) or events (asynchronous), never a direct call into another service's code, never a shared DB.
- **Event bus** (RabbitMQ + MassTransit) as the inter-service backbone. No runtime governance service ("EventManager"); consistency happens at design time, via PR and CI.
- **Secrets** via `.env` (gitignored) plus a committed `.env.example`, never hardcoded.

## Repo structure

```
/gateway      # YARP skeleton, copied as-is into every generated app
/generator    # Script that clones selected services and assembles the app
/contracts    # Versioned event schemas + service manifest specification
CONTRIBUTING.md, LICENSE
```

This repo contains no service code: each cataloged service lives in its own independent repo, e.g. [`ostrun-auth-service`](https://github.com/Thykimik/ostrun-auth-service) and [`ostrun-mailer-service`](https://github.com/Thykimik/ostrun-mailer-service).

## Documentation

- [`contracts/README.md`](./contracts/README.md): event governance and naming convention
- [`contracts/service-manifest.md`](./contracts/service-manifest.md): service manifest specification
- [`generator/README.md`](./generator/README.md): how the generator works, known MVP limits
- [`CONTRIBUTING.md`](./CONTRIBUTING.md): contribution process and the [package versions every .NET service targets](./CONTRIBUTING.md#service-stack-versions-net-services)

## License

Distributed under the [Apache-2.0](./LICENSE) license.
