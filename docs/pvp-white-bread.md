# White Bread during PvP

Implemented in `0.66.3`, 2026-10-19. The installed English name is **White Crust
Bread**, Korean **흰 테두리 빵** (item 1119). This is the White Bread artifact
requested by the user.

## Behavior

Active PvP participants receive none of this artifact's added post-hit
invulnerability time. This applies to casual friendly fire, Deathmatch,
Tournament's fighting pair, Play Tag, and Zombie Survival, with or without the
optional PvP balancing toggle. There is no additional setting.

The shared `PvpParticipation` policy scopes the rule to current participants.
Spectators, pre-fight countdowns, non-player units and ordinary PvE retain native
behavior. While a player participates, the extra window is suppressed for any
incoming damage that reaches the native hit-invulnerability check, including
enemy damage. Turning PvP off or ending/cancelling the mode immediately restores
the artifact's native protection. Normal native base hit protection, fall-recovery
grace, dodge, parry and revive invulnerability remain in place.

## Implementation and lifecycle

`PvpWhiteBread.IsHitInvincible` adapts only the boolean decision in
`UnitAvatar.ApplyDamage`. `FriendlyFireWhiteBreadHooks` adds it to the existing
shared damage transpiler after the attacker callbacks and before native dodge
protection. The adapter reads the current registered unique artifact via
`ItemDatabase.FindItemById(1119)` and `GridInventory.TryGetUniqueEffect`, checks
its owner/enabled state, and reads its effective level's `addTimeByLevel` entry.

For this decision only, the allowed window is the native `invincibleTime` minus
the active artifact contribution, plus the original fall grace when applicable.
It never changes artifact enablement, stats, native durations, timers, or SyncVars.
Native `Update` continues to age and expire the original window. Consequently,
turning PvP off during an existing window restores its remaining native duration;
it does not start a new window. Suppression also works immediately when PvP is
enabled during an existing artifact window.

Native inventory activation/level changes and removal remain responsible for the
real duration. Lookup is live, so there are no cached avatar, connection, item or
contribution records to invalidate on rejoin, equip, restart, unload or mode
changes. Invalid/missing artifact inputs leave native protection alone. The
adapter performs a bounded dictionary lookup only for an active player's enabled
native hit window; it adds no polling, inventory scan, network message or warmed
per-hit allocation.

The host already makes the native damage decision and replicates HP. Unmodified
guests need no addon state or new network message for this change. Live multiplayer
combat has not been exercised by this task.

## Installed-game evidence

- `Assembly-CSharp.dll` SHA-256:
  `C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
  REA selected metadata for `Charm_HitInvincible` / `UnitAvatar`:
  `ev_6992047d73308a51015010ce080880520573d6eb7ed8d633585e4a60571e5c78`.
  Same-hash cached managed decompilation was reused for control flow.
- `StreamingAssets/Localization/en-US.json` and `ko-KR.json` map
  `Item_HitInvincible_Name` to the above names and
  `Charm_HitInvincible_Effect` to post-hit invincibility duration.
- `resources.assets` identifies `1119_HitInvincible`, id 1119 and that name key.
  Only those correctly decoded identity fields were used; generic UnityPy
  decoding of later ItemEntity collection fields was misaligned.
- `sharedassets0.assets`, `DCharm_1119Invincible`, component path ID 220019:
  `Charm_HitInvincible`, unique effect, max level 2, serialized durations
  approximately `[0.4, 0.8, 1.2]` seconds. These override the C# initializer.
  The shared-assets hash is
  `73135788932F461054DA76FDE2BBA1793C64748E77E9DFA3028A1AB6DA47EE36`.
- `Charm_HitInvincible.OnEnabledEffect`, `OnDisabledEffect`, `OnUpdatedLevel`
  add/remove its contribution to `UnitAvatar.invincibleTime`; there is no separate
  White Bread on-hit buff to disable.
- `Charm_Basic.EnableEffect` registers the unique effect; `DisableEffect` removes
  it; `Disconnect` disables it before dropping the owner. `CurrentLevelToIdx`
  uses the capped effective level. The native array accessor clamps to its range.
- `UnitAvatar.Update` ages `invincibleTimer` and adds `restoreFallingTimer.time`
  for `EInvincibleType.Fall`. `ApplyDamage` reads `isHitInvincibleEnabled` once
  to deny damage. Its other invulnerability mechanisms use separate checks.

## Verification

The regression first failed with the extra window rejecting a valid PvP hit,
then passed with the adapter. Executable Harmony fixtures cover level/array and
owner changes, missing/disabled/removed items, base/fall/other protection,
non-authoritative avatars, spectators, invalid values, already-running windows,
immediate on/off behavior, PvE/NPC behavior and zero allocation over 1,000 warmed
hits. Real shared-mode fixtures cover all modes, balancing off, selection,
countdown and disconnect cleanup. Installed-game tests check the actual native
admission IL, untouched instructions/labels, inventory lifecycle and rejection
of missing/duplicate/reordered anchors.

Release build: zero warnings/errors. Combat fixtures: 4,987 checks. Shared runtime:
3,184 checks plus 69 Bat checks. Portable/installed-game contracts and installed
Harmony API compatibility passed. These are automated fixtures and static native
contracts, not a live host/guest gameplay claim. Nothing was deployed.
