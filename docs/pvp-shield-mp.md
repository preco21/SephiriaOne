# PvP shield MP minimum

Updated 2026-10-22, addon `0.84.0`. Supersedes the original guard-only,
reduction-preserving behavior in [PvP balancing](pvp-balancing.md).

## Perfect Guard multiplier (`0.84.0`)

**PvP Balance → Perfect Guard MP multiplier** adds an independent toggle
(default off) and a native-cost multiplier (default x2, range x1..x100 with up
to two decimals). This applies to permitted PvP hits only while balancing is
enabled, across normal friendly fire and all modes using the shared policy.

```text
/one friendlyfire perfect-guard on
/one friendlyfire perfect-guard multiplier x3
/one friendlyfire perfect-guard off
/one friendlyfire perfect-guard multiplier reset
/one friendlyfire perfect-guard reset
/one save
```

When enabled, a successful native Perfect Guard costs
`ceil(native integer MP cost × multiplier)`, **exempt from the shield MP
minimum**. The user explicitly allowed Perfect Guard to cost less than other
shields. Native cost includes Guard Efficiency, the Perfect Guard half-cost
and artifact cost callbacks, followed by the game's integer truncation. For
example, native 5 MP becomes 15 MP at x3 even if the shield minimum is 50 MP;
native 3 MP becomes 8 MP at x2.5. x1 keeps native cost and native zero remains
zero. Invalid costs cannot produce negative spending; overflow clamps safely.

Turning this option off restores existing balanced guard costs, including the
shield minimum. Turning balancing/PvP off restores native behavior. Ordinary
guards, parries, MP shields and automatic shields keep their existing rules.
Only available MP is spent; native guard-break logic remains. Positive multiplied
costs reuse the existing enforcement against synchronous refunds and INFINITYMP.

The native Perfect Guard branch is marked before callbacks can alter guard
timers. The flag and calculated amount live in the existing nested hit context,
with exception cleanup and connection/mode revalidation. There are no live stat
writes, new RPCs, actor scans or frame loops. Stock MP replication covers guests.
The multiplication uses bounded integer hundredths and adds no per-hit allocation.

Commands/UI edits apply on the next guard without stopping matches, resetting
K/D/A or clearing damage budgets. Value reset restores x2; family reset restores
off/x2. `/one save` stores configured intent in v45, including a custom multiplier
while disabled; older presets inherit off/x2. See [preset rules](session-preset.md).
The UI and status have EN/KO translations and explain the minimum exemption.

Executable and installed-IL tests cover native branch identity, cost modifiers,
floor exemption, nested hits, zero/huge/insufficient MP, live changes and stale
receipts. Live host/guest weapon-animation and MP-display verification remains
necessary; no game files are deployed by this change.

Verification for `0.84.0`: non-deploying Release build, zero warnings/errors;
9,001 executable combat checks, 13,931 session/runtime checks, 212 Perfect Guard
policy checks, 4,448 catalog checks, panel/localization suites and full
installed-game contracts passed. The warmed multiplier allocated zero bytes
over 1,000 hits. Independent review found no actionable issues after the
minimum exemption; invalid/sub-integer native-cost cases were also added.

## Current behavior

With PvP balancing enabled, a permitted hostile player's successful defensive
block or shield absorption costs at least **5% of the defender's current maximum
MP**, rounded up. The user explicitly chose a strict minimum after Guard
Efficiency, perfect-guard and artifact reductions, and then lowered the default
from 8% to 5%. Existing explicit custom preset values remain unchanged. The
enabled Perfect Guard multiplier above is the explicit exception to this floor.

Use **PvP Balance → Shield MP min (%)**
(`보호막 최소 MP (%)`) or `/one friendlyfire guard-mp 5`.
`/one friendlyfire guard-mp reset` restores 5%; `/one save` persists a custom
value. Range remains 0..100 with two decimal places. Zero waives the additional
floor; the prior percentage-based standard guard setting still allows zero-cost
guards at zero, while MP shields retain their native damage-to-MP consumption.

- Standard guards, staff special/dash guards and weapon-generated guard windows
  use the common native guard branch. Native cost increases above its 10-MP base
  still scale proportionally; reductions cannot undercut the floor, except for
  an enabled Perfect Guard multiplier.
- Dagger and katana parries use the common successful-parry branch. Existing
  activation costs remain separate. A synchronous dagger refund cannot erase
  the minimum. An empty MP pool cannot initiate another successful parry through
  this branch; already-granted native bonus invulnerability is unchanged.
- All `MPSHIELD` absorption is covered, including Protection of Nature. Full
  absorption costs the greater of the floor and native integer MP damage.
  Partial absorption still spends remaining MP and preserves native HP spill.
- Ordinary shield points, including dash-triggered `Charm_DashShield`, and
  `UnitSubShield` automatic blocks also pay the floor. **They retain native
  absorption when MP runs out**, as requested; only available MP is spent.
- Multiple shield layers pay one minimum per incoming hit, not one per layer.
  Pure invulnerability, evasion and offensive counter windows do not become MP
  defenses. Simply activating or holding an action does not incur the floor.

