using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using HarmonyLib;

const string expectedGameHash = "c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510";
if (args.Length != 2)
    throw new ArgumentException("Arguments: <installed game Managed directory> <freshly built addon DLL>");
string managed = Path.GetFullPath(args[0]);
string addonPath = Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
string gamePath = Path.Combine(managed, "Assembly-CSharp.dll");
using (var stream = File.OpenRead(gamePath))
{
    string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    if (hash != expectedGameHash)
        throw new Exception($"Installed assembly fingerprint changed: {hash}. Audit contracts before updating the expected hash.");
}
var game = Assembly.LoadFrom(gamePath);
var addon = Assembly.LoadFrom(addonPath);
var hooks = addon.GetType("SephiriaOne.ProjectileLimitHooks", true)!;
var runtime = addon.GetType("SephiriaOne.ProjectileLimitRuntime", true)!;
const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
MethodInfo Hook(string name) => AccessTools.DeclaredMethod(hooks, name)
    ?? throw new MissingMethodException(hooks.FullName, name);
MethodInfo RuntimeMethod(string name) => AccessTools.DeclaredMethod(runtime, name)
    ?? throw new MissingMethodException(runtime.FullName, name);
MethodInfo Native(string type, string name) => AccessTools.DeclaredMethod(game.GetType(type, true), name)
    ?? throw new MissingMethodException(type, name);
