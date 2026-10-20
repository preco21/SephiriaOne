using UnityEngine;

namespace SephiriaOne
{
    public sealed class Entry : HorayModBase
    {
        private static readonly EntryLifetime lifetime = new EntryLifetime();
        private GameObject controller;
        private MultiplayerNameController nameColor;
        private ModChatCommands chatCommands;
        private SessionSettingsController sessionSettings;
        private SettingsPanelController settingsPanel;
        private UpdateController updates;
        private HotkeyController hotkeys;

        protected override void OnModLoaded()
        {
            if (!lifetime.BeginLoad(this))
            {
                Debug.Log("[SephiriaOne] Reusing active runtime after addon reload.");
                return;
            }
            try
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
                LobbyRegenFeature.Initialize();
                ReviveAllFeature.Initialize();
                DeathmatchFeature.Initialize();
                JarSpawnFeature.Initialize();
                EventSpawnFeature.Initialize();
                BatCostumeFeature.Initialize();
                CollinFeature.Initialize();
                RequitalRuntime.Initialize();
                ExperienceFeature.Initialize();
                ProjectileLimitFeature.Initialize();
                DisconnectDiagnosticsFeature.Initialize();
                if (!nameColor)
                {
                    controller = new GameObject("SephiriaOne.Controllers");
                    Object.DontDestroyOnLoad(controller);
                    updates = controller.AddComponent<UpdateController>();
                    sessionSettings = controller.AddComponent<SessionSettingsController>();
                    settingsPanel = controller.AddComponent<SettingsPanelController>();
                    hotkeys = controller.AddComponent<HotkeyController>();
                    nameColor = controller.AddComponent<MultiplayerNameController>();
                    chatCommands = controller.AddComponent<ModChatCommands>();
                }

                Debug.Log($"[SephiriaOne] Loaded v{metadata.modVersion}");
                lifetime.CompleteLoad();
            }
            catch { lifetime.Failed(); throw; }
        }

        private void OnDatabasesReady()
        {
            RabbitStartingArtifactFeature.Apply();
            CollinFeature.OnDatabasesReady();
            RabbitLevelUpFeature.OnDatabasesReady();
            LargePotionGrant.Load();
            RequitalRuntime.Load();
            Debug.Log("[SephiriaOne] All databases ready");
        }

        protected override void OnModUnloaded()
        {
            Entry owner = lifetime.BeginUnload(this);
            if (owner == null) return;
            try
            {
                if (owner.updates) { owner.updates.enabled = false; owner.updates = null; }
                // Keep controllers/guards available if verified cleanup needs recovery.
                DeathmatchFeature.Shutdown();
                BatCostumeFeature.Shutdown();
                CollinFeature.Shutdown();
                ExperienceFeature.Shutdown();
                ProjectileLimitFeature.Shutdown();
                RequitalRuntime.Shutdown();
                ChoiceFeature.Shutdown();
                EventSpawnFeature.Shutdown();
                JarSpawnFeature.Shutdown();
                FriendlyFireFeature.Shutdown();
                LobbyRegenFeature.Shutdown();
                ReviveAllFeature.Shutdown();
                ItemRestrictionFeature.Shutdown();
                MerchantFeature.Shutdown();
                ResourceFeature.Shutdown();
                RabbitDescriptionFeature.Shutdown();
                RabbitStartingArtifactFeature.Shutdown();
                RabbitLevelUpFeature.Shutdown();
                LargePotionGrant.Clear();
                RabbitPotionFeature.Shutdown();
                DisconnectDiagnosticsFeature.Shutdown();
                if (owner.hotkeys) { owner.hotkeys.enabled = false; owner.hotkeys = null; }
                if (owner.settingsPanel)
                {
                    owner.settingsPanel.enabled = false;
                    owner.settingsPanel = null;
                }
                HorayModAPI.OnAllDatabasesReady -= owner.OnDatabasesReady;
                if (owner.sessionSettings)
                {
                    owner.sessionSettings.enabled = false;
                    owner.sessionSettings = null;
                }
                if (owner.chatCommands)
                {
                    owner.chatCommands.enabled = false;
                    owner.chatCommands = null;
                }

                SessionBoundaryFeature.Shutdown();

                if (owner.nameColor)
                {
                    // Request native name restoration before Unity destroys the object.
                    owner.nameColor.enabled = false;
                    owner.nameColor = null;
                }
                // Initialization can fail before the name controller is assigned.
                if (owner.controller)
                {
                    Object.Destroy(owner.controller);
                    owner.controller = null;
                }

                Debug.Log("[SephiriaOne] Unloaded");
                L.Shutdown();
                lifetime.CompleteUnload();
            }
            catch { lifetime.Failed(); throw; }
        }
    }
}
