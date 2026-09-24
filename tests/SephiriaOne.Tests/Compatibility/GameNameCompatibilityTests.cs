using System.Reflection;
using HarmonyLib;

internal static class GameNameCompatibilityTests
{
    // Inspect the native transport used by unmodified peers, without running Unity.
    internal static void Run(Assembly game, Assembly addon)
    {
        var avatar = game.GetType("PlayerAvatar", true)!;
        var nameField = AccessTools.DeclaredField(avatar, "playerNameSource");
        if (nameField?.FieldType != typeof(string))
            throw new Exception("Native character-name field changed.");

        var publish = AccessTools.DeclaredMethod(avatar, "SetPlayerName", new[] { typeof(string) });
        var setter = AccessTools.PropertySetter(avatar, "NetworkplayerNameSource");
        var command = AccessTools.DeclaredMethod(avatar, "CmdSetPlayerName", new[] { typeof(string) });
        var receive = AccessTools.DeclaredMethod(avatar, "UserCode_CmdSetPlayerName__String");
        if (publish == null || publish.ReturnType != typeof(void) || setter == null || command == null || receive == null ||
            !Calls(publish, setter) || !Calls(publish, command) || !Calls(receive, setter))
            throw new Exception("Name publication no longer uses the native server setter/owned command.");
        if (!Instructions(setter).Any(i => Equals(i.operand, nameField)) ||
            !Instructions(setter).Any(i => i.operand is MethodInfo m && m.Name == "GeneratedSyncVarSetter"))
            throw new Exception("Character-name setter no longer updates a native SyncVar.");
        if (!Instructions(command).Any(i => i.operand is MethodInfo m && m.Name == "SendCommandInternal"))
            throw new Exception("Native name command transport changed.");

        foreach (string method in new[] { "SerializeSyncVars", "DeserializeSyncVars", "get_Name" })
        {
            var consumer = AccessTools.DeclaredMethod(avatar, method);
            if (consumer == null || !Instructions(consumer).Any(i => Equals(i.operand, nameField)))
                throw new Exception("Native character-name serialization/consumer changed: " + method);
        }

        var transport = addon.GetType("SephiriaOne.MultiplayerNameColor", true)!;
        foreach (string method in new[] { "Publish", "Restore" })
        {
            var writer = AccessTools.DeclaredMethod(transport, method);
            if (writer == null || !Calls(writer, publish))
                throw new Exception("Addon name " + method + " no longer uses native replicated transport.");
        }
        Console.WriteLine("Verified native name publication/restoration, server setter, command transport and SyncVar serialization (not live peer rendering).");
    }

    private static List<CodeInstruction> Instructions(MethodInfo method) => PatchProcessor.GetOriginalInstructions(method);
    private static bool Calls(MethodInfo caller, MethodInfo target) => Instructions(caller).Any(i => Equals(i.operand, target));
}
