# RaidRaid multiplayer, item and local UI investigation

Research dated 2026-11-01. No implementation, deployment, target execution or
multiplayer test was performed for this note. The requested destination is
SephiriaOne with stock guests for gameplay; optional client installations are
allowed for chat and multiplayer profile UI improvements. Local UI is therefore
a feasible part of the proposal, with a stock-client fallback.

## Evidence and scope

The installed RaidRaid 3.324 artifact is the authority for current behavior:

- `AddOns/RaidRaid/RaidRaid.dll`, 751,616 bytes, SHA-256
  `5ea5bddbb560f637636ea605172b9b4504cceb13bef085129a36723aa5787542`.
- Module name `RaidRaid_test.dll`, assembly version `0.0.0.0`, MVID
  `3ea7e45e-d664-4e66-afc4-fe241d5c319b`. The release label is not its assembly
  version. REA identity evidence:
  `ev_0c06991fe654b2960422de9c88cd7982dd9af481ea0c3ed035c846c2f773f88e`.
- Focused REA member inspection selected 35 relevant exact types, with no
  unmatched names, complete metadata/CIL coverage, 182 methods and 1,882 call
  edges: `ev_318fd7172b1907eeab623fd061d5c53ba90185e90c1ae60d6f58ab1f728d6791`.
  All call-edge pages were inspected (offsets 0, 400, 800, 1200, 1600); the final
  page was exhausted. First/final page evidence:
  `ev_484ce53ead9c9c43dbeb52db323b85106c60c1c9a1d445509cff49dd5dfe6c2c`,
  `ev_105af0e72f5774f550225d03349878140bec01aa2dfb4c7e369f4066a2296ee5`.
- Current installed beta `Assembly-CSharp.dll` SHA-256
  `383a0d5ea9218b797d6eacd1f67964a73606ba50572b7af2d10ac5357b621d01`, MVID
  `ba32c2d4-ef71-4869-856b-49ec7aea12f4`. Focused REA member evidence for 13
  relevant chat/UI/shop/item types:
  `ev_ecf21de373170efcb82d1b2ac4f4f1f9dbd677d1dfb4ab22955abe10b0438ab2`.
  All selected names matched and metadata/CIL coverage is complete. Its user
  strings have six ECMA terminal-flag discrepancies; readable UTF-16 was
  retained, and this is not proof of runtime acceptance or failure.

Method semantics were read in private ILSpy exports under
`%TEMP%/sephiria-raidraid-investigation/main` and `%TEMP%/sephiria-beta110/game`.
REA confirms artifact-bound methods and static call edges; it does not execute
the code or itself establish C# source equivalence. All tokens below are
build-local coordinates for the hashes above. No game source or binaries are
included in this repository. Static conclusions are distinguished from port
recommendations and unverified peer behavior.

## Compatibility classification

| Capability | Installed RaidRaid behavior | Port classification |
| --- | --- | --- |
| Bloodstone Earrings ban | Server acquisition hooks, unlock-list edits, periodic inventory removal and drop destruction | Host-only enforcement feasible; current implementation has coverage and economy holes |
| Lucky Medal share | Allowed donor's actual trade reward copied to other players | Host-only feasible; highest-medal selection is a new policy, absent from RaidRaid |
| Highest negotiation share | All collected players raised to highest natural effective negotiation | Host-only feasible through native synchronized stats |
| Chat history, size, lock, timestamp, rarity colors | Local log subscriptions and local HUD patches | Optional client UI; each viewer needs addon for these improvements |
| Chat name colors | Local receiver/sanitizer/render overrides | Optional client UI; host alone cannot override stock receiver sanitization |
| Multiplayer profile scale | Scales multiplayer HP-bar contents container | Optional client UI; does not resize the separate player-info panel |
| HP/lobby/user-list paging | Locally hides/shows existing native UI entries | Optional client UI; independent from server capacity |
| Room player cap | Host lobby selection, network/transport limits and connection admission | Host-only candidate, separately verify transport and native game-mode limits |
| Lobby hiding | Local Steam lobby-list filter persisted by owner/lobby ID | Local convenience feature, not a server ban/kick |
| Lobby title color syntax | Converts local creation input to color markup in ordinary lobby metadata | Native metadata transport; stock prefab rendering remains unverified |
| Own marker / boss aggro outline | Local visual objects; aggro feed supplied locally | Local presentation; not an automatic shared guest effect |

