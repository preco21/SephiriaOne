using System;
using UnityEngine;

namespace SephiriaOne
{
    // Use the game's costume-owned item lifecycle: no independent inventory grants
    // or per-player bookkeeping. Native code tracks/removes the exact instance IDs.
    internal static class RabbitStartingArtifactFeature
    {
        private const string CostumeId = "HolyRabbit";
        private const int ArtifactId = 1314;
        private const string ArtifactNameKey = "Item_SwordShieldGrowth_Enhanced_Name";
        private static CostumeEntity editedCostume;
        private static ItemEntity addedItem;
        private static ItemEntity[] originalItems;
        private static ItemEntity[] installedItems;

        // CostumeDatabase loads before ItemDatabase. Call only after all databases
        // are ready, before any player is initialized or the costume UI is opened.
        public static void Apply()
        {
            try
            {
                CostumeEntity costume = CostumeDatabase.FindCostumeByID(CostumeId);
                ItemEntity artifact = ItemDatabase.FindItemById(ArtifactId);
                if (!costume || costume.id != CostumeId || !artifact || artifact.id != ArtifactId ||
                    artifact.aName?.key != ArtifactNameKey)
                {
                    Debug.LogWarning("[SephiriaOne] Rabbit starting artifact unavailable: native HolyRabbit or Crest of the Iron Wall does not match the expected item identity.");
                    return;
                }

                if (!ReferenceEquals(editedCostume, costume)) Shutdown();
                ItemEntity[] items = costume.startingItems;
                if (items != null)
                    foreach (ItemEntity item in items)
                        if (item && item.id == ArtifactId) return;

                // Also release a superseded template if another addon replaced it.
                Shutdown();
                originalItems = costume.startingItems;
                installedItems = new ItemEntity[(originalItems?.Length ?? 0) + 1];
                if (originalItems != null) Array.Copy(originalItems, installedItems, originalItems.Length);
                installedItems[installedItems.Length - 1] = artifact;
                addedItem = artifact;
                editedCostume = costume;
                costume.startingItems = installedItems;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[SephiriaOne] Rabbit starting artifact unavailable: " + error);
            }
        }

        public static void Shutdown()
        {
            if (editedCostume)
            {
                ItemEntity[] current = editedCostume.startingItems;
                if (IsUnchanged(current)) editedCostume.startingItems = originalItems;
                else if (current != null)
                {
                    // Preserve later edits by other addons; remove only one occurrence
                    // of the native entity we appended, never all matching item IDs.
                    for (int i = current.Length - 1; i >= 0; i--)
                    {
                        if (!ReferenceEquals(current[i], addedItem)) continue;
                        var restored = new ItemEntity[current.Length - 1];
                        Array.Copy(current, 0, restored, 0, i);
                        Array.Copy(current, i + 1, restored, i, current.Length - i - 1);
                        editedCostume.startingItems = restored;
                        break;
                    }
                }
            }
            editedCostume = null;
            addedItem = null;
            originalItems = null;
            installedItems = null;
        }

        private static bool IsUnchanged(ItemEntity[] current)
        {
            if (!ReferenceEquals(current, installedItems) || current == null ||
                !ReferenceEquals(current[current.Length - 1], addedItem)) return false;
            for (int i = 0; i < current.Length - 1; i++)
                if (!ReferenceEquals(current[i], originalItems[i])) return false;
            return true;
        }
    }
}
