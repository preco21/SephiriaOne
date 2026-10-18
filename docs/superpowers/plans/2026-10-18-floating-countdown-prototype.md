# Floating countdown prototype implementation plan

> Execute inline using the approved design from the conversation. The user asked
> to proceed while retaining chat until multiplayer presentation is verified.

**Goal:** Show short floating respawn and Play Tag bomb countdowns alongside all
existing chat/bubble notices on host and stock clients.

**Architecture:** Add one cosmetic native transport adapter and one shared match
countdown helper. Reuse existing per-second change detection, participant
receipts, match tokens and localization. No new timer, per-player component,
damage call, saved setting or guest protocol. Keep the native broadcast's
unreliable channel and owner inclusion; do not claim live verification.

**Constraints:** Work in the authorized checkout; all builds use
`-p:DeployMod=false`. No deployment/game launch. Commit and push after verification.

## Approved design and boundaries

- Only initial and subsequent respawn seconds and the active bomb timer gain
  floating text. Chat/bubbles, global announcements, results, long opponent and
  costume notices retain their current behavior.
- Respawn text uses the existing localized short sentence. Bomb particles use
  `Bomb: {0}s` / `폭탄: {0}초`; chat retains the full passing instruction.
- `NativePlayerFloatingText.Show(PlayerAvatar, string)` calls the exact native
  cosmetic RPC through a cached delegate. Validate native payload, reader,
  cosmetic receiver, channel and owner inclusion once. Disable this optional
  presentation and warn once on failure until feature reload.
- Reject stale/unready player identities, empty text and non-finite positions.
  Allow dead avatars. Use their head position and the native normal-text style.
  Broadcast once; do not also spawn locally. Native particles own their fade.
- `BroadcastCountdown(player, chat, floating)` retains chat first, captures the
  current match token/participant receipt and revalidates before optional output.
  Existing second-change checks prevent per-frame duplicates.

## Steps and verification

- [x] Add `tests/SephiriaOne.RuntimeTests/FloatingCountdownTests.cs`, including a
  cosmetic boundary fixture. Exercise actual match code: 3/2/1 exactly once,
  ordinary chat retained, successful revival, manual stop, stale connection,
  run restart, optional presentation failure, Tag carrier/fuse transfer and
  one-second respawn. Register it in the runtime runner; observe failure before
  adding implementation.
- [x] Add `SephiriaOne/Infrastructure/NativePlayerFloatingText.cs` and lifecycle
  reset calls in `DeathmatchFeature.cs`. Add a focused shared countdown helper in
  `CombatMatchRuntime.cs`; use it at two respawn sites and the carrier site in
  `CombatMatchTag.cs`. Add the EN/KO short bomb key.
- [x] Add `GameFloatingTextTests.cs` to the installed-game compatibility suite:
  native registration/observer delivery, exact writer/reader order, no damage/log
  side effects, normal particle style, localization and mutations that must fail
  closed. Validate the compiled adapter's payload arguments.
- [x] Run `dotnet run --project tests/SephiriaOne.RuntimeTests -c Release
  -p:DeployMod=false` and fix any relevant regressions.
- [x] Run `dotnet build SephiriaOne/SephiriaOne.csproj -c Release
  -p:DeployMod=false`, then the main test runner with the installed managed
  directory and built addon path, all with `-p:DeployMod=false`.
- [x] Update the feature findings, documentation index and version. Review
  optional-failure and stale-session boundaries, validate the diff, commit and
  push. Document the remaining live test: host/guest countdown visibility,
  drift/readability, one emission per event, unchanged chat and gameplay.
