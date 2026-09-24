# Host-only multiplayer compatibility

As of `0.12.2`, the addon retains features whose effects can reach unmodified
guests through Sephiria's native multiplayer state. The user explicitly removed
the requirement for extra UI styling that only a modded viewing client could see.
The local presentation registry, UI hooks, label adapters, platform-style mapping,
and automatic stat-panel refresh from `0.12.0`/`0.12.1` have been removed.

## Retained behavior

| Feature | Effect with the addon on the host |
| --- | --- |
| Fountain points and carryover limit | Native synchronized capacity/limit changes for all eligible players, including newcomer inheritance and restart reconciliation. |
| Character stats | Native synchronized stat changes, cumulative relative offsets, multiplier maintenance, resets and saved presets. |
| Extra item, weapon and miracle choices | Native synchronized extra-choice stats used by subsequent native generation. Existing offers are not regenerated. |
| Host's character-name gradient | Owned `PlayerAvatar.SetPlayerName` publishes the formatted runtime name to the native `playerNameSource` SyncVar. Guests do not need a custom addon protocol to receive it. |
| Commands, status and preset controls | Local controls for those native effects; these are retained as supporting tools. `/mod status` does not acknowledge delivery or rendering on another client. |
| Host settings panel (0.13.0) | Pause-menu button or `/mod ui` exposes the same native-effect services. Guests do not need the panel or a custom network protocol. |

The shared reconciliation coordinator, host snapshots, journaled writes, readback,
fault recovery and critical native read guards remain in use across gameplay
features. Removing presentation does not replay or reset their retained intent.
The [host panel](control-panel.md) owns its own controls; it does not restore
foreign-name styling, stock-panel refresh patches or platform-name changes.

## Name surfaces and native UI limits

The gradient still samples each letter's midpoint between `#408af1` and `#a8d7fa`.
The addon publishes only the owned character's name while another player is in
the session. It requests the plain runtime name when alone again or on unload,
provided ownership/network readiness still permit sending. The profile name is
read-only; native run snapshots can contain the formatted runtime name.

| Surface | Behavior in 0.12.2 |
| --- | --- |
| Native overhead, character-panel, player-list and party labels | Can consume the replicated character name, with each renderer's normal rich-text settings and native refresh timing. No addon label writes or cache refreshes. The alias portion of a party label remains native. |
| Steam/EOS lobby member rows and lobby host/status summaries | Native platform nicknames remain unchanged. These sources do not become styled from host character-name publication. |
| Solo local name labels | No local-only gradient override. Name publication is multiplayer-only. |
| Chat, notifications and chat bubbles | Native sanitization remains; no host-side guarantee of colored names. |
| Story/dialogue substitutions, loading placeholders and other cached labels | Native source/cache behavior; no presentation patches. |
| Open stats and Fountain panels, existing offers | Native refresh and interaction lifecycle. No automatic panel refresh, reopening, item granting or offer regeneration. |
| Profile, rename and other input fields | No direct addon changes. Native inputs that read the runtime name can still see its published markup. |

A native replicated name is not a guarantee that every native UI renders its
tags. In particular, the multiplayer lobby status window will retain its native
platform-name appearance even when the host installs this addon.

## Extension rule

Before adding a feature, identify the native server/owned-client write path,
serialization, and consumer on an unmodified peer. A host-local rendering patch
alone does not satisfy this project's compatibility scope. Add rules to the
[shared synchronization paths](synchronization-guide.md) when appropriate; do not
reintroduce a guest addon requirement or a local-only UI feature to claim coverage.

## Verification

The `0.12.2` Debug and Release builds pass with zero warnings/errors. The portable
suite passes 639 checks and the runtime command/session fixtures pass 221 checks
(860 total). Removed checks belonged to the removed presentation implementations;
gameplay regression coverage is retained.

`GameNameCompatibilityTests` inspects native name publication/restoration,
server setter, command transport, SyncVar serialization/deserialization and the
native name getter against installed Sephiria `1.0.33`. The nine lifecycle paths,
Fountain read boundary, candidate guards and embedded dependencies also pass.
These checks do not execute Unity or establish visible delivery on a live guest.

Still pending: host with an unmodified guest across join/leave, repeated runs,
name changes, native panel refreshes and reset/unload. Inspect the guest's native
character-name consumers after their normal refresh interval; lobby platform
names should remain native. Builds used `-p:DeployMod=false`; no deployment occurred.
