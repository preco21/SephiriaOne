using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

// Establishes the shipped routing/receiver boundary and production guard.
// Static inspection does not prove live rendering on a guest.
internal static class GameDarkCloudVisualTests
{
    internal static void Run(Assembly game, Assembly addon)
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
        var hooks = addon.GetType("SephiriaOne.FriendlyFireDarkCloudVisualHooks", true)!;
        var combat = addon.GetType("SephiriaOne.FriendlyFireHooks", true)!;
        Require(Code(Method(combat, "Install")).Any(i => i.Calls(Method(hooks, "TryInstall"))) &&
            Code(Method(combat, "Uninstall")).Any(i => i.Calls(Method(hooks, "Uninstall"))), "combat lifecycle installs and removes the cosmetic adapter");
        var installer = Method(hooks, "Install");
        Require((installer.GetMethodImplementationFlags() & MethodImplAttributes.NoInlining) != 0 &&
            Code(installer).Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(Delegate) && m.Name == "CreateDelegate"),
            "optional installation isolates JIT failures and uses framework binding available alongside HarmonyX");
        var presentation = addon.GetType("SephiriaOne.FriendlyFireDarkCloudVisuals", true)!;
        var delegateType = AccessTools.DeclaredField(presentation, "send").FieldType;
        var binding = Delegate.CreateDelegate(delegateType, target);
        Require(binding.Target == null && binding.Method == target,
            "framework open delegate binds the installed native RPC signature without invoking Unity or sending messages");
        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        Require(Code(Method(runtime, "CanShowDarkCloudStrike")).Any(i => i.Calls(Method(runtime, "CanAffectTeam"))),
            "visual admission shares production participant and PvP mode rules");
        Require((bool)Method(hooks, "Validate").Invoke(null, null)!, "production transport guard accepts installed sender, receiver, animation and observer initialization");
        var validate = Method(hooks, "ValidateCode");
        bool Accept(List<CodeInstruction> s, List<CodeInstruction> r, List<CodeInstruction> b) => (bool)validate.Invoke(null, new object[] { s, r, b })!;
        var changed = sender.Select(i => new CodeInstruction(i)).ToList();
        changed.RemoveAt(changed.FindIndex(i => Calls(i, "WriteBool")));
        Require(!Accept(changed, receiver, body), "reject a missing variant field");
        changed = sender.Select(i => new CodeInstruction(i)).ToList();
        changed[changed.FindIndex(i => Calls(i, "SendTargetRPCInternal")) - 1] = new CodeInstruction(OpCodes.Ldc_I4_1);
        Require(!Accept(changed, receiver, body), "reject a changed native channel");
        changed = receiver.Select(i => new CodeInstruction(i)).ToList();
        changed.Add(new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(cloud, "isOwned")));
        Require(!Accept(sender, changed, body), "reject a new receiver ownership filter");
        changed = body.Select(i => new CodeInstruction(i)).ToList();
        changed.Add(new CodeInstruction(OpCodes.Call, Method(cloud, "FireLightning")));
        Require(!Accept(sender, receiver, changed), "reject gameplay added to the cosmetic body");
        Console.WriteLine("Verified Dark Cloud stock positional/ice/empowered receiver, production observer-FX contract and four incompatible-layout mutations (no live guest rendering).");
    }
}
