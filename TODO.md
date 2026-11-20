# TODO

## Done

- [x] Private GitHub repo `introVRt-Lounge/vrchat-privacy`
- [x] Tier I baseline (README, CONTRIBUTING, SECURITY, CI with test/secret-scan/owasp-sast/audit, Dependabot)
- [x] Core library: world id parse, log join parse (live-verified pattern), protection rules/decisions
- [x] Unit tests for parse + decision invariants (no blue restore)
- [x] Research notes + milestone 1 plan

## Next

- [ ] [#1](https://github.com/introVRt-Lounge/vrchat-privacy/issues/1) Log directory watcher (follow new `output_log_*.txt`)
- [ ] [#2](https://github.com/introVRt-Lounge/vrchat-privacy/issues/2) DPAPI credential store + VRChat.API session (login/2FA)
- [ ] [#3](https://github.com/introVRt-Lounge/vrchat-privacy/issues/3) Milestone-1 live demo: apply `ask me`/`busy`, confirm in-game
- [ ] [#6](https://github.com/introVRt-Lounge/vrchat-privacy/issues/6) Windows wizard installer (self-contained + Inno) - in progress on `feat/6-wizard-installer`
- [ ] Tray settings UI (world list, orange/red, pause, startup-with-Windows)
- [ ] Bounded backoff on auth/network failure
- [ ] Operator supplies final protected world IDs + orange vs red preference

## Out of scope (v1)

- Quest / non-Windows
- Guaranteeing zero blue flash during transitions
- Controlling what others disclose about the instance
