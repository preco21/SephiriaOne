# Friendly-fire downed-player effect

Implemented in `0.48.0`, 2026-10-12. Uses the installed game assembly fingerprint
recorded in [domain findings](domain-findings.md). Asset inspection and native
contracts are evidence about this build, not proof of live multiplayer rendering.

## Behavior

When effective friendly fire is enabled, a participating player entering the dead
state receives a short red world-space burst. This includes casual friendly fire,
active Deathmatch and active Tournament rounds. Enemy/environment deaths during
those active periods also show it; a PvP final-blow attribution is not required.
Selection uses the existing rules: no selected players means everyone; otherwise
only selected players receive the indicator. Countdown periods with effective
friendly fire disabled retain vanilla presentation.

The host broadcasts a native cosmetic RPC already understood by unmodified
guests. No guest addon, custom assets, projectile or damage is created. The dying
player keeps their normal local death animation and also sees the smaller burst.
The indicator adds no sound and does not depend on the PartyMemberFX setting.

## Resource investigation

The remembered red crash is **world-space**, not a fullscreen UI effect:

- `PlayerSpawner.HandleDieClientside` spawns `dieFxPrefab` at the spawner's world
  position only when `isLocalPlayer`. Remote deaths take its log-only branch.
- `sharedassets0.assets` GameObject `79251`, `HeartHitFx-PC`: ordinary `SpriteFx`
  with a child `SpriteRenderer`, unlit material, no Canvas/camera attachment.
- Animation set `177768`: nine frames at 20 fps, no frame/sound events, one-shot
  auto-despawn. The 105-pixel sprites use 16 pixels per world unit. Sprite
  `PlayerHitFX04` visibly contains the red cracked circle. Pool limit: two.
- A local `SpriteFx.Pool.Spawn` accepts an arbitrary world position, but does not
  replicate. The prefab has no network identity.

`SkillController.RpcCreateCastFx` can select this prefab by name on stock guests,
but is unsuitable for the indicator. It attaches to the casting point, clears
charging visuals, and applies each viewing client's PartyMemberFX setting. That
can hide the animation completely. It also switches the pooled material, which
`SpriteFx.OnDespawn` does not restore, affecting a subsequent normal local death.
Its two-instance pool can truncate simultaneous deaths. No casting transport or
reverse patch is shipped.

Other inspected alternatives: `HeartHitFx-NPC` is a small heart break;
`SuperArmorBreakFx` is a small green shield break; White Egg's execution flash
requires that charm's network object. Shield-of-Reason's break RPC can open
tutorial UI. Replaying `RpcDie` or fake damage would run gameplay callbacks. These
are not used. A reference scan found no bullet impact/destroy binding to the exact
player crash prefab.

The selected fallback is **`PlayerDefaultBigBulletDestroyFx`**, a red flash/ring:

- Native bullet key `PlayerDefaultBigBullet`, GameObject `58222`, serialized
  `DestroyModule` `212898`; exact base `BulletDestroyModule`.
- Effect GameObject `79384`, animator `213946`: seven frames at 20 fps, 13-pixel
  sprites at 16 pixels per world unit. Native pool size six, auto-despawn, no
  animation events, sound, alternate effects or fragments.
- Scale four makes the burst approximately 3.25 world units wide. Position is
  the downed avatar's world position plus its native visual body height.

## Shared lifecycle and transport

`FriendlyFireNames.BeforeDeath` captures a current participant receipt and epoch
only for a live player with effective friendly fire enabled. The existing `Die`
finalizer verifies the player actually became dead, remains connected in the same
scope, and still participates. It skips exceptional/incomplete native deaths.
The visual dispatch precedes round-resolution callbacks and is separately caught,
so an optional cosmetic failure cannot skip KDA or match recovery.

Repeated `Die` calls on an already-dead avatar capture no receipt. Extra-life saves
never reach this boundary. There is no remembered per-player FX state to leak
across revives, run restarts or reconnects. Native `ForceDie` remains untouched:
it also handles restored-dead late joins and scripted cutscene/administrative
deaths, which must not replay a combat-down indicator.

`FriendlyFireDeathVisuals` calls:

```text
DungeonManager.BroadcastDestroyBullet(
    victim.netId, false, "PlayerDefaultBigBullet",
    victim.transform.position, bodyHeight, 0, 4)
```

Despite its name, this wrapper only broadcasts `RpcBulletDestroyed`. The receiver
looks up a **prefab** and calls `CreateDestroyVisualOnClient`; it does not create,
destroy, move, explode or damage with a projectile. The native reliable RPC hash
is `-1283831235`, includes the owner, and uses existing dungeon observers. `false`
skips the native transparency branch; the sprite spawn has no owner, avoiding
the hidden-party-effect replacement. Standard pool cleanup resets body height,
color and scale. The native player-death pool is not touched.

Compatibility validation is lazy and cached. Each death cheaply rechecks the
current prefab/pool, exact module type, single one-shot effect and absence of
sound/fragments/alternate effects. Temporary missing pools skip that indicator
without disabling later runs. Changed contracts/assets disable only the cosmetic
adapter and warn once until addon reload. Position/height must be finite. There
are no per-frame scans, deferred replays, new network objects or save fields.

## Verification and remaining limits

Executable death-hook tests cover repeated deaths, another life, extra life,
enemies/environment, disabled FF, spectators, changed selection, disconnect,
changed run, callback revival/destruction, native exceptions and optional FX
failure. Shared runtime tests exercise the same hook during Deathmatch and
Tournament. Installed-code tests check payload order, native RPC registration,
fixed scale/transparency arguments, cosmetic-only dispatch and rejection of
changed payload/receiver/gameplay contracts.

Native pools are finite: six concurrent bursts share this pool with the native
bullet effect, so unusually dense overlap can recycle an older animation. The
native cosmetic renderer also consumes Unity random values even with zero
position/angle variation. These native limitations are unchanged; no custom
renderer or global random-state patch is introduced.

Live validation remains necessary: host/stock guest deaths in lobby and dungeon,
each mode, hidden party effects, simultaneous deaths, nearby spectators, native
revives, and reconnects. Check size/height/readability on all costumes. No game
was launched or addon deployed for this change.

Verification on 2026-10-12: Debug and Release builds passed with zero warnings or
errors; 939 executable combat checks, 1,700 shared runtime checks, 69 Bat lifecycle
checks, 27,864 Tournament scheduler checks and the full portable/installed-game
contract suite passed. The replacement adapter received an independent read-only
review with no blocking findings. Tests do not render Unity scenes or establish a
live guest connection.
