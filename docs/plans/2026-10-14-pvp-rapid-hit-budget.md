# PvP rapid-hit balancing plan

**Goal:** retain existing per-hit softening and add a shared sustained-damage
budget under the existing PvP balancing toggle, without live stat or PvE changes.

**Design:** use a token budget per directed attacker/victim connection pair,
shared by direct attacks, artifact projectiles/procs, debuffs and owned companions.
Capacity is 20% of the victim's actual maximum HP; initial refill is 30%
per game second (stated assumption while the optional pace question is pending). Apply after the
existing damage percentage and soft curve, before native shields. Store tokens
as HP fractions, so HP changes do not reset the budget or rely on current HP.
Independent attackers remain independent. Unused tokens refill up to capacity;
there is no fixed-window boundary burst and no queued damage debt.

Per-item cooldowns would need an expanding item allowlist and suppress native
effects. A victim-wide budget would let one attacker shield a victim from others.
The directed shared budget preserves attack scheduling, projectiles, targeting and
utility stats while limiting the damage of rapid attacks. Native damage-dependent
effects still consume the reduced damage; zero-budget hits may deal no damage.
This does not normalize proc frequency, crowd control, or incoming monster damage.

Reuse participant receipts, combat/selection epochs, balance revisions and floor
identities. Dead-to-revived recovery invalidates that player's outgoing and incoming budget life;
queued attacks from dead owners otherwise continue sharing their existing budget.
No timers, scene scans, guest payloads, or per-hit allocations after pair warmup.
Reserve before callbacks; settle against actual native shield/MP/HP reports after
successful completion, refunding unused fractions. Preserve reservations after
exceptions. Reset generations reject stale nested refunds. Native extra lives
continue their budget since they never enter the dead-to-Revive path. Super-armor
reductions are refunded: repeated saturation can still reach the actual-HP ceiling.

- [x] Add a failing executable regression for rapid same-pair impacts and native
  callbacks; retain slow-hit, fractional percentage, shield and PvE coverage.
- [x] Implement pure budget arithmetic and weak receipt-keyed runtime windows;
  integrate only in the existing player-victim balance branch. Test refill,
  inactive toggles, scopes, reconnects, floors, revival, effects and allocations.
- [x] Update shared command help and EN/KO catalogs, compatibility checks,
  current feature docs, findings and version. Keep the existing control/preset.
- [x] Run non-deploying Debug/Release builds, combat/runtime/portable/native
  suites and alternate Harmony API audit; independently review the diff.

Delivery: conventional commit and push the focused changes after verification.
Do not deploy or launch the game.

Evidence: native `Charm_PallasCard.OnBeginAttackAnimation` emits three homing
bullets per proc through `Bullet.Pool.Spawn`; final impacts use the existing host
damage boundary. The general boundary does not depend on recognizing an item's
translated name. Test fixtures validate damage/lifecycle contracts, not live match
balance or rendering; playtesting remains necessary after manual installation.
