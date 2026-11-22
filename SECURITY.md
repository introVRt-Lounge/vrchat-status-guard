# Security

## Scope

This Windows tray app authenticates to the unofficial VRChat API and reads the local user's VRChat output logs. Session cookies are stored with Windows DPAPI on the member PC.

## Reporting

**Do not open a public GitHub Issue** for credential exposure, auth bugs, or other vulnerabilities.

**Primary path:** GitHub **Private vulnerability reporting**.

→ [Report a vulnerability](https://github.com/introVRt-Lounge/vrchat-status-guard/security/advisories/new)

That form is the dedicated private channel (Settings → Code security → Private vulnerability reporting is enabled). Maintainers triage via Security advisories; coordinated disclosure only.

**Fallback:** email `h34vyg33@gmail.com` with subject `vrchat-status-guard security` if you cannot use the form.

Do not paste live secrets, session cookies, or recovery codes beyond what is needed to prove impact; redact where possible.

## Rules for contributors and agents

- Never log or commit passwords, OTP/TOTP codes, recovery codes, `auth` / `twoFactorAuth` cookies, or session tokens.
- Persist reusable auth with Windows DPAPI (or equivalent OS secret store). Plaintext cookie files are a defect.
- Prefer sanitized log fixtures; live logs may contain player ids and must stay off git.
- Respect VRChat's community API abuse guidance (including low request rates). Account bans are a real risk.

## Prod / staging

There is no hosted service in v1. "Prod" means the installed tray binary on a member PC. Compromise of that machine compromises the stored session - treat DPAPI + local account lockdown accordingly.
