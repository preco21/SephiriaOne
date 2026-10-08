# Friendly fire

Version 0.31.0 adds host-controlled friendly fire. It defaults to **off** with a
100% allied-damage scale. Open `/one ui` → **Combat**, enable it, move the slider,
and press **Apply damage**. Dragging the slider does not send gameplay updates.
The range is 0–300% in whole percentage points.

Commands use the same host service as the panel:

```text
/one friendlyfire on
/one friendlyfire damage 25
/one friendlyfire damage 200
/one friendlyfire off
/one friendlyfire status
/one friendlyfire reset
/one save
```

`off` keeps the selected percentage for later; `reset` restores off/100%.
`/one save` stores applied settings for future sessions. Old presets keep friendly
fire off. Nondefault combat settings use preset v12; malformed or duplicate rows
reject the entire preset.

## Behavior and boundaries

- Direct player attacks that reach an allied UnitAvatar can damage other players,
  followers and friendly NPC factions. Melee and projectile collision handling
  runs on the server. Existing area damage delivered to allies uses the same path.
- Enemy-seeking spells, homing, AI selection, healing and faction relationships
  keep native targeting. This option does not make every automatic spell seek
  allies or add targets to skills with their own enemy-only selection.
- Enemy attacks, attacks originating from NPC followers, self damage and system
  damage keep native behavior. Safe-area peace mode, invulnerability, dodge and
  guard checks still apply. NPC crime and retaliation rules are not disabled.
- Allied damage scales after defense/critical/true-damage calculation, before
  normal and MP shields. Native super armor, extra lives and death follow.
  At 0%, the allied hit is rejected before shields, protection points or on-hit
  callbacks. Negative/non-finite input and non-finite derived damage are guarded;
  extreme values are capped below the native integer conversion limit.
- Nested player-to-ally retaliation is rejected within the same hit chain. This
  prevents thorns/on-hit feedback loops before native hit invulnerability starts.
- Confirmed allied deaths produce a localized `SephiriaOne` chat notice naming
  killer and victim. Extra-life revival does not count as a kill. Names have markup
  and control characters removed and are length-limited. The host's language is
  used for broadcast notices. Logging failure cannot interrupt native death.

## Synchronization and performance

Only the host needs the addon. UnitAvatar's native health/shield/MP/death writes
and existing `DungeonManager.Chat` RPC replicate outcomes to stock clients. There
are no custom guest messages, assets, altered player stats or faction mutations.

Shared session policy is read at hit time. Changes apply to already-existing
projectiles on their next hit, and new/reconnecting avatars use the same current
settings. Run restarts keep session settings; session replacement resolves a new
scope and its saved preset. Reset and unload restore native admission immediately.

There is no combat polling, per-player cache, new scene scan or per-hit packet.
Hit context is a thread-local value type, restored by a Harmony finalizer even
when callbacks throw. Only actual kill notices allocate text or send extra chat.
The native DamageInstance is not modified by the addon, so shared attacks cannot
carry a reduced multiplier from an ally to an enemy.

## Verification

Executable fixtures exercise the production Harmony hooks: off/on, ally/follower/
enemy/self distinction, shields and true damage, 0/50/300%, recursion, reused
damage instances, death/revival, exceptional cleanup, MP integer limits, invalid
derived values, changed IL rejection and unload. Shared-runtime tests cover
command authority, immediate policy reads, run restart, replacement connections,
presets, defaults and failure/reset behavior. Installed-game compatibility checks
verify the actual damage patch locations, native collision/death and chat paths.

Live testing remains required; no deployment was performed. With an unmodified
guest, test host→guest and guest→host melee/projectile hits at 25/100/200%, shields,
death/revival notices, guest quit/rejoin, second run, settings changed while an
attack is in flight, and off/reset. Check enemy-targeted skills still target
enemies, and verify Combat slider focus/drag/Apply and EN/KO labels in game.

Verification on 2026-10-09: Debug and Release builds passed without warnings;
32 executable combat checks, 29 policy/preset checks, 869 shared runtime checks,
48 localization checks, 26 item restriction checks, and the full portable/native
compatibility suite passed. Independent code review found no remaining issues.
