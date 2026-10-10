using SephiriaOne;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
var crest = new ItemEntity { id = 1314, aName = new() { key = "Item_SwordShieldGrowth_Enhanced_Name" } };
var blood = new ItemEntity { id = 1171, aName = new() { key = "Item_BloodofObrus_Name" } };
var original = new ItemEntity { id = 3031 };
var otherModItem = new ItemEntity { id = 101 };
var nativeItems = new[] { original };
var rabbit = new CostumeEntity { id = "HolyRabbit", startingItems = nativeItems };
var other = new CostumeEntity { id = "PinkRabbit", startingItems = nativeItems };
CostumeDatabase.Items.Add(rabbit.id, rabbit);
CostumeDatabase.Items.Add(other.id, other);
ItemDatabase.Items.Add(crest.id, crest);
ItemDatabase.Items.Add(blood.id, blood);

RabbitStartingArtifactFeature.Apply();
Check(rabbit.startingItems.SequenceEqual(new[] { original, crest, blood }), "Rabbit retains native items and gains exactly one Crest and Blood of Obrus");
Check(ReferenceEquals(other.startingItems, nativeItems), "Other costumes and shared source arrays remain untouched");
var installedItems = rabbit.startingItems;
for (int i = 0; i < 100; i++) RabbitStartingArtifactFeature.Apply();
Check(ReferenceEquals(installedItems, rabbit.startingItems), "Repeated database-ready callbacks neither duplicate nor allocate another list");
RabbitStartingArtifactFeature.Shutdown();
Check(ReferenceEquals(rabbit.startingItems, nativeItems), "Unload restores original array");
RabbitStartingArtifactFeature.Shutdown();
Check(ReferenceEquals(rabbit.startingItems, nativeItems), "Repeated unload is harmless");

foreach (var existing in new[] { new[] { original, crest }, new[] { original, blood }, new[] { original, crest, blood } })
{
    rabbit.startingItems = existing;
    RabbitStartingArtifactFeature.Apply();
    Check(rabbit.startingItems.Length == 3 && rabbit.startingItems.Count(item => item == crest) == 1 &&
        rabbit.startingItems.Count(item => item == blood) == 1, "Existing costume artifacts are not duplicated");
    RabbitStartingArtifactFeature.Shutdown();
    Check(ReferenceEquals(rabbit.startingItems, existing), "Pre-existing artifacts are not claimed on unload");
}

rabbit.startingItems = nativeItems;
RabbitStartingArtifactFeature.Apply();
rabbit.startingItems = new[] { blood, otherModItem, crest, original };
RabbitStartingArtifactFeature.Shutdown();
Check(rabbit.startingItems.SequenceEqual(new[] { otherModItem, original }), "Unload preserves another addon's later edits and ordering");

rabbit.startingItems = nativeItems;
RabbitStartingArtifactFeature.Apply();
rabbit.startingItems[0] = otherModItem;
RabbitStartingArtifactFeature.Shutdown();
Check(rabbit.startingItems.SequenceEqual(new[] { otherModItem }), "Unload preserves in-place edits by another addon");

rabbit.startingItems = null;
RabbitStartingArtifactFeature.Apply();
Check(rabbit.startingItems.SequenceEqual(new[] { crest, blood }), "A missing starting-item array is treated as empty");
RabbitStartingArtifactFeature.Shutdown();
Check(rabbit.startingItems == null, "Unload restores the original null array");

foreach (string failure in new[] { "costume", "wrong-costume", "destroyed-costume" })
{
    rabbit.startingItems = nativeItems;
    switch (failure)
    {
        case "costume": CostumeDatabase.Items.Remove(rabbit.id); break;
        case "wrong-costume": rabbit.id = "PinkRabbit"; break;
        case "destroyed-costume": rabbit.Destroyed = true; break;
    }
    int warnings = UnityEngine.Debug.Warnings.Count;
    RabbitStartingArtifactFeature.Apply();
    Check(ReferenceEquals(rabbit.startingItems, nativeItems), failure + " must leave the costume unchanged");
    Check(UnityEngine.Debug.Warnings.Count > warnings, failure + " is diagnosable");
    rabbit.id = "HolyRabbit";
    rabbit.Destroyed = false;
    CostumeDatabase.Items[rabbit.id] = rabbit;
    RabbitStartingArtifactFeature.Shutdown();
}