None of the inspected item/medal/negotiation/chat/profile mechanisms requires a
new network message type. `TradeMoneyAdded/` is already a stock custom-message
payload understood by `UI_ShopPanel`, not a RaidRaid-only protocol. This is a
bounded conclusion about these feature families, not all RaidRaid features.

## Bloodstone Earrings: identity, coverage and holes

`ItemBanCore.ResolveEarring` (`0x060002e5`) first tries ID **1188** with localization
key **`Item_HPSteal_Name`**, then scans item IDs for that key or an object name
containing `HPSteal`. Its final fallback accepts item 1188 even if its identity
did not match. A port should require a verified key/component contract and
disable this feature on mismatch; do not retain that arbitrary-ID fallback.

The beta resource audit independently confirms ID 1188: `resources.assets`
path ID `29421`, ItemEntity name `1188_HPSteal`, name key `Item_HPSteal_Name`,
flavor key `Item_HPSteal_FlavorText`, active type 4, rarity 3, item type 5,
resource-prefab reference file 3/path `69342`. Resource SHA-256 is
`d54dc846c1f4b748349301cbd72151926472bad46b28cc9a4e635b14b28a9990`;
the raw item SHA-256 is
`c3fc2501022b1560df847ce546d535f44e880fc9b6cab056327b62008f1d6dbe`.
The private decoder verified the metadata/script and read 168/204 bytes; the
unused tail remains unparsed. Evidence is retained outside Git in
`%TEMP%/sephiria-raidraid-item1188.log` and its companion script.

Observed admission/removal paths:

| RaidRaid method / token | Native surface | Behavior |
| --- | --- | --- |
| `ItemBanAdd_Patch.Prefix` / `0x060002fb` | `GridInventory.LocalAddItem` | Return false before adding; notify and attempt refund |
| `ItemBanAddPos_Patch.Prefix` / `0x060002fc` | `LocalAddItemAtPosition` | Return enum value 9; beta value 9 is `ItemNotFound` |
| `ItemBanPocket_Patch.Prefix` / `0x060002fd` | `PlayerSpawner.AddDimensionPocketItemsOnServer(int[])` | Remove banned IDs from Fountain carryover array |
| `ItemBanDrop_Patch.Postfix` / `0x060002fe` | `Item.OnStartServer` | Destroy newly spawned banned floor item |
| `ItemBanInit_Patch.Postfix` / `0x060002ff` | `Item.Initialize` | Destroy already-networked banned item after initialization |
| `ItemBanCore.SweepInventories` / `0x060002f3` | `inventoryMatrix`, `subBagMatrix` | Global active `GridInventory` scan; force-remove main item under native `Permission`, decrease sub-bag quantity |
| `ItemBanCore.ApplyShopFloor` / `0x060002ec` | Player unlock SyncLists | Remove banned item normally; temporarily add it while on `PocketDimensionShop` to avoid local unlock offer |
| `ItemBanShopPick_Patch.Postfix` / `0x06000304` | `PocketDimensionShop.PickRandomItem` | Replace banned selections with eligible charm, favor same rarity |
| `ItemBanShopList_Patch.Postfix` / `0x06000303` | `ItemDatabase.GetAllPocketDimensionItems` | During local `FillHands`, filter banned/already-purchased entries |

The broad add hooks intercept many ordinary rewards, merchant transfers,
training grants and reload paths that call them. That does **not** prove an
all-source ban. Specific holes found in current beta contracts:

1. Existing ground items are not swept when enabling. New-spawn hooks do not
   remove a banned drop already on the floor; picking it up can still fail.
