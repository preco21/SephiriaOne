# Bond artifacts in the Wishing Fountain

Investigated 2026-09-24 against installed Sephiria 1.0.33. No toggle was added:
the requested ordinary-menu behavior cannot reach unmodified guests from a
host-only addon.

`StartingFountain.OpenUI` builds the selectable item list on each viewing client.
It includes unlocked items only when `cannotBeReward` is false and `isDual` is
false. `isDual` identifies the game's bond artifacts; drop-weight code also
uses it for bond-specific behavior.

The server's `PlayerSpawner.AddDimensionPocketItemsOnServer` validates item
existence and Fountain rarity capacity, then adds and binds the selected items.
That method has no equivalent `isDual` prohibition. An unmodified guest already
filters these items before sending their selection to the host.

A host patch can change the host's own menu, but syncing policy, unlocked item IDs,
or Fountain points cannot remove the guest's local filter. Globally changing item
definitions on the host also leaves guest definitions unchanged and would alter
unrelated bond drop behavior.

A host-directed item-grant feature could deliver bond artifacts through native
server inventory paths, but would require a different selection flow. Enabling
the ordinary Fountain selection UI for every participant requires client patches.
Neither alternative is substituted for the requested host-only toggle.

Evidence was read from the installed assembly and temporary decompilation:
`StartingFountain.OpenUI`, `PlayerSpawner.AddDimensionPocketItemsOnServer`, and
`GridInventory.GetItemDropWeight`. Game source and binaries remain outside this
repository. The assembly fingerprint is recorded in the resource investigation.