foreach (ItemEntity invalid in new[] { crest, blood })
foreach (string failure in new[] { "item", "wrong-id", "wrong-key", "no-name", "destroyed" })
{
    int id = invalid.id;
    var name = invalid.aName;
    ItemEntity valid = invalid == crest ? blood : crest;
    rabbit.startingItems = nativeItems;
    switch (failure)
    {
        case "item": ItemDatabase.Items.Remove(id); break;
        case "wrong-id": invalid.id = 999; break;
        case "wrong-key": invalid.aName = new() { key = "DifferentItem" }; break;
        case "no-name": invalid.aName = null; break;
        case "destroyed": invalid.Destroyed = true; break;
    }
    int warnings = UnityEngine.Debug.Warnings.Count;
    RabbitStartingArtifactFeature.Apply();
    Check(rabbit.startingItems.SequenceEqual(new[] { original, valid }), id + ": " + failure + " skips only the invalid artifact");
    Check(UnityEngine.Debug.Warnings.Count == warnings + 1, id + ": " + failure + " is diagnosable");
    invalid.id = id;
    invalid.aName = name;
    invalid.Destroyed = false;
    ItemDatabase.Items[id] = invalid;
    RabbitStartingArtifactFeature.Apply();
    Check(rabbit.startingItems.Length == 3 && rabbit.startingItems.Contains(crest) && rabbit.startingItems.Contains(blood),
        id + ": " + failure + " recovers independently without duplicating the valid artifact");
    RabbitStartingArtifactFeature.Shutdown();
    Check(rabbit.startingItems.SequenceEqual(nativeItems), "Recovery releases both owned additions");
}

RabbitStartingArtifactFeature.Apply();
var freshRabbit = new CostumeEntity { id = "HolyRabbit", startingItems = new[] { otherModItem } };
CostumeDatabase.Items[freshRabbit.id] = freshRabbit;
RabbitStartingArtifactFeature.Apply();
Check(rabbit.startingItems.SequenceEqual(nativeItems), "Database replacement releases the previous costume edits");
Check(freshRabbit.startingItems.SequenceEqual(new[] { otherModItem, crest, blood }), "A replacement database receives both additions");
RabbitStartingArtifactFeature.Shutdown();
Check(freshRabbit.startingItems.SequenceEqual(new[] { otherModItem }), "Replacement database cleanup preserves its native items");

CostumeDatabase.Items[rabbit.id] = rabbit;
foreach (ItemEntity removed in new[] { crest, blood })
{
    rabbit.startingItems = nativeItems;
    RabbitStartingArtifactFeature.Apply();
    rabbit.startingItems = rabbit.startingItems.Where(item => item != removed).ToArray();
    RabbitStartingArtifactFeature.Apply();
    Check(rabbit.startingItems.Length == 3 && rabbit.startingItems.Contains(crest) && rabbit.startingItems.Contains(blood),
        "Replacing a template and removing one addition can be repaired without duplicate grants");
    RabbitStartingArtifactFeature.Shutdown();
    Check(rabbit.startingItems.SequenceEqual(nativeItems), "Superseded template cleanup releases only addon entries");
}

rabbit.startingItems = nativeItems;
RabbitStartingArtifactFeature.Apply();
rabbit.startingItems = new[] { crest, original, blood, crest, blood, otherModItem };
RabbitStartingArtifactFeature.Shutdown();
Check(rabbit.startingItems.SequenceEqual(new[] { crest, original, blood, otherModItem }),
    "Unload removes one occurrence per owned addition, preserving later duplicate additions");

Console.WriteLine($"Passed {checks} Rabbit starting-artifact registration and ownership checks.");
