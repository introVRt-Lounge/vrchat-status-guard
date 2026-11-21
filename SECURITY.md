# Security

## Scope

This private Windows tray app authenticates to the unofficial VRChat API and reads the local user's VRChat output logs.

## Reporting

Email security concerns to the introVRt Lounge maintainers (HeavyGee / org admins). Do not open a public issue for credential exposure or auth bugs.

## Rules for contributors and agents

- Never log or commit passwords, OTP/TOTP codes, recovery codes, `auth` / `twoFactorAuth` cookies, or session tokens.
- Persist reusable auth with Windows DPAPI (or equivalent OS secret store). Plaintext cookie files are a defect.
- Prefer sanitized log fixtures; live logs may contain player ids and must stay off git.
- Respect VRChat's community API abuse guidance (including low request rates). Account bans are a real risk.

## Prod / staging

There is no hosted service in v1. "Prod" means the installed tray binary on a member PC. Compromise of that machine compromises the stored session - treat DPAPI + local account lockdown accordingly.
