# Starting leaves changed in the lobby

2026-09-25, SephiriaOne 0.15.4. The reported solo case was reproduced against the
actual starting-resource hook adapters. A leaves multiplier set after lobby
initialization was accepted but did not affect the imminent first departure.

## Cause and change

`InitializeMoney` recorded both the native seed and the configured setting.
`PlanDepartureMoney` used that frozen setting even when the host subsequently
changed it through chat or the settings panel. Earlier tests deliberately asserted
the old timing; it did not match the expected lobby workflow.

The departure boundary now reads the shared resource policy's latest **intent**,
including an explicit reset. It updates only the pending allowance before the
existing planner calculates `target - grantedSeed`. It does not reset or multiply
the wallet, and does not require another synchronization frame. Changes to Set,
relative offsets and multipliers share the same path.

The existing completed-grant check precedes the lookup. New intent cannot refill
a completed grant after spending, a later floor, reload or reconnect. An existing
write plan remains fixed between planning and its native write. Native write
checkpoints and uncertain-write quarantine remain unchanged.

A saved pending checkpoint is used when a new host scope has no leaves intent;
an explicit current setting/reset supersedes it. A decrease below seed already
paid cannot be honored without confiscating money, so the planner uses the native
allowance and logs a warning. Configure such decreases before fresh initialization.
Dice still use their native initialization boundary.

## Reproduction and regression coverage

The failing regression initialized a native seed of 200 with no leaves setting,
spent 50, earned 20, then chose `x3` before a native departure bonus of 100.
It expected 870 after departure and failed before the fix. The corrected result
is 170 current money plus 700 outstanding allowance; another departure adds zero.

Additional cases cover zero native departure bonus, replacing x2 with x3, Set and
offset edits, reset after a withheld seed, addon reload, saved pending intent,
unsafe decreases, edits between plan/write, five distinct player baselines and
repeated reconnects. Shared runtime tests check that reset intent is available to
the boundary even though reset removes the active nondefault setting, and verify
that commands leave current balances unchanged.

Verification uses API fixtures and installed-game IL checks; it does not run a
live Unity solo or multiplayer session. Deployment remains disabled.

**1,732 checks passed**: 946 portable, 635 shared runtime, 71 starting hooks,
66 budget/inventory hooks and 14 disconnect boundaries. Installed-game IL checks
and Debug/Release builds passed with zero warnings/errors. Independent review
found no blocking issue. The installed addon remains 0.15.3; the built 0.15.4
has not been deployed or tested in a live game.
