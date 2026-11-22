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

## Happy git place (session end / between tasks)

Default resting state for this repo is **clean main only**. Before claiming idle, done, or handing off:

1. **Branch:** `main`, tracking `origin/main`, up to date (`git pull --ff-only`).
2. **Worktree:** no extra worktrees (`git worktree list` shows only this checkout). Remove leftovers with `git worktree remove`.
3. **Working tree:** no uncommitted or untracked work you own for this session (commit+PR, stash only if operator asked, or discard only if operator said so).
4. **PRs:** no open PRs left unmerged that this session created or was driving - merge (when green + operator OK) or close with reason.
5. **Issues:** close GitHub issues this session actually finished (`Fixes #N` / `gh issue close`). Do not leave "done in code, open on GitHub."
6. **Local branches:** delete merged topic branches locally and on origin after merge.
7. **Ship:** after **feat / breaking** merges to `main`, confirm a **GitHub Release** exists for that HEAD (or newer) - auto major/minor. **Patch**-level work (`fix`/`docs`/`ops`/…) does **not** auto-ship; cut on demand with `gh workflow run release.yml --ref main` (optional `-f version=X.Y.Z`). Do not call the session done while a feat/breaking change is unreleased. Local `deploy-local-install.ps1` is not a substitute.

**Not clean** = still on a feature branch, dirty tree, stray worktree, open PR, finished work still filed as an open issue, or **feat/breaking on main with no Release covering it**. Patch backlog without a dispatch is OK until you need users to get the fix.

## Design invariants

- World **ID** matching only.
- Never invent blue / `join me` on leave; blue only when explicitly set for a world.
- Separate Core (testable) from App (WinForms).
- Rate-limit VRChat API mutations (~60s).
