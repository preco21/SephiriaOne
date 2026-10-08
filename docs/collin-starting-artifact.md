# Costume-bound Collin

Added in `0.34.0`, default **off**. The option grants native **Hiring Crest
“Collin”** to **Mole**, **Farmer Squirrel** and **Turtle** as a starting artifact.
Only the host needs the addon; inventory and companion effects use native
replication and existing assets on stock guests.

Open `/one ui` → **Costumes** (의상), or use:

| Command | Effect |
| --- | --- |
| `/one collin on` | Enable the costume starting grant. |
| `/one collin off` | Disable future grants/restocks. |
| `/one collin reset` | Same as off. |
| `/one collin status` | Show current intent and compatibility. |
| `/one save` | Save current intent for future hosted sessions. |

Changes apply at the **next costume equip or fresh-run starting-item restock**.
If already wearing an eligible costume in the lobby, switch away and back to
receive the newly enabled grant now. Entering the dungeon alone does not equip
the costume again. Changing settings during a run does not inject an artifact
or immediately remove one from that run's inventory.

Switching costumes removes the exact granted instance before granting the new
costume's items. This includes switching between two eligible costumes: only one
grant remains. Separately acquired Collin copies are preserved. When given-item
unlock is enabled, a transferred grant is also removed from connected players
when the source costume changes. Normal starting-item restrictions apply
otherwise. There is no guarantee of a separate follower for each crest owner:
the native Collin effect can recruit the same existing NPC and change its leader.

Saved runs restore their existing inventory; no retroactive Collin is injected.
The game blocks ordinary in-dungeon costume changes. Like vanilla starting
artifacts, forced costume changes from another addon/debug tool during a
restored run are outside this ownership guarantee: restored inventory IDs can
differ from the game's reconstructed costume registry. Unmarked Collin copies
are never guessed to be addon-owned by item type alone.

## Native implementation findings

- Costume IDs: `Mole`, `Squirrel`, `Turtle` (native resource assets `10_Mole`,
  `12_Squirrel`, `13_Turtle`). Item ID **1197**, name key
  `Item_WeaselKnight_Name`, item asset `1197_WeaselKnight`.
- Native prefab `DCharm_1197_WeaselKnight` uses **Charm_LeadNPC** and a native
  NetworkIdentity. Its effect adopts an existing NPC with the same social ID
  or spawns the stock prefab; unequipping clears its leader through native code.
- Harmony prefix/postfix on `PlayerAvatar.UpdateCostumeData` extends the native
  ownership lifecycle without replaying all costume stats/items. Lobby grants
  use `GridInventory.AddStartingItem`, including its pending/full-inventory path.
  A `finally` records IDs registered before an interrupted placement callback.
- `GridInventory.RestockStartingItem` prefix reads current host intent. If a
  grant is missing, it registers only metadata; the original restock performs
  placement, owner binding and restrictions once. Using AddStartingItem here
  would place once in the lobby and then duplicate the restock.
- IDs are enrolled in native `costumeStartingItemInstanceIDs`. Provenance uses
  native global item metadata plus weak avatar/dungeon-scoped receipts. Native
  restart clears metadata while retaining starting-item registrations, so the
  receipt bridges that clear; enabled restocks republish provenance, while
  disabled restocks remove the exact registration. Network IDs are never used
  to reuse a departed player's receipt.
- No new frame loop, per-frame inventory scans, custom RPC, client state or
  costume asset edits. The native costume selection preview stays unchanged.
  Unloading stops extra grant hooks; existing items remain under native costume
  ownership rather than being deleted during addon shutdown.

Preset v15 stores `collin starting 1`; missing/older rows default off. Invalid
and duplicate rows reject the whole preset. Off/reset does not rewrite a saved
preset until `/one save`, matching other commands.

## Verification

Automated checks cover eligible/ineligible costumes, default/off, repeated
boundaries, exact-ID removal, independent copies, pending inventory, interrupted
grant registration, saved-run exclusion, enabled/disabled restock after native
metadata clearing, transferred grants, hook reload and fresh connections.
Installed-game IL checks confirm native grant/restock, restrictions, saves,
restart clearing, companion leader behavior and addon lifecycle wiring. Shared
runtime, policy/preset and EN/KO catalog suites also exercise this option.

Audited installed `Assembly-CSharp.dll` SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.

Debug/Release builds and tests use `-p:DeployMod=false`. These checks are not live
multiplayer or rendered UI verification. Before use with a group, manually test:

1. Enable in the lobby, switch each eligible costume away/back, and inspect the
   crest on host and unmodified guest. Confirm other costumes receive nothing.
2. Obtain a second ordinary Collin; switch away and confirm only the grant leaves.
3. Play second/third runs with the same costume, including off/on before restart.
4. Save settings, reopen the host session and reconnect a guest. Resume a saved
   run and verify its existing inventory is preserved without another grant.
5. With given-item unlock on, transfer the grant, then switch the source costume.
6. Have multiple eligible players equip their crests and check the native NPC
   leader behavior, plus full inventories and both EN/KO panel layouts.

No game deployment or live multiplayer test was performed for this change.
