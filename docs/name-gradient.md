# Player name gradient

Current behavior (`0.12.2`): retain this formatter and native multiplayer name
publication, with no local label overrides or rendering-setting changes. The
gradient can reach unmodified guests through native character-name replication;
each native renderer controls its appearance and refresh timing. Solo labels and
lobby platform nicknames receive no addon styling. See the current
[compatibility inventory and live checklist](presentation-compatibility.md).
The implementation plan and verification results below record the original
`0.8.0` work; its local-only rendering portion has since been removed.

## Design and implementation plan

Replace the solid blue name with a per-letter gradient from `#408af1` to
`#a8d7fa`. As requested, each letter receives one solid color sampled at the
midpoint of its interval: `(index + 0.5) / letterCount`. Interpolate RGB channels
and round to the nearest byte, with halves rounded upward. A single-letter name
uses `#74B1F6`; the two endpoint colors describe the gradient boundaries.

Use TextMeshPro color tags for both local labels and the synchronized name. This
provides the same per-letter result to unmodified multiplayer clients without a
shared gradient preset. Keep character/stats and existing overhead targets,
ownership checks, profile preservation, alpha fades, and native name transport.
Text elements keep surrogate pairs and combining marks together; whitespace and
existing rich-text tags remain intact. Spaces do not consume color steps.
Native `<noparse>` blocks remain unchanged so generated color tags cannot appear
as literal text inside them.

1. Add a pure gradient formatter and tests for midpoint colors, Unicode, spaces,
   rich-text preservation, legacy blue normalization, and idempotence. Update the
   existing network state tests to use the gradient and retain all session cases.
2. Replace local solid tinting with cached formatted text, white RGB tint, rich
   text enabled, color-tag overrides disabled, and vertex gradients disabled.
   Restore only owned formatting and original rendering flags on rebind/unload,
   preserving game-controlled alpha and any later text changes.
3. Publish the same formatter through `NetworkNameState` and update multiplayer
   acknowledgment logging. Bump to 0.8.0 and update development notes.
4. Run portable checks, Debug/Release builds, installed-game compatibility checks,
   review, and deployment hash verification. Commit/push the verified changes.

Formatting is character-based, not a smooth blend within an individual glyph.
The native name refresh on remote HUDs may take about eight seconds. Existing
name transport/save limitations are recorded in [blue-player-name.md](blue-player-name.md).
The installed `UI_RenameButton` reads `playerNameSource` for its input, so local
label-only formatting in solo play does not enter the profile rename field.

## Live checks

Restart the game and verify both local name labels. Check short/long names,
spaces, Korean letters, a combining-mark name, and an emoji supported by the
game font. Confirm game fades still work and the solo rename input stays plain.
Join/host with an unmodified peer and inspect the nameplate and character panel
after the native refresh interval. Check leaving multiplayer and addon unload
restore plain runtime names and the original local rendering settings.

## Verification results

On 2026-09-24, all 310 automated checks passed, including 37 gradient/label checks
and 21 network-name state checks. Debug and Release builds completed with zero
warnings/errors. Existing installed-game candidate-guard compatibility and
embedded dependency/license checks also passed.

Code review identified literal-angle and escaped-text edge cases; both were
fixed with regression coverage and re-reviewed without remaining findings.
Release 0.8.0 deployed successfully, and the DLL and metadata SHA-256 hashes
matched build output. Assembly version is `0.8.0.0`. Unity rendering, fades, and
live peer visuals have not been verified; follow the live checks after restart.
