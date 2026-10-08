# Security Policy

## Reporting a vulnerability

**Do not open a public issue for a security vulnerability.** A public issue exposes the problem to everyone before a fix exists.

Instead, use the **Security > Report a vulnerability** tab on the relevant repo (GitHub Security Advisories). It opens a private report visible only to you and the maintainer, with a discussion channel and the security advisory mechanism if the report is confirmed.

If you don't have access to that feature for any reason, contact the maintainer directly (open an issue asking for a private channel, without detailing the vulnerability).

Please include, as much as possible:

- the repo and version/commit affected,
- a description of the issue and its potential impact,
- reproduction steps (or a minimal PoC),
- your assessment of severity, if you have one.

## Scope

Applies to the `ostrun` repo (gateway, generator, contracts) and the reference services `ostrun-auth-service` / `ostrun-messaging-service`. For a third-party service catalogued by someone else, contact its maintainer directly. Ostrun has no runtime governance service and cannot patch code it doesn't own.

## Response time

Maintained on personal time. No formal SLA, but a security report is prioritized over regular issues/PRs. Acknowledgment is targeted within a few days.

## Supported versions

The project has no versioned release yet (MVP). Security fixes apply to the `main` branch of each repo, which always reflects the latest state.
