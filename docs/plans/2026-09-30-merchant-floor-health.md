# Hostile merchant floor health

Multiply every addon-spawned merchant's base HP by `max(1, floorNumber)` once at
spawn. Reuse the shared one-based eligible route ordinal from the merchant `from`
condition, across stage boundaries and Grassland mission visits. Optional rooms
inherit current route progress; unknown progress safely uses 1. Retain native
stage/multiplayer percentages, attacks, defense, ownership and penalty exemptions.

The formula is `base HP × floor number × native HP percentage factor`. Add the
extra base HP through `UnitAvatar.AddMaxHp`, whose native server implementation
writes replicated max/current HP. Validate finite positive base/final HP and the
planned calculation before writing, then fill health once. Natural merchants,
shared prefabs and already-spawned actors are unaffected. No new option, saved
field, network payload or per-frame work is needed.

- [x] Add a failing floor-3 spawn regression, then implement the scale using the
  existing spawn context in `MerchantRuntime`.
- [x] Cover all variants, native multiplayer/percentage composition, floor 1,
  optional/unknown routes, no stacking across refresh/re-entry, and overflow cleanup.
- [x] Update installed-game health/replication contracts, EN/KO status and docs.
- [x] Build Debug/Release and run relevant suites with `-p:DeployMod=false`, then
  review. Deliver by Conventional Commit and push; do not deploy or claim live
  Unity validation.

Verification: initial floor-3 test failed at the old 8,250 HP; after implementation
it passes at 24,750 HP. Merchant fixtures pass 551 checks in 75 scenarios; route
fixtures pass 31 checks. Portable tests, 1061 catalog checks and installed-game
compatibility checks pass. Debug and Release production builds have zero warnings
or errors with deployment disabled. Native inspection confirms `AddMaxHp` writes
`NetworkmaxHp`, and existing native serialization covers maximum/current HP.
The shared command/session suite also passes all 820 checks. Independent read-only
review found no actionable issues. Live gameplay and guest health-bar rendering
remain unverified; no deployment script was run.
