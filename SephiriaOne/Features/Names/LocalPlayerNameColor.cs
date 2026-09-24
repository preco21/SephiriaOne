using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

namespace SephiriaOne
{
    public sealed class LocalPlayerNameColor : MonoBehaviour
    {
        private readonly PresentationRegistry registry = new PresentationRegistry();
        private readonly MultiplayerNameColor multiplayerName = new MultiplayerNameColor();
        private readonly NameStyleDirectory styles = new NameStyleDirectory();
        private readonly HashSet<TMP_Text> overhead = new HashSet<TMP_Text>();
        private bool installed;
        public static string Diagnostics { get; private set; } = "presentation not initialized";

        private void OnEnable()
        {
            try
            {
                HarmonyRuntime.EnsureLoaded();
                InstallHooks();
                installed = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SephiriaOne] Presentation discovery degraded: " + exception.Message);
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InstallHooks() => NamePresentationHooks.Install(registry, styles);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RemoveHooks() => NamePresentationHooks.Uninstall();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private string HookStatus() => NamePresentationHooks.Status;

        private void LateUpdate()
        {
            UIManager ui = UIManager.Instance;
            GameObject playerObject = ui ? ui.connectedPlayer : null;
            PlayerSpawner player = playerObject ? playerObject.GetComponent<PlayerSpawner>() : null;
            if (player && player.isOwned) multiplayerName.Update(player);
            else multiplayerName.Restore();

            styles.Clear();
            var seen = new HashSet<PlayerSpawner>();
            var current = new HashSet<TMP_Text>();
            foreach (PlayerSpawner subject in PlayerSpawner.MultiplayerList)
            {
                if (!subject || !seen.Add(subject)) continue;
                ObserveStyle(subject);
                if (!subject.WorldUserName) continue;
                TMP_Text label = subject.WorldUserName;
                current.Add(label);
                if (overhead.Add(label)) registry.Register(label, new NameLabelBinding(label,
                    () => Character(subject ? subject.PlayerAvatar : null)));
            }
            // Solo avatar is not guaranteed to have entered MultiplayerList yet.
            if (player && seen.Add(player)) ObserveStyle(player);
            if (player && player.WorldUserName)
            {
                var label = player.WorldUserName;
                current.Add(label);
                if (overhead.Add(label)) registry.Register(label, new NameLabelBinding(label,
                    () => Character(player ? player.PlayerAvatar : null)));
            }
            foreach (var label in new List<TMP_Text>(overhead))
                if (!current.Contains(label)) { registry.Remove(label); overhead.Remove(label); }
            registry.Tick();
            Diagnostics = multiplayerName.Status + "; " + (installed ? HookStatus() : "native UI hooks unavailable");
        }
        private void ObserveStyle(PlayerSpawner subject)
        {
            PlayerAvatar avatar = subject.PlayerAvatar;
            styles.Observe(subject.steamID, avatar ? avatar.playerNameSource : null, subject.isOwned || (avatar && avatar.isOwned));
        }
        private static NameView Character(UnitAvatar avatar)
        {
            if (!avatar) return new NameView(null, false);
            string name = avatar.Name;
            bool own = avatar is PlayerAvatar player && player.isOwned;
            return NamePresentation.Character(name, own);
        }
        private void OnDisable()
        {
            if (installed) RemoveHooks();
            installed = false;
            registry.Clear();
            overhead.Clear();
            styles.Clear();
            multiplayerName.Restore();
            Diagnostics = "presentation disabled";
        }
    }
}
