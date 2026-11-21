# Agent notes

## Project

Windows tray guard for VRChat status protection (orange/red status). Org: `introVRt-Lounge`. Private.

## Identity

- GitHub ops for this org: **`heavygee`** via working `gh` (on this Windows box keyring tokens may be stale - use the LAN `server` host's `gh` or re-auth locally).
- Git remotes: `git@github.com:introVRt-Lounge/vrchat-status-guard.git` (SSH as heavygee).

## Issues-first workflow (mandatory)

**No ticket, no workee.** Do not implement features or bugfixes requested in chat until a tracking GitHub Issue exists.

| Request in chat | Agent action |
|-----------------|--------------|
| New feature | Create issue (`[feat]: …`) via `gh issue create`, then branch |
| Bug / regression | Create issue (`[bug]: …`), then branch |
| Question / explanation only | Answer; no issue required |
| Docs typo (one line) | Fix directly; issue optional |

Before writing implementation code:

1. Confirm repo has a GitHub remote and `gh` auth for the owning identity (**github-identity-management**).
2. Open or find the issue; note the number.
3. Branch name should include the issue number when practical (`feat/123-short-name`).
4. PR body must include `Fixes #N`.

**Exceptions** (no issue required):

- Operator explicitly says "skip issue" or "no ticket".
- One-line typos, comment-only edits, or doc fixes with no behavior change.
- Active production incident when the operator declares hotfix mode.

If the operator describes substantive work but no issue exists, **create the issue first**, paste the URL back, then proceed.

## Design invariants

- World **ID** matching only.
- Never invent blue / `join me` on leave; blue only when explicitly set for a world.
- Separate Core (testable) from App (WinForms).
- Rate-limit VRChat API mutations (~60s).
