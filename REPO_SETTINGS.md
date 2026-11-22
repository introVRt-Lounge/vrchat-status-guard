# Repo settings

| Field | Value |
|-------|-------|
| Owner | `introVRt-Lounge` |
| Name | `vrchat-status-guard` |
| Visibility | **Public** |
| Default branch | `main` |
| Profile | Public OSS - Velopack Setup.exe is the only installer |

## CI

Workflow: `.github/workflows/ci.yml`  
Required aggregate job name: **`ci`** (test + installer + secret-scan + owasp-sast + security-audit).  
Runners:
- **`windows-latest`**: test, security-audit, installer (Velopack pack artifact)
- **`ubuntu-latest`**: gitleaks + Semgrep only (Linux container images; not the product runtime)

Release: `.github/workflows/release.yml` on `v*` tags - Velopack only (no Inno).

Public repo: enable branch protection with required check **`ci`** when the plan allows. Dependabot + `automerge` label use embedded `merge-on-green` on green `ci`.

## Dependabot

- `.github/dependabot.yml` (nuget + github-actions, 7-day cooldown)
- PRs auto-merge on green `ci`
- Vulnerability alerts + security updates enabled via API at bootstrap.

## Platform security (probe and record)

| Control | Expected (public) | Notes |
|---------|-------------------|-------|
| Secret scanning / push protection | Enable in repo settings | CI still runs gitleaks |
| CodeQL / Code Quality | Optional | Document if enabled |
| Dependabot alerts | On | Via API at bootstrap |

Last probe: 2026-11-21 (public; docs sweep #64).

## Git hygiene

Default PR path: issue → branch → PR with `Fixes #N` → squash merge. See [AGENTS.md](AGENTS.md)
(**issues-first** + **happy git place**: clean `main` only between tasks).

Local iteration install: `.\scripts\deploy-local-install.ps1` (Startup Run target, not `bin\Release`).
Release pack: `.\scripts\build-velopack.ps1 -Version <semver>` (CI smoke uses `0.1.0`).
