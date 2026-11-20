# Research: VRChat logs and status API

Last verified: 2026-11-20 (this Windows PC + public docs / NuGet).

## Log location (PC)

`%UserProfile%\AppData\LocalLow\VRChat\VRChat\output_log_yyyy-MM-dd_HH-mm-ss.txt`

Official help: [Where do I find my Output Logs](https://help.vrchat.com/hc/en-us/articles/9521522810899-Where-do-I-find-my-Output-Logs-and-Crash-Dumps).

Files older than ~24h are deleted when VRChat launches. Each VRChat start creates a new file - the watcher must follow the newest file.

## Live join lines (this machine)

From `output_log_2026-11-20_00-42-24.txt` (player/group ids redacted in fixtures):

```text
2026.11.20 00:42:39 Debug      -  [Behaviour] Entering Room: VRChat Home
2026.11.20 00:42:39 Debug      -  [Behaviour] Joining wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd:28829~friends(usr_…)~region(eu)
2026.11.20 00:42:39 Debug      -  [Behaviour] Joining or Creating Room: VRChat Home
2026.11.20 00:42:39 Debug      -  [Behaviour] Successfully joined room
2026.11.20 00:43:18 Debug      -  [Behaviour] Entering Room: Cuddle ＆ Sleep
2026.11.20 00:43:18 Debug      -  [Behaviour] Joining wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3:75618~group(grp_…)~groupAccessType(public)~ageGate~region(us)
```

**Parser target:** `[Behaviour] Joining wrld_<uuid>:<instance…>`  
Do **not** key off room display names (`Entering Room:` / `Joining or Creating Room:`).

Encoding observed here: UTF-8 (no BOM), lines start with `yyyy.MM.dd`.

## Status colours ↔ API strings

| UI colour | Common name | API `status` |
|-----------|-------------|--------------|
| Blue | Join Me | `join me` |
| Green | Online | `active` |
| Orange | Ask Me | `ask me` |
| Red | Do Not Disturb | `busy` |

Sources: [VRChat Wiki - Social](https://wiki.vrchat.com/wiki/Social), community OpenAPI ([update user](https://vrchat.vercel.app/reference/update-user)).

Protection applies **only** `ask me` or `busy`. Never auto-set `join me`.

Update path: `PUT /api/1/users/{userId}` with `{ "status": "ask me" }` (auth cookie required).

## Auth / 2FA (.NET)

Package: [`VRChat.API` 2.24.0](https://www.nuget.org/packages/VRChat.API/2.24.0) (net8.0).

- Preferred: TOTP secret + `LoginAsync()` for seamless re-auth.
- Interactive tray: manual `GetCurrentUser` → `Verify2FA` / `Verify2FAEmailCode` flow from the SDK README.
- Persist `auth` + `twoFactorAuth` cookies via DPAPI (`ICredentialStore`).

Identify the client (`WithApplication` / User-Agent). VRChat rejects anonymous clients.

## Abuse / rate guidance (community)

Unofficial API disclaimer (VRChat team via Tupper, quoted in SDK README):

- No official support.
- Do not query more than once per 60 seconds.
- Abuse may terminate the account.
- Endpoints can break without notice.

**Implication:** status changes must be coalesced; startup + enter-world storms need a single-flight + ≥60s spacing.

## Friction / kill criteria

| Risk | Kill / pause criterion |
|------|-------------------------|
| Account ban / ToS hammer | Any enforcement contact or sudden persistent 401 after compliant rate → stop auto updates, operator-only mode |
| Log format drift | Join regex miss rate >5% on a known session → pause protection, ship parser fix |
| Rate limit / Cloudflare | Repeated 429 → exponential backoff capped; surface Disconnected/Failed in tray |
| Brief blue exposure | Accepted in v1; not a kill - document only |

Cheapest falsification for milestone 1: run Core parser against a fresh live log after one world hop; login once; `PUT` orange; confirm friends list / Quick Menu shows Ask Me within ~60s.
