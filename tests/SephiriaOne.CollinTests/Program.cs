using SephiriaOne;
using Mirror;

int checks = 0;
void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
foreach (string id in new[] { "Mole", "Squirrel", "Turtle", "HolyRabbit", "PinkRabbit" })
    CostumeDatabase.Items.Add(id, new() { id = id });
ItemDatabase.Items.Add(1197, new() { id = 1197, aName = new() { key = "Item_WeaselKnight_Name" } });
PlayerAvatar Player()
{
    var p = new PlayerAvatar(); var s = new PlayerSpawner { PlayerAvatar = p, currentPlayerIdx = PlayerSpawner.MultiplayerList.Count };
    p.spawner = s; p.Inventory = new() { UnitAvatar = p }; PlayerSpawner.MultiplayerList.Add(s); return p;
}
CollinRuntime.ValidateItem();
CollinHooks.Install();
try
{
    var player = Player();
    player.UpdateCostumeData("Mole", true);
    Check(player.Owned.Count == 0, "Default off preserves native costume items");
    SessionSettings.CollinForUse = true;
    foreach (string costume in new[] { "Mole", "Squirrel", "Turtle" })
    {
        player.UpdateCostumeData(costume, true);
        Check(player.Owned.Count == 1 && player.Inventory.Held.Count == 1, costume + " grants one owned Collin");
        int id = player.Owned.Single();
        CollinRuntime.AfterCostume(player, costume);
        Check(player.Owned.Single() == id && player.Inventory.Held.Count == 1, "Repeated boundary does not duplicate");
    }
    var purchased = new ItemMetadata(99999, 1197, 1); player.Inventory.Held.Add(purchased.instanceID, purchased);
    player.UpdateCostumeData("HolyRabbit", true);
    Check(player.Inventory.Held.Keys.SequenceEqual(new[] { 99999 }) && player.Owned.Count == 0, "Switch removes grant and preserves purchased Collin");
    player.UpdateCostumeData("Mole", true);
    SessionSettings.CollinForUse = false;
    Check(player.Inventory.Held.Count == 2, "Changing policy does not rewrite current run inventory");
    player.Inventory.Held.Clear(); DungeonManager.Instance.globalItemStatTable.Clear(); player.Inventory.RestockStartingItem(0);
    Check(player.Inventory.Held.Count == 0 && player.Owned.Count == 0, "Next fresh restock removes disabled gift registration");
    SessionSettings.CollinForUse = true;
    player.Inventory.RestockStartingItem(0);
    Check(player.Inventory.Held.Count == 1 && player.Owned.Count == 1, "Enable before same-costume restart grants exactly once");
    for (int i = 0; i < 10; i++) { player.Inventory.Held.Clear(); DungeonManager.Instance.globalItemStatTable.Clear(); player.Inventory.RestockStartingItem(0); }
    Check(player.Inventory.Held.Count == 1 && player.Owned.Count == 1, "Repeated runs retain a single registration");
    var receiver = Player(); var transfer = player.Inventory.Held.Single();
    player.Inventory.Held.Remove(transfer.Key); receiver.Inventory.Held.Add(transfer.Key, transfer.Value);
    player.UpdateCostumeData("PinkRabbit", true);
    Check(receiver.Inventory.Held.Count == 0, "Switch retires transferred addon grant by exact ID");
    player.Inventory.Full = true; player.UpdateCostumeData("Mole", true);
    Check(player.Inventory.Pending.Count == 1 && player.Owned.Count == 1, "Full inventory retains native pending grant");
    player.UpdateCostumeData("PinkRabbit", true);
    Check(player.Inventory.Pending.Count == 0, "Switch retires pending grant"); player.Inventory.Full = false;
    player.Inventory.ThrowAfterRegister = true; player.UpdateCostumeData("Mole", true);
    Check(player.Owned.Count == 1 && player.Inventory.startingItems.Count == 1, "Interrupted native grant keeps removal ownership");
    CollinRuntime.AfterCostume(player, "Mole");
    Check(player.Owned.Count == 1, "Retry cannot duplicate interrupted registered item");
    player.UpdateCostumeData("PinkRabbit", true);
    Check(player.Inventory.startingItems.Count == 0, "Interrupted grant cleans up on switch");
    HorayNetworkAuthenticator.AccessDeny_InDungeon = true;
    player.UpdateCostumeData("Mole", false);
    Check(player.Owned.Count == 0 && player.Inventory.Held.Count == 0, "Saved-run restore gets no retroactive grant");
    player.Inventory.RestockStartingItem(0);
    Check(player.Owned.Count == 1 && player.Inventory.Held.Count == 1, "Fresh restart registers even if native dungeon flag is still set");
    HorayNetworkAuthenticator.AccessDeny_InDungeon = false;
    player.UpdateCostumeData("PinkRabbit", true);
    CostumeDatabase.Items["Mole"].startingItems = new[] { ItemDatabase.Items[1197] };
    player.UpdateCostumeData("Mole", true);
    Check(player.Inventory.Held.Count == 1 && player.Owned.Count == 1, "Native/other-addon starting Collin is not duplicated");
    SessionSettings.CollinForUse = false;
    player.Inventory.Held.Clear(); player.Inventory.RestockStartingItem(0);
    Check(player.Inventory.Held.Count == 1, "Off preserves unmarked native/other-addon starting gift");
    CostumeDatabase.Items["Mole"].startingItems = Array.Empty<ItemEntity>();
    SessionSettings.CollinForUse = true;
    player.UpdateCostumeData("Turtle", true);
    CollinHooks.Uninstall(); CollinHooks.Install();
    SessionSettings.CollinForUse = false; player.Inventory.Held.Clear(); player.Inventory.RestockStartingItem(0);
    Check(player.Inventory.Held.Count == 0, "Hook reload preserves provenance without retained player objects");
    SessionSettings.CollinForUse = true;
    var rejoined = Player(); rejoined.UpdateCostumeData("Squirrel", false);
    Check(rejoined.Inventory.Held.Count == 1, "New connection receives current costume policy");
    rejoined.UpdateCostumeData("PinkRabbit", false); rejoined.isServer = false;
    CollinRuntime.AfterCostume(rejoined, "Squirrel");
    Check(rejoined.Inventory.Held.Count == 0, "Client avatar cannot grant");
    rejoined.isServer = true; NetworkServer.active = false;
    CollinRuntime.AfterCostume(rejoined, "Squirrel");
    Check(rejoined.Inventory.Held.Count == 0, "Guest cannot grant"); NetworkServer.active = true;
    // A marker can rebuild ownership after a managed reload without adopting an
    // unrelated player's receipt. Model native owned registration on a fresh object.
    var restored = Player(); restored.UpdateCostumeData("Mole", true);
    var receipts = typeof(CollinRuntime).GetField("receipts", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
    receipts.GetType().GetMethod("Remove", new[] { typeof(PlayerAvatar) }).Invoke(receipts, new object[] { restored });
    SessionSettings.CollinForUse = false; restored.Inventory.Held.Clear(); restored.Inventory.RestockStartingItem(0);
    Check(restored.Owned.Count == 0 && restored.Inventory.Held.Count == 0, "Native provenance rehydrates ownership without an existing receipt");
    SessionSettings.CollinForUse = true; restored.UpdateCostumeData("Turtle", true);
    DungeonManager.Instance = new(); SessionSettings.CollinForUse = false;
    restored.Inventory.Held.Clear(); restored.Inventory.RestockStartingItem(0);
    Check(restored.Inventory.Held.Count == 1, "New dungeon cannot reuse an old receipt to remove unmarked native state");
    ItemDatabase.Items[1197].aName.key = "DifferentItem";
    Check(!CollinRuntime.ValidateItem(), "Changed native item identity fails closed");
    Console.WriteLine($"Passed {checks} Collin lifecycle fixture checks (not live networking).");
}
finally { CollinHooks.Uninstall(); }
