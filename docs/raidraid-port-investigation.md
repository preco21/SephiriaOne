# RaidRaid → SephiriaOne: port investigation

Research date: 2026-11-01. **Investigation only: no port code, settings, patches,
deployment or live multiplayer tests were added.**

## Recommendation and scope

Most requested gameplay features are feasible as **independent, host-authoritative
SephiriaOne features using native synchronization**. Reimplement their intended
behavior around SephiriaOne's existing services; do not embed RaidRaid or copy its
global patch runner. Several reference behaviors differ from the request, and
some of its lifetime, reward and filtering shortcuts should not be retained.

The updated scope permits optional client installations for **chat and multiplayer
profile UI**. These can provide local history, timestamps, formatting, paging and
profile scaling while unmodified guests retain native chat/UI. Gameplay remains
host-only. **Boss pinning is excluded**, including hidden pinning inside the final
boss helper. No new hotkey is needed for the proposed settings section.

Three approaches considered:

1. **Recommended: independent modules using existing SephiriaOne infrastructure.**
   More initial contract work, but clear ownership, compatibility checks, cleanup,
   localization and test coverage; no RaidRaid installation dependency.
2. Literal port of individual patches. Quicker initially, but imports the six-floor
   heuristics, shared mutable objects, global encounter state and reward gaps.
3. Embed/call RaidRaid as a dependency. Least suitable: retains an unmaintained
   binary/loader, duplicate hooks and settings, and does not solve beta compatibility.

## Evidence and confidence

Primary evidence is the **installed RaidRaid 3.324.0 binary** (metadata author: Osi), not the screenshot
or an assumption about its latest published release. `metadata.json` selects
`RaidRaidLoader.Entry`; the implementation identifies itself as `RaidRaid_test`
with assembly version `0.0.0.0`.

| Artifact | Identity |
| --- | --- |
| `RaidRaid.dll` | 751,616 bytes; SHA-256 `5ea5bddbb560f637636ea605172b9b4504cceb13bef085129a36723aa5787542`; MVID `3ea7e45e-d664-4e66-afc4-fe241d5c319b` |
| `RaidRaidLoader.dll` | SHA-256 `84a86e0e4430c5ce8c4759f7c3993fdce2ec5fafcc33f76c1d618a91d9b44f4a`; MVID `5b5ec0a0-3b39-44ee-9de3-d701fb731ec3` |
| Inspected game | Sephiria `1.1.0` workshop-beta, Steam build `26324456`; Assembly-CSharp SHA-256 `383a0d5ea9218b797d6eacd1f67964a73606ba50572b7af2d10ac5357b621d01` |
| Destination | SephiriaOne `0.107.0`; [beta compatibility audit](beta-110-compatibility.md) |

REA supplied artifact identity, managed types/methods, CIL anchors and static call
edges without loading/executing the target. Private ILSpy exports supplied readable
control-flow inspection; targeted Unity asset decoding confirmed the banned item.
Some REA string inventories report terminal-flag discrepancies while retaining
readable UTF-16; this is not a runtime failure diagnosis. Selected native symbols
were checked against the beta, but neither RaidRaid's complete `PatchAll` nor guest
behavior was certified by this investigation.

