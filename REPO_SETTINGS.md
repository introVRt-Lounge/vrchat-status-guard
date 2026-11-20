# Repo settings

| Field | Value |
|-------|-------|
| Owner | `introVRt-Lounge` |
| Name | `vrchat-privacy` |
| Visibility | **Private** |
| Default branch | `main` |
| Profile | Private app - Tier **I** (+ I+ extras) |

## CI

Workflow: `.github/workflows/ci.yml`  
Required aggregate job name: **`ci`** (test + secret-scan + owasp-sast + security-audit).

Free private: branch protection / required checks often unavailable. Dependabot + `automerge` label use embedded `merge-on-green` on green `ci`.

## Dependabot

- `.github/dependabot.yml` (nuget + github-actions, 7-day cooldown)
- PRs auto-merge on green `ci`
- Enable vulnerability alerts + security updates via API when auth available:

```bash
gh api -X PUT repos/introVRt-Lounge/vrchat-privacy/vulnerability-alerts
gh api -X PUT repos/introVRt-Lounge/vrchat-privacy/automated-security-fixes
```

## Platform security (probe and record)

| Control | Expected on free private | Notes |
|---------|--------------------------|-------|
| Secret scanning / push protection | Often unavailable | Rely on gitleaks CI + local discipline |
| CodeQL / Code Quality | May 403 | Document skip if REST fails |
| Dependabot alerts | Enable via API | See above |

Last probe: 2026-11-20 (repo created; alerts enable attempted at push time).

## Git hygiene

Default PR path; solo direct-to-`main` allowed during bootstrap with wrap-up (CI green, TODO current).
