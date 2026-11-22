# TODO

## Done

- [x] Private GitHub repo `introVRt-Lounge/vrchat-status-guard`
- [x] Tier I baseline (README, CONTRIBUTING, SECURITY, CI with test/secret-scan/owasp-sast/audit, Dependabot)
- [x] Core library: world id parse, log join parse (live-verified pattern), protection rules/decisions
- [x] Unit tests for parse + decision invariants (no blue restore)
- [x] Research notes + milestone 1 plan

## Done (runtime)

- [x] [#1](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/1) Log directory watcher
- [x] [#2](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/2) DPAPI + VRChat.API session (login/2FA)
- [x] [#3](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/3) Apply orange/red on protected world entry (in-game confirm = operator manual check)
- [x] [#6](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/6) Windows wizard installer
- [x] Default protected world: `wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd` (VRChat Home)
- [x] Tray: login, pause, protected worlds, status

## Done (ops)

- [x] Start-with-Windows enabled on Teemo (HKCU Run + installer option default checkedonce)
- [x] Protection status set to Red for live check; tray prompts Login on first start (no credential scraping)

## Next

- [x] [#33](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/33) Perfect-GitHub public readiness + flip public
- [x] [#34](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/34) Velopack auto-update from GitHub Releases
- [x] [#35](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/35) What's New UI from GitHub release notes
- [x] [#38](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/38) About page: feedback + introVRt Lounge credits
- [x] [#40](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/40) About Lounge links + missing tray icon
- [x] [#42](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/42) Single Velopack installer (drop dual Inno path)
- [x] [#10](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/10) Login + 2FA works (cookie rebuild after TOTP)
- [x] [#11](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/11) Settings Save/Cancel button bar
- [x] [#12](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/12) Protected worlds Add/Remove list UI
- [x] [#13](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/13) World name + thumbnail in settings list
- [x] [#14](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/14) Recent worlds picker → Add to protected
- [x] [#15](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/15) Side-by-side Recent|Protected with large thumbs + move across
- [x] [#16](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/16) Per-world group representation (registry order + API names/banners)
- [x] [#17](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/17) Tray double-click opens protected worlds
- [x] [#18](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/18) Per-world status including Blue (join me)
- [x] [#19](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/19) Tray single-click toggles protection pause
- [x] [#20](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/20) First-run privacy setup wizard
- [x] [#21](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/21) VRChat-styled protection hub UI
- [x] [#22](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/22) Tray double-click must not toggle pause
- [x] [#23](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/23) High-DPI text clipping in protection hub
- [x] [#24](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/24) Hub perf, empty protected cards, selection auto-advance
- [x] [#25](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/25) Hub edit flow + progressive selection summary
- [x] [#26](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/26) Hub sole-focus add/edit + world thumb in summary tile
- [x] [#27](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/27) Group step Next vs Skip + scroll to selected
- [x] [#28](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/28) Group logo on protected world cards
- [x] [#29](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/29) Prefix protected card group with Repping
- [x] [#31](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/31) Tray single-click pause + full card outline
- [x] [#32](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/32) Rebrand Status Guard + purge sleep-world + Windows release CI
- [x] [#49](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/49) Join coalesce + supersede stale apply; tray balloon on status/group change
- [x] [#51](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/51) Status apply priority over deferred repping
- [x] [#52](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/52) Document Tupper 60s rate limit in README and About
- [x] [#53](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/53) Transparent About/README logo (no white square)
- [x] [#54](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/54) In-app activity log viewer
- [x] [#55](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/55) Activity log Basic vs Debug views
- [x] [#56](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/56) Log watcher no longer replays full join history on startup
- [x] [#57](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/57) Startup Run target = installed copy; `deploy-local-install.ps1`
- [x] [#58](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/58) Notification / UI branding = Status Guard only (no VrchatPrivacy.App)
- [x] [#59](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/59) Halt mutations when VRChat client process is not running
- [x] [#60](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/60) Tray shows Logout… when authenticated
- [x] [#61](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/61) Tray balloons use world/group names + status labels, not IDs
- [x] [#62](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/62) API/client status disparity treated as kill criterion
- [x] [#62](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/62) Verify API status after apply; document in-game Blue stickiness
- [x] [#62](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/62) Removed ClientUnconfirmed confirm chore (API apply stays Working)
- [x] [#63](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/63) Tray status uses group/world names not IDs
- [x] [#64](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/64) Doc sweep: CONTRIBUTING/SUPPORT/refs vs current scripts
- [x] [#66](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/66) Public Tier H: PVR, secret scan, CodeQL, Code Quality, branch protection
- [x] [#68](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/68) Social preview asset `.github/social-preview.png` (operator uploads in Settings)
- [x] [#69](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/69) First-run wizard includes login (no public VRChat OAuth)
- [x] [#71](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/71) Cut catch-up release from main (ship gap vs v0.1.6) → v0.1.7
- [x] [#72](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/72) Restore auto-release on main + happy-git ship gate → v0.1.8
- [ ] [#77](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/77) Auto-release major/minor only; patch on demand
- [ ] End-user path confirm: enter protected world → status becomes orange/red in-game (blocked on VRChat client sync)
- [ ] Bounded backoff UI polish on repeated auth/network failure
- [ ] Optional: store TOTP secret for seamless re-login (today: interactive OTP)

## Out of scope (v1)

- Quest / non-Windows
- Guaranteeing zero blue flash during transitions
- Controlling what others disclose about the instance
