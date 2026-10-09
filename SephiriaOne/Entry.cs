using UnityEngine;

namespace SephiriaOne
{
    public sealed class Entry : HorayModBase
    {
        private MultiplayerNameController nameColor;
        private ModChatCommands chatCommands;
        private SessionSettingsController sessionSettings;
        private SettingsPanelController settingsPanel;
        private UpdateController updates;
        private HotkeyController hotkeys;

        protected override void OnModLoaded()
        {
            L.Initialize(System.IO.Path.Combine(Application.persistentDataPath, "SephiriaOne", "localization"),
                warning => Debug.LogWarning("[SephiriaOne] " + warning));
            HorayModAPI.OnAllDatabasesReady += OnDatabasesReady;
            ChoiceFeature.Initialize();
            SessionBoundaryFeature.Initialize();
            ResourceFeature.Initialize();
            RabbitPotionFeature.Initialize();
            RabbitLevelUpFeature.Initialize();
            RabbitDescriptionFeature.Initialize();
            MerchantFeature.Initialize();
            ItemRestrictionFeature.Initialize();
            FriendlyFireFeature.Initialize();
            ReviveAllFeature.Initialize();
            DeathmatchFeature.Initialize();
            JarSpawnFeature.Initialize();
            EventSpawnFeature.Initialize();
            BatCostumeFeature.Initialize();
            CollinFeature.Initialize();
            DisconnectDiagnosticsFeature.Initialize();
            if (!nameColor)
            {
                var controller = new GameObject("SephiriaOne.Controllers");
                Object.DontDestroyOnLoad(controller);
                updates = controller.AddComponent<UpdateController>();
                sessionSettings = controller.AddComponent<SessionSettingsController>();
                settingsPanel = controller.AddComponent<SettingsPanelController>();
                hotkeys = controller.AddComponent<HotkeyController>();
                nameColor = controller.AddComponent<MultiplayerNameController>();
                chatCommands = controller.AddComponent<ModChatCommands>();
            }

            Debug.Log($"[SephiriaOne] Loaded v{metadata.modVersion}");
        }

        private void OnDatabasesReady()
        {
            RabbitStartingArtifactFeature.Apply();
            CollinFeature.OnDatabasesReady();
            RabbitLevelUpFeature.OnDatabasesReady();
            Debug.Log("[SephiriaOne] All databases ready");
        }

        protected override void OnModUnloaded()
        {
            if (updates) { updates.enabled = false; updates = null; }
            // Keep controllers/guards available if verified cleanup needs recovery.
            DeathmatchFeature.Shutdown();
            BatCostumeFeature.Shutdown();
            CollinFeature.Shutdown();
            ChoiceFeature.Shutdown();
            EventSpawnFeature.Shutdown();
            JarSpawnFeature.Shutdown();
            FriendlyFireFeature.Shutdown();
            ReviveAllFeature.Shutdown();
            ItemRestrictionFeature.Shutdown();
            MerchantFeature.Shutdown();
            ResourceFeature.Shutdown();
            RabbitDescriptionFeature.Shutdown();
            RabbitStartingArtifactFeature.Shutdown();
            RabbitLevelUpFeature.Shutdown();
            RabbitPotionFeature.Shutdown();
            DisconnectDiagnosticsFeature.Shutdown();
            if (hotkeys) { hotkeys.enabled = false; hotkeys = null; }
            if (settingsPanel)
            {
                settingsPanel.enabled = false;
                settingsPanel = null;
            }
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

            SessionBoundaryFeature.Shutdown();

            if (nameColor)
            {
                // Request native name restoration before Unity destroys the object.
                nameColor.enabled = false;
                Object.Destroy(nameColor.gameObject);
                nameColor = null;
            }

            Debug.Log("[SephiriaOne] Unloaded");
            L.Shutdown();
        }
    }
}
