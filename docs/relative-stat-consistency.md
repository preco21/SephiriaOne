# Relative character-stat consistency

Version `0.11.0`, investigated on 2026-09-24 against the installed Sephiria 1.0.33
assembly documented in [development notes](development-notes.md).

## Confirmed behavior

- `/stats luck +10`, then `+5`: each character's native luck +15.
- `/stats luck set 100`, then `+10`: switch to each character's native luck +10.
- Native equipment, buffs, and stat multipliers may change afterward. The host
  recalculates the raw addon contribution to retain the displayed offset.
- Absolute sets remain one-time changes. Reset removes the tracked contribution
  and cancels maintenance. Saving stores the desired mode/offset for future sessions.

Native means current stats excluding this addon's tracked raw contribution,
including ordinary native bonuses. It does not freeze the character's gear or
original lobby stats. Fountain and candidate command semantics are unchanged.

## Findings and fix

The previous manual planner added a delta to the current effective stat, while
inheritance recomputed the retained offset from a native baseline. Those paths
could disagree after a multiplier changed. For example, native raw luck 5 with
`+10` became raw 15. After a 2x multiplier, another `+10` produced effective 40
for an existing player, but an equivalent late joiner received 30. Both should
receive native 10 + cumulative offset 20 = 30.

Fractional rounding caused another error: native raw 5 at a 0.5x multiplier,
followed by `+1` and `-1`, could leave raw 4 instead of restoring raw 5. The same
displayed value concealed that drift until the multiplier changed again.

`SessionStatPolicy` now composes the requested setting first and shares one
planner across current commands, inheritance, and automatic maintenance:

```text
nativeRaw = currentRaw - trackedAddonRawContribution
nativeEffective = native game calculation(nativeRaw, bonus, amplifier)
relativeTarget = displayed(nativeEffective) + cumulativeDisplayedOffset
```

The existing exact integer solver converts that target into a raw stat and a new
contribution marker. Net-zero offsets bypass inverse rounding and restore
`nativeRaw` exactly. Relative commands following a set discard the old absolute
target and start a new offset. All players in a manual batch must be valid before
any command writes or policy updates occur.

`SessionRelativeStats` observes raw value, marker, calculated bonus, and amplifier
only for successfully applied relative settings. Host `LateUpdate` and command
preparation recalculate on changed inputs; unchanged frames perform no writes.
It caches the state after its own writes, so the contribution cannot feed back
into the next baseline or stack on subsequent frames. Ready-player guards apply
during native initialization. Server/dungeon changes and unload clear tracking.

If an exact offset becomes impossible (e.g. +1 luck with a 2x multiplier), remove
that stat's addon contribution. Since `0.15.5`, verified restoration is reported
as `NativeFallback` and is fresh for native consumers. Warn once per fallback,
retry when inputs change, and resume if compatible. If even baseline subtraction
overflows, leave that value untouched and report it instead. Desired offsets
remain in the policy and saved preset. Initially rejected full inheritance still
applies nothing and is not retried silently; a successful explicit command is
needed to enroll that stat for that avatar.
Well-formed stat multipliers now have per-player native fallback during initial
planning too, so an incompatible costume does not reject all-family inheritance.
Add/Subtract and absolute Set commands retain their initial validation rules.
See the [penalty-stat review](penalty-stat-sync-review.md).

## Synchronization and limits

The installed `UnitAvatar` declares `customStats`, `calculatedBonusStats`, and
`customStatsAmp` as native synchronized dictionaries. Only the authoritative
server calculates/writes the addon contribution; guests use native replication.
No client-side solver or additional network protocol is introduced. Current
commands, joining players, restored markers, and saved settings use each
character's own inputs rather than copying the host's values.

Native integer/float rounding is reproduced, including overflow guards. This
prevents the reproduced arithmetic mismatch; it does not establish that clients
display every change in the same rendered frame. Native equipment changes and
our `LateUpdate` correction can occur at different points in a frame, and network
delivery has latency. Another mod overwriting raw stats without maintaining the
corresponding marker cannot be reconstructed reliably. Normal additive changes
and replacing both native raw stats and markers are handled.

## Verification

Regression tests first reproduced multiplier divergence, fractional cancellation,
and missing automatic maintenance. Coverage now includes distinct native values,
set-to-relative mode changes, multiple multipliers and stat units, failed batches,
suspension/recovery, reset/unload/server guards, and rejected initial inheritance.
Cross-path checks compare live planning, maintenance, inheritance, and preset
reload against the same expected native-plus-offset result.

Debug and Release builds passed with zero warnings/errors using
`-p:DeployMod=false`. All 619 portable checks and 138 runtime fixture checks passed
(757 total), including 219 relative consistency checks. Installed-game candidate
guard compatibility and the embedded Harmony runtime/license checks passed.
Independent code review found no actionable issues. Release assembly version is
`0.11.0.0`. The installed addon DLL and metadata SHA-256 hashes remained unchanged;
no deployment script ran. Fixtures use isolated temporary preset paths and do not
exercise Unity or a live Mirror connection.

For live verification after manual installation, host with an unmodified guest
whose native luck differs. Compare `/one status` and both character panels after
`+10`, `+5`, `set 100`, and `+10`. Equip/remove multiplier gear and native bonuses,
join/reconnect another guest, restart a run, and reset. Save, fully restart, and
repeat. Test an unrepresentable offset for one player: verify one warning, native
value while suspended, resumption on compatible inputs, and independent behavior
for the other player. Live results remain pending.
