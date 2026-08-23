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
| `basePath` | string | optionnel (défaut `/`) | Préfixe interne sous lequel le service monte ses routes (ex. `/auth`). Sans ce champ, la gateway suppose que le service sert ses routes à la racine — si ce n'est pas le cas, elle strip le préfixe applicatif et casse le routing (`/auth/register` → `/register` côté service → 404). |
| `events.published` | string[] | oui (peut être vide) | Noms complets des events publiés (3 segments), référencent des fichiers dans `/contracts`. |
| `events.consumed` | string[] | oui (peut être vide) | Noms complets des events consommés (3 segments). Le routage vers ce service se fait par abonnement à un exchange RabbitMQ nommé d'après l'event (pas d'endpoint HTTP à déclarer) — voir « Bus d'events » ci-dessous. |
| `env` | object[] | oui (peut être vide) | Variables d'environnement attendues. Chaque entrée : `name`, `description`, `required` (bool), `default` (optionnel). |
| `ports` | object[] | oui | Ports exposés par le service dans son conteneur. Chaque entrée : `containerPort` (number), `protocol` (optionnel, ex. `http`), `description` (optionnel). Un tableau plutôt qu'un seul port, pour les services qui exposent plus d'un port. |
| `needsDb` | boolean | oui | Indique si le service nécessite sa propre base de données. Si `true`, le générateur provisionne un conteneur DB dédié dans le `docker-compose.yml` et ajoute les variables de connexion à `.env.example` — jamais une DB partagée entre services. |
| `dependencies` | string[] | oui (peut être vide) | Services requis (`{provider}/{service}`) devant être sélectionnés en même temps que celui-ci. |
| `healthCheck.path` | string | oui | Chemin de l'endpoint de health check (ex. `/health`). |
| `healthCheck.method` | string | oui | Méthode HTTP à utiliser pour l'appel de health check (ex. `GET`). |
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
  "ports": [
    { "containerPort": 8080, "protocol": "http", "description": "API HTTP" }
  ],
  "needsDb": true,
  "dependencies": [],
  "healthCheck": {
    "path": "/health",
    "method": "GET"
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
    "published": ["Ostrun.Mailer.EmailSent"],
    "consumed": ["Ostrun.Auth.UserRegistered"]
  },
  "env": [
    {
      "name": "SMTP_HOST",
      "description": "Hôte du serveur SMTP utilisé pour l'envoi.",
      "required": true
    }
  ],
  "ports": [
    { "containerPort": 8081, "protocol": "http", "description": "API HTTP" }
  ],
  "needsDb": false,
  "dependencies": ["ostrun/auth"],
  "healthCheck": {
    "path": "/health",
    "method": "GET"
  },
  "repository": "https://github.com/Thykimik/ostrun-mailer-service",
  "registry": null
}
```

## Bus d'events

Les services publient/consomment via RabbitMQ + MassTransit. Le nom d'exchange est forcé au nom complet de l'event déclaré dans `/contracts` (`cfg.Message<T>(m => m.SetEntityName("Ostrun.Auth.UserRegistered"))` côté MassTransit), pas dérivé du nom du type CLR — chaque service définit son propre type de message local, aucun package de contrat partagé n'est requis, un futur service dans un autre langage suit la même convention de nommage d'exchange sans dépendance C#. Le générateur ajoute un conteneur `rabbitmq` unique à l'app dès qu'au moins un service sélectionné publie ou consomme un event.

En l'absence de `RabbitMq__Host` en configuration, chaque service bascule sur le transport in-memory de MassTransit — aucun broker n'est requis pour lancer un service seul, dans son propre `docker-compose.yml` standalone.

## Statut

Champs `basePath`, `events.published`/`events.consumed`, `ports[]`, `needsDb` et `healthCheck.method` alignés (2026-08-21) sur l'implémentation réelle et les besoins du gateway, identifiés lors du spike du premier app généré — les manifestes livrés avant cette date utilisaient encore `events.publishes`/`events.consumes`, `port`, `needs_db` (snake_case) et `healthCheck.format`.

`events.consumed` simplifié en tableau de strings (2026-08-24) : la livraison passe désormais par RabbitMQ/MassTransit au lieu d'un endpoint HTTP par consommateur, donc `path`/`method` n'ont plus de sens dans le manifeste.

Le schéma JSON formel (pour validation automatique par la CI) n'est pas encore écrit — à faire au moment du scaffold generator, comme pour la validation des events (`/contracts/README.md`).
