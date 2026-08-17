# Contribuer à Ostrun

Merci de l'intérêt porté à Ostrun ! Ce projet est pensé dès le départ pour accueillir des contributions externes, avec quelques règles pour que ça reste cohérent et maintenable.

## Avant de contribuer

### 1. CLA (Contributor License Agreement)

Toute contribution (code, doc, config) nécessite l'acceptation du [CLA](./CLA.md). Sur votre première pull request, un bot vous guidera automatiquement pour l'accepter en un clic — rien à faire en amont.

### 2. Licence du projet

Ostrun est distribué sous licence **Apache-2.0**. En contribuant, vous acceptez que votre contribution soit distribuée sous cette licence (voir le CLA pour le détail).

## Principes d'architecture à respecter

Avant de proposer un nouveau service, gardez en tête les règles non négociables du projet :

- **Indépendance stricte** : votre service a sa propre base de données/son propre schéma. Jamais de DB partagée, jamais d'appel direct au code d'un autre service.
- **Communication uniquement via API (synchrone) ou events (asynchrone)**.
- **Polyglotte autorisé** : écrivez votre service dans le langage de votre choix (Node, Python, etc.), tant qu'il respecte le contrat (API + events) et fournit un `Dockerfile` + un endpoint `/health`.
- **Pas de couplage runtime** : aucune dépendance à un service "central" au démarrage, en dehors du bus d'events et de la gateway.

## Ajouter un nouveau service

1. Choisissez un **provider** : un identifiant déclaratif (libre, ce n'est pas forcément votre handle GitHub) qui vous identifie comme contributeur/mainteneur. La CI vérifie automatiquement qu'il n'entre pas en collision avec un provider déjà utilisé par quelqu'un d'autre.
2. Générez le squelette avec le template officiel (`dotnet new ostrun-service` ou équivalent selon votre stack — voir `/scaffold`). Votre service est rangé dans `/services/{provider}/{service}/` — un même provider peut héberger plusieurs services.
3. Complétez le **manifeste de service** (`services/{provider}/{service}/ostrun.manifest.json`) :
   - `provider`, `service`, `version`
   - nom affiché et description (pour le catalogue)
   - events publiés / consommés (référence aux fichiers `/contracts`)
   - variables d'environnement attendues (nom, description, requis/optionnel, valeur par défaut)
   - port(s) exposé(s)
   - dépendances vers d'autres services (ex. si votre service nécessite qu'Auth soit aussi sélectionné)
   - chemin/format du health check
4. Assurez-vous que votre service expose `/health` et démarre seul via son propre `Dockerfile`.
5. Ne mettez **aucun secret en dur** dans le code ou l'image : passez par des variables d'environnement, chargées via `.env` en local (fournissez un `.env.example` documentant les clés attendues, sans valeurs réelles).
6. Ajoutez votre service au `docker-compose.yml` du projet.

Pour le moment, l'ajout d'un service au **catalogue officiel** (celui exposé dans l'UI de génération) est validé au cas par cas par le mainteneur du projet — ce point sera revu une fois le MVP public.

## Ajouter ou modifier un event

C'est le point le plus sensible du projet — merci de suivre ces règles à la lettre :

1. **Convention de nommage stricte à 3 segments** : `{Provider}.{Service}.{Entité}{VerbePassé}` — ex. `Ostrun.Auth.UserRegistered`. Le `Provider` évite les collisions entre deux services de même nom écrits par des contributeurs différents.
2. Le dernier segment se termine par un **participe passé** décrivant un fait accompli (`UserRegistered`, jamais `CreateUser`). La validation reste **souple** (pas de liste fermée de verbes) : le catalogue de services étant amené à grandir, une liste stricte imposerait une PR à chaque nouveau verbe métier.
3. Toute évolution d'un event existant crée une **nouvelle version** (`v2`, etc.) plutôt que de casser l'existante.
4. Le schéma (JSON Schema / Protobuf) va dans `/contracts/{provider}/{service}/{Entité}{VerbePassé}.v1.json`, versionné.
5. Votre pull request doit passer la **CI de validation des contrats** : unicité du nom, cohérence entre le `provider` déclaré dans le manifeste et celui utilisé dans les events publiés, validité du schéma. Une PR qui ne passe pas cette validation ne sera pas mergée.

Il n'y a **pas de service central de gouvernance des events** — la cohérence se fait via revue de PR, pas via un point d'exécution unique.

> Spécification complète de validation (regex, heuristique sur les verbes, cas de test) : voir [`/contracts/README.md`](./contracts/README.md).

## Process de pull request

1. Forkez le repo, créez une branche depuis `main`.
2. Assurez-vous que les tests et la CI passent (build, tests, validation des contrats si vous touchez à `/contracts`).
3. Décrivez clairement dans la PR : ce que fait le service/changement, les events publiés/consommés le cas échéant.
4. Acceptez le CLA si c'est votre première contribution.
5. Un mainteneur relit et merge.

## Signaler un bug ou proposer une idée

Ouvrez une issue avec un maximum de contexte (comportement attendu vs observé, logs, étapes de reproduction). Pour une proposition d'architecture ou de nouveau service, une discussion en issue avant la PR est bienvenue pour éviter le travail perdu.

## Maintenance et délais

Ce projet est maintenu sur mon temps personnel — les retours sur les issues/PR peuvent prendre un peu de temps. Merci de votre patience.