2. `temporaryInventory` is not inspected. Beta
   `PlayerSpawner.UserCode_CmdBuyPocketDimensionItem__Int32__Boolean` directly
   appends there when inventory is full and sends its native notification.
   RaidRaid does not guard this command body. The pending item can remain until
   a later transfer fails.
3. `LocalAddItemToSubBag` and direct sub-bag save restoration bypass the two
   admission hooks. The one-second sweep eventually removes active sub-bag
   contents, rather than denying them atomically. Direct `SetItem` creates
   charm effects and inserts into the main matrix; it too is not an admission
   hook here. Inactive inventories are outside `FindObjectsOfType`'s default
   active-object scan.
4. Ordinary reward/merchant selection pools are mostly influenced through
   unlock removal, not comprehensive result filtering. Already-created offers
   and generation paths that do not consult those lists can still offer the
   item. Rejected addition is not evidence that a cost/reward transaction was
   cancelled or replaced successfully.
5. Pocket shop is a local `MonoBehaviour`. Host patches of `FillHands` and
   `PickRandomItem` do not execute in an unmodified guest process. Temporarily
   synchronizing unlock state can suppress the unlock offer, but timing and
   existing guest choices are not covered. Adding the item to the unlock list
   also makes it eligible for the native random-buy pool; only modded local
   pickers have the replacement patch. Replacement leaves the banned entry in
   place if no substitute is found.
6. `MaybeRefund` (`0x060002ef`) pays `sapphirePrice * 2` for **any** blocked add
   to a player's inventory while that player is on the shop floor. It proves
   neither a charge nor a purchase type, quantity or unique transaction. The
   beta shop charges the client's `sapphireUseInRun` first; unlock price is
   `sapphirePrice`, while ordinary buy price is twice that. Thus the heuristic
   can overpay, pay on an unrelated blocked grant, or miss a full-inventory
   temporary grant. Do not port it as a transaction guarantee.
7. No Harmony finalizer resets `ItemBanCore.InFillHands` after a thrown original
   method. This global boolean is not safe for nested execution either.

Lifecycle/performance observations: `Tick` (`0x060002e6`) runs from the shared
runner each frame but sweeps at one-second intervals. It allocates the global
inventory array and temporary lists. `_removed` and `_shopTemp` retain spawner
objects; server stop clears them, disabling restores recorded unlocks, but
there is no explicit per-disconnect/new-run identity receipt. `_mute` is a
name/item/reason dictionary with a five-second throttle and no observed pruning.
Deleting an item is permanent; disabling restores unlock entries, not items.

Recommended port contract: use one verified item policy across Fountain,
reward generation, merchant transactions, inventory admission (main,
positioned, temporary and sub bag), save restoration and ground spawning.
Filter before a charge or reward commitment where possible. At enable/scope
attachment, reconcile already-existing ground, main, temporary and sub-bag
state once. Use native removal APIs so effect teardown and stock replication
remain correct; do not silently skip `SetItem` if its caller assumes insertion
succeeded. Bind caches to run/spawner/avatar/inventory identities and clear on
all corresponding boundaries. Subscribe to relevant native changes rather
than searching the scene every second. Keep source-specific refunds behind
verified transaction receipts; the host-only native shop flow may require a
limited rejection-and-notice UX for stock guests because local offer and charge
state cannot be rewritten by a host UI patch. Optional client UI can hide the
offer before selection. Define whether existing copies are deleted when
enabling before implementing the feature; this is a destructive policy choice.

## Lucky Medal and highest negotiation

### What RaidRaid actually does

`MedalShare_Patch.Prefix` / `Postfix` (`0x0600022d` / `0x0600022e`) wrap
`Charm_TradeMoney.HandleTradeBegin`. Prefix captures the donor's money only on
the server, when sharing is on and `MedalShare.Allowed` accepts the donor.
Postfix uses the **positive money delta**, then loops all other spawners and
awards it once per recipient's `HaveTraded/<socialID>` marker. It additionally
adds the recipient's positive `MoneyDrop` percentage of the donor's unmodified
per-level base amount. It writes each recipient's marker before awarding and
sends the native `TradeMoneyAdded/<amount>` notification.

