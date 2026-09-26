# Configurable Rabbit MP cost implementation plan

**Goal:** Let hosts change the Wing-Eared Rabbit HP-potion MP fee in game.

**Architecture:** Extend the existing immutable Rabbit settings, shared command
dispatch and preset persistence. Read the cost at the existing completed-drink
boundary; use existing synchronized MP without any new guest state or replay.

## Design

- `/one rabbit mp-cost N` sets an absolute integer fee, 0..10000, and enables it.
  Existing `on`/`off` retain the amount. Reset restores default 10 and all flags off.
  A zero fee does no MP write. There is no character-specific multiplier baseline
  for this fee, so relative/multiplier syntax is not supported.
- Numeric input and Set & on button use the existing panel draft/scope guards.
  Settings changes refresh status and the host's costume description immediately.
- Custom amounts persist in v6 as `rabbit mp-amount N`, independently from the
  existing boolean `rabbit mp-cost 0|1`. Both row orders preserve the toggle.
  Old v1-v5 presets retain their meanings and default cost 10. Malformed, duplicate,
  unsupported-version or out-of-range rows fail atomically. Only custom fees need v6.
- Keep host authority, HP-only/costume scope, cancellation, insufficient-funds,
  Survival suppression and lifetime checks unchanged. Use current fee at completion.
- Default struct values must still report 10; a disabled custom fee remains saved.
- Work in C:/Users/preco/repos/SephiriaOne. Every build/test uses
  `-p:DeployMod=false`. User authorized implementation, commit and push; no deployment.

## Steps

- [x] Add parser/policy/preset tests, run Release pure suite and observe numeric command rejection.
- [x] Implement numeric command, amount state and strict v6 persistence; run pure suite.
- [x] Add native fee and description tests using real shared settings rather than
  duplicated settings fixtures. Observe failures, then read captured fee and render it.
- [x] Add panel amount control; test shared dispatch, notifications, saved reload,
  authority, reconnect policy, invalid input and compatibility failure paths.
- [x] Verify Debug/Release builds, affected test runners and installed IL contracts;
  review scope/default/zero boundaries and compatibility with old presets.
- [x] Update version/docs and recorded checks; Conventional Commit and push main.
