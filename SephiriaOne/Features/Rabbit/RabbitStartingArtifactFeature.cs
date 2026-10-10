using System;
using UnityEngine;

namespace SephiriaOne
{
    // Use the game's costume-owned item lifecycle: no independent inventory grants
    // or per-player bookkeeping. Native code tracks/removes the exact instance IDs.
    internal static class RabbitStartingArtifactFeature
    {
        private const string CostumeId = "HolyRabbit";
        private static readonly StartingArtifact[] Artifacts =
        {
            new StartingArtifact(1314, "Item_SwordShieldGrowth_Enhanced_Name", "Crest of the Iron Wall"),
            new StartingArtifact(1171, "Item_BloodofObrus_Name", "Blood of Obrus")
        };

        // CostumeDatabase loads before ItemDatabase. Call only after all databases
        // are ready, before any player is initialized or the costume UI is opened.
        public static void Apply()
        {
            foreach (StartingArtifact artifact in Artifacts) artifact.Apply();
        }

        public static void Shutdown()
        {
            // Unwind appended templates in reverse order to restore the original
            // array, while retaining each item's independent ownership/validation.
            for (int i = Artifacts.Length - 1; i >= 0; i--) Artifacts[i].Shutdown();
        }

        private sealed class StartingArtifact
        {
            private readonly int artifactId;
            private readonly string nameKey;
            private readonly string displayName;
            private CostumeEntity editedCostume;
            private ItemEntity addedItem;
            private ItemEntity[] originalItems;
            private ItemEntity[] installedItems;

            public StartingArtifact(int artifactId, string nameKey, string displayName)
            {
                this.artifactId = artifactId;
                this.nameKey = nameKey;
                this.displayName = displayName;
            }

            public void Apply()
            {
                try
                {
                    CostumeEntity costume = CostumeDatabase.FindCostumeByID(CostumeId);
                    ItemEntity artifact = ItemDatabase.FindItemById(artifactId);
                    if (!costume || costume.id != CostumeId || !artifact || artifact.id != artifactId ||
                        artifact.aName?.key != nameKey)
                    {
                        Debug.LogWarning("[SephiriaOne] Rabbit starting artifact unavailable: native HolyRabbit or " + displayName + " does not match the expected item identity.");
                        return;
                    }

                    if (!ReferenceEquals(editedCostume, costume)) Shutdown();
                    ItemEntity[] items = costume.startingItems;
                    if (items != null)
                        foreach (ItemEntity item in items)
                            if (item && item.id == artifactId) return;

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
                    Debug.LogWarning("[SephiriaOne] Rabbit starting artifact unavailable (" + displayName + "): " + error);
                }
            }

            public void Shutdown()
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

            private bool IsUnchanged(ItemEntity[] current)
            {
                if (!ReferenceEquals(current, installedItems) || current == null ||
                    !ReferenceEquals(current[current.Length - 1], addedItem)) return false;
                for (int i = 0; i < current.Length - 1; i++)
                    if (!ReferenceEquals(current[i], originalItems[i])) return false;
                return true;
            }
        }
    }
}
