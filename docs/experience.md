# XP restriction

Implemented 2026-10-20, addon 0.73.0.

## Controls and behavior

The **Progression / 성장** tab has two independent toggles:

- **Block XP gain / 경험치 획득 차단**: discard new XP grants for all players
  in the hosted session, including the host and unmodified guests.
- **Exempt Wing-Eared Rabbit / 날개귀 토끼 제외**: while blocking, allow the
  currently equipped Wing-Eared Rabbit costume to gain XP and level normally.

Both default off. The exception has no effect while blocking is off. This is a
session-wide progression setting, independent of the friendly-fire participant
list. Commands and UI use the same authority checks and policy:

```text
/one xp block on|off|reset
/one xp rabbit-exempt on|off|reset
/one xp status
/one xp reset
/one save
```

`reset` clears both toggles; resetting one option clears only that option.
`/one save` persists them in v39 presets. Older presets preserve native XP by
default. Global reset and session teardown clear the rule through shared policy;
new sessions load the saved preset when present.

Only new XP grants are discarded. Existing levels, XP, previously earned reward
queues and saved progression restoration remain native. Pickup/fountain
interaction visuals and sounds remain native even when the grant is blocked.
There is no stored XP to replay after disabling the option. Native initialization
and explicit saved-level restoration are not new XP grants.

## Native evidence and design

Installed `Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
MVID: `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA static metadata inspection rechecked this identity on 2026-10-20; cached
decompilation and installed-assembly IL contracts establish the following paths:

| Source | Native path |
| --- | --- |
| XP drops, including orb/shop-produced pickups | `Exp.Update` → `LevelController.AddExp` |
| Magic fountain XP reward | `MagicFountain.Update` → `AddExp` |
| Preserved XP on reconnect | `PlayerSpawner.RestorePreservedRejoinables` → `AddExp` |
| Developer XP grant | `UI_DevCommandlinePanel` → `AddExp` |
| Stock guest command | `UserCode_CmdAddExp__Int32` → `LocalAddExp` |
| Host grant | `AddExp` → `LocalAddExp` |
| Saved level/XP restore | `Initialize` → `LocalInitialize`; intentionally preserved |

The Harmony prefix skips **the whole `LocalAddExp(int)` grant**, before
`NetworkcurrentExp`, forwarded `FarmAbilityController.AddExp`, the level threshold
loop, heals or queued level-up rewards can change anything. It also skips empty
grant calls, so they cannot process queued thresholds while blocking is active.
This does not change separate native talent budgets or retroactively remove
earned levels.

The existing Rabbit level-up transpiler also patches `LocalAddExp`. Skipping the
native method skips its earned-level wrapper. Exempt Rabbit players run both
normally, including enabled random-potion rewards.

The rule reads the shared session policy and `LevelController.Avatar.currentCostume`
at each grant. No player/connection/costume cache, per-frame loop, XP rollback,
custom network message or guest addon is needed. Native `NetworkcurrentExp` and
level SyncVars retain their stock replication for allowed grants.

Do not require completed roster enrollment here: preserved XP arrives inside
player initialization. `Awake` already sets the controller's avatar; native
`PlayerSpawner.Initialize` restores the saved level, equips the costume, then
awards preserved XP. Death, floor changes, new runs, costume swaps and reconnects
therefore consult current intent directly. The next grant after disabling is
native without waiting for a synchronization frame.

The hook validates native grant/guest-dispatch/restoration contracts before
installing. Compatibility failure or a runtime exception disables this hook and
leaves native XP behavior active; status/UI report unavailability, and off/reset
remain available. Unload removes only this feature's Harmony owner.

## Verification and remaining live checks

Automated coverage includes command authority, strict parsing, independent
toggles, reset/save/new-session restore, v39/old-preset behavior, stock-client and
host grant entry points, immediate costume changes, rejoin/death, saved-level
restoration, native reward suppression, and both installation orders alongside
Rabbit level-up rewards. Installed-game contracts verify the native source
routes, write locations, SyncVar transport, avatar initialization and Entry
lifecycle. EN/KO catalogs include controls, help, status and compatibility text.

Builds and tests use `-p:DeployMod=false`. No live multiplayer test or deployment
is implied by these checks. Verify in game with an unmodified guest: block
pickup/fountain XP, enable the Rabbit exception and switch costumes, reconnect,
then turn blocking off and collect fresh XP. Previously saved levels should
remain unchanged throughout.
