using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameZombieVisualTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Require(bool value, string why) { if (!value) throw new Exception("Zombie visual contract: " + why); }
        List<CodeInstruction> Code(Type type, string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(type, name)).ToList();
        IEnumerable<MethodInfo> Calls(IEnumerable<CodeInstruction> code) => code.Select(i => i.operand).OfType<MethodInfo>();
        var adapter = addon.GetType("SephiriaOne.ZombieRoleVisuals", true)!;
        var native = addon.GetType("SephiriaOne.ZombieVisualNative", true)!;
        Require((uint)AccessTools.Field(native, "AssetId").GetRawConstantValue()! == 4098245047,
            "Green X uses the registered native Chim carrier, not Poison particles");
        var debuff = game.GetType("CharacterDebuff", true)!;
        var chim = game.GetType("CharacterDebuff_ChimDamage", true)!;
        var mirror = debuff.BaseType!.Assembly;
        var identity = mirror.GetType("Mirror.NetworkIdentity", true)!;
        var client = mirror.GetType("Mirror.NetworkClient", true)!;
        var server = mirror.GetType("Mirror.NetworkServer", true)!;
        Require((bool)AccessTools.DeclaredMethod(native, "ValidateNativeCode").Invoke(null, null)!, "exact game/Mirror code fingerprints accepted");
        foreach (string name in new[] { "OnStartServer", "OnStartClient", "OnUpdate_Client", "OnDestroy", "Start" })
            Require(AccessTools.Method(chim, name).DeclaringType == debuff, "Chim inherits audited " + name);
        Require(AccessTools.Method(chim, "Awake") == null, "Instantiation has no Chim gameplay callback");
        Require(Code(debuff, "Start").All(i => i.opcode == OpCodes.Ret || i.opcode == OpCodes.Nop), "Deferred Start is empty");

        Require(Calls(Code(chim, "DestroyInner")).Any(m => m.Name == "ApplyDamage") &&
            Calls(Code(chim, "DestroyInner")).Any(m => m.Name == "RpcChimFx"), "Chim gameplay expiry is a real damage/audio hazard");
        Require(!Calls(Code(debuff, "OnDestroy")).Any(m => m.Name is "DestroyInner" or "DestroyDelay") &&
            AccessTools.DeclaredMethod(chim, "OnStopServer") == null && AccessTools.DeclaredMethod(debuff, "OnStopServer") == null,
            "Actual network/object removal does not enter gameplay expiry");
        Require(AccessTools.DeclaredMethod(native, "ValidateAndGetPrefab").ReturnType == chim &&
            Code(native, "ValidateAndGetPrefab").Any(i => Equals(i.operand, "CHIM_DAMAGE")), "Adapter resolves exact Chim type and database ID");

        var update = Code(debuff, "Update");
        int serverGate = update.FindIndex(i => i.operand is MethodInfo m && m.Name == "get_isServer");
        Require(serverGate >= 0 && update[serverGate + 1].Branches(out Label? branch) && branch.HasValue, "Native update gates server work");
        Label serverEnd = (Label)update[serverGate + 1].operand;
        int end = update.FindIndex(i => i.labels.Contains(serverEnd));
        Require(end > serverGate && !Calls(update.Skip(end)).Any(m => m.Name is "OnUpdate_Server" or "Destroy" or "ApplyDamage"), "Guest update cannot tick Chim or expire carrier");
        foreach (string method in new[] { "OnStartClient", "OnUpdate_Client", "CreateLoopFx", "CreateStartFx", "SetLoopFxPositionClientside", "OnDestroy" })
            Require(!Calls(Code(debuff, method)).Any(m => m.Name is "ApplyDamage" or "ApplyDebuff" or "ApplyStatus" or "AddCustomStatUnsafe" or "CreateEffectHUD"), "Presentation-only native path: " + method);
        Require(Calls(Code(debuff, "OnUpdate_Client")).Any(m => m.Name == "SetLoopFxPositionClientside") &&
            Calls(Code(debuff, "SetLoopFxPositionClientside")).Any(m => m.Name == "set_position"), "Native loop follows target position");
        Require(Calls(Code(debuff, "OnDestroy")).Any(m => m.DeclaringType?.Name == "ElementalParticle" && m.Name == "Stop") &&
            Calls(Code(debuff, "OnDestroy")).Any(m => m.DeclaringType?.FullName == "UnityEngine.Object" && m.Name == "Destroy"), "Actual destruction stops and destroys independent loop");

        var serialized = Code(debuff, "SerializeSyncVars");
        var fields = serialized.Select(i => i.operand).OfType<FieldInfo>().Where(f => f.DeclaringType == debuff).Select(f => f.Name).Distinct().Order().ToArray();
        Require(fields.SequenceEqual(new[] { "CurrentStack" }) && Calls(serialized).Any(m => m.Name == "get_NetworkTarget") &&
            Calls(serialized).Any(m => m.Name == "get_NetworkAttacker"), "Only stack/target/attacker sync; enabled not serialized");
        foreach (string method in new[] { "OnStartServer", "OnStartClient", "SerializeServer" })
            Require(!Calls(Code(identity, method)).Any(m => m.Name is "get_enabled" or "get_isActiveAndEnabled"), "Disabled host component retained by " + method);
        var initialize = Code(identity, "InitializeNetworkBehaviours");
        int components = initialize.FindIndex(i => i.operand is MethodInfo m && m.Name == "GetComponentsInChildren");
        Require(components > 0 && initialize[components - 1].opcode == OpCodes.Ldc_I4_1, "Mirror inventories inactive/disabled network components");
        Require(Calls(Code(client, "SpawnPrefab")).Any(m => m.Name == "Instantiate") &&
            !Calls(Code(client, "ApplySpawnPayload")).Any(m => m.Name == "set_enabled"), "Guests instantiate original prefab without host disabled flag");
        Require(Calls(Code(client, "DestroyObject")).Any(m => m.DeclaringType?.FullName == "UnityEngine.Object" && m.Name == "Destroy"), "Stock network destroy executes loop teardown");
        var spawnFields = mirror.GetType("Mirror.SpawnMessage", true)!.GetFields().Select(f => f.Name).ToArray();
        Require(!spawnFields.Contains("enabled"), "Spawn envelope cannot replicate Behaviour.enabled");

        var allCode = adapter.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SelectMany(m => Code(adapter, m.Name)).ToList();
        Require(!Calls(allCode).Any(m => m.Name is "ApplyDebuff" or "InitializeAndSpawn" or "ApplyStatus" or "OnUpdate_Server" or "CreateEffectHUD" or "InitializeInner" or "DestroyInner" or "RequestEnd" or "AddStack"), "Adapter never registers or drives gameplay debuff paths");
        Require(!Calls(allCode).Any(m => m.DeclaringType == debuff && m.Name == "Destroy"), "Adapter never ends a gameplay debuff");
        Require(Code(adapter, "EnsureNative").Any(i => Equals(i.operand, "Destroy")), "Native expiry entry has an owned-only guard");
        var create = Code(adapter, "Update");
        int disable = create.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_enabled");
        int spawn = create.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == server && m.Name == "Spawn");
        Require(disable > 0 && disable < spawn && create[disable - 1].opcode == OpCodes.Ldc_I4_0, "Host component disabled before network exposure");
        Require(Calls(Code(adapter, "Retire")).Any(m => m.DeclaringType == server && m.Name == "Destroy"), "Role removal broadcasts actual network destruction");
        var runtime = addon.GetType("SephiriaOne.CombatMatchRuntime", true)!;
        foreach (string method in new[] { "BeginZombie", "InfectZombie", "AdvanceZombie" })
            Require(Calls(Code(runtime, method)).Any(m => m.DeclaringType == adapter && m.Name == "Update"), "Shared role lifecycle creates/reconciles visual: " + method);
        Require(Calls(Code(runtime, "ClearZombie")).Any(m => m.DeclaringType == adapter && m.Name == "Clear"), "Shared match cleanup removes every carrier");
        Console.WriteLine("Verified Zombie cosmetic carrier code contracts: server isolation, target following, stock guest spawning/serialization and network teardown (not live rendering).");
    }
}
