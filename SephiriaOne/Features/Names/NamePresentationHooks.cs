using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace SephiriaOne
{
    internal static class NamePresentationHooks
    {
        private const string Owner = "preco21.SephiriaOne.Presentation";
        private static Harmony harmony;
        private static PresentationRegistry registry;
        private static NameStyleDirectory styles;
        private static readonly List<string> missing = new List<string>();
        public static string Status => "local bindings=" + (registry?.Count ?? 0) + "; " + registry?.Diagnostics + "; missing hooks=" + (missing.Count == 0 ? "none" : string.Join(",", missing)) + "; Fountain budget/offer cache refresh unsupported; peer rendering unverified";

        public static void Install(PresentationRegistry value, NameStyleDirectory nameStyles)
        {
            registry = value;
            styles = nameStyles;
            harmony = new Harmony(Owner);
            missing.Clear();
            Add("UI_MultiplayerUserIcon", "UpdateState");
            Add("UI_MultiplayerUserIcon_E", "UpdateState");
            Add("UI_HUDMultiplayerRoomViewer", "UpdateRoomName");
            Add("UI_HUDMultiplayerRoomViewer", "HandleLanguageChanged");
            Add("UI_MultiplayerInDungeonUserIcon", "SetUser");
            Add("UI_OtherCharacterPanel", "OnOpened");
            Add("UI_StatsPanel", "OnOpened");
            Add("UI_MultiplayerHPBar", "SetSteamProfile", nameof(AfterPartyProfile));
            Add("UI_MultiplayerHPBar", "SetSpawner", nameof(AfterPartySpawner));
            // One bootstrap inventory covers a mod loaded while views are already open.
            // Subsequent discovery uses the exact native hooks above, never frame scans.
            foreach (string name in new[] { "UI_MultiplayerUserIcon", "UI_MultiplayerUserIcon_E", "UI_HUDMultiplayerRoomViewer", "UI_MultiplayerInDungeonUserIcon", "UI_OtherCharacterPanel", "UI_StatsPanel" })
            {
                Type type = typeof(PlayerAvatar).Assembly.GetType(name);
                if (type == null) continue;
                foreach (UnityEngine.Object item in UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None))
                    AfterBind(item);
            }
        }

        private static void Add(string typeName, string methodName, string postfix = nameof(AfterBind))
        {
            try
            {
                Type type = typeof(PlayerAvatar).Assembly.GetType(typeName);
                MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, methodName);
                if (method == null) throw new MissingMethodException(typeName, methodName);
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(NamePresentationHooks), postfix));
            }
            catch (Exception exception)
            {
                missing.Add(typeName + "." + methodName);
                Debug.LogWarning("[SephiriaOne] Presentation hook unavailable: " + typeName + "." + methodName + ": " + exception.Message);
            }
        }
        private static void AfterBind(object __instance)
        {
            try { Bind(__instance); }
            catch (Exception exception) { Debug.LogWarning("[SephiriaOne] Presentation binding failed: " + exception.Message); }
        }
        public static void Uninstall()
        {
            harmony?.UnpatchAll(Owner);
            harmony = null;
            registry = null;
            styles = null;
        }
        internal static object Read(object instance, string name)
        {
            if (instance == null) return null;
            var type = instance.GetType();
            var field = AccessTools.Field(type, name);
            return field != null ? field.GetValue(instance) : AccessTools.Property(type, name)?.GetValue(instance);
        }
        private static bool SteamOwn(object user)
        {
            if (user == null) return false;
            object me = AccessTools.Property(user.GetType(), "Me")?.GetValue(null);
            // Native UserData.Equals(UserData) differs from its boxed object
            // overload, which compares against a ulong and rejects boxed UserData.
            return Read(user, "SteamId") is ulong id && id != 0 && Read(me, "SteamId") is ulong local && id == local;
        }
        private static bool SteamGradient(object user) => SteamOwn(user) ||
            (Read(user, "SteamId") is ulong id && styles != null && styles.UseGradient(id));

        private static void AfterPartySpawner(UI_MultiplayerHPBar __instance)
        {
            // A reused bar must not combine its new player with the old alias.
            if (registry != null && __instance && __instance.playerNameText) registry.Remove(__instance.playerNameText);
        }

        private static void AfterPartyProfile(UI_MultiplayerHPBar __instance, string nickname)
        {
            if (registry == null || !__instance || !__instance.playerNameText) return;
            TMP_Text label = __instance.playerNameText;
            registry.Register(label, new NameLabelBinding(label, () =>
            {
                var player = Read(__instance, "player") as PlayerAvatar;
                return NamePresentation.Party(player ? player.playerNameSource : null, nickname,
                    Loc._("#ParenthesisOpen"), Loc._("#ParenthesisClose"), player && player.isOwned);
            }, fitTextWidth: true));
        }
        private static NameView Platform(string name, bool own) => NamePresentation.Platform(name, own);
        internal static NameView Character(UnitAvatar avatar)
        {
            if (!avatar) return new NameView(null, false);
            string name = avatar.Name;
            bool own = avatar is PlayerAvatar player && player.isOwned;
            return NamePresentation.Character(name, own);
        }
        private static void Label(object owner, string field, Func<NameView> source)
        {
            TMP_Text text = Read(owner, field) as TMP_Text;
            if (text) registry.Register(text, new NameLabelBinding(text, source));
        }
        private static void Bind(object owner)
        {
            if (registry == null || !(owner is Component component) || !component) return;
            switch (owner.GetType().Name)
            {
                case "UI_MultiplayerUserIcon":
                    Label(owner, "userNameText", () => {
                        object user = Read(owner, "userData");
                        return Platform(Read(user, "Nickname") as string, SteamGradient(user));
                    });
                    break;
                case "UI_MultiplayerUserIcon_E":
                    Label(owner, "userNameText", () => {
                        object member = Read(owner, "member"), lobby = Read(owner, "lobby");
                        string id = Read(member, "puid") as string, local = Read(lobby, "LocalPuid") as string;
                        string name = Read(member, "displayName") as string;
                        if (string.IsNullOrEmpty(name)) name = string.IsNullOrEmpty(id) ? "?" : id.Substring(0, Math.Min(8, id.Length));
                        return Platform(name, !string.IsNullOrEmpty(id) && id == local);
                    });
                    break;
                case "UI_HUDMultiplayerRoomViewer":
                    Label(owner, "infoText", () => {
                        var view = (UI_HUDMultiplayerRoomViewer)owner;
                        object manager = Read(owner, "networkLobby"), lobby = Read(manager, "Lobby");
                        object user = Read(Read(lobby, "Owner"), "user");
                        string host = Read(user, "Nickname") as string ?? view.roomHost;
                        bool own = SteamGradient(user);
                        return NamePresentation.HostSummary(view.roomNameString.ToString(), view.roomName,
                            view.roomHostString.ToString(), host, view.roomChapterString.ToString(), view.roomChapter, own);
                    });
                    break;
                case "UI_MultiplayerInDungeonUserIcon":
                    Label(owner, "nameText", () => {
                        var spawner = Read(owner, "spawner") as PlayerSpawner;
                        return Character(spawner ? spawner.PlayerAvatar : null);
                    });
                    break;
                case "UI_OtherCharacterPanel":
                    Label(owner, "characterNameText", () => Character(Read(owner, "otherCharacter") as UnitAvatar));
                    break;
                case "UI_StatsPanel":
                    Label(owner, "characterNameText", () => Character(Read(owner, "playerAvatar") as UnitAvatar));
                    registry.Register(owner, new StatsPanelBinding((UI_StatsPanel)owner));
                    break;
            }
        }
    }
}
