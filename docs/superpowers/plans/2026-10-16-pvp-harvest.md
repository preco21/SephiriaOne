# PvP Harvest reduction implementation plan

**Goal:** HP and MP Harvest recover 20% of their native amounts on balanced PvP hits in casual friendly fire, Deathmatch, Tournament and Play Tag.

**Architecture:** Reuse the existing authoritative `ApplyDamage` hit scope and current participant/mode policy. Insert a stateless amount adjustment immediately before its two native Harvest recovery calls. Preserve all native damage arithmetic, HP penalties/caps and fractional MP accumulation. Do not rewrite stats, pooled damage, or generic healing methods. This is preferable to temporarily scaling stats (PvE/restoration hazards) or globally patching recovery (would also affect potions and unrelated on-hit healing).

**Constraints:** Existing balancing toggle; no separate preference or migration. PvE, self/system damage, non-player victims, unselected players, potions and regeneration stay native. Read current policy at each recovery; there is nothing retained to restore on off/end/reset/rejoin/unload. Use host calculation and native HP/MP replication for stock guests. Do not deploy or launch the game. Build/test with `-p:DeployMod=false`.

## Implementation and verification

- [x] Add representative native Harvest arithmetic and healing accumulators to the combat fixture, and a failing test showing 12.5 HP damage at 1000 Harvest produces 2.5 HP/MP recovery under balance instead of 12.5.
- [x] Add `FriendlyFireHarvest.cs` with the shared amount helper and guarded two-site rewrite; compose it into `FriendlyFireTranspiler.Rewrite` so validation/uninstall use the existing patch lifecycle.
- [x] Cover native/off/PvE paths; native HP/MP stat plus per-hit HP Harvest; fractional MP; shield-only/blocked hits; callbacks, nested hits, exceptions, reflection, debuffs, companions, participant/rejoin and pair changes; zero warmed allocations.
- [x] Add installed-game IL tests verifying both native Harvest calls, original arithmetic/labels preservation, changed-contract rejection and native HP/MP replication. Verify the exact installed assembly fingerprint.
- [x] Run Release build, combat and runtime regressions, installed-game compatibility and co-installed Harmony audit. Request read-only review and address findings.
- [x] Record findings/results in `docs/pvp-balancing.md`, domain findings/index/status; bump version for the verified Conventional Commit and push.

## Completion evidence

Release build: zero warnings/errors. Combat: 3,589 checks, zero bytes per 1,000
warmed Harvest hits. Shared runtime: 2,233 checks plus 69 Bat lifecycle checks.
Full portable/installed-game suite and 32-reference co-installed Harmony 2.9 API
audit passed. Independent read-only review found no actionable issues. Version
`0.57.2`; no deployment, game launch or live host/guest test.

## Native evidence

Installed `Assembly-CSharp.dll` SHA-256: `C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
`UnitAvatar.ApplyDamage` calculates Harvest after shields/current-HP clamping and attack callbacks, before death. HP combines `HPSteal` and `DamageInstance.hpSteal`; MP uses `MPSteal`. All rates divide by 1000. Both undo positive `RECEIVEDDAMAGEINCREASE` before `Heal(float)` / `HealMpFloat(float)`. HP Harvest excludes training dummies. `HealMpFloat` accumulates fractional recovery before native integer MP writes. Full-source search found these as the only stat-based Harvest grant sites; other occurrences define/write/display the stats or per-hit HP rate. Decompiled material stays outside the repository.