object? Invoke(MethodInfo method, params object?[] parameters)
{
    try { return method.Invoke(null, parameters); }
    catch (TargetInvocationException error) when (error.InnerException != null)
    {
        ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        throw;
    }
}
var emitters = ((IEnumerable)Invoke(Hook("Emitters"))!).Cast<object>().ToArray();
T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, all)!.GetValue(instance)!;
var spawn = (MethodInfo)hooks.GetProperty("Spawn", all)!.GetValue(null)!;
var replacement = RuntimeMethod("Spawn");
int checks = 0, cases = 0, failures = 0;
void Check(bool condition, string reason)
{
    if (!condition) throw new Exception(reason);
    checks++;
}
void Test(string name, Action action)
{
    cases++;
    try { action(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
void Reject(Action action, string reason)
{
    bool rejected = false;
    try { action(); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, reason);
}
List<CodeInstruction> Code(MethodInfo method) => PatchProcessor.GetOriginalInstructions(method).ToList();
List<CodeInstruction> Rewrite(object emitter, List<CodeInstruction> code, ILGenerator generator) =>
    ((IEnumerable<CodeInstruction>)Invoke(Hook("Rewrite"), code, generator, Field<MethodInfo>(emitter, "Method"))!).ToList();

Test("Exact audited emitter inventory excludes primary and held-fire weapons", () =>
{
    var expected = new Dictionary<string, (string? source, int calls)>(StringComparer.Ordinal)
    {
        ["Charm_PallasCard"] = ("pallas-card", 1),
        ["SpecialProjectile_FuryMP"] = ("liberation", 1),
        ["Charm_NearMagicBullet"] = ("yearning-robe", 1),
        ["Charm_CreateBulletOnSweep"] = ("ice-vine", 1),
        ["Charm_AirSlash"] = ("ice-air-slash", 1),
        ["Charm_IceSpear"] = ("ice-spear", 1),
        ["Charm_IceBow"] = ("ice-bow", 1),
        ["Charm_IceHammer"] = ("ice-hammer", 1),
        ["Charm_ElectricEarring"] = ("thunder-earring", 1),
        ["Charm_FireFeather"] = ("phoenix-wing", 1),
        ["Charm_FlameGround_Meteor"] = ("meteor", 1),
        ["Charm_FlamePlantRoot"] = ("flame-root", 1),
        ["Charm_RockElephant"] = ("rock-elephant", 1),
        ["Charm_Kunai"] = ("kunai", 1),
        ["GreenBat"] = ("planets", 4),
        ["WeaponAddonGreatsword_FireNeedleBullet"] = ("weapon-spike", 1),
        ["Charm_FlameBall"] = ("flame-ball", 1),
        ["BulletDestroyModule_CreateOtherBullet"] = (null, 1),
        ["BulletDestroyModule_Explode"] = (null, 1),
        ["BulletDestroyModule_HeavyExplode"] = (null, 1),
        ["BulletDestroyModule_CreateBulletSpread"] = (null, 1)
    };
    Check(emitters.Length == expected.Count, "An emitter was added or removed without an audit");
    foreach (var emitter in emitters)
    {
        var method = Field<MethodInfo>(emitter, "Method");
        string name = (method.DeclaringType!.DeclaringType ?? method.DeclaringType).Name;
        Check(expected.Remove(name, out var contract), "Unexpected or duplicate emitter: " + method);
        Check(Field<string?>(emitter, "Source") == contract.source && Field<int>(emitter, "Calls") == contract.calls,
            "Wrong source or native spawn count for " + name);
    }
    Check(expected.Count == 0, "Missing expected emitter");
    Check(!emitters.Any(e => Field<MethodInfo>(e, "Method") == spawn), "Generic native spawning must not be globally denied");
});

Test("Ice Weapon denials preserve loop timing and skip every spawn-dependent operation", () =>
{
    var contracts = new[]
    {
        (source: "ice-air-slash", type: "Charm_AirSlash", name: "AirSlashCoroutine", iterator: true),
        (source: "ice-spear", type: "Charm_IceSpear", name: "FireSpearCoroutine", iterator: true),
        (source: "ice-bow", type: "Charm_IceBow", name: "FireArrow", iterator: false),
        (source: "ice-hammer", type: "Charm_IceHammer", name: "HammerCoroutine", iterator: true)
    };
    var swingId = AccessTools.Field(game.GetType("ProjectileBase", true), "swingId");
    foreach (var contract in contracts)
    {
        var matches = emitters.Where(e => Field<string?>(e, "Source") == contract.source).ToArray();
        Check(matches.Length == 1, "Missing or duplicated Ice Weapon emitter: " + contract.source);
        var emitter = matches[0];
        var entry = Native(contract.type, contract.name);
        var expectedMethod = contract.iterator
            ? AccessTools.DeclaredMethod(entry.GetCustomAttribute<System.Runtime.CompilerServices.IteratorStateMachineAttribute>()!.StateMachineType, "MoveNext")
            : entry;
        var method = Field<MethodInfo>(emitter, "Method");
        Check(method == expectedMethod, "The exact native emission body is required: " + contract.source);
        MemberInfo expectedTail = contract.source == "ice-hammer" ? Native("Charm_IceHammer", "RpcCreateHammer") : swingId;
        Check(Equals(Field<MemberInfo>(emitter, "GuardTail"), expectedTail), "Wrong denial boundary: " + contract.source);
        var original = PatchProcessor.GetOriginalInstructions(method, out var generator).ToList();
        var rewritten = Rewrite(emitter, original, generator);
        int call = rewritten.FindIndex(i => i.Calls(replacement));
        Label denied = (Label)rewritten[call + 4].operand;
        int continuation = rewritten.FindIndex(i => i.labels.Contains(denied));
        var guarded = rewritten.Skip(call + 5).Take(continuation - call - 5).ToList();
        Check(guarded.Count(i => i.StoresField(swingId)) == 1,
            "Denied shots must skip the FROSTRELIC mutation: " + contract.source);
        if (contract.source == "ice-hammer")
        {
            var active = AccessTools.Field(game.GetType("Charm_IceHammer", true), "activeBullets");
            Check(guarded.Count(i => i.LoadsField(active)) == 1 &&
                guarded.Count(i => i.operand is MethodInfo m && m.Name == "Add" &&
                    m.DeclaringType!.IsGenericType && m.DeclaringType.GetGenericTypeDefinition() == typeof(List<>)) == 1 &&
                guarded.Count(i => i.Calls((MethodInfo)expectedTail)) == 1,
                "Hammer denial must skip activeBullets admission and the throw RPC together");
        }
        if (contract.iterator)
        {
            Check(rewritten.Skip(continuation).Take(4).Any(i => i.LoadsConstant(0.25f)) &&
                rewritten.Skip(continuation).Take(4).Any(i => i.operand is ConstructorInfo c && c.DeclaringType!.Name == "WaitForSeconds"),
                "Denied attempts retain their immediate quarter-second wait: " + contract.source);
            Check(rewritten.Skip(continuation).Any(i => i.operand is MethodInfo m && m.Name == "CalculateRetriggerCoroutine"),
                "Denied attempts must still reach native retrigger processing: " + contract.source);
        }
        else
        {
            var increment = rewritten.Skip(continuation).Take(4).ToArray();
            Check(increment.Any(i => i.LoadsConstant(1)) && increment.Any(i => i.opcode == OpCodes.Add) &&
                increment.Any(i => i.opcode == OpCodes.Stloc_S),
                "Denied Bow amplification consumes its loop iteration");
        }
    }
});

Test("Native spawn replacement preserves every argument and result", () =>
{
    Invoke(Hook("ValidateSpawnSignature"));
    Check(replacement.ReturnType == spawn.ReturnType && spawn.ReturnType.Name == "Bullet", "Spawn result type changed");
    Check(replacement.IsStatic && !spawn.IsStatic, "Replacement must receive the original spawner explicitly");
    Check(replacement.GetParameters().Select(p => p.ParameterType).SequenceEqual(
        new[] { spawn.DeclaringType! }.Concat(spawn.GetParameters().Select(p => p.ParameterType)).Append(typeof(string))),
        "Source must be appended after every native argument");
});

foreach (var emitter in emitters)
{
    var method = Field<MethodInfo>(emitter, "Method");
    Test("Validate and rewrite installed emitter " + method.DeclaringType!.Name + "." + method.Name, () =>
    {
        var original = PatchProcessor.GetOriginalInstructions(method, out var generator).ToList();
        Invoke(Hook("ValidateEmitter"), original, emitter);
        var rewritten = Rewrite(emitter, original, generator);
        int expected = Field<int>(emitter, "Calls");
        Check(original.Count(i => i.Calls(spawn)) == expected, "Original spawn count mismatch");
        Check(rewritten.Count(i => i.Calls(replacement)) == expected && !rewritten.Any(i => i.Calls(spawn)),
            "Every audited spawn must be replaced exactly once");
        string? source = Field<string?>(emitter, "Source");
        foreach (int index in Enumerable.Range(0, rewritten.Count).Where(i => rewritten[i].Calls(replacement)))
            Check(source == null ? rewritten[index - 1].opcode == OpCodes.Ldnull :
                rewritten[index - 1].opcode == OpCodes.Ldstr && Equals(rewritten[index - 1].operand, source),
                "The wrapper receives the exact captured source");
        Check(original.Where(i => i.operand is MethodBase && !i.Calls(spawn)).Select(i => i.operand)
            .SequenceEqual(rewritten.Where(i => i.operand is MethodBase && !i.Calls(replacement)).Select(i => i.operand)),
            "Unrelated native calls, including cadence and cooldowns, must stay in order");
        Check(original.Where(i => i.operand is FieldInfo).Select(i => (i.opcode, i.operand))
            .SequenceEqual(rewritten.Where(i => i.operand is FieldInfo).Select(i => (i.opcode, i.operand))),
            "Native field reads and writes must stay in order");
        IlStackVerifier.Verify(method, original);
        IlStackVerifier.Verify(method, rewritten);
        NativeRewriteJit.Verify(method, (nativeCode, nativeGenerator) => Rewrite(emitter, nativeCode, nativeGenerator));
        checks += 3;
    });
}

foreach (var emitter in emitters.Where(e => Field<MemberInfo?>(e, "GuardTail") != null))
{
    var method = Field<MethodInfo>(emitter, "Method");
    Test("Null denial skips configuration but reaches native continuation: " + method.DeclaringType!.Name, () =>
    {
        var original = PatchProcessor.GetOriginalInstructions(method, out var generator).ToList();
        var rewritten = Rewrite(emitter, original, generator);
        int call = rewritten.FindIndex(i => i.Calls(replacement));
        Check(rewritten[call + 1].opcode == OpCodes.Dup &&
            rewritten[call + 2].opcode == OpCodes.Brtrue &&
            rewritten[call + 3].opcode == OpCodes.Pop &&
            rewritten[call + 4].opcode == OpCodes.Br,
            "Denial must consume its null result and skip all result dereferences");
        Label denied = (Label)rewritten[call + 4].operand;
        Label accepted = (Label)rewritten[call + 2].operand;
        int deniedIndex = rewritten.FindIndex(i => i.labels.Contains(denied));
        int acceptedIndex = rewritten.FindIndex(i => i.labels.Contains(accepted));
        MemberInfo tail = Field<MemberInfo>(emitter, "GuardTail");
        int tailIndex = rewritten.FindIndex(i => tail is MethodInfo tailMethod
            ? i.Calls(tailMethod) : i.StoresField((FieldInfo)tail));
        Check(deniedIndex == tailIndex + 1 && acceptedIndex == call + 5,
            "Null jumps directly after the audited configuration tail; success preserves native configuration");
        Check(deniedIndex < rewritten.Count - 1, "Denial must continue the method instead of skipping its native tail");
        Check(GuardExecution.Run(rewritten.Skip(call + 1).Take(5).ToArray(), null) == 1,
            "JIT-compiled null guard must reach continuation without executing configuration");
        Check(GuardExecution.Run(rewritten.Skip(call + 1).Take(5).ToArray(), new object()) == 11,
            "JIT-compiled accepted guard must retain configuration and continuation");
        if (method.DeclaringType!.Name == "SpecialProjectile_FuryMP")
        {
            var progress = AccessTools.Field(method.DeclaringType, "spawnedBulletCount");
            int progressIndex = rewritten.FindIndex(deniedIndex, i => i.StoresField(progress));
            Check(progressIndex > deniedIndex, "Denied Liberation attempts must consume spawnedBulletCount progress");
            Check(rewritten.Skip(deniedIndex).Take(progressIndex - deniedIndex).Any(i => i.LoadsConstant(1)),
                "Liberation must advance one attempt rather than queue it for later");
        }
    });
}

Test("Native special controller initialization wrapper and prefix bindings", () =>
{
    var special = (MethodInfo)hooks.GetProperty("SpecialSpawn", all)!.GetValue(null)!;
    var nativeInitialize = Native("ProjectileBase", "OnSpawn");
    var original = Code(special);
    Invoke(Hook("ValidateSpecial"), original);
    var rewritten = ((IEnumerable<CodeInstruction>)Invoke(Hook("RewriteSpecial"), original)!).ToList();
    Check(original.Count(i => i.Calls(nativeInitialize)) == 1 &&
        rewritten.Count(i => i.Calls(RuntimeMethod("SpawnEmitter"))) == 1 &&
        !rewritten.Any(i => i.Calls(nativeInitialize)), "Only the single native OnSpawn call is replaced");
    Check(RuntimeMethod("SpawnEmitter").GetParameters().Single().ParameterType == nativeInitialize.DeclaringType &&
        RuntimeMethod("SpawnEmitter").ReturnType == typeof(void), "Special initializer wrapper has identical stack effect");
    Check(Code(RuntimeMethod("SpawnEmitter")).Count(i => i.Calls(nativeInitialize)) == 1,
        "Special wrapper preserves one native initialization");
    var owner = special.GetParameters().SingleOrDefault(p => p.Name == "owner");
    Check(owner != null && owner.ParameterType.Name == "UnitAvatar", "Harmony owner parameter binds to the native name and type");
    Check(Hook("BeforeEmitter").GetParameters().Single(p => p.Name == "owner").ParameterType == owner!.ParameterType,
        "Prefix owner parameter type matches the installed method");
    IlStackVerifier.Verify(special, rewritten);
    NativeRewriteJit.Verify(special, (nativeCode, _) =>
        ((IEnumerable<CodeInstruction>)Invoke(Hook("RewriteSpecial"), nativeCode)!).ToList());
    checks += 2;
    Reject(() => Invoke(Hook("ValidateSpecial"), original.Where(i => !i.Calls(nativeInitialize)).ToList()),
        "A missing controller initialization must disable hooks");
    Reject(() => Invoke(Hook("ValidateSpecial"), original.Concat(new[] { new CodeInstruction(OpCodes.Callvirt, nativeInitialize) }).ToList()),
        "A duplicate controller initialization must disable hooks");
});

foreach (var emitter in emitters)
{
    var method = Field<MethodInfo>(emitter, "Method");
    Test("Reject changed installed spawn contract: " + method.DeclaringType!.Name, () =>
    {
        var original = Code(method);
        Reject(() => Invoke(Hook("ValidateEmitter"), original.Where(i => !i.Calls(spawn)).ToList(), emitter),
            "Removed spawn call must be rejected");
        Reject(() => Invoke(Hook("ValidateEmitter"), original.Concat(new[] { new CodeInstruction(OpCodes.Callvirt, spawn) }).ToList(), emitter),
            "Additional unreviewed spawn call must be rejected");
        int lastCall = original.FindLastIndex(i => i.Calls(spawn));
        Reject(() => Invoke(Hook("ValidateEmitter"), original.Take(lastCall + 1).ToList(), emitter),
            "A truncated spawn result must be rejected with a compatibility failure");
        var tail = Field<MemberInfo?>(emitter, "GuardTail");
        if (tail != null)
        {
            Reject(() => Invoke(Hook("ValidateEmitter"), original.Where(i => !Equals(i.operand, tail)).ToList(), emitter),
                "A missing configuration tail must be rejected");
            var changed = original.Select(i => new CodeInstruction(i)).ToList();
            changed[changed.FindIndex(i => i.Calls(spawn))].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            Reject(() => Invoke(Hook("ValidateEmitter"), changed, emitter), "A guard crossing exception scopes must be rejected");
        }
        else
        {
            var changed = original.Select(i => new CodeInstruction(i)).ToList();
            int call = changed.FindIndex(i => i.Calls(spawn));
            if (method.DeclaringType!.Name == "Charm_CreateBulletOnSweep")
            {
                changed.RemoveAll(i => i.operand is MethodInfo m && m.Name == "op_Implicit" && m.DeclaringType?.FullName == "UnityEngine.Object");
                Reject(() => Invoke(Hook("ValidateEmitter"), changed, emitter), "Ice Vine must retain its native Unity null guard");
            }
            else
            {
                changed[call + 1] = new CodeInstruction(OpCodes.Dup);
                Reject(() => Invoke(Hook("ValidateEmitter"), changed, emitter), "A new result consumer requires explicit audit");
            }
        }
    });
}

Test("Liberation validator rejects removal of emission progress", () =>
{
    var emitter = emitters.Single(e => Field<MethodInfo>(e, "Method").DeclaringType!.Name == "SpecialProjectile_FuryMP");
    var method = Field<MethodInfo>(emitter, "Method");
    var progress = AccessTools.Field(method.DeclaringType, "spawnedBulletCount");
    Reject(() => Invoke(Hook("ValidateEmitter"), Code(method).Where(i => !i.StoresField(progress)).ToList(), emitter),
        "An emitter that no longer consumes attempts must fail closed");
});

Test("Incoming spawn labels move to source loading before the replacement call", () =>
{
    foreach (var emitter in emitters)
    {
        var method = Field<MethodInfo>(emitter, "Method");
        var original = PatchProcessor.GetOriginalInstructions(method, out var generator).ToList();
        int firstCall = original.FindIndex(i => i.Calls(spawn));
        Label incoming = generator.DefineLabel();
        original[firstCall].labels.Add(incoming);
        var rewritten = Rewrite(emitter, original, generator);
        int mappedCall = rewritten.FindIndex(i => i.Calls(replacement));
        Check(rewritten[mappedCall - 1].labels.Contains(incoming) &&
            rewritten.All(i => !i.labels.Contains(incoming) || ReferenceEquals(i, rewritten[mappedCall - 1])),
            "A native jump to Spawn must not bypass loading its appended source: " + method);
    }
});

Test("Bullet accounting binds in the native OnSpawn prefix before callbacks", () =>
{
    var before = Code(Hook("BeforeBullet"));
    Check(before.Count(i => i.Calls(RuntimeMethod("BeforeSpawn"))) == 1, "Native prefix must enter the accounting boundary");
    var boundary = Code(RuntimeMethod("BeforeSpawn"));
    int retire = boundary.FindIndex(i => i.Calls(RuntimeMethod("Despawned")));
    int bind = boundary.FindIndex(i => i.Calls(RuntimeMethod("Bind")));
    Check(retire >= 0 && bind > retire, "A reused projectile retires its old lease and binds the new one before native callbacks");
    Check(Code(Hook("Install")).Any(i => i.LoadsConstant("BeforeBullet")) &&
        !Code(Hook("Install")).Any(i => i.LoadsConstant("AfterBullet")),
        "Binding must not wait until an OnSpawn postfix after reentrant callbacks");
    Check(Code(spawn).Any(i => i.operand is MethodInfo m && m.Name == "OnSpawn"),
        "Native pool creation invokes the audited initialization boundary");
});

Test("Rate-only local effects gate only audited creation senders", () =>
{
    var effects = addon.GetType("SephiriaOne.ProjectileEffectRateHooks", true)!;
    MethodInfo Effect(string name) => AccessTools.DeclaredMethod(effects, name)!;
    var contracts = new[]
    {
        (type: "Charm_IceSword", sender: "RpcCreateSwords", prefix: "BeforeIceSword", source: "ice-sword"),
        (type: "Charm_Guillotine", sender: "RpcCreateGuillotine", prefix: "BeforeGuillotine", source: "ice-guillotine")
    };
    foreach (var contract in contracts)
    {
        var sender = Native(contract.type, contract.sender);
        var original = Code(sender);
        Invoke(Effect("ValidateSender"), sender, original);
        var prefix = Effect(contract.prefix);
        Check(prefix.ReturnType == typeof(bool) && prefix.GetParameters().Single().ParameterType == sender.DeclaringType,
            "Creation prefix must bind the exact sender instance: " + contract.source);
        Check(Code(prefix).Count(i => i.Calls(RuntimeMethod("AdmitEffect"))) == 1 &&
            Code(prefix).Count(i => i.LoadsConstant(contract.source)) == 1,
            "Each creation sender charges its exact rate source once: " + contract.source);
        Check(!Code(prefix).Any(i => i.Calls(RuntimeMethod("Spawn")) || i.opcode == OpCodes.Stfld),
            "Rate-only admission must not manipulate local objects or native state");
        var send = original.Single(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
        Reject(() => Invoke(Effect("ValidateSender"), sender, original.Where(i => !ReferenceEquals(i, send)).ToList()),
            "Missing creation RPC send must fail compatibility validation");
        Reject(() => Invoke(Effect("ValidateSender"), sender, original.Concat(new[] { new CodeInstruction(send) }).ToList()),
            "An additional creation RPC send must fail compatibility validation");
        var changed = original.Select(i => new CodeInstruction(i)).ToList();
        int call = changed.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
        changed[call - 1] = new CodeInstruction(OpCodes.Ldc_I4_0);
        Reject(() => Invoke(Effect("ValidateSender"), sender, changed), "Creation RPC must continue including its owner");
        changed = original.Select(i => new CodeInstruction(i)).ToList();
        changed[call - 2] = new CodeInstruction(OpCodes.Ldc_I4_1);
        Reject(() => Invoke(Effect("ValidateSender"), sender, changed), "Creation RPC must retain the reliable channel");
        changed = original.Select(i => new CodeInstruction(i)).ToList();
        changed.Single(i => i.opcode == OpCodes.Ldstr).operand = "ChangedRpc";
        Reject(() => Invoke(Effect("ValidateSender"), sender, changed), "Changed RPC identity requires another audit");
        Reject(() => Invoke(Effect("ValidateSender"), Native(contract.type, "GetDamage"), original),
            "An unrelated method must never qualify as an emission sender");
        IlStackVerifier.Verify(prefix, Code(prefix));
        NativeRewriteJit.Verify(prefix, (code, _) => code);
        checks += 2;
    }
    Check(Code(Hook("Install")).Count(i => i.Calls(Effect("Install"))) == 1,
        "Local rate hooks must join the same all-or-nothing hook installation");
    Check(!Code(Effect("Install")).Any(i => i.operand is string s &&
        (s.StartsWith("UserCode_", StringComparison.Ordinal) || s == "Initialize" || s == "OnDestroy")),
        "Receivers and lifetimes are not rate-admission boundaries");
});

Test("Only the Frost Flame Sword callback gains per-sword admission", () =>
{
    var effects = addon.GetType("SephiriaOne.ProjectileEffectRateHooks", true)!;
    MethodInfo Effect(string name) => AccessTools.DeclaredMethod(effects, name)!;
    var callback = Native("ComboEffect_FlameSword", "HandleAttackUnit");
    var batch = Native("ComboEffect_FlameSword", "ServerFireSword");
    var fire = Native("ComboEffect_FlameSword", "FireSword");
    var use = Native("ComboEffect_FlameSword", "UseSword");
    var create = Native("ComboEffect_FlameSword", "CreateFlameSword");
    var originalCallback = Code(callback);
    Invoke(Effect("ValidateCallback"), originalCallback);
    var rewrittenCallback = ((IEnumerable<CodeInstruction>)Invoke(Effect("RewriteCallback"), originalCallback)!).ToList();
    Check(originalCallback.Count(i => i.Calls(batch)) == 2 && rewrittenCallback.Count(i => i.Calls(batch)) == 1 &&
        rewrittenCallback.Count(i => i.Calls(Effect("FrostBatch"))) == 1,
        "Only the second, Frost-specific callback call may be wrapped");
    int first = originalCallback.FindIndex(i => i.Calls(batch));
    Check(rewrittenCallback[first].Calls(batch), "The direct/magic callback call remains native");
    Check(originalCallback.Where(i => i.operand is FieldInfo).Select(i => (i.opcode, i.operand)).SequenceEqual(
        rewrittenCallback.Where(i => i.operand is FieldInfo).Select(i => (i.opcode, i.operand))),
        "Native callback eligibility and cooldown writes are preserved");
    foreach (string flag in new[] { "FLAMESWORDCALLBACKFROST", "FROSTRELIC" })
        Reject(() => Invoke(Effect("ValidateCallback"), originalCallback.Where(i => !i.LoadsConstant(flag)).ToList()),
            "Missing Frost eligibility flag must disable this adapter: " + flag);
    Reject(() => Invoke(Effect("ValidateCallback"), originalCallback.Where(i => !i.Calls(batch)).ToList()),
        "Changed callback call count must fail validation");
    var mutatedCallback = originalCallback.Select(i => new CodeInstruction(i)).ToList();
    mutatedCallback[mutatedCallback.FindLastIndex(i => i.Calls(batch)) - 1] = new CodeInstruction(OpCodes.Ldc_I4_1);
    Reject(() => Invoke(Effect("ValidateCallback"), mutatedCallback), "Frost must not silently become an asynchronous targeting path");

    var originalBatch = PatchProcessor.GetOriginalInstructions(batch, out var batchGenerator).ToList();
    Invoke(Effect("ValidateBatch"), originalBatch);
    var rewrittenBatch = ((IEnumerable<CodeInstruction>)Invoke(Effect("RewriteBatch"), originalBatch, batchGenerator)!).ToList();
    Check(rewrittenBatch.Count(i => i.Calls(Effect("TakeBatchRequest"))) == 1 &&
        rewrittenBatch.Count(i => i.Calls(Effect("BatchSword"))) == 1 && !rewrittenBatch.Any(i => i.Calls(fire)),
        "Each synchronous batch captures provenance once and hands it to each sword");
    var autoSearch = Native("ComboEffect_FlameSword", "AutoSearchNearEnemyAndFireSword");
    Check(rewrittenBatch.Count(i => i.Calls(autoSearch)) == 1,
        "The separate manual/autosearch coroutine stays native");
    Reject(() => Invoke(Effect("ValidateBatch"), originalBatch.Where(i => !i.Calls(fire)).ToList()),
        "Missing per-sword loop call must disable the adapter");
    Reject(() => Invoke(Effect("ValidateBatch"), originalBatch.Concat(new[] { new CodeInstruction(OpCodes.Call, fire) }).ToList()),
        "Additional per-sword call requires a new audit");

    var originalFire = PatchProcessor.GetOriginalInstructions(fire, out var fireGenerator).ToList();
    Invoke(Effect("ValidateFireSword"), originalFire);
    var rewrittenFire = ((IEnumerable<CodeInstruction>)Invoke(Effect("RewriteFireSword"), originalFire, fireGenerator)!).ToList();
    int gate = rewrittenFire.FindIndex(i => i.Calls(Effect("AdmitFlameSword")));
    Check(rewrittenFire.FindIndex(i => i.Calls(use)) < gate && gate < rewrittenFire.FindIndex(i => i.Calls(create)),
        "Native sword stock is consumed before admission, and denied effects never start their coroutine");
    Check(rewrittenFire.Count(i => i.Calls(Effect("TakeSwordRequest"))) == 1 &&
        rewrittenFire[gate + 1].opcode == OpCodes.Brtrue && rewrittenFire[gate + 2].LoadsConstant(1) &&
        rewrittenFire[gate + 3].opcode == OpCodes.Ret,
        "A denied, already-consumed sword returns native success and continues its outer loop");
    Check(originalFire.Where(i => i.operand is MethodBase).Select(i => i.operand).SequenceEqual(
        rewrittenFire.Where(i => i.operand is MethodBase && !i.Calls(Effect("TakeSwordRequest")) && !i.Calls(Effect("AdmitFlameSword"))).Select(i => i.operand)),
        "Native calls retain their order, including stock and coroutine operations");
    var gateCode = rewrittenFire.Skip(gate - 3).Take(7).ToArray();
    Check(RunEffectGate(gateCode, false, false) == 11, "Unscoped direct, magic and manual sword paths bypass rate admission");
    Check(RunEffectGate(gateCode, true, false) == 101, "Denied Frost sword consumes stock without starting FX/damage/pickup work");
    Check(RunEffectGate(gateCode, true, true) == 111, "Admitted Frost sword continues into the native coroutine");
    Reject(() => Invoke(Effect("ValidateFireSword"), originalFire.Where(i => !i.Calls(use)).ToList()),
        "Removing native stock consumption must fail validation");
    Reject(() => Invoke(Effect("ValidateFireSword"), originalFire.Where(i => !i.Calls(create)).ToList()),
        "Changed coroutine construction must fail validation");
    var changedFire = originalFire.Select(i => new CodeInstruction(i)).ToList();
    changedFire[changedFire.FindIndex(i => i.Calls(use)) + 1].opcode = OpCodes.Brfalse_S;
    Reject(() => Invoke(Effect("ValidateFireSword"), changedFire), "Changed stock-success branch must fail validation");
    foreach (var pair in new[] { (callback, rewrittenCallback), (batch, rewrittenBatch), (fire, rewrittenFire) })
    {
        IlStackVerifier.Verify(pair.Item1, pair.Item2);
        checks++;
    }
    NativeRewriteJit.Verify(callback, (code, _) => ((IEnumerable<CodeInstruction>)Invoke(Effect("RewriteCallback"), code)!).ToList());
    NativeRewriteJit.Verify(batch, (code, generator) => ((IEnumerable<CodeInstruction>)Invoke(Effect("RewriteBatch"), code, generator)!).ToList());
    NativeRewriteJit.Verify(fire, (code, generator) => ((IEnumerable<CodeInstruction>)Invoke(Effect("RewriteFireSword"), code, generator)!).ToList());
    checks += 3;
    foreach (string wrapper in new[] { "FrostBatch", "BatchSword" })
        Check(Effect(wrapper).GetMethodBody()!.ExceptionHandlingClauses.Any(c => c.Flags == ExceptionHandlingClauseOptions.Finally),
            "The native-call handoff must restore prior state even on exceptions: " + wrapper);
});

Test("Frost provenance handoffs are consumed once and cannot label nested manual calls", () =>
{
    var effects = addon.GetType("SephiriaOne.ProjectileEffectRateHooks", true)!;
    foreach (var pair in new[] { (field: "requestedBatch", take: "TakeBatchRequest"), (field: "requestedSword", take: "TakeSwordRequest") })
    {
        var slot = effects.GetField(pair.field, all)!;
        var take = AccessTools.DeclaredMethod(effects, pair.take)!;
        Check(slot.IsDefined(typeof(ThreadStaticAttribute)), "Provenance handoffs must remain thread-local");
        object marker = new object();
        try
        {
            slot.SetValue(null, marker);
            Check((bool)Invoke(take, marker)!, "The intended immediate native entry receives its provenance");
            Check(!(bool)Invoke(take, marker)!, "A nested/manual call cannot reuse the consumed provenance");
            slot.SetValue(null, marker);
            Check(!(bool)Invoke(take, new object())! && slot.GetValue(null) == null,
                "A different effect cannot inherit or leave behind the pending provenance");
            Check(!(bool)Invoke(take, new object?[] { null })!, "An absent request never matches a null instance");
        }
        finally { slot.SetValue(null, null); }
    }
});

int RunEffectGate(IReadOnlyList<CodeInstruction> gate, bool scoped, bool allowed)
{
    var method = new DynamicMethod("EffectAdmissionBranch", typeof(int), new[] { typeof(bool), typeof(bool) });
    var il = method.GetILGenerator();
    var calls = il.DeclareLocal(typeof(int));
    Label continuation = il.DefineLabel();
    foreach (var instruction in gate)
    {
        if (instruction.opcode == OpCodes.Ldloc) il.Emit(OpCodes.Ldarg_0);
        else if (instruction.opcode == OpCodes.Ldarg_0) il.Emit(OpCodes.Ldnull);
        else if (instruction.opcode == OpCodes.Call)
        {
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldc_I4, 100);
            il.Emit(OpCodes.Stloc, calls);
            il.Emit(OpCodes.Ldarg_1);
        }
        else if (instruction.opcode == OpCodes.Brtrue || instruction.opcode == OpCodes.Brfalse)
            il.Emit(instruction.opcode, continuation);
        else if (instruction.opcode == OpCodes.Ret)
        {
            il.Emit(OpCodes.Ldloc, calls);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ret);
        }
        else il.Emit(instruction.opcode);
    }
    il.MarkLabel(continuation);
    il.Emit(OpCodes.Ldloc, calls);
    il.Emit(OpCodes.Ldc_I4, 11);
    il.Emit(OpCodes.Add);
    il.Emit(OpCodes.Ret);
    return method.CreateDelegate<Func<bool, bool, int>>()(scoped, allowed);
}

Console.WriteLine($"Projectile native contracts: {checks} checks in {cases} cases, {failures} failed. Assembly SHA-256 verified; no Unity/game methods executed.");
return failures == 0 ? 0 : 1;