This is not highest-medal sharing. It does not compare equipped medal levels
or effects. The first allowed donor to visit can claim the merchant marker
for everyone, preventing a stronger later donor's reward. A donor's own
MoneyDrop already affects the captured delta; recipients then get their own
positive MoneyDrop contribution on top. Negative recipient MoneyDrop is
ignored by this sharing code. There is no explicit run/floor/death/readiness
filter beyond available avatars and marker checks. A later error can leave a
marker without its award. Default settings enable medal sharing and host
donation; guest donors require a persisted Steam-ID allowlist. Steam ID zero
cannot be allowed. Cached resolved Steam persona names never refresh in the
inspected helper; failed lookups retry every five seconds.

Beta `Charm_TradeMoney.HandleTradeBegin` (`0x060012d7`) remains the corresponding
native handler. Its declared default level array is `{100,100,100,200}`; this
is not a verification of serialized prefab overrides. Native adds the holder's
MoneyDrop once and uses the same merchant marker. Stock `UI_ShopPanel` already
handles the message, including optional audio and an open-shop reward visual.
Those local presentation conditions mean sending a notice is not proof that
every guest displayed the animation.

`NegoShare.Tick` (`0x0600030b`) is different: it collects every available avatar,
finds the highest effective natural negotiation, and raises each to that value.
Natural is `(customStats[NEGOTIATION] - customStats[RAIDRAIDNG] + bonus)` with
the native amplifier/truncation. `ApplyTarget` (`0x06000311`) solves an integer
raw bonus under each recipient's amplifier; `Write` (`0x06000312`) changes
native `NEGOTIATION` and the `RAIDRAIDNG` contribution marker. Recursion is
suppressed with `_applying`; matching contribution values produce no writes.
Amplifiers at or below -100% are skipped. Its adjustment loops are bounded at
64/128 iterations, but arithmetic still uses `int` multiplication and float
rounding, with no explicit overflow guard.

Negotiation is enabled by the **same MedalShareOn toggle**, only while a run is
started. It does not use the medal donor allowlist. Dirty hooks observe
`AddCustomStatUnsafe`, `AddCustomStatAmp`, `PlayerAvatar.OnStopServer`,
`PlayerSpawner.Initialize`, and `HorayNetworkManager.NewGame`. Tick returns
early unless dirty, so lifecycle detection also depends on those notifications.
Direct dictionary writes/clears and bonus changes can bypass method hooks.
This matters in SephiriaOne, whose own stat writes use the SyncDictionary
directly. Highest-player departure, reconnect, new run, restored save and
lowering the previous maximum all need explicit coverage. Raising native
negotiation also affects every other consumer, including advanced-negotiation
combat calculations; it is not only a displayed shop discount.

### Recommended host-only policy

- Make highest Lucky Medal and highest negotiation independent settings.
  Use a generic validated party roster; do not implicitly reuse the PvP
  participant selection or RaidRaid's persistent Steam-only donor allowlist.
- For medal sharing, select the maximum **active equipped medal base reward**
  from live native charm components, including virtual-level handling. Apply
  the recipient's own MoneyDrop exactly once. Recommended trigger is each
  player's ordinary first trade with that merchant, using the party maximum
  at that time. If simultaneous party-wide payout is desired instead, specify
  that explicitly; it has different join/merchant-order behavior.
- Preserve exactly-once native merchant entitlement and native holder behavior.
  A custom share receipt and the native `HaveTraded/` marker must agree; do not
  stack a second payout or consume entitlement before an award can complete.
  Validate run, connection, avatar and merchant context at commit. Reconnect,
  save/load, holder removal, virtual-level changes, two donors and callback
  reentrancy are required fixtures before claiming correctness.
- For negotiation, retain a separate owned contribution and compute the max
  from values excluding that contribution. Define composition with
  `SEPHIRIAONE_STAT_NEGOTIATION`: recommended order is native stats, ordinary
  SephiriaOne stat policy, then party-share contribution. Reset removes only
  the share contribution. Never let a previous shared maximum become a new
  baseline and ratchet upward.
