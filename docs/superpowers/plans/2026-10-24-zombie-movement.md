# Zombie movement penalty implementation plan

**Goal:** Zombies walk/run at 80% of their normal speed during Zombie Survival;
Humans, spectators and other modes remain unchanged. Restore on role loss/end.

**Architecture:** Native `UnitAvatar.moveSpeed` is synchronized separately from
equipment/status `moveSpeedMultiplier`, frostbite and local weapon movement
modifiers. Use its native `NetworkmoveSpeed` setter, not a host-only movement
getter patch (stock guests control their own movement). Store original/applied
values in the captured match roster entry. Reuse the existing Zombie roster
loop and cleanup; no new controller, global scan or per-frame allocation.

The user's explicit request authorizes the fixed 20% role rule. No new toggle,
settings schema or tuning input is needed. This is independent of general PvP
balancing and dynamic team balance. The existing native dash/knockback mechanics
remain unchanged. Already-current Zombies retain the rule through death.

## Implementation and verification

- [x] Add a native movement fixture and failing assertions in
  `ZombieSurvivalRuntimeTests`: initial role, conversion/manual and automatic
  revival, Humans/spectators, stable ticks, interruption/reconnect/authority loss.
- [x] Add `ZombieMovementSpeed` as an owned scalar state in `RosterEntry`.
  `Update(PlayerAvatar, bool)` scales finite positive base speed once, observes
  external replacement, and restores only the value still owned by the addon.
  Call during initial assignment/infection/advance, and clear before the Zombie
  roster is discarded. Unready players remove the applied penalty until ready.
- [x] Validate native base-speed setter/serialization and both movement
  consumers in `GameZombieTests`; record source fingerprint and limits in
  `docs/zombie-survival.md`. Add concise EN/KO status/UI information.
- [x] Run Release build, runtime/portable/native compatibility tests and
  relevant combat/localization checks, always `-p:DeployMod=false`.
- [x] Review and update version to 0.93.1. Release through a conventional commit
  and push after verification. Do not deploy.
