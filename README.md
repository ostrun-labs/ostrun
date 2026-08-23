# Ostrun

**Catalogue de services + générateur one-shot** pour bâtir des backends microservices : tu choisis des services, tu obtiens un repo indépendant prêt à cloner — aucune synchronisation ni dépendance runtime à Ostrun après génération.

Vitrine technique pensée pour démontrer une architecture distribuée prod-ready : découplage par events, indépendance stricte des services, gouvernance pensée pour accueillir du code externe dès le départ.

> Repo privé pour l'instant. MVP en cours de finalisation.

## Démarrage rapide

Les services disponibles sont listés dans [`catalog.json`](./catalog.json) — pas besoin de connaître leur URL de repo, juste leur `ref` (`{provider}/{service}`) :

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
cp .env.example .env   # remplir tous les "change-me"
docker compose up --build -d
```

Détail de ce que fait le générateur et ses limites connues : [`generator/README.md`](./generator/README.md).

## Principes

- **Cœur en C#/.NET** (gateway YARP, service Auth de référence) ; les services contribués peuvent être écrits dans n'importe quel langage tant qu'ils respectent le contrat (API + events).
- **Indépendance stricte de chaque service** : sa propre base de données, son propre repo + `Dockerfile`, communication uniquement via API (synchrone) ou events (asynchrone) — jamais d'appel direct au code d'un autre service, jamais de DB partagée.
- **Bus d'events** (RabbitMQ + MassTransit) comme colonne vertébrale inter-services. Pas de service de gouvernance runtime ("EventManager") — la cohérence se fait à la conception, via PR + CI.
- **Secrets** via `.env` (gitignored) + `.env.example` committé — jamais en dur.

## Structure du repo

```
/gateway      # Squelette YARP, copié tel quel dans chaque app générée
/generator    # Script qui clone les services sélectionnés et assemble l'app
/contracts    # Schémas d'events versionnés + spécification du manifeste de service
CONTRIBUTING.md, CLA.md, LICENSE
```

Ce repo ne contient **aucun code de service** : chaque service catalogué vit dans son propre repo indépendant, par ex. [`ostrun-auth-service`](https://github.com/Thykimik/ostrun-auth-service) et [`ostrun-mailer-service`](https://github.com/Thykimik/ostrun-mailer-service).

## Documentation

- [`contracts/README.md`](./contracts/README.md) — gouvernance et convention de nommage des events
- [`contracts/service-manifest.md`](./contracts/service-manifest.md) — spécification du manifeste de service
- [`generator/README.md`](./generator/README.md) — fonctionnement du générateur, limites connues du MVP
- [`CONTRIBUTING.md`](./CONTRIBUTING.md) — process de contribution ; [`CLA.md`](./CLA.md) à accepter sur la première PR

## Licence

Distribué sous licence [Apache-2.0](./LICENSE).
