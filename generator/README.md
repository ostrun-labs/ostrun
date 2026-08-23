# ostrun/generator

Script MVP qui automatise ce que le spike (`brainstorm/first-app-spike-plan.md`) faisait à la main : cloner les services sélectionnés, valider leurs manifestes, et générer `docker-compose.yml`, `gateway/routes.generated.json` et `.env.example`.

Pas un outil packagé (CLI/UI web = post-MVP) — un projet console qu'on lance avec `dotnet run`.

## Usage

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

dotnet run --project ostrun/generator -- app.json ./my-app

cd my-app
cp .env.example .env   # remplir tous les "change-me"
docker compose up --build -d
```

`ref` seul suffit pour un service listé dans [`../catalog.json`](../catalog.json) — le générateur y résout `repository`/`rev`. Pour un service perso pas encore catalogué, donner `repository` (et optionnellement `rev`) directement dans l'entrée `app.json`, ça court-circuite le lookup catalogue.

## Ce qu'il fait

1. Résout chaque `ref` d'`app.json` : `repository`/`rev` donnés explicitement, sinon lookup dans `catalog.json`.
2. Clone chaque service (`git clone --depth 1`) dans `services/{ref}/`.
3. Lit et valide chaque `ostrun.manifest.json` (un seul port HTTP, `healthCheck.method == GET`, dépendances résolues dans la sélection) — échoue bruyamment plutôt que de générer une app cassée.
4. Copie le squelette gateway (`ostrun/gateway/`) tel quel.
5. Génère `docker-compose.yml` : un service `{key}-api` par service sélectionné, un `{key}-db` si `needsDb`, un `rabbitmq` si au moins un service publie/consomme des events. Aucun port host publié à part la gateway.
6. Génère `gateway/routes.generated.json` : une route catch-all + une route santé dédiée par service (le health check est toujours à un chemin absolu sur le conteneur, indépendant de `basePath`).
7. Génère `.env.example` : union des `env[]` de chaque manifeste, préfixée par service, en excluant tout ce qui est dérivable (connection strings, `ASPNETCORE_URLS`, config RabbitMQ).

## Limites connues (MVP)

- Un seul port HTTP par service (`ports[]` avec plus d'une entrée fait échouer la validation).
- Pas de mode "prêt à l'emploi" (`registry`) — clone systématique depuis `repository`.
- Pas de résolution transitive de dépendances : si `app.json` sélectionne Mailer sans Auth, la validation échoue plutôt que d'ajouter Auth automatiquement.
- `catalog.json` est maintenu à la main pour l'instant — pas de CI qui vérifie qu'il reste synchronisé avec les manifestes réels des services listés (juste sa propre validité structurelle, voir `.github/workflows/ci.yml`).
