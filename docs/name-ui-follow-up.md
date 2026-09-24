# Name UI follow-up

Historical `0.12.1` investigation. **Superseded in `0.12.2`:** the user requested
removal of features that require a modded viewing client. All local presentation
adapters described here have been removed. Native character-name publication is
retained; lobby platform names remain native. See the current
[host-only compatibility inventory](presentation-compatibility.md).

Scope: continue the approved name-color/UI work using the shared presentation
registry, preserving the midpoint gradient `#408af1 -> #a8d7fa`.

## Confirmed gaps and design

The 0.12.0 Steam row/host-summary adapters style only `UserData.Me`. A modded
guest can receive a host's canonical colored character name, yet show that host's
platform nickname plainly. Native `UserData.SteamId` and replicated
`PlayerSpawner.steamID` provide an exact identity mapping. Rebuild a small style
directory from current spawners before refreshing registered labels; never infer
identity from nicknames. Missing, zero, or duplicate IDs do not grant remote style.
Keep the platform nickname itself unchanged and restore it on unbind.

Review confirmed an additional root cause in the local fallback: `UserData` is
a struct, and the reflected `me.Equals(user)` invokes its `Equals(object)`
overload. That overload compares a `ulong` to the boxed user instead of using the
native `Equals(UserData)` implementation. Comparing nonzero numeric Steam IDs
fixes local lobby/host labels before an owned avatar enters the roster.

`UI_MultiplayerHPBar.SetSteamProfile` constructs a separate cached
`characterName(nickname)` label and sizes it to its preferred width. Register that
label when this native method supplies the nickname, observe later character-name
changes, and preserve localized parentheses, alpha, mixed-label tint and layout.
On `SetSpawner`, remove any old binding so a recycled bar cannot retain another
player's alias. Already-open bars bind at their next native profile refresh.

EOS retains its local-identity adapter: the inspected Steam build has no usable
replicated remote PUID mapping. Unmodified peers still cannot receive local UI
adapters. Chat sanitizers, profile fields and cached story substitutions remain
outside this fix. No platform nickname or persistent name is written.

## Implementation and verification plan

- [x] Add portable regressions for stable-ID style resolution, duplicates,
  departure/replacement, remote style changes, alias preservation and mixed labels.
- [x] Add the style directory under `Features/Names/`, refresh it from the existing
  bounded spawner loop, and use it in Steam row/host bindings.
- [x] Register live party labels at native profile/rebind boundaries using the
  existing label presenter and registry; resize only changed text/rendering.
- [x] Expand installed-game signature/IL checks for identity and party-label paths;
  run portable/runtime suites and Debug/Release builds with `-p:DeployMod=false`.
- [x] Review the focused diff and update version/coverage/history.

Commit/push follows the repository's authorized Conventional Commit workflow.
Deployment remains disabled.

## Verification (2026-09-24)

- Debug/Release 0.12.1 builds passed with zero warnings/errors.
- 698 portable and 221 runtime fixture checks passed (919 total), plus installed
  assembly checks for nine name/presentation hooks, native party-label sizing,
  Steam identity, stat refresh, gameplay boundaries and embedded dependencies.
- The actual Steam ownership adapter was invoked with a boxed-user fixture whose
  equality behavior matches the inspected native implementation. It failed
  before the numeric-ID fix and passed afterward, including other-user/zero IDs.
- Rendering-state regression failed before layout invalidation was added and
  passed afterward. Registry tests cover remote style arrival/removal, identical
  nicknames on different IDs, composite rename/language updates and restoration.
- Focused review found the boxed equality issue above; fixed with regression
  evidence. No other actionable findings were reported.
- Installed addon DLL and metadata hashes remain unchanged from 0.12.0 work.
  No deployment or live multiplayer/Unity rendering test was performed.

Live checks: verify your own lobby row/host summary before avatar initialization;
on a modded guest verify the styled host's platform alias remains that alias;
then rename, change language, leave/rejoin and repeat runs while checking party
labels and widths. An unmodified guest has native character-name support but
does not receive these local platform/party adapters. Existing live party labels
bind at the next native profile refresh; loading placeholders remain native.
