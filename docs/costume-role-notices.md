# Costume role notices (0.62.0)

Added 2026-10-18. Equipping Wing-Eared Rabbit, Mole, Farmer Squirrel or Turtle
shows a concise private chat summary to that player. The host sends the stock
chat message, so guests do not need the addon. No command or extra toggle is
required. Messages use the host's selected EN/KO catalog.

Wing-Eared Rabbit is labelled **Healer / 치유 담당**. Its optional clauses cover
unlimited HP potions (one must be owned), the enabled live MP fee, enabled
shared-heal range, and level-up random potions excluding HP/MP-related types.
The Sample exception is stated when HP-potion options appear. Compatibility
failures hide the affected clauses independently. A role-only message is valid
when none of these options is active; it does not promise a disabled bonus.

Mole, Farmer Squirrel and Turtle are labelled **Companions / 동료 특화**. Only
configured, applied companion stat adjustments appear: attack speed, damage,
defense, critical chance, critical inheritance and revival speed. Multipliers
show their configured factor (for example `Companion attack speed ×3`); additive
settings show their signed offset and units; absolute settings show the current
effective value, including later native bonuses. The owner-bonus semantics from
[companion modifiers](companion-stat-modifiers.md) still apply. A suspended
multiplier, native fallback or zero contribution is omitted. An enabled Collin
starting grant appears only after its costume-owned registration exists; merely
enabling a future grant mid-run does not advertise a present artifact.

## Current-state and lifetime handling

`SessionCostumeRoles` is a read-only view in the existing session synchronization
pass, after native-baseline stat maintenance. It observes equipped costume,
policy/language revisions, feature compatibility, registered Collin ownership,
and exact companion stat inputs. It uses the shared connection/initialization
receipts and subject cleanup. No new native hooks or gameplay writes are added.

New connections/avatars, re-entry and new session/run receipts receive a fresh
summary after readiness. Changed displayed settings refresh the message on the
next synchronization pass, including reset and native stat changes. Identical
rendered summaries do not resend for unrelated commands or unchanged frames.
Leaving a supported costume clears the notice; returning announces it again.
Old chat entries remain historical messages in the native log.

The stock `DungeonManager.NormalizeChatMessage` limits messages to **120
characters**. `CostumeRoleText` packs short localized clauses into complete lines
within that limit and applies the stock sanitizer. `NativePlayerChat.Show` now
returns whether it dispatched the native targeted packet; incomplete readiness
or failure does not mark the line sent. Pending lines retry at most once a
second and are replaced if the current summary changes. This is dispatch
tracking, not a remote acknowledgement protocol. Null-avatar payloads preserve
all combat/respawn/bomb bubbles and do not broadcast to unrelated players.

The healing radius currently has **no player-configurable setting**: the existing
10-world-unit radius is 5 tiles. `RabbitPotionSettings.ShareRadiusTiles` now owns
the value used by both the actual squared-distance check and its descriptions.
This extraction preserves the strict `< 100` distance check and changes no
healing behavior. Future radius configuration must update this shared source.

## Verification and limits

- Runtime integration covers toggles, nondefault fees, reset, unavailable hooks,
  all supported costumes, native input changes/fallback, reconnect with reused
  IDs/objects, delayed readiness, failed transport retry, presets and EN/KO.
- A warmed observation of an unchanged companion role allocated **0 bytes over
  1,000 checks** in the executable fixture. Formatting occurs on changed inputs,
  not every frame. Cache lifetime follows enrolled subjects and session cleanup.
- Installed-game contracts verify whole English/Korean lines survive native
  normalization, multiplier symbols are preserved, and the existing targeted
  chat payload/host delivery remains unchanged.
- Release build, portable policy/catalog tests, Rabbit drink/description tests,
  Collin lifecycle tests and installed-game contracts pass. Live Unity/guest
  display still needs an in-game check; fixtures are not proof of rendering.

Nothing was deployed. All development builds use `-p:DeployMod=false`.