The supplied [2026-08-29 tester article](https://gall.dcinside.com/mgallery/board/view/?id=sephiria&no=30270)
was also checked. It is useful historical context, not a current implementation
specification. Features and numbers below come from the installed binary unless
explicitly described as recommendations. The screenshot is a UI reference; its
selected values are not treated as defaults.

Detailed evidence, formulas, native methods, build-local tokens and limitations:

- [Enemy scaling, compensation and rewards](raidraid-scaling-investigation.md)
- [Boss patterns, routes, anvil and encounter reporting](raidraid-encounters-investigation.md)
- [Item policy, party sharing, chat and profile UI](raidraid-multiplayer-investigation.md)

Root REA identity records: `ev_723469ab19b60c065b9fbd2b2f6222fe917a02fad30ae70f0621325429ffb495`
and loader `ev_a8d7950e19e649a4ab97d80619f34f3cd95666a3ecbbffc0303b77fe790127a4`.
Startup/settings/support member records:
`ev_ea5339d1f3083a6e942d97c43595d37a9092b5ae5490ce02e9b45eb1a23eb413` and
`ev_e3086e6f46e33ecada2d0a1bc285a90ceecec9c226f11fc1b32699acb164fdac`.
Hashes and method names remain the portable identity if retained tool records are
not available in a future agent session.

## Requested features: findings and implementation paths

### Difficulty and compensation

| Feature | Actual RaidRaid behavior | Recommended path and host-only status | Main dependencies / risks |
| --- | --- | --- | --- |
| Per-floor enemy HP | Separate normal/miniboss/boss values; once-per-instance multiplication, preserving current HP fraction. Setter accepts far larger values than ordinary UI. | **Feasible, host-only.** Apply a validated multiplier to an owned post-initialization baseline; write native synchronized HP/max HP. | Native classification and spawn readiness; exclude players, companions and friendly NPCs. Record the applied multiplier per enemy, not only current settings. |
| Enemy damage | One floor-wide **additive AllDamageBonus percentage**, shared by all enemy classes; not an independent final multiplier per class. | **Feasible, host-only.** Choose and label the semantics explicitly. Separate class settings would be an extension to RaidRaid. | Native damage calculation, boss-specific derived damage and double application. Do not route enemy bonuses through player PvP stat limiting. |
| Spawn count / rate | Normal/miniboss count multipliers; boss count fixed at one. Also raises active enemy limits and shortens small spawn waits. | **Feasible with medium/high risk, host-only.** Separate total count, concurrent population and spawn-work pacing instead of one overloaded “rate.” | Shared spawn data, hard-mode population bonuses, wave completion, AI and network load. SephiriaOne event probability controls do not cover these enemy waves. |
| Pattern/attack speed | Divides selected miniboss/boss `Timer.time` values by speed using field-name heuristics. It is not a universal animation, movement or attack-speed multiplier. | **Per-AI support is feasible.** Use an explicit audited server-timer registry with baseline restoration. | Coroutines, float/array timers and client-only animation are not covered. Shortened host timers do not automatically change every guest telegraph. |
| Festival of Blood compensation | For compensation fraction `q` and configured HP multiplier `H`, scale its HP-based heal by `1 − q + q/H`. Reads current settings rather than the enemy's applied HP policy. | **Feasible, host-only.** Base compensation on the actual scaled enemy contribution and the exact native healing source. | Player/companion identification; unrelated Rabbit/shared heals must not be modified. Mid-run setting changes otherwise mismatch already-spawned enemies. |
| XP and Leaf compensation | Both use the same percentage setting and `max(1/C, percent/100)` for spawn-count multiplier `C`. At 0%, multiplying enemy count should approximately preserve base aggregate rewards; at 100%, each extra enemy keeps full reward. | **Feasible, host-only.** Retain native reward bonuses and specify whether XP/Leaves need independent controls. Associate reward policy with each spawn. | Integer rounding, overflow, summoned enemies and exceptions; apply compensation once, before distribution. Existing expdrop/leafdrop modifiers must not be applied twice. |

Keep native difficulty, hard-mode, party scaling and addon scaling as distinct
inputs. A large HP pool, a larger population and faster patterns are different
costs and should not share an undocumented multiplier. The six ordinal floors
in RaidRaid are inferred partly from localized names and approximate world
positions; use verified stage/floor identity and explicit unsupported-map behavior
for the beta's custom stages.

RaidRaid's XP/Leaf compensation applies to normal enemies and minibosses, not
boss drops. Random Leaf bonuses are not fully compensated, and fractional carries
are shared across runs. A clean implementation should define full native reward
arithmetic and reset carry at its owned run/category boundary.

### Encounters and reporting

| Feature | Actual RaidRaid behavior | Recommended path and host-only status | Main dependencies / risks |
| --- | --- | --- | --- |
| Extra boss patterns | Nine helper families: Mole Chieftain/Mogdi, Odner, Rataka, Lerid, Pantaxis, Askard chapters 2/3, Kobes, Kraz and final Qliphoth. Reuses native attacks, warnings, bullets and emitters. | **Feasible selectively, highest risk.** Port one named pattern family at a time through an encounter-scoped adapter. | Each warning/damage pair must reach stock guests with matching timing. Global boss references, queued callbacks, local warning-pool mutation and large salvos cannot be copied blindly. |
| First-floor anvil | Moves an existing later anvil into the first branch row of stage 1. Creating an absent anvil additionally requires native upgrade need; missing candidates/prefab can prevent it. One offered branch is not an anvil on every path. | **Good early host-only candidate.** Modify generated native floor data once before replication; preserve graph identity and event metadata. | Define whether “guaranteed” overrides native completed-upgrade behavior. Check interaction with merchant schedules, native special routes and other graph modifiers. |
| Floor-5 sequential bosses | Specifically builds **Kobes → Kraz**, then retains later Q progression. It is not a generic enumeration of every boss. | **Feasible, later phase, host-only native route candidate.** Generate a validated sequence from intended floor-5 candidates. | Doors, travel, completion, native ALL/hard-mode routes and rewards. RaidRaid's sequence predicates can also match unrelated outgoing boss rooms. Intermediate reward suppression is incomplete for some native reward prefabs. |
| First miniboss entrant notice | At battle-start prefix, selects the first living roster entry inside the trigger rectangle. Deduplicates by name/time. | **Good early host-only feature.** Prefer the actual successful trigger event and one receipt per room encounter; broadcast ordinary native chat. | Roster order is not proof of first entry. Concurrent entrants, same names, loading and vetoed starts need correct attribution. No automatic punishment is implied. |
| Boss/miniboss results / DPS | Tracks HP loss and five award categories; **does not calculate DPS with elapsed time**. Uses one global encounter and loose scene/proximity assumptions. | **Feasible, host-only summaries; medium risk.** Track encounter identity, elapsed active time and actual damage, then publish bounded results. | Attribute companion/DoT damage to owners, exclude PvP/overkill, handle phases and simultaneous minibosses. Count successful revives rather than attempted method calls. Optional richer local display can come later. |
| Boss pinning | Includes explicit choices and a final-boss helper that can call pinning even when the extra-pattern toggle is off. | **Excluded.** Do not import selectors, pin calls or associated state. | Extra-pattern adapters must be checked for hidden pin dependencies; none is required by the native attack APIs inspected. |

A native RPC or network prefab is a useful transport, not proof of correct guest
presentation. In particular, expanding only the host's local warning pool can
leave a guest with fewer visible warnings than damaging attacks. Patterns with
that gap must be redesigned or deferred until a stock-client-safe equivalent is
demonstrated. Boss projectiles are outside the current player-owned projectile
limiter, so that limiter is not a safety net for these additions.

### Rewards, sharing and local multiplayer presentation

| Feature | Actual RaidRaid behavior | Recommended path and host-only status | Main dependencies / risks |
| --- | --- | --- | --- |
| Immediate XP / Leaves | Replaces `DropItemOnDie` drops with native per-recipient AddExp/AddMoney. It does not cover every independently created XP/Leaf entity. | **Feasible, host-only; medium/high risk.** First preserve native arithmetic and eligible recipients, then implement an idempotent grant transaction. | Different XP/money recipient loops, early consumption flags and partial grants can lose/duplicate rewards. Define dead/loading/other-floor recipients and reconnect policy. Orb suppression must match the exact grant producer. |
| Bloodstone Earrings ban | Resolves item 1188/key `Item_HPSteal_Name`; filters several add/shop/drop paths and periodically removes inventory items. | **Feasible host-only acquisition enforcement, but broader than a simple filter.** Reject before charging, filter generation, and guard final server acquisition/stash paths. | Current ban misses temporary inventory, some guest shop paths and existing ground drops. Its floor-based refund guess can mint currency. Stock guests may still see an offer unless native offer state is synchronized. |
| Highest Lucky Medal share | RaidRaid copies an allowed donor's actual trade reward; it does **not** choose the strongest medal. Recipient bonus can be applied again to an already donor-adjusted amount. | **Requested behavior is feasible but is a new policy.** Resolve highest applicable live medal award; apply each recipient's native reward modifier once. | Per-run/merchant/player grant receipt; trade re-entry, level/virtual level, disconnected holders and eligibility. Avoid donor-order dependence. |
| Highest Negotiation share | Raises all collected players to the highest natural effective negotiation under the medal toggle, using a separate contribution marker. | **Feasible host-only.** Independent toggle; compose an owned contribution through the existing stat reconciliation path. | Exclude the shared contribution from the next maximum; lower correctly when the best player leaves/changes gear. Negotiation can also affect combat, not only shop prices. |
| Multiplayer chat | Local bounded history, scroll/lock/resize, timestamps, name and item-rarity decoration. History is in-memory, not a permanent archive. | **Feasible optional client feature.** Use native chat for messages and decorate/store locally in modded clients. | Stock beta clients sanitize markup again; host-only formatting cannot override that. Preserve sanitization, IME/input focus, Unicode and bounded history; avoid a global one-item pending-color slot. |
| Profile UI scale | Scales multiplayer HUD contents to 30–100%; separate player-info UI is not the same target. Also has paging. | **Low-risk optional client feature.** Capture/restore each HUD object's original scale, pivot and position; refresh only on UI/settings changes. | Does not replicate to other viewers. Each guest chooses their local percentage. Handle HUD recreation, screen sizes and native layout. 80% size means 20% smaller. |

The beta asset check confirmed Earrings item 1188. A future version should still
validate ID **and** name key and fail closed if identity changes; RaidRaid's final
“whatever item has ID 1188” fallback is unsuitable. Before implementing the ban,
decide explicitly whether enabling it also removes already-owned items. Rejecting
future acquisitions and retroactively confiscating possessions are separate rules.

Immediate XP should still call the native AddExp → LocalAddExp path. This preserves
SephiriaOne's XP block/Rabbit exception and native level-up rewards; writing levels
directly would bypass them. An acquisition transaction needs a stable recipient
snapshot, successful delivery receipts, range checks and connection revalidation.
Do not consume the drop before validation, then replay the whole transaction after
some recipients have already received it.

## Shared architecture and synchronization

Use [existing synchronization extension points](synchronization-guide.md) rather
than a second session manager:

1. **Policy and UI:** SessionSettings, command authority, presets, snapshots,
   SettingsChanged and EN/KO localization. Keep client UI preferences in a separate
   local store so host presets cannot resize guests' HUDs.
2. **Identity and readiness:** reuse the native initialization resolver, ready-player
   collection and connection/object-lifetime receipts. Add enemy/room ownership
   adapters without making raid participants depend on the PvP selection list.
3. **Applied state:** own each contribution, exclude it from baseline observations,
   preflight native writes, verify them and commit policy after successful writes.
   Registration is complete only after a successful application; bounded retries
   handle late native initialization.
4. **Encounter/route state:** use run + floor + encounter identity; cancel work on
   floor disposal, restart, session stop and addon unload. An object instance ID,
   reused connection ID or last player's name alone is not a lifetime.
5. **One-time effects:** reward delivery, notices and first-floor graph edits need
   receipts distinct from continuously reconciled stats. Reconciliation must never
   replay a payout, spawn or heal.
6. **Native adapters:** validate method signatures/IL/assets independently per
   feature and roll back that feature's own hooks on failure. Unknown beta layouts
   stay disabled with an actionable reason.

Recommended application semantics for later implementation: snapshot structural
route/wave policy before generation and enemy policy at confirmed spawn. Display
when an edit takes effect. Live rescaling, if offered, must explicitly preserve
current HP fraction and own the prior contribution; an “off/reset” action must
not pretend it restored existing actors when it only changes future spawns.

Required lifecycle coverage includes fresh lobby, existing lobby, solo and guest
join, first run, repeated run/restart, same-object reinitialization, reconnect with
reused IDs, late ready/loaded avatars, costume/equipment/native-stat changes,
floor/boss phase changes, preset reset/load, toggle changes, host stop and reload.

## Interactions with SephiriaOne

| Existing subsystem | Integration rule |
| --- | --- |
| PvP and all sub-modes | Enemy scaling targets verified PvE enemies only. Do not scale players, zombies, bomb carriers, companions or friendly NPCs. Define whether raid encounter work pauses during a PvP match in the same room; never silently count PvP hits as boss damage. |
| Stats | Highest Negotiation and any new perk compose with the existing baseline/amp/cap logic. Existing direct SyncDictionary writes mean RaidRaid's method-only dirty hooks are insufficient. HP/elemental/PvP caps must retain their current scope. |
| XP restrictions and Rabbit | Use native XP gates and level-up events. Instant rewards may cause immediate Rabbit potion grants; avoid duplicate level-up handling. Festival compensation must not touch unrelated potion healing. |
| Merchant / event spawns | Existing event-probability control is separate from enemy population. Decide deliberately whether addon-created hostile merchants receive raid scaling, otherwise merchant floor HP and raid HP can multiply unintentionally. Generate route changes before merchant schedules cache floor history. |
| Cover / teleport / Combat Mode | Cover can block boss warnings or exits; encounter design must consider it. Recovery teleport and stage acknowledgements must not bypass encounter completion or grant another payout. |
| Projectile optimization | Current ownership resolution follows a player/companion leader and ignores enemy-owned salvos. Add encounter work/live-hazard budgets when needed; do not cap ordinary player attacks or reuse player caps for bosses without a separate policy. |
| Fountain / item grants / slates / supplies | A common item-policy boundary must cover generation, purchase, transfer, temporary storage and direct native grants without breaking existing loadout ownership, rollback or potion supplies. |
| Chat / notices | Use the existing safe native message transport for stock guests; add bounded local decoration/history only for installed clients. Encounter output should not replace combat countdown bubbles. |
| Auto-update / localization / panel | Reuse SephiriaOne's systems. RaidRaid's updater, signed manifests, hotkeys and separate UI renderer are not required dependencies. |

## Performance and network risks

No timing/FPS/network benchmark was run. These are concrete work patterns and
risks found in the code, not claims about the cause of past disconnects.

- **Enemy population dominates.** More enemies mean more AI, pathfinding, physics,
  hit tests, observer traffic and death processing. Shortening spawn waits raises
  load faster; it is not a performance safeguard.
- **Boss additions can burst.** Kobes can schedule 27 falling hazards for nine
  living players in one rain cycle; Kraz includes 7×7/5×5 explosion patterns;
  final-boss overdue queues can drain together. Use bounded per-frame work and
  live-hazard limits while keeping warning/damage pairs intact.
- **Avoid blanket polling.** RaidRaid's runner invokes over twenty subsystems each
  LateUpdate; many throttle internally, but item inventory discovery, UI work and
  native capacity writes still recur. Prefer lifecycle events, dirty generations,
  cached registries and bounded fallback reconciliation.
- **Do not scan scenes per hit.** Enemy provenance, party maxima and item identity
  should be cached at their real change boundaries. DPS accounting should perform
  O(1) updates, allocating per encounter/player rather than per attack.
- **Instant rewards can reduce orb entities but move work.** Per-player grants,
  level-ups, reward UIs and costume rewards still cost work. Replace per-100-XP
  loops with mathematically equivalent bounded arithmetic after rounding tests;
  coalesce only when native reward semantics and exact-once receipts are preserved.
- **Use native network lifetimes.** Do not locally destroy a guest-visible network
  object or suppress a spawn after consumers assume it exists. No new mandatory
  custom messages should be sent to stock guests.
- **Measure affected guests too.** A host FPS measurement does not establish guest
  rendering cost. Test 1/4/9 players, multiple supported boss types, artificial low
  frame rates and latency, plus late observer entry into a room.

## Features/supporting behavior missing from the request

These were found in the installed binary; discovery is **not approval to port**.

| Additional behavior | Why it matters / recommendation |
| --- | --- |
| Difficulty-derived Sapphire bonus | Run-start prerequisites/perk snapshot, sampled minimum difficulty and final-boss settlement. Formula differs from displayed maximum. Changes permanent progression; defer until scaling/completion policy is settled and stock-client settlement tested. |
| Room-route choice expansion | Alters stage branches, not item/weapon/miracle candidates. Distinct from SephiriaOne `/choices`; interacts with anvil, boss sequence and merchant scheduling. |
| Augment selection system | 192 definitions plus offers, picks/rerolls, shop/effects and a custom `AugMsg` Mirror protocol/HELLO handling. Not host-only as implemented; a separate large feature, outside the client exception granted only for chat/profile UI. |
| Boss Rush hard-mode mode | Separate from the floor-5 sequence, with shard/UI/route/portal/reward adjustments. Do not import implicitly. |
| Final-boss healing penalty | Matching one-shot-laser damage IDs can apply +75 healing-penalty points for 25 seconds, refreshed on repeat, with a reused poison appearance. Current matching lacks boss-origin validation, so native lasers can also qualify. Define attack ownership and independent stat/FX cleanup; the cosmetic appearance should not add actual poison. |
| +1 HP Steal run perk | Grants during an initial run window with an ownership marker. Overlaps existing stat/costume controls and disabled PvP HP steal; no need to port as a parallel path. |
| Inventory slot every N levels | Has run-step locking and granted-slot cleanup. Reuse inventory ownership if requested; interacting XP restrictions and immediate level-ups require explicit tests. |
| Acquisition watchdog | Monitors items, XP, Sapphire, Leaves/dice and contextual purchase/reward operations; can label unknown paths suspicious. Legitimate SephiriaOne grants would need explicit recognition. Do not import as an anti-cheat guarantee or add punishment implicitly. |
| Increased lobby capacity / paging | Host capacity 4–16, separate transport ceiling and admission checks; local lobby/HP/user-list paging. Profile size does not require changing connection limits. Keep capacity separate from UI improvements. |
| Lobby hiding / title styling | Local lobby filtering and title markup; not equivalent to a host ban policy. Unrelated to the requested raid gameplay. |
| Boss HP numbers, self highlight, aggro display, zoom, crosshair, automatic dash | Mostly local UI/input features. Only chat/profile have the new explicit client-install exception; defer the rest unless requested. Auto-dash changes input behavior and must not be enabled as part of UI work. |
| Training-ground additions | Inventory/miracle controls and reused NPC/spawn behavior, including local UI. Separate from raid encounters; reuse existing resources where appropriate. |
| Training-dummy protection | Raises HP/max HP to two billion before damage. Separate from raid enemy scaling; do not quietly alter testing targets in a port. |
| Stale entity-state message suppression | Client prefix drops state messages when their network entity is absent, with periodic logging. This is not proof of repaired synchronization; do not import a blanket network-message filter that can conceal ordering problems. |
| Party/encounter safeguards | Duplicate-addon detection, deferred scaling, random-spawner tracking, story-progress gate and cached lookups. Preserve the useful intent with clearer scope and lifecycle handling. |
| Signed loader/updater, localization, global log filtering | Loader validates signed release files and applies pending updates/backups. No need for a second updater. Global log suppression should not hide native diagnostics; prefer scoped, rate-limited addon logs. |

Important reference pitfalls to avoid: broad PatchAll with partial-failure state;
once-per-instance scaling caches not reset by log reset; six-floor localization
heuristics; current-config compensation for old spawns; shared authored spawn
data mutations; global boss/encounter queues; insufficient reward receipts;
name-based notice deduplication; guessed refunds and incomplete inventory coverage.

## Proposed “레이드” navigation section

Add a section to the existing left navigation, using existing rounded controls,
procedural section icons, padding and scroll behavior. Suggested pages:

- **층별 난이도** — floor selector; normal/miniboss/boss HP, explicit damage
  semantics, counts and supported pattern speed; requested/effective application
  boundary; copy-to-floors/reset.
- **보상 및 공유** — XP/Leaf and Festival compensation, immediate collection,
  strongest Medal and highest Negotiation policies.
- **조우 규칙** — early anvil, Earrings acquisition ban, miniboss entry notice,
  floor-5 sequence when implemented. No boss-selection/pinning controls.
- **보스 패턴** — only audited families, availability reasons and conservative
  hazard/work controls; added in the final phase.
- **레이드 기록** — encounter results and concise applied-policy/failure records.
- **멀티플레이 UI** — local chat history/appearance and profile scale/paging.
  Clearly identify these as local preferences; guest controls for host gameplay
  remain read-only.

Do not recreate RaidRaid's window, updater buttons or Ctrl+2 binding. Opening the
existing SephiriaOne panel already provides access. The new section need only
contain implemented pages during gradual rollout; do not ship inert placeholders.

## Recommended implementation order

| Phase | Work | Exit criterion |
| --- | --- | --- |
| 0 — contract and ownership foundation | Raid policy/identity conventions, independent hook failure handling, coexistence detection, EN/KO/presets, test fixtures | Reset/rejoin/restart contracts specified; unknown beta surfaces reject safely; no new gameplay behavior yet |
| 1 — isolated improvements | Local profile scale; bounded chat history/scroll; successful miniboss-entry notice; early anvil | Local UI restores correctly; stock guests receive accurate notices/anvil route; no extra hotkey |
| 2 — bounded rules and observation | Full-path Earrings acquisition policy; encounter damage/time accounting; highest Negotiation; strongest Medal policy | Purchases/refunds and inventory overflow safe; correct decreases on departure; payouts once per recipient; summaries separate PvP |
| 3 — simple enemy scaling | Enemy identity registry; independent HP and damage policies; effective spawn snapshots | No double scaling or friendly-actor changes; explicit live-edit/reset semantics; stock guests see native health/damage |
| 4 — population and rewards | Wave count/concurrency/pacing; compensation; immediate XP/Leaves; explicit supported AI cooldowns | Bounded spawning, correct wave completion/native arithmetic, XP restrictions preserved, no duplicate/lost distribution |
| 5 — progression | Floor-5 boss chain, intermediate/final reward and travel rules | Native special routes, final clear, restart, late join and recovery teleport cannot stall/duplicate progression |
| 6 — harder encounters | One audited extra-pattern family at a time | Every damaging hazard has a valid stock-client warning/FX; low-FPS/9-player queue bounds and cancellation tested |

Sapphire bonuses, augmentation, Boss Rush, expanded capacity and other omitted
systems should receive separate scope decisions after these phases. Phase 1 is
the recommended first implementation package; this report does not start it.

## Porting gates and remaining decisions

- Choose whether enemy “damage multiplier” should match RaidRaid's additive stat
  or be a true multiplier, and whether each class needs independent damage/speed.
- Define all-recipient reward eligibility (dead/loading/other-floor/rejoining)
  and strongest-medal entitlement; do not inherit inconsistent loops silently.
- Define live edits versus next-floor/run effects and whether anvil/ban settings
  override native upgrade need or remove existing possessions.
- Decide whether raid encounters pause while a PvP match is active in their room.
- Test both addon load orders while RaidRaid remains installed. Never assume its
  master toggle removes its hooks or all behavior. Refuse or explain overlapping
  modules instead of unpatching another addon. Remove RaidRaid from the rollout
  test installation once corresponding behavior is replaced.
- Audit each enabled native contract against the installed beta and every later
  update. The current symbol survey is not blanket compatibility certification.
- Use the [official developer guide/materials](references.md#official-developer-guide-and-materials).
  No source license/readme was found in the installed RaidRaid package scan.
  Treat this as behavioral research, retain attribution, and establish permission
  before reusing third-party source/assets; being unmaintained is not a license.

## Research validation and handoff

No target assembly was executed and no live network behavior was observed. REA
identity/member inspection, source/call-path comparison and the targeted Earrings
asset check support the findings. Uncertain guest presentation, full boss routes,
complete item acquisition coverage and exact reward settlement are explicitly
implementation/test gates, not completed claims. No performance benchmark or
disconnect-cause attribution is made.

All contributors/agents should use this report and the three focused companion
notes as working material, then verify the current binary before implementing.
Private decompilation/assets/logs remain outside the repository. Only documentation
is changed by this investigation.
