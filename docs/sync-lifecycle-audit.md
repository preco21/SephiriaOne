# Synchronization and lifecycle audit

Audited on 2026-09-24 against installed Sephiria 1.0.33; fixes released as
`0.11.1`. Covers stats, Fountain points/carryover, candidates, names, addon
presets, and native loadouts. Game fingerprint: [development notes](development-notes.md).

## Findings fixed

1. **Native Fountain increases left the carryover cap behind.** Costume/passive
   changes can increase `GridInventory.dimensionPocket` after `/fountain` ran.
   The UI could allow more items than the server would grant because
   `PlayerSpawner.AddDimensionPocketItemsOnServer` also clamps against the separate
   `DIMENSIONPOCKETLIMIT`. Eligible players' capacity/marker/inventory changes now
   schedule cap repair without replaying point/stat/choice commands.
2. **Same-frame edits could beat LateUpdate.** A loadout change followed by run
   entry can reach granting before polling. A narrow Harmony prefix reconciles
   ready host state immediately before `AddDimensionPocketItemsOnServer` reads
   capacity. It also processes ready arrivals and pending restart repair; it does
   not select or grant items itself.
3. **Negative capacity could block another player's repair.** Removing a native
   status after a low absolute Fountain set can make capacity negative. Cap
   planning treats that player's required allowance as zero; their actual points
   and marker stay untouched. Other players' cap repair and native reset baselines
   remain valid.
4. **Rejected inheritance needed separate Fountain eligibility.** Review caught
   a rejected newcomer triggering shared cap repair through the global policy.
   Observation and repair now require successful Fountain enrollment for that
   avatar or a nonzero contribution marker. Successful zero-contribution sets
   remain eligible; reset removes enrollment. Rejected joins cannot undo an
   independent cap edit.

The grant guard has independent Harmony ownership and error handling. It shares
the existing embedded pinned runtime with candidate patches. If installation
fails, frame polling remains active and `/mod status` reports the missing guard.
Unload removes the guard and clears tracking. Tests install/remove the actual
prefix on a fixture method and inspect its installed-game target signature.

## Event coverage

| Event / transition | Native path and audit result |
| --- | --- |
| Native preset apply/edit/paste | `UI_PresetPanel.UpdateCurrentPlayer` equips costume/weapon, resets/loads passives and updates selected Fountain items. Native effects change additively: relative stats recalculate, candidate markers survive, and changed Fountain capacity now repairs its cap. |
| Costume / character loadout | `PlayerAvatar.UpdateCostumeData` removes old `StatusInstance`s and applies new ones, including starting items/effects. Current native bonuses stay in each player's baseline. `SetRace` changes the race reference; native initialization supplies associated effects. No command replay or host-value copying. |
| Skin change | `UpdateCostumeOutfit` replaces the visual object and invokes its equip path. Any native stat effects feed the same observed values; labels rebind independently. |
| Passive purchase/subtract/reset/load | `AddPassiveStatOnServer` removes/recreates its own statuses; `UserCode_CmdResetPassiveStat` removes passive effects/perks. Additive raw changes preserve addon markers. Relative offsets follow new inputs; absolute sets remain one-time. |
| Root's Retreat reward | `ServerReceiveHardModeReward` increments synchronized `maxPassivePoint`. Unspent points do not change stats; spending them follows the passive path. The addon does not change this spendable budget. |
| Equipment, tablets, miracles, buffs/expiration | Native statuses change raw/bonus/amplifier inputs or Fountain capacity. Relative stats observe all inputs plus their marker. Candidates intentionally remain raw additive bonuses that scale with native amplification. |
| Offset becomes impossible/out of bounds | Suspend only the enrolled relative stat's contribution, warn once, retry on input changes. Status shows suspension; saved intent remains. Initially rejected inheritance is not silently retried. |
| Death/revive/floor movement | Same avatar retains contributions; relative settings follow removal/reapplication of native effects. Readiness guards defer initialization. No repeated inheritance merely because the floor changes. |
| Run end / return to lobby / restart | `RestartGameCoroutine` removes run effects, reloads dungeon constants and keeps avatars. SDK session-start schedules cap repair; the new grant prefix covers run entry before another polling frame. No duplicate player adjustments. |
| Saved native run resumed | Profile, passives, costume, inventory and saved effects restore before readiness. Addon saved policy loads once per host scope. Native saves do not reconstruct addon set-versus-relative intent. |
| Late join/reconnect/new avatar | Once-per-avatar inheritance uses the new avatar's baseline; subtract restored markers before applying settings. A same-frame first Fountain grant also checks readiness. |
| Inheritance rejected | Validate the entire per-avatar plan before writes, apply none, warn once. Do not enroll rejected Fountain/stats; a successful explicit command can enroll them later. |
| `/mod save`, `/mod forget`, preset file edit | Saved addon policy is an explicit separate snapshot. Save/forget do not reapply current stats; reset does not erase the file. No live file reload: a new host/dungeon scope loads it. |
| Server stop/new dungeon/host-to-client transition | Clear active policy, joins, observations and Fountain enrollment. Only hosts load saved policy and calculate/write gameplay changes. |
| Unload/reload | Stop maintenance, restore names, remove candidate contributions and patches. Stats/Fountain retain their existing adjustments until reset/session end, as previously documented. Saved-policy reload uses markers to avoid stacking. |
| Rename/join/leave/avatar replacement | Native preset does not edit PlayerName. Name code follows the current profile, reliable acknowledgment state and avatar rebinding; rich text is never saved to the profile. |

