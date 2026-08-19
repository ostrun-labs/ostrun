# Ostrun

**Plateforme backend microservices** — vitrine technique pensée pour démontrer une architecture distribuée prod-ready, ouverte aux contributions externes dès le départ.

> ⚠️ Projet en cours de démarrage. Le dépôt est privé pour le moment ; la structure ci-dessous décrit la cible.

## Pourquoi ce projet

Ostrun n'est pas un simple outil perso — c'est une **vitrine technique** conçue pour démontrer des compétences d'architecture backend/distribué : découplage via events, indépendance stricte des services, observabilité dès le MVP, et une gouvernance pensée pour accueillir du code venant d'autres développeurs.

## Principes d'architecture

- **Cœur en C#/.NET** (gateway, service Auth de référence)
- **Polyglotte autorisé pour les services contribués** — un contributeur peut écrire son service en Node, Python, etc., tant qu'il respecte le contrat (API + events)
- **Indépendance stricte de chaque service** :
  - sa propre base de données / son propre schéma (jamais de DB partagée)
  - son propre repo ou dossier + `Dockerfile`, déployable seul
  - communication uniquement via API (synchrone) ou events (asynchrone) — jamais d'appel direct au code d'un autre service
- **Découplage via bus d'événements** comme colonne vertébrale de la communication inter-services

## Stack technique

| Brique | Choix | Pourquoi |
|---|---|---|
| API Gateway | [YARP](https://github.com/microsoft/reverse-proxy) | Natif .NET, plus léger que Traefik pour ce contexte |
| Event bus | RabbitMQ via [MassTransit](https://masstransit.io/) | Simple à opérer ; MassTransit abstrait le transport (migration Kafka possible sans réécrire les consommateurs) |
| Observabilité | OpenTelemetry dès le MVP | Une vraie compétence "prod-ready", pas juste "ça tourne en local" |
| Orchestration | Docker Compose (MVP) | Suffisant à l'échelle portfolio ; K8s envisageable plus tard si besoin réel |
| Contrats d'events | JSON Schema (ou Protobuf), versionnés dans `/contracts` | Source de vérité indépendante du langage de chaque service |
| Secrets | `.env` (+ `.env.example` committé) | Simple et standard pour du Compose local ; Docker Secrets écarté (pensé pour Swarm, complexité inutile ici) |

## Génération

Ostrun n'est **pas une plateforme partagée multi-tenant** : c'est un catalogue de services + un générateur one-shot. Un dev choisit ses services dans une UI web, récupère un repo indépendant prêt à cloner — aucune synchronisation ultérieure avec Ostrun après génération. Ostrun ne crée pas le repo Git à la place de l'utilisateur (pour le MVP).

## Gouvernance des events

- **Convention de nommage à 3 segments** : `{Provider}.{Service}.{Entité}{VerbePassé}` — ex. `Ostrun.Auth.UserRegistered`. Le `Provider` (déclaratif, choisi par le contributeur) évite les collisions entre deux services de même nom écrits par des contributeurs différents ; la CI vérifie l'unicité (insensible à la casse).
- `/contracts` = source de vérité versionnée (v1, v2…) ; toute évolution d'un event crée une nouvelle version plutôt que de casser l'existante
- **Pas de service "EventManager" runtime** — la gouvernance se fait à la conception, via PR + CI
- Verbes en participe passé, validation **souple** (pas de liste fermée — le catalogue est amené à grandir)
- Spécification complète de validation (regex, cas de test) : [`/contracts/README.md`](./contracts/README.md)

## Manifeste de service

Chaque service déclare un `ostrun.manifest.json` lu par l'UI de génération et la CI pour construire le catalogue et le `docker-compose.yml`. Spécification complète et exemples : [`/contracts/service-manifest.md`](./contracts/service-manifest.md).

## Structure du repo (cible)

```
/gateway                  # YARP
/services
  /ostrun
    /auth                 # Service Auth (C#) — JWT, register/login
/contracts                # Schémas d'events versionnés, imbriqués par provider/service
/scaffold                 # Template de génération de service pour contributeurs
docker-compose.yml
CONTRIBUTING.md
CLA.md
LICENSE
```

> Les services sont rangés par `{provider}/{service}` — y compris les services du cœur (sous le provider `ostrun`) — car un même type de service peut avoir plusieurs providers concurrents (ex. `billing` chez Stripe vs PayPal) avec des noms qui se recoupent.

## MVP

1. Gateway (YARP)
2. Service Auth (C#) — JWT, register/login
3. Bus RabbitMQ + MassTransit
4. Docker Compose pour tout lancer en local
5. Documentation + pipeline CI (build, tests, validation des contrats d'events)

Pour le MVP, l'ajout d'un service au catalogue officiel est validé uniquement par le mainteneur (repo privé pour l'instant).

## Après le MVP

- Ouverture aux contributions externes (voir [CONTRIBUTING.md](./CONTRIBUTING.md)) et gouvernance du catalogue revue en conséquence
- CLI de génération (`dotnet new ostrun`), en complément de l'UI web
- Ajout d'un 2ᵉ service, idéalement en Node, pour prouver concrètement le polyglottisme
- Kafka envisagé uniquement si un vrai besoin de replay / event sourcing apparaît

## Contribuer

Les contributions externes sont bienvenues une fois le MVP publié. Voir [CONTRIBUTING.md](./CONTRIBUTING.md) pour le process, les conventions d'events, et le [CLA](./CLA.md) à accepter sur votre première pull request.

## Licence

Distribué sous licence [Apache-2.0](./LICENSE).
