using System.Reflection;
using HarmonyLib;

internal static class GamePresentationCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var hooks = new[] {
            ("UI_MultiplayerUserIcon", "UpdateState", "userData", "userNameText"),
            ("UI_MultiplayerUserIcon_E", "UpdateState", "member", "userNameText"),
            ("UI_HUDMultiplayerRoomViewer", "UpdateRoomName", "networkLobby", "infoText"),
            ("UI_HUDMultiplayerRoomViewer", "HandleLanguageChanged", "roomHost", "infoText"),
            ("UI_MultiplayerInDungeonUserIcon", "SetUser", "spawner", "nameText"),
            ("UI_OtherCharacterPanel", "OnOpened", "otherCharacter", "characterNameText"),
            ("UI_StatsPanel", "OnOpened", "playerAvatar", "characterNameText")
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
        if (AccessTools.Property(user, "Me") == null || AccessTools.Property(user, "Nickname") == null)
            throw new Exception("Steam own-user identity/nickname contract changed.");
        var refresh = AccessTools.DeclaredMethod(game.GetType("UI_StatsPanel"), "Refresh", Type.EmptyTypes);
        if (refresh == null) throw new Exception("Stat-only presentation refresh missing.");
        var calls = PatchProcessor.GetOriginalInstructions(refresh).Select(i => i.operand).OfType<MethodInfo>().ToArray();
        if (!calls.Any(m => m.Name == "UpdateStats") || calls.Any(m => m.Name.StartsWith("Cmd") || m.Name is "OnOpened" or "Instantiate" or "Destroy"))
            throw new Exception("Stat-only presentation refresh needs a new side-effect audit.");
        var postfix = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.NamePresentationHooks"), "AfterBind");
        if (postfix == null || !postfix.IsStatic || postfix.GetParameters().Length != 1)
            throw new Exception("Presentation hook postfix missing.");
        Console.WriteLine("Verified 7 native presentation hooks, Steam own identity and stat-only refresh IL (not live Unity rendering).");
    }
}
