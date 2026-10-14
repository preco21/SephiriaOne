using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameRemoteAudioTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Check(bool ok, string why) { if (!ok) throw new Exception("Remote audio: " + why); }
        var rabbit = addon.GetType("SephiriaOne.RabbitPotionNativeHooks", true)!;
        Check((bool)AccessTools.Method(rabbit, "ValidateHealPresentation").Invoke(null, null)!, "Native potion audio/visual contract");
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType(type, true), method)).ToList();
        var sender = Code("PotionEffect", "RpcCreateDrinkVisual");
        var reader = Code("PotionEffect", "InvokeUserCode_RpcCreateDrinkVisual__Vector3");
        var body = Code("PotionEffect", "UserCode_RpcCreateDrinkVisual__Vector3");
        var drink = Code("PotionEffect", "CreateDrinkVisual");
        var heal = Code("PotionEffect_Regeneration", "CreateDrinkVisual");
        bool Validate(List<CodeInstruction> s, List<CodeInstruction> r, List<CodeInstruction> b, List<CodeInstruction> d, List<CodeInstruction> h) =>
            (bool)AccessTools.Method(rabbit, "ValidateHealPresentationCode").Invoke(null, new object[] { s, r, b, d, h })!;
        foreach (string mutation in new[] { "hash", "owner", "payload", "receiver", "audio", "heal" })
        {
            var s = sender.Select(i => new CodeInstruction(i)).ToList(); var r = reader.Select(i => new CodeInstruction(i)).ToList();
            var b = body.Select(i => new CodeInstruction(i)).ToList(); var d = drink.Select(i => new CodeInstruction(i)).ToList();
            var h = heal.Select(i => new CodeInstruction(i)).ToList();
            if (mutation == "hash") s[s.FindIndex(i => i.LoadsConstant(-1992013641))] = new CodeInstruction(OpCodes.Ldc_I4, 0);
            if (mutation == "owner") { int at = s.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal"); s[at - 1] = new(OpCodes.Ldc_I4_0); }
            if (mutation == "payload") r.RemoveAll(i => i.operand is MethodInfo m && m.Name == "ReadVector3");
            if (mutation == "receiver") b.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(game.GetType("UnitAvatar"), "HealPercent", new[] { typeof(float) })));
            if (mutation == "audio") d.RemoveAll(i => i.operand is MethodInfo m && m.Name == "start");
            if (mutation == "heal") h.RemoveAll(i => i.operand is FieldInfo f && f.Name == "regenerationSoundEvent");
            Check(!Validate(s, r, b, d, h), "Reject changed heal contract: " + mutation);
        }
        Check(Code("PotionEffect", "CreateEffect_OnDrink").Count(i => i.operand is MethodInfo m && m.Name == "RpcCreateDrinkVisual") == 1,
            "The native drinker already broadcasts presentation exactly once");
        Check(PatchProcessor.GetOriginalInstructions(game.GetType("PotionEffect", true)!.TypeInitializer!).Any(i =>
            i.opcode == OpCodes.Ldstr && Equals(i.operand, "System.Void PotionEffect::RpcCreateDrinkVisual(UnityEngine.Vector3)")), "Stock guests register the native potion message");
        var death = addon.GetType("SephiriaOne.FriendlyFireDeathAudio", true)!;
        Check((bool)AccessTools.Method(death, "ValidateSoundPlayback").Invoke(null, null)!, "Native impact calls positional audio before testing any FX");
        var impact = Code("BulletDestroyModule", "CreateDestroyVisualOnClient");
        foreach (string mutation in new[] { "call", "position", "gate" })
        {
            var changed = impact.Select(i => new CodeInstruction(i)).ToList();
            if (mutation == "call") changed.RemoveAll(i => i.operand is MethodInfo m && m.Name == "PlayOneShot");
            if (mutation == "position") changed[6] = new(OpCodes.Ldarg_0);
            if (mutation == "gate") changed[3] = new(OpCodes.Brfalse_S, changed[3].operand);
            Check(!(bool)AccessTools.Method(death, "ValidateSoundCode").Invoke(null, new object[] { changed })!, "Reject changed impact playback: " + mutation);
        }
        var fmod = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "FMODUnity");
        var manager = fmod.GetType("FMODUnity.RuntimeManager", true)!;
        var eventReference = fmod.GetType("FMODUnity.EventReference", true)!;
        var vector = game.GetType("BulletDestroyModule", true)!.GetMethod("CreateDestroyVisualOnClient")!.GetParameters()[2].ParameterType;
        var play = AccessTools.Method(manager, "PlayOneShot", new[] { eventReference, vector });
        var calls = PatchProcessor.GetOriginalInstructions(play).Select(i => i.operand).OfType<MethodInfo>().ToList();
        var guidPlay = calls.Single(m => m.DeclaringType == manager && m.Name == "PlayOneShot");
        Check(PatchProcessor.GetOriginalInstructions(guidPlay).Select(i => i.operand).OfType<MethodInfo>().Select(m => m.Name)
            .SequenceEqual(new[] { "CreateInstance", "To3DAttributes", "set3DAttributes", "start", "release" }),
            "Stock positional one-shot reaches exactly one FMOD start and release, not a silent FX endpoint");
        var rpcRegistration = PatchProcessor.GetOriginalInstructions(game.GetType("DungeonManager", true)!.TypeInitializer!).ToList();
        int registered = rpcRegistration.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand,
            "System.Void DungeonManager::RpcBulletDestroyed(System.UInt32,System.Boolean,System.String,UnityEngine.Vector3,System.Single,System.Single,System.Single)"));
        Check(registered >= 0 && rpcRegistration.Skip(registered).Take(4).Any(i => i.operand is MethodInfo m &&
            m.Name == "InvokeUserCode_RpcBulletDestroyed__UInt32__Boolean__String__Vector3__Single__Single__Single"),
            "Stock guests register the exact impact reader used by targeted delivery");
        Check((bool)AccessTools.Method(death, "ValidateLocalDeathSound").Invoke(null, null)!, "Native victim-only death audio gate");
        var localDeath = Code("PlayerSpawner", "HandleDieClientside");
        localDeath.RemoveAll(i => i.operand is MethodInfo m && m.Name == "get_isLocalPlayer");
        Check(!(bool)AccessTools.Method(death, "ValidateLocalDeathCode").Invoke(null, new object[] { localDeath })!, "Changed local audio scope must fail closed");
        Console.WriteLine("Verified stock potion/impact audio receivers, impact FMOD start/release and world position, single native source send, local-only death sound and changed-contract rejection (not live audio).");
    }
}
