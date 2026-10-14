# Lobby HP/MP recovery during friendly fire

Added in `0.54.0`, 2026-10-14. Default **off**. In Combat, enable
**Block lobby HP/MP recovery** (`대기실 HP/MP 자동 회복 차단`), or use:

```text
/one friendlyfire block-lobby-regen on
/one friendlyfire block-lobby-regen off
/one friendlyfire status
/one save
```

The preference does not enable friendly fire itself. While both are enabled,
participants lose the waiting-room HP restoration tick and automatic HP/MP
regeneration, including passive stat regeneration and the native out-of-battle
MP recovery bonus. Potions, skills, lifesteal, damage-restoration effects, direct
healing, revival and follower recovery retain native behavior.

Existing participant selection applies: no selections means everyone; otherwise
only selected players. Deathmatch uses its effective combat switch. Tournament
affects only the current fighting pair; spectators and countdowns keep recovery.
Changing this option does not stop either mode, reset K/D/A or refill PvP budgets.
Match end, friendly-fire off, option off, leaving the lobby or addon unload
immediately resumes native automatic recovery. No saved HP/MP values or stat
overrides need restoration. Rejoining avatars resolve the current policy through
the existing connection receipts instead of inheriting old per-player flags.

`/one friendlyfire reset` also resets this preference. `/one save` persists its
intent using preset v23 (`friendlyfire block-lobby-regen 1`). Old presets and
missing rows default to off. Invalid/duplicate rows reject the whole preset;
turning the setting off exports the lowest remaining required schema.

## Native evidence and implementation

Inspected the installed game assemblies and decompiled source outside this repo:

- `PlayerAvatar.Update`: server-side `healPlaceTickTimer`, `Race.healPlaces` and
  generated-floor lookup gate `Heal(10f, false, false)`.
- `UnitAvatar.Update`: server-side `hpRegenCounter` calls `Heal(1f)` after reading
  `HPREGEN`; `mpRegenCounter` calls `HealMp(1)` after reading `MPREGEN`. MP also
  adds an out-of-battle recovery bonus based on its current fraction.
- `DungeonManager` generates the initial stage from `RaceEntity.lobbyStage`.
  `StageEntity.GenerateStage` records the stage's name in `FloorData.stageName`.
  Compare that value with this player's `Race.lobbyStage.name` using the actual
  `currentFloorGuid`. A global run-start flag or generic safe-floor flag cannot
  reliably identify the waiting room for each player during transitions.
- The ordinary `Heal`/`HealMp` methods and HP/MP synchronization remain native.
  Suppression runs on the authoritative host; stock guests need no addon code,
  custom messages or new state. Live guest playtesting remains required.

`Features/Combat/LobbyRegen` owns an independently validated Harmony patch group.
It replaces only those three Update call sites with conditional wrappers using
the same argument stack. Counters/timers still advance normally, so suppression
does not introduce a catch-up queue. It never skips Update, intercepts all healing,
rewrites regen stats, spoofs battle state or alters native maximum-resource clamps.
Original instructions, branch labels and exception boundaries are retained.

The wrappers read `SessionSettings.FriendlyFireForHit`, the current generated
floor, `FriendlyFireParticipants.Includes`, and Tournament's shared `AllowsPair`
via `AllowsParticipant`. There is no new controller loop, persistent suppression
cache, custom RPC, stat write or periodic synchronization work. Disabled/non-player
paths return early; current connection validity is evaluated only for eligible
players in the lobby. Shared participant checks retain their existing bounded
selection pruning, roster membership and connection-map checks.

Installation validates unique healing calls, their exact arguments and native
regen/room markers. A changed or ambiguous contract rolls back this patch group
and leaves native recovery active. Its compatibility failure cannot disable
ordinary friendly-fire hooks. Enabling the failed option is rejected; off/reset
stay available, and status reports the limitation even for a saved preference.
Saved preferences also remain inert if the core friendly-fire hooks fail to load.

## Verification and remaining checks

- Policy/command tests cover defaults, independent edits, schema v23 round trips,
  rejection of malformed/old-schema rows and reset.
- Runtime tests execute the real Harmony rewrites on native-shaped Update
  fixtures. They check host/guest recovery, unrelated Update continuation,
  direct healing, revival, followers, selection, retirement/reconnection,
  leaving/returning to lobby, optional-hook failure and saved-policy restoration.
- Shared match tests cover Deathmatch countdown/active/end and Tournament
  active pair versus spectators. Other tests verify recovery edits preserve
  scores and balance revisions.
- Installed-game contract tests verify all three call sites and reject removed,
  duplicate or changed healing calls. The compiled Harmony API audit includes
  the co-installed HarmonyX 2.9.0 library.
- Debug/Release builds use `-p:DeployMod=false`. No deployment or game launch.

Final verification: both builds completed with zero warnings/errors; 1,313 combat
hook checks, 1,983 session integration checks plus 69 Bat checks passed. Portable
policy/localization and installed-game compatibility checks passed, including
29 compiled Harmony API references checked against the co-installed library.

Still verify in game with an unmodified guest: lower HP/MP in the waiting room,
toggle recovery blocking during casual combat and both match modes, check potion
and Rabbit healing, then enter a floor and return. Confirm native recovery resumes
and the compact Korean checkbox remains readable. Fixture/IL checks do not prove
live rendering or guest delivery.
