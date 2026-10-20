# PvP HP potion policy correction

**Goal:** Active PvP infinite-HP drinks retain Small/Large potions, cost no MP
and suppress only Survival's random-stat drink callback.

**Cause:** Quantity retention uses `PvpPotionPolicy`, but the fee and Survival
callback still consult Rabbit settings alone.

**Design:** Reuse the guarded controller completion, native heal capture and
current connection/item validation. One PvP admission predicate owns retention
and suppression; a captured eligible drink bypasses the Rabbit fee. Spectators,
countdowns, disabled infinite-HP settings, Sample/MP potions and PvE retain their
existing policy. Do not add a second patch, player cache, frame task or RPC.
Standalone/nested direct effect calls cannot borrow an outer controller's PvP
policy. Once PvP eligibility is revoked during a drink, do not reacquire it in
that drink after Survival may already have run.

Alternative: changing Survival or MP globally would affect unrelated potions
and PvE. Per-mode patches would duplicate the existing shared participant rules.
The shared completion scope is the narrow existing boundary for this correction.

- [x] Extend `tests/SephiriaOne.RabbitPotionHookTests/PvpInfinitePotionTests.cs`
  with failing Survival/fee regressions for both IDs, costumes and local/remote
  owners; cover exclusions, mode eligibility, death and callback reentry.
- [x] Update `RabbitPotionNativeHooks.cs` and `RabbitPotionNativeBalance.cs` to
  apply the same current PvP admission to retention and Survival, skipping the
  Rabbit fee for admitted PvP drinks.
- [x] Run potion hooks, shared session tests and installed native contracts with
  `-p:DeployMod=false`; verify localization after updating command help.
- [x] Document corrected semantics in `docs/pvp-potions.md`, update EN/KO help
  and version, review the diff. Delivery uses conventional commit and push; no deployment.

## Results

Reproduced 24 failing policy regressions before the implementation. Independent
review found temporary identity/readiness revocation could still restore retention
after allowing Survival; four further regressions failed before the correction.
Final executable potion suite: 333 passed, zero failed, plus 12 localized alerts.
Shared runtime/session suite: 13,704 passed plus 69 Bat lifecycle checks, including
all modes, spectators/countdowns, live toggles, selection and session cleanup.
Release 0.77.1 built with zero warnings/errors. Localization (50 checks) and the
full portable/catalog/installed-game compatibility suite passed, including native
Survival-before-heal ordering and 33 Harmony API references. Review confirmed its
finding was resolved. No new frame task, cache, network message or native patch.
Live Unity/stock-guest gameplay has not been tested; no deployment or launch.
