using UnityEngine;

namespace SephiriaOne
{
    public sealed class Entry : HorayModBase
    {
        private LocalPlayerNameColor nameColor;
        private FountainChatCommands fountainCommands;

        protected override void OnModLoaded()
        {
            HorayModAPI.OnAllDatabasesReady += OnDatabasesReady;
            if (!nameColor)
            {
                var controller = new GameObject("SephiriaOne.Controllers");
                Object.DontDestroyOnLoad(controller);
                nameColor = controller.AddComponent<LocalPlayerNameColor>();
                fountainCommands = controller.AddComponent<FountainChatCommands>();
            }

            Debug.Log($"[SephiriaOne] Loaded v{metadata.modVersion}");
        }

        private void OnDatabasesReady()
        {
            Debug.Log("[SephiriaOne] All databases ready");
        }

        protected override void OnModUnloaded()
        {
            HorayModAPI.OnAllDatabasesReady -= OnDatabasesReady;
            if (fountainCommands)
            {
                fountainCommands.enabled = false;
                fountainCommands = null;
            }

            if (nameColor)
            {
                // Restore labels immediately; Unity destroys the object later.
                nameColor.enabled = false;
                Object.Destroy(nameColor.gameObject);
                nameColor = null;
            }

            Debug.Log("[SephiriaOne] Unloaded");
        }
    }
}
