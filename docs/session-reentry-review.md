# Repeated session re-entry review

Version 0.15.1, 2026-09-24. Requested scenario: an unmodified guest joins a
modded host, fully quits the game, then reconnects to that same running host,
repeatedly. Host commands may change or reset settings while the guest is absent.

The review used installed Sephiria 1.0.33, Assembly-CSharp SHA256
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Decompiled game code and binaries remain outside the repository.

## Existing paths retained

| State | Re-entry behavior |
| --- | --- |
| Shared synchronization | Subjects and observations belong to avatar object references. A new avatar/connection gets new inheritance even with a reused GUID, save slot or network ID, and even without an intervening cleanup frame. Connection, character and inventory readiness defer inheritance until native initialization is complete. |
| Stats, Fountain, choices | The current host policy is planned against the new avatar's native inputs. Native saves do not restore the addon raw dictionaries/ownership markers or Fountain contribution. Offline resets therefore do not resurrect old adjustments. |
| Fruit budget | Current policy follows the new avatar's native default/raw/bonus/amplifier inputs and committed selection cost. No saved scalar checkpoint bypasses current policy. |
| Names | Owned-avatar replacement resets name acknowledgment/retry state. The native handshake supplies the current profile name. Existing native replication and guest UI refresh timing still apply. |
| Dice and leaves | The disconnect hook checkpoints current balances, including zero, and retires the old avatar's grant record. Reconnect restores these balances and the original grant metadata; changed future-grant policies cannot refill them. A pending first-departure allowance remains frozen and can complete only once. |

Native GUID-to-save-slot resolution precedes player initialization. Native server
disconnect saving precedes destruction of the connection's avatar, and Mirror
initial state serialization supplies the newly connected client with current
SyncVars/SyncObjects. These are inspected code paths, not transport acknowledgments.

## Defects reproduced and fixed

Inventory and talent checkpoints must restore capacity **before** the native
game loads saved items or clamps talent allocations. Previously, restoring an old
checkpoint marked it as having received the *current* host setting. An old
`set 36` checkpoint could be labeled as current `set 48`, suppressing the actual
update indefinitely. An offline reset disappeared from active policy and could
not remove that restored contribution either.

The shared resource policy now assigns an identity to each successful command,
including resets and repeated identical absolute commands. Checkpoints save the
identity actually applied to that avatar. Restoration records its old identity
without enrolling resource maintenance or acknowledging newer intent. The shared
inheritance journal then reconciles current host intent once the avatar is ready.
An unchanged absolute command preserves later native gains; a reissued command
or changed mode applies its new target.

Reset intent survives in the current host scope, including when there are no
active nondefault settings. It is not written into the user's future preset, and
scope teardown clears it. A reset with no addon ownership does not interfere with
unmanaged native selections. Existing version-1 checkpoints remain readable;
missing command identity is treated as unknown, never proof of current intent.

If an offline decrease/reset conflicts with occupied slots or allocated talents,
the checkpoint remains intact. Early talent loading may retain that sufficient
saved capacity so native allocations can load without clamping. The complete
ordinary inheritance batch stays pending, with its reason in `/one status`, and
retries until the conflict is resolved. Leaving/rejoining while pending saves the
old applied identity, not the unapplied desired setting. Invalid arithmetic,
insufficient restored capacity, identity drift and partial-write faults still
fail through the existing guards/journal.

Review also reproduced a native-baseline ownership error: native level-up capacity
can be saved without its grants being replayed during native initialization.
Restoration used to classify the difference as addon ownership. A later reset or
multiplier could then remove or exclude a legitimate native gain. Both checkpoint
adapters now keep the saved addon contribution separate:

```
restored native baseline = max(saved total - saved addon contribution,
                               reconstructed native baseline)
restored addon contribution = saved addon contribution
```

This retains conservative capacity restoration without counting a reconstructed
award twice or turning an unreplayed native gain into addon state.

## Verification

The stale absolute checkpoint, native-gain multiplier and reset/unmanaged-selection
regressions each failed before their respective fixes.

`ReentryRuntimeTests` composes actual inventory hooks, talent checkpoints, resource
runtime, host commands, policy and reconciliation. Coverage includes:

- Three consecutive reconnects with offline absolute edits, multipliers and reset.
- Stable GUID/save slot with reused and changed network IDs and fresh connections.
- Readiness delay and same-frame avatar replacement without a cleanup frame.
- Unchanged absolute intent/native gains versus reissued identical commands.
- Reset-all with an empty active preset; preservation of occupied/allocated data.
- Rejoining again while a reset is pending, then completing all state when safe.
- Legacy checkpoints without command identities.
- Repeated current stats, Fountain, choices and fruit inheritance without stacking.

The separate actual starting-hook fixture adds three cycles with changed/reset
future policies, spent-zero balances, independent host state, duplicate native
boundaries, and native SaveVersion 0-to-2 migration. Existing hook fixtures cover
GUID/save isolation, initialization boundaries, same-avatar restarts, fault
recovery and saved-item enumeration order. Results are recorded in the
[implementation history](resource-settings-implementation.md#repeated-re-entry-follow-up-0151).

## Native limits and remaining live checks

Full native player-state saving on disconnect is conditional on a started run.
The addon's town disconnect hook additionally saves money/dice. This review does
not make every unsaved native town menu draft persistent; current host settings
are reapplied independently of those drafts.

An immediate town reconnect before the old connection is removed can receive a
new GUID from native authentication. Native stale-connection takeover is limited
to the closed/in-dungeon branch. Such a connection is a new native player/save
identity and may receive fresh starting grants; the addon does not override
authentication to guess identity. Normal full quit followed by resolved
disconnect retains the native rejoin identity.

Fixtures do not run Unity, full native saved-item/effect reconstruction, Mirror
transport or the guest renderer. Live verification remains: keep the host running,
fully quit and reopen an unmodified guest three times in town and during a run,
change/reset settings while absent, and compare host status with reopened guest
panels/gameplay. Include spent-zero dice/leaves, native capacity/point gains,
occupied expanded slots, allocated talents, and conflict resolution after rejoin.
No deployment was performed for this review.
