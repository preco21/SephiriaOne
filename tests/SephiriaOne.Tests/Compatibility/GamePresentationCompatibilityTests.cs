using System.Reflection;
using HarmonyLib;

internal static class GamePresentationCompatibilityTests
{
    // Matches the inspected UserData boxing trap without invoking Steam/native code.
    private readonly struct BoxedSteamUser
    {
        public ulong SteamId { get; }
        public static BoxedSteamUser Me => new(42);
        public BoxedSteamUser(ulong id) { SteamId = id; }
        public override bool Equals(object? value) => SteamId.Equals(value);
        public override int GetHashCode() => SteamId.GetHashCode();
    }

    internal static void Run(Assembly game, Assembly addon)
    {
        var hooks = new[] {
            ("UI_MultiplayerUserIcon", "UpdateState", "userData", "userNameText"),
            ("UI_MultiplayerUserIcon_E", "UpdateState", "member", "userNameText"),
            ("UI_HUDMultiplayerRoomViewer", "UpdateRoomName", "networkLobby", "infoText"),
            ("UI_HUDMultiplayerRoomViewer", "HandleLanguageChanged", "roomHost", "infoText"),
            ("UI_MultiplayerInDungeonUserIcon", "SetUser", "spawner", "nameText"),
            ("UI_OtherCharacterPanel", "OnOpened", "otherCharacter", "characterNameText"),
            ("UI_StatsPanel", "OnOpened", "playerAvatar", "characterNameText"),
            ("UI_MultiplayerHPBar", "SetSteamProfile", "player", "playerNameText"),
            ("UI_MultiplayerHPBar", "SetSpawner", "spawner", "playerNameText")
        };
        foreach (var (typeName, methodName, subjectField, textField) in hooks)
        {
            var type = game.GetType(typeName, true)!;
            var method = AccessTools.DeclaredMethod(type, methodName);
            if (method == null || method.IsStatic || method.ReturnType != typeof(void) ||
                AccessTools.Field(type, subjectField) == null || AccessTools.Field(type, textField) == null)
                throw new Exception("Presentation adapter signature changed: " + typeName + "." + methodName);
        }
        var user = AccessTools.Field(game.GetType("UI_MultiplayerUserIcon"), "userData").FieldType;
        if (AccessTools.Property(user, "Me") == null || AccessTools.Property(user, "Nickname") == null ||
            AccessTools.Property(user, "SteamId")?.PropertyType != typeof(ulong) ||
            AccessTools.Field(game.GetType("PlayerSpawner"), "steamID")?.FieldType != typeof(ulong))
            throw new Exception("Steam own-user identity/nickname contract changed.");
        var party = game.GetType("UI_MultiplayerHPBar", true)!;
        var profile = AccessTools.DeclaredMethod(party, "SetSteamProfile");
        var parameters = profile.GetParameters();
        if (parameters.Length != 2 || parameters[0].Name != "nickname" || parameters[0].ParameterType != typeof(string) ||
            parameters[1].ParameterType.FullName != "UnityEngine.Texture2D")
            throw new Exception("Party nickname hook contract changed.");
        var partyCalls = PatchProcessor.GetOriginalInstructions(profile).Select(i => i.operand).OfType<MethodInfo>().ToArray();
        if (!partyCalls.Any(m => m.Name == "get_preferredWidth") || !partyCalls.Any(m => m.Name == "set_sizeDelta"))
            throw new Exception("Party label sizing requires a new native audit.");
        var nativeHooks = addon.GetType("SephiriaOne.NamePresentationHooks", true)!;
        var steamOwn = AccessTools.DeclaredMethod(nativeHooks, "SteamOwn");
        if (steamOwn == null || !(bool)steamOwn.Invoke(null, new object[] { new BoxedSteamUser(42) })! ||
            (bool)steamOwn.Invoke(null, new object[] { new BoxedSteamUser(7) })! ||
            (bool)steamOwn.Invoke(null, new object[] { new BoxedSteamUser(0) })!)
            throw new Exception("Lobby ownership must compare Steam IDs instead of boxed UserData.Equals(object).");
        var partyHook = AccessTools.DeclaredMethod(nativeHooks, "AfterPartyProfile");
        var resetHook = AccessTools.DeclaredMethod(nativeHooks, "AfterPartySpawner");
        if (partyHook?.GetParameters().Length != 2 || resetHook?.GetParameters().Length != 1 ||
            partyHook.GetParameters()[0].ParameterType != party || partyHook.GetParameters()[1].Name != "nickname")
            throw new Exception("Party presentation postfix signature changed.");
        var steamGradient = AccessTools.DeclaredMethod(nativeHooks, "SteamGradient");
        if (steamGradient == null || !PatchProcessor.GetOriginalInstructions(steamGradient)
            .Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "NameStyleDirectory" && m.Name == "UseGradient"))
            throw new Exception("Steam presentation lost stable-ID remote style mapping.");
        var refresh = AccessTools.DeclaredMethod(game.GetType("UI_StatsPanel"), "Refresh", Type.EmptyTypes);
        if (refresh == null) throw new Exception("Stat-only presentation refresh missing.");
        var calls = PatchProcessor.GetOriginalInstructions(refresh).Select(i => i.operand).OfType<MethodInfo>().ToArray();
        if (!calls.Any(m => m.Name == "UpdateStats") || calls.Any(m => m.Name.StartsWith("Cmd") || m.Name is "OnOpened" or "Instantiate" or "Destroy"))
            throw new Exception("Stat-only presentation refresh needs a new side-effect audit.");
        var postfix = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.NamePresentationHooks"), "AfterBind");
        if (postfix == null || !postfix.IsStatic || postfix.GetParameters().Length != 1)
            throw new Exception("Presentation hook postfix missing.");
        Console.WriteLine("Verified 9 native presentation hooks, Steam identity/style mapping, party layout and stat-only refresh IL (not live Unity rendering).");
    }
}
