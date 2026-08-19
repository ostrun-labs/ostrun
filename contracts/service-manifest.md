# Manifeste de service — spécification

Chaque service déclaré au catalogue Ostrun (cœur ou contribué) fournit un manifeste. C'est ce que l'UI de génération et la CI lisent pour construire le catalogue, câbler la gateway et générer le `docker-compose.yml`.

## Emplacement

```
services/{provider}/{service}/ostrun.manifest.json
```

Ex. : `services/ostrun/auth/ostrun.manifest.json`

## Champs

| Champ | Type | Requis | Description |
|---|---|---|---|
| `provider` | string | oui | Identifiant déclaratif du contributeur/mainteneur, PascalCase. Doit matcher le segment `Provider` des events publiés. |
| `service` | string | oui | Nom court du service, PascalCase si composé (`Auth`, `Mailer`, `OrderFulfillment`). |
| `version` | string | oui | Version du service lui-même (semver, ex. `1.0.0`). |
| `displayName` | string | oui | Nom affiché dans le catalogue (UI). |
| `description` | string | oui | Description courte affichée dans le catalogue. |
| `events.publishes` | string[] | oui (peut être vide) | Noms complets des events publiés (3 segments), référencent des fichiers dans `/contracts`. |
| `events.consumes` | string[] | oui (peut être vide) | Noms complets des events consommés. |
| `env` | object[] | oui (peut être vide) | Variables d'environnement attendues. Chaque entrée : `name`, `description`, `required` (bool), `default` (optionnel). |
| `port` | number | oui | Port exposé par le service dans son conteneur. |
| `dependencies` | string[] | oui (peut être vide) | Services requis (`{provider}/{service}`) devant être sélectionnés en même temps que celui-ci. |
| `healthCheck.path` | string | oui | Chemin de l'endpoint de health check (ex. `/health`). |
| `healthCheck.format` | string | oui | Format de réponse attendu (ex. `plain-200`, `json-status`). |
| `repository` | string (URL) | optionnel | Lien vers le repo Git du service — utilisé par le générateur pour cloner le code source (mode MVP "starter à bidouiller"). |
| `registry` | string (URL/image) | optionnel | Référence de l'image Docker déjà buildée — utilisée pour le mode "prêt à l'emploi" (post-MVP), permet de sauter le clone et juste référencer l'image dans le `docker-compose.yml`. |

## Exemple — service Auth

```json
{
  "provider": "Ostrun",
  "service": "auth",
  "version": "1.0.0",
  "displayName": "Auth",
  "description": "Comptes, sessions et JWT prêts à l'emploi.",
  "events": {
    "publishes": [
      "Ostrun.Auth.UserRegistered",
      "Ostrun.Auth.UserLoggedIn"
    ],
    "consumes": []
  },
  "env": [
    {
      "name": "JWT_SIGNING_KEY",
      "description": "Clé de signature des JWT émis par le service.",
      "required": true
    },
    {
      "name": "JWT_EXPIRATION_MINUTES",
      "description": "Durée de validité des tokens émis, en minutes.",
      "required": false,
      "default": "60"
    }
  ],
  "port": 8080,
  "dependencies": [],
  "healthCheck": {
    "path": "/health",
    "format": "plain-200"
  },
  "repository": "https://github.com/Thykimik/ostrun-auth-service",
  "registry": null
}
```

## Exemple — service avec dépendance (Mailer)

```json
{
  "provider": "Ostrun",
  "service": "mailer",
  "version": "1.0.0",
  "displayName": "Mailer",
  "description": "Envoi d'emails déclenché par les events des autres services.",
  "events": {
    "publishes": ["Ostrun.Mailer.EmailSent"],
    "consumes": ["Ostrun.Auth.UserRegistered"]
  },
  "env": [
    {
      "name": "SMTP_HOST",
      "description": "Hôte du serveur SMTP utilisé pour l'envoi.",
      "required": true
    }
  ],
  "port": 8081,
  "dependencies": ["ostrun/auth"],
  "healthCheck": {
    "path": "/health",
    "format": "plain-200"
  },
  "repository": "https://github.com/Thykimik/ostrun-mailer-service",
  "registry": null
}
```

## Statut

Le schéma JSON formel (pour validation automatique par la CI) n'est pas encore écrit — à faire au moment du scaffold generator, comme pour la validation des events (`/contracts/README.md`).
