# Merchant guarantee toggle

Add an independent `guarantee on|off` option for each registered merchant type,
including the existing shorthand for Wandering Merchant. Default to on for the
current types so existing presets keep their behavior. The merchant spawn toggle
continues to control all encounters; guarantee off leaves chance rolls active.

1. Extend the shared command/settings model and persist nondefault guarantees in
   preset v9. Keep v1-v8 import and canonical exports when the new field is unused.
2. Gate target selection, forced encounters, and reserved cap slots on the option.
   Preserve progress, saved targets, completed guarantees, counts, and consumed
   floor rolls across toggles, presets, reconnects, and runtime reconstruction.
3. Add per-type panel controls, status, English/Korean strings, and usage docs.
4. Verify command/preset compatibility, independent types, chance-only caps,
   disable/re-enable timing, saved runs, and host authority. Build both production
   configurations without deployment, review, then commit and push.

Turning the guarantee back on may fulfill a pending target at a later eligible
floor, but cannot reopen consumed rolls, replenish completed guarantees, or
exceed a cap already consumed while the guarantee was off.

## Verification

Completed in v0.25.0. The first new command regression failed before implementation.
Final Release suites pass: 475 merchant checks across 62 scenarios, 820 shared
runtime checks, 182 merchant settings checks, 31 route checks, 1054 bundled catalog
checks and 12 localized feature checks, together with the other portable suites
and installed-game compatibility checks. Production Debug and Release builds pass
with zero warnings/errors, always using `-p:DeployMod=false`.

Independent read-only review found no actionable issues. No deployment or live
Unity session was run; panel rendering and end-to-end multiplayer behavior still
need the live checks in the merchant guide.