No player stats or native skill activation costs are rewritten. Incoming PvE,
self/system damage, disallowed teams/participants and unbalanced PvP retain
native behavior. Normal friendly fire and all match modes use the same live
policy and connection receipts. Disabling balancing or ending effective PvP
restores native behavior without a separate restore pass. Native `INFINITYMP`
cannot waive the positive PvP floor, but retains its native behavior elsewhere.

## Artifact and weapon audit

The installed artifact called **Crest of the Iron Wall** (`철벽의 문장`, item
1314, `1314_SwordShieldGrowth_Enhanced`) uses `Charm_GrowthGuard`, with Guard
Efficiency 20/30/40 and guard-angle bonuses. It does not replace the guard or
directly refund MP. The previous formula charged 8% before reductions: at 40%
efficiency it became 4.8%, or 2.4% for a perfect guard. Stacked 100% efficiency
could make that zero. This explains a bypass of the advertised minimum without
requiring maximum-MP overflow. Asset identities: `sharedassets0.assets:73009`
prefab, component `187243`, ItemEntity `179888`.

Do not confuse that artifact with **Protection of Nature** (`자연의 보호`, item
1265, `1265_MPShield`): `Charm_MPShieldActive` enables `MPSHIELD`, using a distinct
damage-absorption branch. Its serialized asset duration/cooldown are 10s/20s,
different from the C# defaults. **Artificial Fairy, Iphiel** (1078) separately
grants regeneration during guard animation; later ordinary regeneration remains
native. The floor is a cost per hit, not a ban on all MP recovery.

Audited producers include `WeaponControllerSimple.StartGuard/StartGuardCustomAngle`,
`WeaponSimple_Quarterstaff` special/dash guard, dagger animation parry,
`WeaponSimple_Katana` deflection (animation 9999), `WeaponSkill_Blocking`,
`WeaponSkillSubAction_Block`, `WeaponAddon_OppositeAttackGuard`, and Sword & Shield
attack/back-shield variations. Katana sheath/Cloud Slash counters and direct
greatsword offensive spends are distinct. Uninspected animation assets are not
claimed individually verified; anything entering the audited shared guard or
parry boundary inherits the rule.

Automatic defenses use `UnitAvatar.UseShield(float)` and
`UnitSubShield.OnCalculateDamage`. `Charm_DashShield.OnBeginDash` grants shield
points with `AddShield`, rather than MP absorption. The native subshield uses
its own guard-power resource. Neither is incorrectly reclassified as `MPSHIELD`.

## Implementation and verification

`FriendlyFireGuard` handles the overflow-safe maximum, floor, actual MP spend
and native guard break. `FriendlyFireActiveDefense` adapts parry/MP-shield/
ordinary-shield boundaries and observes automatic subshield success. Nested
`HitContext` flags record absorption and whether an MP-defense charge already
occurred. Native callbacks run once; an exact `Networkmp` top-up enforces the
debit if synchronous refunds or `INFINITYMP` waived it, after rechecking current
host, participant, connection and mode permission. No custom guest message is
required: stock MP SyncVars provide replication.

Enforced spends are clamped to available MP before calling `UseMp`; no callback
is emitted for an empty pool. Native `Charm_GainShieldWhileUsingMP` and
`Charm_GainBuffOnMPLoss` count the reported callback amount, so an unpaid floor
must not award free shields or buffs. A zero configured passive-shield floor
also skips `UseMp(0)`, avoiding an unintended MP-regeneration timer reset.

There are no inventory scans, retained player/shield dictionaries, frame loops,
new RPCs or per-hit allocations in the added accounting. Current base/Final MP
are read at settlement with the existing overflow-safe arithmetic. Native
full/partial absorption selection and shield-point depletion remain intact.

Regression coverage includes Iron Wall reductions, perfect guards, zero-cost
callbacks, huge MP pools, parry refunds, all MP-shield branches, ordinary and
subshield blocks, depleted MP, layered shields, callback policy changes,
exceptions, nested attacks and departed/rejoined receipts. Unrelated cadence
tests explicitly disable the shield floor where they measure damage-to-MP
conversion. Installed-game IL contracts validate the exact patch boundaries,
branch/exception metadata, drift rejection and native producer paths.

Evidence: Assembly-CSharp SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`,
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`; matching ILSpy source and UnityPy
localization/asset reads. REA identity references
`ev_b2ff80fd487641fed1eca32253a16cab08bd0d6b79d5bd38673c81845959da36`
and `ev_6870fb8ed120951f5e936e93c7c5da0e19c144d0128d532cea84eca39292edeb`.
REA method analysis hit its metadata budget, so method behavior was checked
against the matching source and executable IL contracts instead. Private
exports stay outside Git. Tests are not a live multiplayer reproduction;
host/stock-guest MP display and real weapon animation timing still need in-game
validation. No deployment is part of this work.

Verification completed: Release build (zero warnings/errors), 8,767 executable
combat-hook checks, 13,796 command/session integration checks plus 69 Bat checks,
156 guard-policy checks, 4,327 catalog checks, panel/localization suites and the
full installed-game compatibility suite, including 34 HarmonyX API references.
Warmed guards and automatic shields allocate zero bytes per 1,000 hits. Review
found and corrected unpaid MP-reward callbacks and zero-floor regeneration
timer resets; layered ordinary/MP shields also share one MP-use callback.
