# PvP Combat Mode

Implemented in 0.99.0, 2026-10-27. Use this document and the synchronization
and host-only research in `docs/*` as shared implementation material.

## Controls and scope

- **PvP 전투 → 전투 모드 / PvP Combat → Combat Mode**; default **off**.
- `/one friendlyfire combat-mode on|off|reset`; `reset` turns this option off.
- `/one save` persists it. Sparse preset v55 writes `friendlyfire combat-mode 1`;
  all older presets default off. Changing this setting does not stop a match,
  reset K/D/A, enable friendly fire, or change potion grants.
- Applies independently of PvP balancing, to selected manual friendly-fire
  participants and the shared match roster in Deathmatch, Tournament, Play Tag,
  and Zombie Survival. Includes countdowns, dead participants and waiting
  Tournament players. Each mode retains its existing late-join rules.
- Disabling the option/master switch, ending a match/session, retiring or
  replacing a participant, native player reinitialization and addon shutdown
  remove the owned restriction. Real dungeon combat restrictions remain.

## What stock guests enforce

The host publishes native `UnitAvatar.NetworkisInBattle`. Unmodified guests
read the synchronized `IsInBattle` property when opening the map, inventory/
character status, stats, passives and level-up panels through standard input
and HUD buttons. Open room/world maps close in their native Update methods.
Room-map teleport checks battle state both at click time and again after the
fade callback. The host additionally rejects a restricted player's
`PlayerAvatar.MoveFloorViaWorldmap` request. Previously queued party world-map
travel checks whether anyone is in battle before completing.

This is **native combat state, not a universal UI lock**. Its limits matter:

- An already open inventory is not automatically closed on a stock guest.
- Identify/enchant altars and Mystic Pot can open inventory directly without
  checking battle state. Those special interaction paths remain available.
- Native preset opening checks battle only inside the dungeon; do not claim
  that every lobby menu is disabled. ESC/settings/leave-session remain usable.
- Native combat also disables sprint, removes out-of-combat MP recovery and
  peaceful movement bonuses, stops some follower recovery and affects
  combat-gated artifacts/interactions. These are consequences of the shared
  flag, not separate stat writes. They revert with the flag when real combat
  is absent.
- Native party travel can be blocked even when an unselected player initiates
  it, because the game checks the entire party for combat.
- Replication has ordinary network latency; this is not anti-cheat enforcement.
- Host `/one teleportall` recovery uses a different native travel path and
  remains available. Combat Mode does not patch all position/floor changes.

No safe replicated per-player interaction-only lock was found. The local
`InteractableChecker.canDoInteractive` is overwritten every frame;
`BlockAvatarInput` is local and would also disable attacks/movement. Global
object mutation, fake death/loading/cutscenes, and guest-only patches were not
used to force those menus shut. A complete menu lock would require guest code
or another verified native protocol.

## Counter ownership and performance

`isInBattle` is a signed-byte native reference counter, not a boolean. Only
native `LocalStartBattle` and `LocalStopBattle` write it through the SyncVar
setter in the inspected assembly. The addon owns a reversible overlay:

1. Capture a current connection/object/run receipt from shared participant
   infrastructure. If native battle count is positive, leave it untouched.
2. Otherwise publish 1 and retain the underlying native value (including a
   pre-existing negative imbalance). Never add to a positive value, avoiding
   signed-byte overflow at 127.
3. Before native Start/Stop, remove the overlay. A Harmony finalizer reapplies
   it after native arithmetic/events, even when a native callback throws.
   Nested calls keep a depth counter. Native start/stop events therefore retain
   their original zero-crossing behavior; enabling the option emits neither.
4. Cleanup restores only an owned overlay, never a stored positive native
   battle snapshot. An unexpected external counter value is not overwritten.
   Release before native player initialization prevents old receipts from
   clearing new native combat. Reentrant disable/re-enable callbacks cannot
   insert an overlay halfway through native counter arithmetic.

Settings changes reconcile immediately. The normal shared LateUpdate catches
roster/lifetime changes. Reused dictionaries/lists avoid LINQ/allocations in
steady-state reconciliation; there are no per-hit patches or UI object scans.
Native SyncVar writes occur on overlay transitions, not on unchanged frames.
The runtime fixture measures zero allocations over 1,000 nine-player ticks.

Optional compatibility validation checks the native counter arithmetic,
replication setter and relevant menu consumers. Failure disables this feature,
not core friendly fire. Off/reset remains available. The option itself is a
policy-only live edit, so loading guests or unrelated stat faults cannot trap
the host in Combat Mode.

## Evidence and verification

Inspected installed `Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`;
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA managed-member evidence:
`ev_33465f9158d21b257cac6d1587f446d34d640d18847ddb53323d74ccab47e2fb`.
Local decompilation stays outside this repository.

Relevant symbols:

- `UnitAvatar.isInBattle`, `NetworkisInBattle`, `IsInBattle`,
  `LocalStartBattle`, `LocalStopBattle`, `SerializeSyncVars`, `DeserializeSyncVars`.
  Native dirty bit: 67108864; initial and incremental messages carry the byte.
- `PlayerInputController.HandleOnOpenCharacterStatusPanel`,
  `HandleOpenStatsPanel`, `HandleOpenPassivePanel`, `HandleOpenLevelUpPanel`,
  `HandleOnOpenMapPanel`, `HandleOpenPresetPanel`; `UI_HUDMenu`.
- `UI_MapPanel.Update`, `UI_NewWorldMapPanel.Update`,
  `UI_Map_Room.TeleportToRoom`, `UI_ClickToTeleport`.
- `PlayerAvatar.MoveFloorViaWorldmap`, `RequestWorldMap`, pending-worldmap
  completion in `Update`; `CombatBehaviour.ReqSetPosition` (owner-authoritative,
  so rejecting only its server command would not secure stock local map travel).
- `AltarOfIdentify.HandleInteractive`, `AltarOfEnchant.HandleInteractive`,
  `MysticPot.HandleInteractive`, `UI_CharacterStatusPanel.OnOpened` are known
  inventory gate exceptions.

Runtime tests cover commands/presets, host and guest counters, all modes,
selection, deaths, reconnects/reinitialization, nested native battles,
exceptions, callback toggles, signed-byte boundaries, loading, cleanup,
world-map rejection and stable-frame cost. Installed-assembly tests verify
actual SyncVar/menu/travel contracts and changed-IL rejection. Build with
`-p:DeployMod=false`. These tests do not substitute for a live stock-guest test.

Manual validation: enable in a lobby with a stock guest; try I/C/map keys and
an already open map; test a room-map click while entering combat. Repeat in
all modes, through conversion/respawn and spectator selection. Disable during
native dungeon combat and confirm native menus remain locked until that
battle ends. Confirm documented altar/preopened-inventory exceptions, normal
controls on disable, and `/one teleportall` recovery. No deployment was made.