- Observe raw/bonus/amplifier dictionary changes and roster/run changes; coalesce
  to one recalculation, reuse collections, write only actual deltas and use
  checked/wider calculations. A bounded O(players) pass is sufficient at this
  lobby scale. Do not rescan every inventory each frame.

## Chat improvements and optional client behavior

`ChatScroll.OnLog` (`0x0600032f`) subscribes to `GameLogWriter.OnLogAdded`.
It stores text/color in memory, removing 50 oldest entries when the count is
already above 300 before adding; the maximum retained count is **301**, not an
unbounded archive. `Disable` (`0x06000350`) unsubscribes, restores the original
pool/size/mask, destroys owned controls/clones and clears history. Only width,
line count and lock settings are saved to PlayerPrefs; message history is not
saved across application restarts. The separate native `UnitAvatar.OnWriteLog`
feeds the HUD directly and is not subscribed by this history collector, so
not every displayed avatar-local log is retained.

The local chat controls provide 3–30 requested lines (also constrained by screen
fit), 40–100% width in ten-percent steps, mouse-wheel/scrollbar browsing and a
lock that holds fade/background. While browsing, `ChatScrollSpawn_Patch` blocks
new native visual spawns while the global log subscriber continues collecting.
Discovery retries are two seconds, line measurement is every 0.5 seconds, and
browsing logs are held visible every 0.4 seconds. Cached UI objects, pooled
entries and shared corner buffers reduce churn, but per-frame mask/bar work
and string-based alpha rewriting remain. A replacement should be event-driven
except active input/fade and use per-viewer owned lifetime restoration.

`ChatStamp.Add` (`0x06000363`) adds local-clock `[HH:mm]` to logs.
`ItemColorChat.Pend` / `OnWriteLog` (`0x0600035f` / `0x06000360`) intercept
`ShowItemAdditionUI`, hold one pending item name for up to one second, and
replace matching text in the next log. Ordinary rarity gets native rarity
color; dual items get a character gradient toward the dual color. The global
one-slot name match can misassociate overlapping/reentrant logs, and iterating
UTF-16 chars for a gradient splits surrogate pairs. Prefer a scoped item-log
context or structured local event and existing Unicode-safe name helpers.

`ChatRpc_Patch.Prefix` (`0x060004c7`) restores colored avatar names only when
their sanitized text matches the received name. `ChatNameColor_Patch.Prefix`
(`0x06000370`) preserves only allowed color markup and a visible-name limit.
`HUDLogSetText_Patch` converts native keywords, strips unwanted tags, enables
rich text and disables control-character parsing; `HUDLogTags` accepts only
6/8-digit hex color tags and closing color tags. Do not simply turn unrestricted
rich text on for arbitrary remote messages.

Current beta receiver `DungeonManager.UserCode_RpcChat__PlayerAvatar__String__String`
normalizes the name (64 visible-character policy) and message (120) **again on
each client**; `UI_HUDLog.SetText` also strips markup and sets `richText=false`.
Therefore host-side color preservation alone cannot color stock guests' chat.
Stock notices and chat can continue using ordinary native RPC payloads; a client
addon adds history, local timestamps, item/name decoration and profile controls.
Neither chat history nor profile scaling needs a custom protocol.

If a later feature needs structured information not present in native state,
use an optional versioned, namespaced capability negotiation and send only to
confirmed compatible peers, with bounded payloads and ordinary native chat as
fallback. Never send unknown required messages to stock clients or overload
chat text with hidden mandatory control packets. This is a future design
option, not a requirement found for the baseline enhancements.

## Profile scale, player capacity, paging and lobby controls

