# PvP hit cadence implementation plan

**Goal:** allow direct/magic hits more damage headroom and modestly slow debuffs
while the existing PvP balancing toggle is active.

**Design:** retain the 20% max-HP burst, per-hit curve, one shared pair budget,
refund accounting and all receipt/life/epoch resets. Increase continuous refill
from 30% to 40% max HP per game second. Scale audited periodic debuff timer
elapsed input by 0.75 for current permitted player pairs. Keep expiry, stacks,
native speed bonuses, blue burn, explosions and native damage callbacks.
No new setting, state table, client protocol, assets or stat writes.

User broadened the scope during implementation to other elements, with Freeze
still able to immobilize players. The shared timer registry now covers Burn,
Plasma burn, Frostbite, Poison (excluding system poison), and Wound. Electric/
Plasma shock and Freeze instead damage on application/refresh/expiry: limit
their damage to independent 0.5s pulses per pair using the existing budget
window and its lifecycle. The user confirmed the 0.5s interval in the tuning
question; no new configurable setting is introduced.
Freeze's StartStun(BuffDuration) remains outside the resolved-damage limiter.

Alternatives: separate weapon/magic/DoT budgets would prevent competition but
grant additive allowances and complicate recursive reservations. Shortening burn
duration changes refresh/expiry mechanics. The chosen shared budget plus timer
adjustment keeps existing behavior and architecture closest to native.

The timer changes frequency, not damage per tick. Very fast burn builds can
still saturate the shared damage budget; this is modest initial tuning, not a
guaranteed reduction of every burn build's DPS. Live balance requires playtesting.

- [x] Adjust executable rapid-hit expectations: 4% refill per 0.1s, 60% total
  first-second ceiling including burst, MP fractional refunds and live max HP.
- [x] Add native-shaped burn timer fixture and integration tests: baseline
  0.5s becomes about 0.667s; native speed bonuses multiply by 0.75; PvE, guest,
  off, selection, Tournament pairs, reconnect, callback and unload behavior.
  Run `dotnet run --project tests/SephiriaOne.FriendlyFireTests -c Release
  -p:DeployMod=false` and observe failure before production changes.
- [x] Change `FriendlyFireDamageBudget.RefillPerSecond` to `0.40d`. Add a
  focused burn hook which rewrites only the exact tick timer delta input,
  validates the native IL shape, and consults shared current hostility policy.
- [x] Verify the rewrite against installed game IL and mutated contracts;
  retain damage, timer, BURNSPEED, stack and expiry calls. Run focused tests,
  Debug/Release non-deploying builds, runtime and native/Harmony checks.
- [x] Generalize periodic hooks, observe Frostbite's failing test, then cover
  all five native timer contracts and unchanged charge/expiry paths.
- [x] Observe Shock's failing test, then add shared-window Shock/Freeze clocks,
  lifecycle and refund coverage, and verify native Freeze still calls StartStun.
- [x] Update English/Korean help, version, current balancing docs and findings.
- [x] Review the diff before the authorized conventional commit and push.
  Never deploy or launch game.

Work is inline in the authorized repository. Native reference material stays
outside the repository under the existing temporary decompilation directory.
