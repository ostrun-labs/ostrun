# Event naming convention, CI validation specification

This document is the **reference specification** for validating Ostrun events, with its test cases. CI only implements part of it today: the contract file checks in §2.6 run on every PR (`validate-contracts` in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)). The maintainer checks the other rules by hand during review until CI covers them. See §5.

## 1. Event name format

```
{Provider}.{Service}.{Entity}{PastParticiple}
```

Example: `Ostrun.Auth.UserRegistered`, `Acme.Billing.InvoiceCreated`

## 2. Validation rules, by severity level

### 2.1 Structure (hard fail, blocks the PR)

The name must be 3 PascalCase segments separated by dots:

```regex
^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$
```

- Each segment starts with an uppercase letter, followed by letters/digits only (no `_`, `-`, spaces)
- Exactly 2 dots, so exactly 3 segments: not 2 (old convention), not 4+

### 2.2 Provider consistency (hard fail)

- The event's `Provider` segment must be **identical** (character for character) to the `provider` field declared in the manifest (`ostrun.manifest.json`) of the service publishing this event.
- A service cannot publish an event under another provider's name.

### 2.3 Provider collision (hard fail, at the catalog level, not the event level)

- Two different services cannot declare the same `provider` (**case-insensitive** comparison) unless they share the same maintainer.
- Check: when a service is added, compare its `provider` against the `provider` of every service already listed in [`catalog.json`](../catalog.json). Case-insensitive collision (`Acme` and `ACME` collide).
- The repo maintainer decides manually when a collision is detected (no auto-resolution).

### 2.4 `PastParticiple` segment (loose validation, warning, doesn't block)

No closed list of verbs: the catalog is meant to grow, and a strict list would require a PR for every new business verb. CI applies a two-step heuristic to the last segment (`{Entity}{PastParticiple}`):

1. **Ends in `ed`**: treated as a regular past participle, silent (the vast majority of cases: `Registered`, `Created`, `Updated`, `Deleted`, `Failed`, `Renewed`...)
2. **Otherwise, checked against an open list** of irregular past participles, stored in `/contracts/_verbs/irregular-participles.txt` (one word per line, PR-extensible without touching the validation script), e.g. `Sent`, `Paid`, `Done`, `Given`, `Taken`, `Written`, `Broken`, `Chosen`, `Held`, `Built`, `Sold`, `Bought`, `Caught`, `Brought`, `Taught`, `Found`, `Left`, `Lost`, `Won`, `Gone`, `Made`
3. **Otherwise**: CI posts a **warning comment** on the PR ("the last segment doesn't look like a known past participle, please confirm") but **doesn't block** the merge

### 2.5 Command detection (hard fail)

Independent of 2.4, if the last segment ends **exactly** with a known imperative/infinitive verb (a near-certain sign of a missed conjugation), CI blocks:

```regex
(Create|Update|Delete|Add|Remove|Set|Get|Send|Make|Register|Cancel|Renew|Pay|Login|Logout)$
```

E.g. `Auth.CreateUser`: **blocked** (`Create` at the end of the segment). `Auth.UserCreated`: OK (ends in `ed`, rule 2.4.1).

### 2.6 Contract file (hard fail)

```
/contracts/{provider}/{service}/{Entity}{PastParticiple}.v{N}.json
```

- The file name (excluding `.v{N}.json`) must match the event's `{Entity}{PastParticiple}` segment.
- The `$id`/`title` declared in the JSON schema must match the full event name (3 segments).
- `{N}` in the file name must match the `version` field declared in the schema.

## 3. Validation algorithm (pseudocode)

```
for each file changed in /contracts/**/*.json (PR):
    read provider, service from the file path
    read eventName, version from the schema content (title/$id, version)

    1. eventName matches the structural regex (§2.1)          -> else FAIL
    2. eventName starts with "{provider}."                     -> else FAIL
    3. service manifest provider == path provider              -> else FAIL
    4. provider (case-insensitive) absent from other manifests
       (unless same maintainer)                                -> else FAIL
    5. last segment ends with a blocklisted verb                -> if so, FAIL (§2.5)
    6. last segment ends in "ed"
       OR present in irregular-participles.txt                 -> else WARNING only (§2.4)
    7. file name == {Entity}{PastParticiple}                   -> else FAIL
    8. {N} in file name == version in schema                   -> else FAIL
```

## 4. Validation test cases

These cases will become the CI script's test suite once it covers the full spec.

| Event name | Expected result | Reason |
|---|---|---|
| `Ostrun.Auth.UserRegistered` | ✅ Pass | structure OK, `ed`, consistent provider |
| `Acme.Billing.InvoiceCreated` | ✅ Pass | structure OK, `ed` |
| `Acme.Mailer.EmailSent` | ✅ Pass (with irregular-list check) | `Sent` is in `irregular-participles.txt` |
| `Auth.UserRegistered` | ❌ Fail | only 2 segments, no provider |
| `ostrun.Auth.UserRegistered` | ❌ Fail | `ostrun` lowercase, not PascalCase |
| `Ostrun.Auth.CreateUser` | ❌ Fail | ends in `Create` (blocklist §2.5) |
| `Ostrun.Auth.UserLogin` | ❌ Fail | ends in `Login` (blocklist §2.5) |
| `Acme.Auth.UserRegistered` (published by a service whose manifest declares `provider: "Globex"`) | ❌ Fail | provider/manifest mismatch (§2.2) |
| `Ostrun.Auth.UserWoken` | ⚠️ Warning, not a fail | doesn't end in `ed`, absent from the irregular list, needs manual confirmation |
| File `/contracts/ostrun/auth/UserRegistered.v1.json` containing `"title": "Ostrun.Auth.UserRegistered"`, `"version": 1` | ✅ Pass | file, title, and version consistent |
| File `/contracts/ostrun/auth/UserRegistered.v2.json` containing `"version": 1` | ❌ Fail | file version (`v2`) != schema version (`1`) |
| Two manifests with `provider: "Acme"` and `provider: "ACME"` from two different maintainers | ❌ Fail | case-insensitive collision (§2.3) |

## 5. Status

Specification validated on 2026-08-17.

What CI enforces today (`validate-contracts` job):

- §2.6: file name matches the entity, `title` provider and service match the directory, file version matches `version`.

Checked by hand in review until CI covers them:

- §2.1 structure regex
- §2.2 provider consistency with the manifest
- §2.3 provider collision
- §2.4 past participle warning
- §2.5 command blocklist