`PartyUiScale.Tick` (`0x06000358`) checks every 0.5 seconds and scales
`UI_MultiplayerHUD.contentsZone` to 30–100% in five-percent setting increments.
It caches the RectTransform, uses `FindAnyObjectByType<UI_MultiplayerHPBar>` as
fallback, pivots to top left while preserving placement, and writes only if
scale differs. Returning to 100% does not restore the original pivot/position;
there is no separate teardown restore in this class. A SephiriaOne adapter
should capture and restore original scale/pivot/position per actual UI object,
handle recreated HUDs, and avoid fighting native layout or another addon.

`MaxPlayerCore.ApplyMaxConnections` (`0x06000611`) sets NetworkManager,
NetworkServer and reflected FizzySteamworks server `maxConnections` to
`Settings.MaxPlayers * 2`. Setting range is 4–16, default 16. It runs on manager
Awake, transport ServerStart and every shared-runner frame; `_vanillaConn` is
captured but not restored by the inspected class. `MaxMemberBox_Patch` makes
the host lobby picker contain `MaxPlayers - 1` options; native selected value
plus two becomes the actual lobby capacity. `RoomCapConnect_Patch.Postfix`
(`0x06000619`) disconnects a newly connected remote when current connections
exceed `RoomGuard.RoomCap` (`0x06000617`), using Steam lobby `MaxMembers` when
available and the setting otherwise. The transport ceiling and player cap are
different values. Native beta additionally clamps capacity to a selected
custom game mode's `maxPlayers`; that restriction should remain respected.

This static route does not demonstrate a stable 16-player game, EOS transport
parity, accounting for pending connections, reconnect pressure or late joins.
A port should apply capacity at bounded lifecycle events, verify the actual
transport fields, and restore owned changes on unload. Test occupancy and
admission separately from display. Avoid per-frame reflection/writes and
ambiguous transport failure swallowing.

`HpBarPages` uses 2–8 bars/page (default 4), with mouse back/forward paging and
left-Ctrl release visibility toggle. It excludes focused text input from these
shortcuts. `MemberPages` uses five lobby icons/page; `UserListPages` uses five
players/page in the native user-list panel. Hooked refresh points include
`SetSpawner`, `SetSteamProfile`, `CreateMemberIcon`, `ClearLobbyMemberIcons`,
`HandleUserLeft`, `UpdateUserList` and panel close. They change local visibility
and navigation, not party membership. Preserve existing custom hotkeys and
native focus ownership; reuse SephiriaOne input handling rather than hard-code
the same Ctrl/mouse bindings.

`LobbyBlock.IsBlocked` (`0x0600064b`) filters by Steam owner ID **or** lobby ID.
`Load`/`Save` (`0x06000644` / `0x06000645`) migrate an older owner list and save
`RaidRaid.LobbyBlock2` entries as owner/lobby/label. The X button from
`LobbyKick_Patch` merely records a hidden room and refreshes local search;
it never disconnects or bans a guest. The patched surfaces are Steam
`UI_MultiplayerPanel.HandleFound` and `UI_MultiplayerLobbyElement.SetLobby`;
there is no corresponding EOS filter in the inspected family. Pooled lobby
rows should update captured identity when reused; RaidRaid's existing-button
check can retain the first captured owner/lobby callback.

`TitleColor.Convert` (`0x060004a4`) converts Korean named colors, `{기본}` and
`{#...}` tokens at Steam/EOS `HandleCreated` into native lobby-name color tags.
Arbitrary `#` payloads are accepted without hex validation. Beta lobby rows
assign normalized lobby-name text directly; actual rich-text prefab settings
and visual output were not verified. Use a strict color allowlist and preserve
readable plain titles if this optional feature is included.

## Reuse in SephiriaOne and intentionally separate features

Current repository source establishes these integration points:

- `Features/Items/ItemRestrictionRuntime`, `ItemRestrictionOverlay` and hooks
  already manage **given-item binding/ownership restrictions**, not an item-ID
  ban. They retain original metadata and save original values without toggling
  live synchronized state. Reuse their ownership/lifetime discipline, but keep
  the ban a separate setting/policy and do not conflate it with `/one items unlock`.
