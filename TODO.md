# TODO

## Done

- [x] Private GitHub repo `introVRt-Lounge/vrchat-privacy`
- [x] Tier I baseline (README, CONTRIBUTING, SECURITY, CI with test/secret-scan/owasp-sast/audit, Dependabot)
- [x] Core library: world id parse, log join parse (live-verified pattern), protection rules/decisions
- [x] Unit tests for parse + decision invariants (no blue restore)
- [x] Research notes + milestone 1 plan

## Done (runtime)

- [x] [#1](https://github.com/introVRt-Lounge/vrchat-privacy/issues/1) Log directory watcher
- [x] [#2](https://github.com/introVRt-Lounge/vrchat-privacy/issues/2) DPAPI + VRChat.API session (login/2FA)
- [x] [#3](https://github.com/introVRt-Lounge/vrchat-privacy/issues/3) Apply orange/red on protected world entry (in-game confirm = operator manual check)
- [x] [#6](https://github.com/introVRt-Lounge/vrchat-privacy/issues/6) Windows wizard installer
- [x] Default protected world: `wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3` (Cuddle & Sleep)
- [x] Tray: login, pause, protected worlds, status

## Next

- [ ] Operator live confirm: enter sleep world while blue → becomes orange in-game
- [ ] Bounded backoff UI polish on repeated auth/network failure
- [ ] Optional: store TOTP secret for seamless re-login (today: interactive OTP)

## Out of scope (v1)

- Quest / non-Windows
- Guaranteeing zero blue flash during transitions
- Controlling what others disclose about the instance
