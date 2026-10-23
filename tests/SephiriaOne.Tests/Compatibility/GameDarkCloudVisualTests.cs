using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

// Research contract only: establishes the shipped routing/receiver boundary.
// This does not forward messages or prove live rendering on a guest.
internal static class GameDarkCloudVisualTests
{
    internal static void Run(Assembly game)
    {
        Type cloud = game.GetType("ComboEffect_DarkCloud", true)!;
        Type visual = game.GetType("DarkCloudObject", true)!;
        MethodInfo Method(Type type, string name) => AccessTools.DeclaredMethod(type, name);
        List<CodeInstruction> Code(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();
        bool Calls(CodeInstruction i, string name) => i.operand is MethodInfo m && m.Name == name;
        void Require(bool value, string reason) { if (!value) throw new Exception("Dark Cloud visual research contract: " + reason); }

        var target = Method(cloud, "TargetCreateFx");
        var strike = Code(Method(cloud, "FireLightning"));
        int send = strike.FindIndex(i => i.Calls(target));
        Require(send > 0 && strike.Count(i => i.Calls(target)) == 1 &&
            strike.Skip(Math.Max(0, send - 6)).Take(6).Any(i => Calls(i, "get_connectionToClient")),
            "player strike addresses native FX to the cloud owner's connection");
        var sender = Code(target);
        var writes = sender.Where(i => i.operand is MethodInfo m && m.Name.StartsWith("Write", StringComparison.Ordinal))
            .Select(i => ((MethodInfo)i.operand).Name).ToArray();
        Require(writes.SequenceEqual(new[] { "WriteVector3", "WriteBool", "WriteBool" }) &&
            sender.Any(i => Equals(i.operand, -1744830929)) && sender.Any(i => Calls(i, "SendTargetRPCInternal")),
            "native target RPC carries position, empowered and ice flags only");

        const string bodyName = "UserCode_TargetCreateFx__NetworkConnectionToClient__Vector3__Boolean__Boolean";
        var receiver = Code(Method(cloud, "Invoke" + bodyName));
        Require(receiver.Any(i => Calls(i, bodyName)) &&
            receiver.Where(i => i.operand is MethodInfo m && m.Name.StartsWith("Read", StringComparison.Ordinal))
                .Select(i => ((MethodInfo)i.operand).Name).SequenceEqual(new[] { "ReadVector3", "ReadBool", "ReadBool" }) &&
            !receiver.Any(i => Calls(i, "get_isOwned")), "stock receiver reads the same payload without an ownership requirement");
        var body = Code(Method(cloud, bodyName));
        var allowed = new HashSet<string> { "op_Implicit", "get_transform", "get_position", "op_Subtraction", "get_Pool",
            "Concat", "Spawn", "GetAngleFromVector", "SetRotation", "get_magnitude", "SetSizeY", "Range", "Attack" };
        Require(body.Where(i => i.operand is MethodInfo).All(i => allowed.Contains(((MethodInfo)i.operand).Name)) &&
            !body.Any(i => Calls(i, "get_isOwned")), "receiver is pooled cosmetic construction, not combat or ownership mutation");
        foreach (string key in new[] { "LightningFx", "IceLightningFx", "LightningHeadFx", "IceLightningHeadFx",
            "LightningTailFx", "IceLightningTailFx", "_EX", "IceLightningHeadFx_Plus" })
            Require(body.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, key)), "native variant key: " + key);

        var start = Code(Method(cloud, "OnStartClient"));
        int initialized = start.FindIndex(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "darkCloudObject");
        int ownerOnly = start.FindIndex(i => Calls(i, "get_isOwned"));
        Require(initialized >= 0 && ownerOnly > initialized, "each observer creates the cloud visual before owner-only radius setup");
        var ring = Code(Method(visual, "SetCloudVisible"));
        Require(ring.Any(i => i.operand is FieldInfo f && f.Name == "isOwned") && ring.Any(i => Calls(i, "SetActive")),
            "native area ring is client-owned presentation, not host-synchronized visibility");
        var animation = Code(Method(visual, "Attack"));
        Require(animation.Any(i => Calls(i, "SetValue")) && animation.Any(i => i.operand is MethodInfo m &&
            m.DeclaringType?.Name == "StudioEventEmitter" && m.Name == "Play"), "native strike includes cloud animation and attack audio");
        Require(Code(cloud.TypeInitializer!).Any(i => i.opcode == OpCodes.Ldftn && Equals(i.operand, Method(cloud, "Invoke" + bodyName))),
            "unmodified clients register the target receiver");
        Console.WriteLine("Verified Dark Cloud owner-only native FX routing, stock positional/ice/empowered receiver, observer creation and owner-only ring (research only; no live guest rendering).");
    }
}
