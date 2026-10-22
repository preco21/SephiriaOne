using System.Reflection;
using HarmonyLib;

if (args.Length != 0)
{
    if (args.Length != 2) throw new ArgumentException("Native contract arguments: <game Managed directory> <built addon DLL>");
    NativeContract.Run(args[0], args[1]);
    return;
}

int checks = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
var hooks = Assembly.GetExecutingAssembly().GetType("SephiriaOne.NativeEffectLifetimeGuardHooks");
void Install() => hooks?.GetMethod("Install", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);
void Uninstall() => hooks?.GetMethod("Uninstall", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);

Install(); Install();
if (UnityEngine.Debug.Warnings.Count != 0)
    foreach (var instruction in PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(typeof(CharacterBuff_StatusInstance_FrostTouch), "OnUpdate_Server")))
        Console.Error.WriteLine(instruction);
Check(hooks != null && (bool)hooks.GetProperty("Available", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null),
    "Real Harmony guard did not install: " + string.Join("; ", UnityEngine.Debug.Warnings));
var effect = new CharacterBuff_StatusInstance_FrostTouch();
effect.NetworkTarget.Destroyed = true;
Exception failure = null;
try { effect.Tick(); } catch (Exception error) { failure = error; }
Check(failure == null && effect.BaseCalls == 1 && effect.CleanupCalls == 1 && effect.FxCalls == 0,
    "Orphaned FrostTouch must skip its FX tail after running the base update and permit the caller cleanup tail");
Check(effect.TimerCalls == 0, "Orphaned FX cannot advance its native FX timers");

effect = new(); effect.OnBase = () => effect.NetworkTarget.Destroyed = true; effect.Tick();
Check(effect.BaseCalls == 1 && effect.CleanupCalls == 1 && effect.FxCalls == 0,
    "Target destroyed by a base callback cannot reach the subsequent transform access");
effect = new(); effect.OnBase = () => effect.NetworkTarget = null; effect.Tick();
Check(effect.BaseCalls == 1 && effect.CleanupCalls == 1 && effect.FxCalls == 0,
    "A target cleared by the preserved base update skips only derived FX");
effect = new(); effect.OnBase = () => effect.IsEndBuff = true; effect.Tick();
Check(effect.BaseCalls == 1 && effect.CleanupCalls == 1 && effect.FxCalls == 0,
    "A buff ended by base work cannot emit late FrostTouch FX");
effect = new(); effect.OnBase = () => effect.Destroyed = true; effect.Tick();
Check(effect.BaseCalls == 1 && effect.CleanupCalls == 1 && effect.FxCalls == 0,
    "A destroyed effect cannot emit its derived FX");

effect = new(); effect.Tick();
Check(effect.BaseCalls == 1 && effect.CleanupCalls == 1 && effect.TimerCalls == 1 && effect.FxCalls == 1 && effect.LastPosition == 42,
    "Valid native base, timer and FX behavior is unchanged");
effect = new() { Due = false }; effect.Tick();
Check(effect.TimerCalls == 1 && effect.FxCalls == 0 && effect.CleanupCalls == 1, "Native FX timer rejection is unchanged");
effect = new() { isServer = false }; effect.Tick();
Check(effect.BaseCalls == 1 && effect.TimerCalls == 0 && effect.FxCalls == 0, "Native server-only FX gating is unchanged");
effect = new(); effect.OnBase = () => throw new InvalidOperationException("base subscriber failure");
failure = null; try { effect.Tick(); } catch (Exception error) { failure = error; }
Check(failure is InvalidOperationException && effect.BaseCalls == 1 && effect.CleanupCalls == 0,
    "Unrelated native base exceptions remain visible and are not broadly suppressed");

var native = AccessTools.DeclaredMethod(typeof(CharacterBuff_StatusInstance_FrostTouch), "OnUpdate_Server");
var rewrite = hooks.GetMethod("Rewrite", BindingFlags.Static | BindingFlags.NonPublic);
foreach (string changed in new[] { "base", "server", "target", "transform", "fx" })
{
    var code = PatchProcessor.GetOriginalInstructions(native).ToList();
    string name = changed switch { "base" => "OnUpdate_Server", "server" => "get_isServer", "target" => "get_NetworkTarget",
        "transform" => "get_transform", _ => "RpcCreateFrostTouchFx" };
    code.RemoveAt(code.FindIndex(i => i.operand is MethodInfo m && m.Name == name));
    bool rejected = false;
    try { rewrite.Invoke(null, new object[] { code }); }
    catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
    Check(rejected, "Changed " + changed + " contract rejects the optional patch before installation");
}

Uninstall();
effect = new(); effect.NetworkTarget.Destroyed = true;
failure = null; try { effect.Tick(); } catch (Exception error) { failure = error; }
Check(failure is NullReferenceException && effect.BaseCalls == 1 && effect.TimerCalls == 1,
    "Uninstall restores the original native orphan access");
Console.WriteLine($"Passed {checks} native effect lifetime Harmony fixture checks.");
