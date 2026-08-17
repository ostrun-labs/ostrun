# Convention de nommage des events — spécification de validation CI

Ce document est la **spécification de référence** pour la validation des events Ostrun. Il ne contient pas encore de code : c'est le contrat que le futur script de validation CI (à écrire au moment du scaffold generator) devra implémenter, avec ses cas de test.

## 1. Format d'un nom d'event

```
{Provider}.{Service}.{Entité}{VerbePassé}
```

Exemple : `Ostrun.Auth.UserRegistered`, `Acme.Billing.InvoiceCreated`

## 2. Règles de validation, par niveau de sévérité

### 2.1 Structure — hard fail (bloque la PR)

Le nom doit être 3 segments PascalCase séparés par des points :

```regex
^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$
```

- Chaque segment commence par une majuscule, suivi de lettres/chiffres uniquement (pas de `_`, `-`, espace)
- Exactement 2 points, donc exactement 3 segments — ni 2 (ancienne convention), ni 4+

### 2.2 Cohérence provider — hard fail

- Le segment `Provider` de l'event doit être **identique** (au caractère près) au champ `provider` déclaré dans le manifeste (`ostrun.manifest.json`) du service qui publie cet event.
- Un service ne peut pas publier un event sous le nom d'un autre provider.

### 2.3 Collision de provider — hard fail (au niveau du catalogue, pas de l'event)

- Deux services différents ne peuvent pas déclarer le même `provider` (comparaison **insensible à la casse**) s'ils ne partagent pas le même mainteneur.
- Vérification : à l'ajout d'un service, comparer son `provider` à tous les `provider` déjà présents dans `/services/**/ostrun.manifest.json`. Collision insensible à la casse (`Acme` et `ACME` entrent en collision).
- Le mainteneur du repo tranche manuellement en cas de collision détectée (pas d'auto-résolution).

### 2.4 Segment `VerbePassé` — validation souple (warning, ne bloque pas)

Pas de liste fermée de verbes (le catalogue est amené à grandir — une liste stricte imposerait une PR à chaque nouveau verbe métier). La CI applique une heuristique en 2 temps sur le dernier segment (`{Entité}{VerbePassé}`) :

1. **Se termine par `ed`** → considéré comme un participe passé régulier, silencieux (cas très majoritaire : `Registered`, `Created`, `Updated`, `Deleted`, `Failed`, `Renewed`...)
2. **Sinon, vérifié contre une liste ouverte** de participes passés irréguliers, stockée dans `/contracts/_verbs/irregular-participles.txt` (un mot par ligne, PR-extensible sans toucher au script de validation) — ex. `Sent`, `Paid`, `Done`, `Given`, `Taken`, `Written`, `Broken`, `Chosen`, `Held`, `Built`, `Sold`, `Bought`, `Caught`, `Brought`, `Taught`, `Found`, `Left`, `Lost`, `Won`, `Gone`, `Made`
3. **Sinon** → la CI poste un **commentaire d'avertissement** sur la PR ("le dernier segment ne ressemble pas à un participe passé connu, merci de confirmer") mais **ne bloque pas** le merge

### 2.5 Détection de commande — hard fail

Indépendamment de 2.4, si le dernier segment se termine **exactement** par un verbe à l'impératif/infinitif connu (signe quasi certain d'un oubli de conjugaison), la CI bloque :

```regex
(Create|Update|Delete|Add|Remove|Set|Get|Send|Make|Register|Cancel|Renew|Pay|Login|Logout)$
```

Ex. `Auth.CreateUser` → **bloqué** (`Create` en fin de segment). `Auth.UserCreated` → OK (se termine par `ed`, règle 2.4.1).

### 2.6 Fichier de contrat — hard fail

```
/contracts/{provider}/{service}/{Entité}{VerbePassé}.v{N}.json
```

- Le nom de fichier (hors `.v{N}.json`) doit être identique au segment `{Entité}{VerbePassé}` de l'event.
- Le `$id`/`title` déclaré dans le schéma JSON doit être identique au nom complet de l'event (3 segments).
- `{N}` dans le nom de fichier doit correspondre au champ `version` déclaré dans le schéma.

## 3. Algorithme de validation (pseudocode)

```
pour chaque fichier modifié dans /contracts/**/*.json (PR) :
    lire provider, service depuis le chemin du fichier
    lire eventName, version depuis le contenu du schéma (title/$id, version)

    1. eventName matche la regex structurelle (§2.1)         → sinon FAIL
    2. eventName commence par "{provider}."                   → sinon FAIL
    3. provider du manifeste du service == provider du chemin → sinon FAIL
    4. provider (case-insensitive) absent des autres manifestes
       (sauf même mainteneur)                                 → sinon FAIL
    5. dernier segment se termine par un verbe de blocklist    → si oui, FAIL (§2.5)
    6. dernier segment se termine par "ed"
       OU présent dans irregular-participles.txt               → sinon WARNING seulement (§2.4)
    7. nom de fichier == {Entité}{VerbePassé}                  → sinon FAIL
    8. {N} du nom de fichier == version dans le schéma         → sinon FAIL
```

## 4. Cas de test de validation

Ces cas serviront de suite de tests au script CI une fois implémenté (post-MVP / au moment du scaffold generator).

| Nom d'event | Résultat attendu | Raison |
|---|---|---|
| `Ostrun.Auth.UserRegistered` | ✅ Pass | structure OK, `ed`, provider cohérent |
| `Acme.Billing.InvoiceCreated` | ✅ Pass | structure OK, `ed` |
| `Acme.Mailer.EmailSent` | ✅ Pass (avec vérif liste irrégulière) | `Sent` dans `irregular-participles.txt` |
| `Auth.UserRegistered` | ❌ Fail | 2 segments seulement, pas de provider |
| `ostrun.Auth.UserRegistered` | ❌ Fail | `ostrun` en minuscule, pas PascalCase |
| `Ostrun.Auth.CreateUser` | ❌ Fail | se termine par `Create` (blocklist §2.5) |
| `Ostrun.Auth.UserLogin` | ❌ Fail | se termine par `Login` (blocklist §2.5) |
| `Acme.Auth.UserRegistered` (publié par un service dont le manifeste déclare `provider: "Globex"`) | ❌ Fail | incohérence provider/manifeste (§2.2) |
| `Ostrun.Auth.UserWoken` | ⚠️ Warning, pas de fail | ne finit pas par `ed`, absent de la liste irrégulière — à confirmer manuellement |
| Fichier `/contracts/ostrun/auth/UserRegistered.v1.json` contenant `"title": "Ostrun.Auth.UserRegistered"`, `"version": 1` | ✅ Pass | fichier, title et version cohérents |
| Fichier `/contracts/ostrun/auth/UserRegistered.v2.json` contenant `"version": 1` | ❌ Fail | version fichier (`v2`) ≠ version schéma (`1`) |
| Deux manifestes avec `provider: "Acme"` et `provider: "ACME"` par deux mainteneurs différents | ❌ Fail | collision insensible à la casse (§2.3) |

## 5. Statut

Spécification validée (2026-08-17). Implémentation du script CI reportée à l'attaque du scaffold generator — voir `ostrun-contexte-complet.md` pour la roadmap.
