# Repo settings

| Field | Value |
|-------|-------|
| Owner | `introVRt-Lounge` |
| Name | `vrchat-status-guard` |
| Visibility | **Public** |
| Default branch | `main` |
| Org plan | Free (public Tier H + branch protection available) |
| Profile | Public OSS - Velopack Setup.exe is the only installer |

## CI

Workflow: `.github/workflows/ci.yml`  
Required aggregate job name: **`ci`** (test + installer + secret-scan + owasp-sast + security-audit).  
Runners:
- **`windows-latest`**: test, security-audit, installer (Velopack pack artifact)
- **`ubuntu-latest`**: gitleaks + Semgrep only (Linux container images; not the product runtime)

Release: `.github/workflows/release.yml` - **push to `main`** auto-publishes only for conventional **major/minor** (`breaking` / `feat`). **Patch** (`fix`, `docs`, `chore`, `ops`, …) is **on demand** via `gh workflow run release.yml --ref main` (empty input = patch bump; or `-f version=X.Y.Z`). Optional `v*` tags. Do not reintroduce tag-only without an auto-tagger (#72).

## Branch protection (`main`)

| Setting | Value |
|---------|-------|
| Required status checks | **`ci`** (strict / up to date) |
| Enforce admins | true |
| Force push / delete | blocked |
| Allow auto-merge | true |
| Delete branch on merge | true |

Public free orgs **can** protect branches - required after private→public flip (perfect-github Tier H).

## Code security (Tier H probe 2026-11-22)

Screenshot row → position:

| UI control | Required (public) | This repo | How |
|-----------|-------------------|-----------|-----|
| Security policy | On | **Enabled** (`SECURITY.md`) | In-repo |
| Security advisories | On | **Enabled** | Platform default |
| Private vulnerability reporting | **On** | **Enabled** | `PUT …/private-vulnerability-reporting` |
| Dependabot alerts | On | **Enabled** | `PUT …/vulnerability-alerts` |
| Dependabot security updates | On | **Enabled** | `PUT …/automated-security-fixes` |
| Code scanning alerts (CodeQL) | On | **Configured** (csharp + actions) | `PATCH …/code-scanning/default-setup` |
| Secret scanning alerts | On | **Enabled** | `PATCH` `security_and_analysis.secret_scanning` |
| Secret push protection | On | **Enabled** | `PATCH` `…secret_scanning_push_protection` |
| Code quality findings | On when REST allows | **Configured** (csharp) | `PATCH …/code-quality/setup` |
| Code coverage (Code Quality) | Optional | Operator UI if wanted | Settings → Code quality → Coverage (no REST) |
| Non-provider secret patterns / validity checks | Optional | Off | Leave unless noise is wanted |

**SECURITY.md** leads with [Report a vulnerability](https://github.com/introVRt-Lounge/vrchat-status-guard/security/advisories/new); email is fallback only.

## Dependabot

- `.github/dependabot.yml` (nuget + github-actions, 7-day cooldown)
- Native auto-merge path available now that `ci` is a required check; embedded `merge-on-green` remains as belt-and-braces

## Git hygiene

Default PR path: issue → branch → PR with `Fixes #N` → squash merge. See [AGENTS.md](AGENTS.md)
(**issues-first** + **happy git place**: clean `main` only between tasks).

Local iteration install: `.\scripts\deploy-local-install.ps1` (Startup Run target, not `bin\Release`).
Release pack: `.\scripts\build-velopack.ps1 -Version <semver>` (CI smoke uses `0.1.0`).

## Not in scope for this product

| Item | Position |
|------|----------|
| GitHub Pages / Tier E docs site | Skip - no library docs site; marketing is introvrtlounge.com |
| FUNDING.yml / Sponsor button | Optional later - not required for Status Guard |
| Social preview Open Graph | **Asset in repo:** `.github/social-preview.png` (1280×640). **Upload:** Settings → General → Social preview (no REST API) - operator one-click after merge |
