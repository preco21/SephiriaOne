# 1.1.0 beta: Slates, Rabbit artifacts, XP and merchants

Audited 2026-11-01 against the installed `workshop-beta`, Steam build `26324456`.
This covers the item/XP/merchant portion of the beta compatibility work.

## Evidence

`Assembly-CSharp.dll` SHA-256:
`383a0d5ea9218b797d6eacd1f67964a73606ba50572b7af2d10ac5357b621d01`.
Module MVID: `ba32c2d4-ef71-4869-856b-49ec7aea12f4`.
REA managed inspection returned complete metadata with retained evidence
`ev_1b33e0797b6da8345bf6c55081ef2d0a945cb80394aecb56a9d470030e480efe`.
Metadata establishes identity, not gameplay behavior. The behavior below was
checked against the matching ILSpy export and installed-assembly IL contracts.
Private binaries, exports and audit logs remain outside the repository.

Current assets were also read with the existing UnityPy 1.25.4 tooling and
CPython 3.12. Generated typetrees misdecoded generic lists, so their output was
discarded. Selected records were instead decoded in native serialized-field order
from raw bytes (aligned strings/booleans, arrays and PPtrs), using the matching
managed field declarations. Prefab names/components were independently resolved.

| Item | Asset location | Confirmed identity and prefab |
| --- | --- | --- |
| Heaven 2099 | `resources.assets`, path 29233 | `Item_StoneTablet_Heaven_Name`; StoneTablet/Eternal/Hidden; `StoneTablet-Heaven`, shared path 69324 |
| Requital 2100 | `resources.assets`, path 29302 | `Item_StoneTablet_Requital_Name`; StoneTablet/Eternal/WitchHat; `StoneTablet-Requital`, shared path 69084 |
| Blood of Obrus 1171 | `resources.assets`, path 29346 | `Item_BloodofObrus_Name`; Charm/Uncommon/Locked; `DCharm_1171_BloodofObrus`, shared path 69017 |
| Crest 1314 | `sharedassets0.assets`, path 185194 | `Item_SwordShieldGrowth_Enhanced_Name`; Charm/Legend/Default; `Charm_SwordShieldGrowth_Enhanced`, shared path 74340 |

Both Slate prefabs contain native `NetworkIdentity` and `StoneTablet` components;
the Crest retains `Charm_GrowthGuard`, and Blood of Obrus `Charm_StatusInstance`.
`HolyRabbit` is `resources.assets` path 28561, with the expected costume ID/name
key and one native starting-item pointer (path 29236). `ItemDatabase.Initialize`
excludes only Disabled records at catalog admission; the audited Hidden and
Locked flags do not prevent these exact item lookups.

Asset file SHA-256 identities:

- `resources.assets`:
  `d54dc846c1f4b748349301cbd72151926472bad46b28cc9a4e635b14b28a9990`.
- `sharedassets0.assets`:
  `7686a9df8106ccee855a67ae108b15b2e98690e85734de80abdcc4206b47cbaf`.

Selected raw-record SHA-256 identities, in the table's order:

- Heaven: `409a7b815e47d1579940ba7d21427376e0e7f7653f21aa26d4f91b0968457f74`.
- Requital: `8ccbbec6504d09a84d7178757541b16b537d38a499e0609a67abce0505b7a6e9`.
- Blood of Obrus: `b4b3db10670cd99f26414951d275e331c187d16f3baa4fa003ed2656fb097d6d`.
- Crest: `3ca696a3f0562121542e36ed120083245fbc0be8171f7024360e7dce497a06c8`.
- HolyRabbit: `126b30736aa6d3b7e6c05db1d0d7a398655d09aa0699ef1eef6b91a022cb1935`.

