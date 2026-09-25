# Penalty costumes, native fallback, and disconnect review

Investigated 2026-09-26 against addon 0.15.4 and installed Sephiria 1.0.33.
Assembly-CSharp SHA-256 remains
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Fix version: 0.15.5. Logs and decompiled source remain outside this repository.

## Confirmed connection between the warnings

A penalty costume made the retained cooldown `x3` target incompatible with the
addon's range. Existing maintenance removed its own contribution, but returned
`Suspended`. The coordinator treated every outcome other than `Applied` as
globally unready. `BeforeNativeRead` then reported failed freshness for Fountain
grants and candidate generation, even though the stat's native restoration was
already complete. These boundaries still allowed the native consumer to run.

The affected player's disconnect snapshot records cooldown raw **-50**, addon
**0**, bonus **0**, amplifier **0**; the other feature rules were `Applied` and
the write-fault state was empty. Its stat rule had only two revisions. This is
evidence of a preserved native penalty and misleading freshness, not repeated
application of -150. The log also records native Fountain grants after the
warning; the warning alone does not establish that items were lost.

A second confirmed bug existed before ordinary maintenance: an incompatible
multiplier rejected the joining player's entire combined plan. Once-only
inheritance then did not enroll that player for later automatic stat recovery.
This could leave compatible Fountain, choices, and resources unapplied too.

## Fix and guarantees

- Shared session stat planning first validates the factor and computes exact
  `nativeRaw = raw - ownedContribution` using wide arithmetic. If that baseline
  cannot fit an integer, it rejects the operation without inventing a baseline.
- A well-formed factor whose derived target fails bounds, arithmetic, or exact
  representability returns native raw and zero owned contribution for that
  player. It preserves native bonuses/amplifiers and does not clamp a penalty to
  zero. Valid players still receive their own native-baseline multiplier.
- Commands, panel actions, inheritance, saved presets, and maintenance use this
  same policy. Desired factors remain retained and resume on compatible native
  raw/bonus/amplifier changes. Negative raw values with positive display offsets
  are evaluated in displayed units; they are not automatically rejected.
- A reusable `NativeFallback` reconciliation outcome is fresh only after native
  writes and their readback succeed. Existing verified offset and Fountain
  maintenance restorations use it too. `WaitingForReadiness`, `Suspended`,
  `Rejected`, and `Faulted` remain unready. Partial writes still retain the journal,
  pause maintenance, and require explicit recovery.
- Post-write observations and no-op batch checks remain intact. Unchanged
  fallback does not repeatedly write synchronized dictionaries, retry the target,
  or emit warnings. Status exposes fallback separately from applied bonuses.
- Absolute/offset command rejection and resource safety constraints remain.
  This change does not lower stat limits or remove inventory/talent allocation
  guards. Preserving native state cannot sanitize invalid inputs from other
  native sources/addons or reconstruct an untracked overwrite of raw/marker data.

The installed `Charm_Magic` calculates spell cooldown by dividing by
`(100 + CooldownRecoverySpeed + AdditionalcooldownRecoverySpeed) * .01f` without
a local clamp. With no additional modifier, -50 means a finite doubled cooldown;
-100 produces a zero divisor, and -150 a negative cooldown. Simply allowing
negative multiplied targets would be unsafe. Native -100 or lower is preserved
under fallback too, but is not declared mathematically safe by this addon.

## Disconnect evidence and limits

The inspected host log has 11 transport-close records: ten peer-initiated
`ClosedByPeer`, reason 1000, and one local timeout, reason 4001. A peer-initiated
close does not prove the user chose to leave; the remote game can close its
connection after an error. The timeout snapshot has all tracked rules `Applied`,
no write fault, and cooldown raw/addon/bonus/amplifier all zero. No addon partial
write or maintenance-paused warning appears in this log.

There are **3,938** `k_EResultLimitExceeded` send errors. Steam defines that result
as too much data already queued for sending; it is not itself an invalid-stat
error. See [Valve's send API contract](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/isteamnetworkingsockets.h).
The installed `NextServer.Send` logs this result without directly disconnecting;
its NoConnection/InvalidParam branches do disconnect. Dropped sends/backlog can
still disrupt a session. The available errors do not identify which player,
channel, payload or producer filled the queue.

Numerous damage-particle pool warnings also appear. Native damage feedback uses
an unreliable RPC and damage itself has a reliable RPC. Heavy combat/rendering
traffic is a plausible source of load, not a proven cause or mod attribution.
The first send-limit error occurs after the first penalty player's disconnect,
so those recorded send-limit errors do not explain that earlier disconnect.

There are 272 unknown-message warnings for ID 39466, beginning before the first
penalty warning. This ID was not identified in the inspected native built-in and
version-handshake message types. The native host path producing these warnings
has exception-disconnect disabled; it logs and drops those messages. A guest
mod/version mismatch remains a hypothesis, not an identified sender feature.
No network protocol or transport retry behavior is changed in this fix.

Host managed-heap snapshots do not measure guest memory or total Unity/GPU use.
No available host evidence establishes an out-of-memory cause. The fix resolves
the demonstrated inheritance/freshness defects; it cannot be called a confirmed
disconnect fix. An affected guest's log/crash report from the same event is still
needed to distinguish a client exception, freeze/crash, or transport failure.

## Verification

Regression tests first failed on native penalty planning and mixed-family join
freshness. Coverage includes penalties, stale owned adjustments, native bonuses
and amplifiers, out-of-range/unrepresentable targets, baseline overflow, malformed
factors, mixed characters, saved presets, costume recovery, repeated reconnects,
run restarts, critical boundaries, zero unchanged writes, and partial fallback
failure/recovery. Shared coordinator tests distinguish all result states.

Verification passed **1,813 checks**: 1,007 portable, 655 runtime, 71 starting-hook,
66 budget/inventory-hook, and 14 disconnect-boundary checks. Debug and Release
builds have zero warnings/errors with `DeployMod=false`. Installed-game lifecycle,
Fountain, resource, name, panel, candidate, disconnect-hook and embedded dependency
checks passed. Independent code review found no blocking issue.

Both five-player unchanged-state allocation checks remain at zero bytes/tick
(10,000 iterations; approximately 5.93 microseconds without settings and 18.07
with all settings in this .NET fixture). The new penalty fixture separately
checks repeated Fountain/candidate boundaries for zero native writes and warnings.
Fixtures execute production planners, coordinator and native write adapters with
game API doubles; they do not reproduce Unity rendering, live Mirror delivery or
guest crashes. No deploy script ran; live validation of 0.15.5 remains pending.
