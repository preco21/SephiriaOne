using SephiriaOne;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
var crest = new ItemEntity { id = 1314, aName = new() { key = "Item_SwordShieldGrowth_Enhanced_Name" } };
var original = new ItemEntity { id = 3031 };
var otherModItem = new ItemEntity { id = 101 };
var nativeItems = new[] { original };
var rabbit = new CostumeEntity { id = "HolyRabbit", startingItems = nativeItems };
var other = new CostumeEntity { id = "PinkRabbit", startingItems = nativeItems };
CostumeDatabase.Items.Add(rabbit.id, rabbit);
CostumeDatabase.Items.Add(other.id, other);
ItemDatabase.Items.Add(crest.id, crest);

RabbitStartingArtifactFeature.Apply();
Check(rabbit.startingItems.SequenceEqual(new[] { original, crest }), "Rabbit retains native items and gains exactly the requested Crest");
Check(ReferenceEquals(other.startingItems, nativeItems), "Other costumes and shared source arrays remain untouched");
var installedItems = rabbit.startingItems;
for (int i = 0; i < 100; i++) RabbitStartingArtifactFeature.Apply();
Check(ReferenceEquals(installedItems, rabbit.startingItems), "Repeated database-ready callbacks neither duplicate nor allocate another list");
RabbitStartingArtifactFeature.Shutdown();
Check(ReferenceEquals(rabbit.startingItems, nativeItems), "Unload restores original array");
RabbitStartingArtifactFeature.Shutdown();
Check(ReferenceEquals(rabbit.startingItems, nativeItems), "Repeated unload is harmless");

rabbit.startingItems = new[] { original, crest };
installedItems = rabbit.startingItems;
RabbitStartingArtifactFeature.Apply();
RabbitStartingArtifactFeature.Shutdown();
Check(ReferenceEquals(rabbit.startingItems, installedItems), "An existing native/other-addon Crest is not duplicated or claimed on unload");

rabbit.startingItems = nativeItems;
RabbitStartingArtifactFeature.Apply();
rabbit.startingItems = new[] { otherModItem, crest, original };
RabbitStartingArtifactFeature.Shutdown();
Check(rabbit.startingItems.SequenceEqual(new[] { otherModItem, original }), "Unload preserves another addon's later edits and ordering");

rabbit.startingItems = nativeItems;
RabbitStartingArtifactFeature.Apply();
rabbit.startingItems[0] = otherModItem;
RabbitStartingArtifactFeature.Shutdown();
Check(rabbit.startingItems.SequenceEqual(new[] { otherModItem }), "Unload preserves in-place edits by another addon");

rabbit.startingItems = null;
RabbitStartingArtifactFeature.Apply();
Check(rabbit.startingItems.SequenceEqual(new[] { crest }), "A missing starting-item array is treated as empty");
RabbitStartingArtifactFeature.Shutdown();
Check(rabbit.startingItems == null, "Unload restores the original null array");

foreach (string failure in new[] { "costume", "item", "wrong-costume", "wrong-id", "wrong-key", "no-name", "destroyed" })
{
    rabbit.startingItems = nativeItems;
    switch (failure)
    {
        case "costume": CostumeDatabase.Items.Remove(rabbit.id); break;
        case "item": ItemDatabase.Items.Remove(crest.id); break;
        case "wrong-costume": rabbit.id = "PinkRabbit"; break;
        case "wrong-id": crest.id = 999; break;
        case "wrong-key": crest.aName.key = "DifferentItem"; break;
        case "no-name": crest.aName = null; break;
        case "destroyed": crest.Destroyed = true; break;
    }
    int warnings = UnityEngine.Debug.Warnings.Count;
    RabbitStartingArtifactFeature.Apply();
    Check(ReferenceEquals(rabbit.startingItems, nativeItems), failure + " must leave the costume unchanged");
    Check(UnityEngine.Debug.Warnings.Count == warnings + 1, failure + " is diagnosable");
    rabbit.id = "HolyRabbit";
    crest.id = 1314;
    crest.aName = new() { key = "Item_SwordShieldGrowth_Enhanced_Name" };
    crest.Destroyed = false;
    CostumeDatabase.Items[rabbit.id] = rabbit;
    ItemDatabase.Items[crest.id] = crest;
    RabbitStartingArtifactFeature.Shutdown();
}

RabbitStartingArtifactFeature.Apply();
var freshRabbit = new CostumeEntity { id = "HolyRabbit", startingItems = new[] { otherModItem } };
CostumeDatabase.Items[freshRabbit.id] = freshRabbit;
RabbitStartingArtifactFeature.Apply();
Check(ReferenceEquals(rabbit.startingItems, nativeItems), "Database replacement releases the previous costume edit");
Check(freshRabbit.startingItems.SequenceEqual(new[] { otherModItem, crest }), "A replacement database receives the current addition");
RabbitStartingArtifactFeature.Shutdown();
Check(freshRabbit.startingItems.SequenceEqual(new[] { otherModItem }), "Replacement database cleanup preserves its native items");

Console.WriteLine($"Passed {checks} Rabbit starting-artifact registration and ownership checks.");
