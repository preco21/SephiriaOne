# Local presentation compatibility inventory

Implemented against inspected Sephiria 1.0.33 assemblies. This inventory describes
local adapters and intentional limits, not a claim of live Unity or peer testing.

| Surface | Coverage | Binding and limits |
| --- | --- | --- |
| Owned native character name | Covered transport | Existing authority-checked `PlayerAvatar.SetPlayerName`; profile remains untouched. Owned readback is checked, with at most three requests two seconds apart for an unacknowledged target. Exhaustion is suspended and visible; a new target, acknowledgment followed by drift, or replacement avatar permits new work. No peer protocol or peer acknowledgment. |
| Own character panel | Covered local | `UI_StatsPanel.OnOpened`, then active registered label observation reads the panel's actual avatar. Native rename and dictionary changes do not require reopening. |
| Overhead names | Covered local | Bounded current `PlayerSpawner.MultiplayerList` plus owned avatar; destroyed/departed labels unbind. Local subject gets the gradient; remote subjects only render the gradient already present in their native character name. |
| Steam lobby member rows | Covered local | `UI_MultiplayerUserIcon.UpdateState` discovers/rebinds; exact `UserData.Me.Equals(userData)` identity. Decorates that user's platform nickname, never substitutes their character/profile name. Other members retain native names. |
| EOS lobby member rows | Covered local | `UI_MultiplayerUserIcon_E.UpdateState`; nonempty member PUID equals lobby local PUID. Preserves EOS display name or native shortened-PUID fallback. |
| Steam room-host HUD | Covered local | `UI_HUDMultiplayerRoomViewer.UpdateRoomName` and language rebuild discover binding. Observe current lobby owner identity; color only own host-name segment, keep room/chapter text and native cached fields unchanged. |
| EOS host summary | Unsupported | No corresponding audited EOS host-summary renderer is registered. |
| In-dungeon player list | Covered local | `UI_MultiplayerInDungeonUserIcon.SetUser`; observe the bound spawner's current avatar name, including remote renames. No inference from matching names. |
| Other-character panel name | Covered local | `UI_OtherCharacterPanel.OnOpened`; observe actual `otherCharacter`, including remote name changes while open. Does not replay panel opening or rebuild inventory. |
| Own stat panel | Covered local | Observe exact, sorted snapshots of raw, bonus and amplifier dictionaries together. Call only audited parameterless `UI_StatsPanel.Refresh()` after changed snapshots while open. No `OnOpened` replay; categories and stat labels refresh without recreating inventory/miracle controls. |
| Other-character panel statistics | Unsupported | Name is refreshed; its inventory/stat lifetime is native. No broad panel refresh is invoked. |
| Live party HP bars / loading placeholders | Native-only | Native labels continue receiving their native name source. Placeholder nickname alone cannot identify a subject safely; no nickname matching or blanket recoloring. |
| Fountain budget/selection panel | Unsupported refresh | Open-panel budget/selection caches are not refreshed. Reopen through normal native interaction to observe changed capacity; no automatic closing/reopening, item granting or selection mutation. |
| Candidate offers, rerolls, anvils | Unsupported existing caches | New native generation retains gameplay guards. Existing offers are not regenerated and selection/reroll state is not rewritten. Follow the native interaction lifecycle; reopening is not promised to regenerate an already cached offer. |
| Chat, HUD notifications, chat bubbles | Intentionally excluded | Preserve native rich-text sanitization. Publishing a formatted name does not repair these renderers. |
| Host signs, dialogue, journal, ending/credits | Unsupported cached substitutions | No broad story/dialogue replay or hostName replacement. |
| Profile, rename input, cloud comparison, room-title input | Intentionally excluded | Native/plain persistent/input values remain unchanged. |

`PresentationRegistry` observes only registered active bindings each frame and uses
the shared coordinator for post-write observations, readiness and fault outcomes.
Native open/bind/update hooks discover views; one bootstrap scan of six known UI
types covers loading the addon with a view already open. There is no per-frame
scene scan. Hidden bindings wait; destroyed bindings are removed; pooled native
bind methods replace bindings and restore prior owned TMP changes. TMP rich-text,
override-color and vertex-gradient settings are saved/restored, alpha remains
native-controlled, and newer native text is not overwritten during restoration.
Missing hook signatures and binding faults appear in presentation diagnostics.
Mixed host-summary labels keep their native base RGB and vertex gradient for
untagged room/chapter text. Audited TMP `SaveGlyphVertexInfo` honors explicit name
color tags when `overrideColorTags` is false. Remote native name markup is retained
verbatim on unbind; taking ownership of TMP settings does not imply text ownership.

Unmodified peers can receive native published character names, but this addon
cannot repair their cached labels, platform nicknames or sanitizing renderers.
Remote appearance is unverified; there is no capability exchange.

## Verification

Portable regression cases cover idle deduplication, changed input, hidden/reopen,
rebind restoration, destruction, teardown, retry deadlines, bounded exhaustion,
late acknowledgment, target supersession and replacement reset. Existing gradient
cases cover Unicode/markup preservation and restoring only owned text.
Portable label-adapter fixtures also exercise the actual rendering state writes:
remote markup preservation, mixed-label RGB/vertex-gradient preservation, rich-text
settings, own-label cleanup, native fade alpha, and newer native text/RGB edits.

`GamePresentationCompatibilityTests` inspects seven installed native hook/field
contracts, own Steam identity access, and the stat-only refresh method's direct IL
calls. It does not execute game methods and cannot prove future transitive refresh
semantics. A compatibility failure requires a new native-code audit.

Still required in a running game: visible TMP alpha/fades/layout and restoration,
pooled rows, language changes, host migration, EOS/Steam identity changes, delayed
native name readback, panel dictionary delivery order, and host/modded-guest/
unmodified-guest sessions across repeated runs. No live test or deployment was
performed by this migration.