The current [official modding rules](https://github.com/team-horay/sephiria_modding/blob/main/README.en.md)
and [DLL guide](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/dll-mods.md)
were consulted. The guide documents database-ready callbacks after all database
initialization, native Harmony, server authority and the separate Workshop
packaging/multiplayer declarations. This audit does not establish Workshop review
or host-only admission for the complete addon.

## Starting Slates

The beta's `PlayerSpawner.RestartNewGame(int)` delegates to
`RestartNewGame(int, ERestartPlacement)`. The latter still initializes the player
and calls `GridInventory.RestockStartingItem(int)` once. The restart coroutine
calls this placement overload directly. The old Slate validator required the
single-argument entry to contain the direct restock call, so it rejected the beta
and disabled the feature despite the delivery boundary remaining intact.

`RequitalHooks.ValidateNative` now resolves the exact placement overload when the
native enum exists, verifies the old wrapper delegates to it once, and validates
the same direct restock/starting-item/`AddItem` anchors. Without the enum it keeps
the previous single-argument contract. The actual patch remains on
`GridInventory.RestockStartingItem`, covering either entry and preserving native
iteration, binding and ownership restrictions. No grant/retraction policy changed.

The beta adds `IsAbsorbedByUniquePair` handling to native `AddStartingItem` and
pending-item processing. `LocalAddItem` confines unique-pair conversion to
`EItemType.Charm`; both automatic Slate identities are validated as
`EItemType.StoneTablet`, so that new artifact path does not require a Slate
delivery adapter. Exact-ID removal still handles carried, engraved, fixed,
temporary, pending, sub-bag and dropped copies. `ForceRemoveAll` still preserves
the pending-starting-item queue, retaining the reason for the existing duplicate
delivery protection during restart waits.

## Rabbit starting artifacts

`GameDataLoader.Awake` now initializes `ItemDatabase` before `CostumeDatabase`.
Both still finish before `HorayModAPI.NotifyAllModDatabasesReady`. Registration
already uses the ready callback, so implementation behavior remains correct.
The native compatibility test now checks that both databases precede the callback
without imposing the previous relative order.

`PlayerAvatar.UpdateCostumeData` still removes the old tracked IDs, grants through
native `AddStartingItem`, and records each returned ID. Starting metadata remains
a native SyncList. The contract selects the placement-aware restart body and
checks that the coroutine clears old inventory before calling that exact body.
Native restock still occurs after player initialization. Host preview and saved
run costume-edit restrictions remain covered by the installed contract.

## XP source audit

The beta's fountain no longer grants XP in `MagicFountain.Update`. Its exact
`ServerRequest(PlayerAvatar, Request, int)` method checks server activity, range
and progress, calls `MagicFountainProgress.TryCollect`, then grants earned XP
through `LevelController.AddExp`. Host `OnChoice` and generated stock-client
command dispatch both reach this server method. The updated contract checks
these paths, retaining the previous `Update` contract for older games.

Other audited sources remain `Exp.Update`,
`PlayerSpawner.RestorePreservedRejoinables` and
`UI_DevCommandlinePanel.OnSubmitCommand`. All converge on `LevelController.AddExp`.
Host grants and `UserCode_CmdAddExp__Int32` still reach `LocalAddExp(int)`, the
existing XP restriction hook. Its XP write, ability-XP forwarding and level-up
path remain intact; saved `LocalInitialize(level, exp)` remains separate. The
runtime XP hook needed no change.

## Merchant restart lifecycle

`HorayNetworkManager.RestartGameCoroutine` now takes
`(bool, ERestartPlacement)`. The native ordering remains `CreateNewTMP`,
`ClearNetworkObjects`, then `NewGame`; `CreateNewTMP` still replaces the saved-run
object. This preserves the temporary fresh-save/old-map window guarded by
`MerchantRuntime`. The compatibility test now selects the exact new signature,
with the older `(bool)` signature retained for earlier games. Merchant runtime
behavior did not change.

## Verification and limits

The beta-shaped Slate fixture first failed at the new wrapper/restock assertion;
after the validator change, all 68 Slate runtime assertions passed. Independent
fixtures also passed: 69 Rabbit artifact checks and 642 merchant checks across
88 scenarios. All commands used `-c Release -p:DeployMod=false`.

The coordinated non-deploying production build succeeded with no warnings, and
the installed-assembly compatibility report passed all 73 groups with no failures,
including all four affected groups. These checks do not execute Unity gameplay
or establish live host/guest behavior.
Manual verification still needs automatic/manual Requital and Heaven grants,
full-inventory restart delivery, Rabbit costume switching/rejoin, fountain XP
blocking and exemption, and merchant run restart without stale schedule state.