The installed native raw-stat write path is additive (`AddCustomStatUnsafe`).
Costume/passive removal does not clear the dictionary. A temporary IL scan across
the game assembly and selected type decompilation established these paths; no
native code or binaries were added to the repository.

## Remaining native UI/cache limitations

These differ from authoritative stat mismatches. A host-only addon cannot directly
refresh arbitrary UI or rebuild an anvil cache on an unmodified guest.

- **Open stat panel can remain stale after amplification changes.** Installed
  `UI_StatsPanel.Connect` observes raw and calculated-bonus dictionaries, not
  `customStatsAmp`. Sync-object registration orders raw before amplifier data.
  Inspection indicates a combined update can refresh with the old amplifier,
  and an amplifier-only update can miss refresh entirely. Native values still
  replicate; reopening the panel reads them again. This is a source-based finding,
  pending live remote reproduction. `/mod status` reads current host values.
- **Generated offers stay cached.** Item lists are generated on the server and
  synchronized, with connection ownership checks; miracle lists are server-cached
  per player identity; anvil lists are local caches from synced stats. Restored
  level-up queues create ungenerated item sources; actual choice drawing is
  deferred to opening, so initialization alone does not freeze their count.
- **An anvil can hide reroll while keeping a short old list.** Cache 3 of 6
  enhancements, then increase extra choices enough to cover all 6. The native
  list remains 3, but `UI_WeaponEnhancementPanel` uses the current stat to hide
  reroll. Use a fresh anvil; reopening the old one does not regenerate it. Command
  feedback now warns about this case. A reliable fix for an unmodified guest
  needs a supported native cache-invalidation route or guest-side code.
- **Remote names use native refresh timing.** Some HUD labels poll roughly every
  eight seconds; open character-detail panels refresh on reopening.
- **Fountain selection panels snapshot their budget.** Reopen after a capacity
  change. Server reconciliation does not add selections or recover selections
  already discarded by a native UI action.

Another addon absolutely overwriting raw stats while leaving our marker intact
cannot be reliably distinguished from an additive native change. Network latency
and one-frame relative recalculation remain; no atomic rendered update across
machines is promised. Candidate bounds are checked when issuing the command;
later native amplification can change the effective count normally.

## Verification and live checklist

All 619 portable and 173 runtime checks passed (792 total), including 35 new
runtime checks. Regressions failed before their fixes. Coverage includes native
loadout replacement, point rewards, cap tracking, rejection/enrollment, actual
Harmony interception, initialization/authority guards, restart ordering and
uninstall. Nine installed-game lifecycle paths, the grant boundary, both candidate
transpilers and embedded runtime/license passed compatibility inspection. Debug
and Release `0.11.1` builds passed with zero warnings/errors and deployment disabled.

Independent reviews covered names/candidates and the new Fountain/hook code. The
enrollment issue was fixed and re-reviewed successfully. Tests used temporary
preset paths. No deployment script ran. Installed `0.11.0` DLL/metadata hashes
remained unchanged from this task's start.

Fixtures model inspected API boundaries; they do not run Unity, spend real passive
points, or establish Mirror transport behavior. Live checks with an unmodified
guest remain:

1. Use distinct native values. Apply all command families, change native preset/
   costume, and allocate/reset passives on each client. Compare host status,
   freshly reopened panels and gameplay; check multipliers and suspension/recovery.
2. Claim hard-mode points, then spend them; only spending should change stats.
3. Raise native Fountain capacity, select items above the old cap, and immediately
   enter a run. Repeat after second/third lobby returns and loadout changes.
4. Join/reconnect, including incompatible inheritance. Reset and change native
   stats again; removed adjustments should stay removed.
5. Restart with a saved addon preset and test save/reset/forget distinctions.
   Separately test old versus fresh anvils and open versus reopened stat panels.