- `Features/Stats/StatCatalog` already includes `NEGOTIATION` and `MONEYDROP`;
  `SessionStatPolicy`, `HostStateAdapter`, `NativeStateWrites` and
  `StateWriteBatch` provide native-baseline tracking, current-scope validation
  and guarded writes. A party-share contribution must compose with those
  mechanisms instead of installing a competing independent stat writer.
- `FriendlyFireParticipants` has connection/avatar/run receipts and cleanup;
  `SettingsPanelParticipants` pools rows and rebinds current identities. Reuse
  those patterns for a general party roster; keep gameplay feature membership
  separate from PvP eligibility and alive-state rules.
- `NativePlayerChat` already validates the stock `RpcChat` transport and can
  send targeted notices without replacing avatar bubbles. `ModChatCommands`
  supplies command routing. Reuse those paths for host notices; client history
  should capture the display boundary once and avoid duplicate delivery.
- `MultiplayerNameController`, `MultiplayerNameColor` and `NetworkNameState`
  already publish the owned avatar's gradient via native name replication with
  bounded three-attempt acknowledgement/retry and restoration. This is separate
  from local chat rich-text support. Preserve existing names and do not replace
  the name controller with RaidRaid's chat-patch behavior.
- `HotkeyInputOwnership`, panel lifetimes, local settings and localized widgets
  are the right home for optional chat/profile UI settings. Such preferences
  should not overwrite the host's gameplay preset or apply to remote screens.

Self-highlight, boss aggro outlines, zoom/cursor/auto-dash, training tools,
acquisition watchdog, boss statistics, inventory growth and sapphire policy
are separate feature families. They are not inferred to be included by
implementing chat/profile/party sharing. `HighlightRunner.LateUpdate` creates
local self/aggro visuals, uses an aggro publication fresh for 0.5 seconds and
limits outlined player targets to four; this is not native guest replication.
These local features remain possible optional additions, not rejected scope.

## Beta symbol status and implementation gates

No missing selected type was reported for the 13-type beta inspection. Key
existing methods include `Item.Initialize` (`0x06001e95`, int/int/sbyte),
`Item.OnStartServer` (`0x06001e92`), shop `PickRandomItem`/`FillHands`
(`0x06003ba8`/`0x06003ba9`), `UI_HUDLog.SetText` (`0x06004c7c`),
`UI_HUDLogViewer.SpawnLog`/`SetTransparency` (`0x06004c82`/`0x06004c84`),
and `UI_MultiplayerPanel.HandleFound`/`HandleCreated`
(`0x060051dc`/`0x060051dd`). Private export checks also found current
`LocalAddItem`, `LocalAddItemAtPosition`, `AddDimensionPocketItemsOnServer`,
`AddCustomStatUnsafe`, `AddCustomStatAmp`, `OnStopServer` and `NewGame`.
`PlayerSpawner.Initialize` is now bool-returning with
`(int, string, string, int, ERestartPlacement)`; hooks must validate the actual
signature, not assume an old initialization contract. This is not a claim that
RaidRaid as a whole loads on this beta or that every Harmony patch is compatible.

Recommended gradual order is local profile scale and bounded chat history;
independent highest-negotiation sharing; highest-medal policy; comprehensive
item ban after transaction/temporary-inventory coverage; then capacity/paging
and optional lobby conveniences. Each gameplay slice needs native-contract
checks plus host/stock-guest tests. Local UI slices need reconnect/recreated-HUD,
resolution, controller/chat focus, hide/show and unload restoration checks.
Record actual guest observations separately from compile/fixture success.

Before an all-source ban claim, test ordinary chest/boss/random rewards,
merchant stock and reroll, Pocket Dimension unlock and buy, Fountain carryover,
training/debug grants, storage/safe/temporary/sub-bag transfers, full inventory,
existing floor drops, save/load, join/rejoin, toggle during a run and a second
run. Before a party-sharing claim, test competing holders, native and modded
stat changes, maximum-holder departure, amplification/rounding, merchant repeat,
reentrant callbacks and stock recipient visuals. No implementation or live
validation is implied by this research.
