# Heaven/Requital Slate Selection Implementation Plan

**Goal:** Extend the existing host-owned Slate grant system with a selector for
Requital (default) or Heaven. Reuse its automatic loadout, count, immediate Give,
exact-ID retraction and native multiplayer transport.

**Design:** One shared selection applies to automatic and manual grants. Keep
auto off/count 1 defaults. A type change retracts old automatic instances;
replacement copies arrive immediately in the lobby or at the next fresh-run
loadout in a dungeon. Manual gifts remain ordinary items. `/one slates type
heaven|requital|reset` and an Items dropdown use the same settings action.
Keep existing internal Requital names and saved owner/tombstone keys for upgrade
compatibility. Track entity type along with instance ID; never reinterpret an
existing Requital ID as Heaven. Add a saved type marker for Heaven, with absent
type meaning legacy Requital. Store nondefault intent in preset v40.

**Evidence:** Installed managed SHA256 matches the existing native contracts.
Fresh UnityPy asset read: resources.assets path 29062 = ID 2099,
Item_StoneTablet_Heaven_Name, Eternal StoneTablet, active type Hidden,
resource pointer file 3/path 68209. Requital remains ID 2100/path 29131,
WitchHat. Validate each selection separately; failure of one must not disable
the other's grant path.

1. Add failing settings/command/preset and native-inventory regressions for
   selection, type switching, overflow, saved/rejoined IDs and manual gifts.
2. Extend immutable settings and v40 parsing; old presets default to Requital.
   Preserve selection across count/auto edits; full reset restores all defaults.
3. Extend the inventory adapter to carry exact ItemMetadata and type provenance
   through registration, delivery, restock, saved recovery and retirement.
   Revalidate captured settings/catalog around callback-bearing writes.
4. Add the dropdown, selected-name status/help and English/Korean translations.
   Reuse the existing command dispatcher, compatibility gates and no-notify UI.
5. Run isolated inventory, shared runtime, policy/native, localization and UI
   checks plus a nondeploying Release build. Review lifecycle/switch faults,
   record findings in docs, conventional commit and push. Do not deploy.
