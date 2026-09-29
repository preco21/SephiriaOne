# Random merchant guarantee

## Goal and design

Schedule one guaranteed hostile merchant across the normal floors of each run,
including its first floor. Keep independent chance-based extras before and after
the guarantee, with at most one addon merchant per floor. Keep native HP scaling,
stock isolation, host authority and crime exemptions unchanged. Do not deploy.

The native run generates future stages lazily and includes branching choices.
Schedule a progression position, not an unvisited branch GUID. Choice stages use
depth; Grassland's board uses distinct mission visit order (three missions chosen
from six). Filter potential positions using supported, non-safe, non-boss native
prefabs. Persist the selected position, progress and fulfilled marker in the
native run save. Never invoke native stage generation to predict future floors.

Unsafe or failed selected floors leave the guarantee pending for the next safe
eligible opportunity. A run ending early or with no remaining safe opportunity
cannot be forced to contain a valid spawn. This is random selection over potential
progression positions, not an exact uniform distribution over future branch choices.

## Implementation and verification

- [x] Add regression tests for delayed guarantees and independent chance extras.
- [x] Add a native route adapter and tests for depth, board visits and exclusions.
- [x] Add persisted scheduling, late-enable progress handling and safe fallback.
- [x] Verify reload, legacy markers, re-entry, duplicate events and failed spawns.
- [x] Update EN/KO help, documentation and version metadata.
- [x] Run merchant/room/route/shared/portable checks and Debug/Release builds with
  `DeployMod=false`; independently review the diff, then commit and push main.

## Results

362 merchant runtime checks across 44 scenarios, 29 native route checks, 34 room
placement checks and 782 shared runtime checks pass. Portable policy, localization
and installed-game compatibility checks pass. Debug and Release builds both have
zero warnings/errors. Independent review's early-Race-initialization issue and
the optional-floor chance regression were reproduced and fixed with tests. Live
Unity playtesting remains required; no deployment script was run.
