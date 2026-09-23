using UnityEngine;

namespace SephiriaOne
{
    public sealed class Entry : HorayModBase
    {
        private LocalPlayerNameColor nameColor;
        private ModChatCommands chatCommands;
        private SessionSettingsController sessionSettings;

        protected override void OnModLoaded()
        {
            HorayModAPI.OnAllDatabasesReady += OnDatabasesReady;
            ChoiceFeature.Initialize();
            if (!nameColor)
            {
                var controller = new GameObject("SephiriaOne.Controllers");
                Object.DontDestroyOnLoad(controller);
                sessionSettings = controller.AddComponent<SessionSettingsController>();
                nameColor = controller.AddComponent<LocalPlayerNameColor>();
                chatCommands = controller.AddComponent<ModChatCommands>();
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
            if (sessionSettings)
            {
                sessionSettings.enabled = false;
                sessionSettings = null;
            }
            if (chatCommands)
            {
                chatCommands.enabled = false;
                chatCommands = null;
            }

            ChoiceFeature.Shutdown();

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
