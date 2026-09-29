# Independent hostile merchant variants

## Goal

Add verified native merchant types with independent settings and conditions,
including multiple types on one floor, while retaining host-only installation,
native guest replication, no crime penalty for addon actors and 1x native scaling.
Preserve the existing Wandering Merchant commands and run-save markers. Do not deploy.

## Design

A pure catalog defines stable variant IDs, native template/controller contracts,
display names, independent deterministic random salts and guarantee policy.
Composable conditions receive an immutable spawn context. Per-type settings live
in the shared session policy and are exposed through commands, snapshots, the
Merchant panel and versioned presets. The initial configurable conditions are an
earliest eligible floor and a per-run spawn cap; defaults retain unrestricted
normal-floor behavior. New variants are disabled by default.

The runtime evaluates every enabled catalog entry. Per-type reservations, successful
spawn counts and schedules isolate misses, failures, setting changes and save
continuation. Only the original type uses legacy keys. Generic actor setup, stock
isolation, native scaling, rollback and ownership hooks remain shared. Room choices
use each type's stable salt and exclude all existing nearby stock containers.

## Work and verification

- [x] Inspect native templates, network prefab registration and combat/death paths.
- [x] Add catalog/settings/conditions and parser/preset migration tests.
- [x] Refactor shared runtime and test same-floor coexistence, independent rates,
  conditions, failure isolation, save/re-entry and legacy markers.
- [x] Add type selection and condition controls to the panel; update EN/KO catalogs.
- [x] Update documentation/version; run relevant fixture suites, installed native
  contracts and Debug/Release builds with `DeployMod=false`.
- [x] Review the final diff, commit using Conventional Commits and push main.

Native candidates are included only after their actual combat and network paths
are verified. Live-game visual and balance testing remains distinct from automated
contract checks. The user requested one independent guarantee per enabled type and
approved editable earliest-floor/per-run-cap conditions.

## Results

Included Wandering Merchant, Papyrus and Taz. Excluded Taga/Rona because their mage
templates lack the required skill controller, and Thunder because its AI cannot
attack. Native avatar/attack prefab registration and inherited combat/death/network
contracts were inspected; no new guest assets or scripts are needed.

459 merchant runtime checks (57 scenarios), 45 room-placement checks, 31 route
checks, 119 settings checks, 811 shared runtime checks, 1038 catalog checks and
the portable/native compatibility suite pass. Debug and Release production builds
have zero warnings/errors. The performance fixture retains 0 allocated bytes per
synchronization tick in all three five-player scenarios. Independent reviews found
one cap-slot edge case on unschedulable routes; regression tests reproduced it and
the fix preserves both chance-only caps and future guarantee reservations.
No deployment or live Unity gameplay test was performed.
