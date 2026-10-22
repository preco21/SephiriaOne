using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using HarmonyLib;

internal static class NativeContract
{
    internal static void Run(string managedDirectory, string addonPath)
    {
        string managed = Path.GetFullPath(managedDirectory);
        string gamePath = Path.Combine(managed, "Assembly-CSharp.dll");
        using (var stream = File.OpenRead(gamePath))
            if (Convert.ToHexString(SHA256.HashData(stream)) != "C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510")
                throw new Exception("Installed game fingerprint changed; re-audit the effect lifetime contract.");
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var game = Assembly.LoadFrom(gamePath);
        var addon = Assembly.LoadFrom(Path.GetFullPath(addonPath));
        var frost = game.GetType("CharacterBuff_StatusInstance_FrostTouch", true);
        var buff = game.GetType("CharacterBuff", true);
        var update = AccessTools.DeclaredMethod(frost, "OnUpdate_Server");
        var baseUpdate = AccessTools.DeclaredMethod(buff, "OnUpdate_Server");
        var enclosing = AccessTools.DeclaredMethod(buff, "Update");
        var destroy = AccessTools.DeclaredMethod(buff, "Destroy");
        var hooks = addon.GetType("SephiriaOne.NativeEffectLifetimeGuardHooks", true);
        var rewrite = AccessTools.DeclaredMethod(hooks, "Rewrite");
        var guard = AccessTools.DeclaredMethod(hooks, "CanRunFrostTouchFx");
        var original = PatchProcessor.GetOriginalInstructions(update).ToList();
        var rewritten = ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { original })).ToList();
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        Check(rewritten.Count == original.Count && rewritten.Count(i => i.Calls(guard)) == 1,
            "Guard must replace one existing FX gate without changing the method layout.");
        Check(rewritten.Count(i => i.Calls(baseUpdate)) == 1 && rewritten.FindIndex(i => i.Calls(baseUpdate)) < rewritten.FindIndex(i => i.Calls(guard)),
            "Native base update must still run once before the FX guard.");
        for (int i = 0; i < original.Count; i++)
        {
            if (rewritten[i].Calls(guard)) continue;
            Check(original[i].opcode == rewritten[i].opcode && Equals(original[i].operand, rewritten[i].operand),
                "Guard changed an unrelated instruction at " + i);
        }
        var outerCode = PatchProcessor.GetOriginalInstructions(enclosing).ToList();
        Check(outerCode.Any(i => i.Calls(baseUpdate)) && outerCode.Any(i => i.Calls(destroy)),
            "Native caller update/expiry contract changed.");
        var destroyCode = PatchProcessor.GetOriginalInstructions(destroy).ToList();
        Check(destroyCode.Any(i => i.operand is MethodInfo m && m.Name == "set_IsEndBuff") &&
            destroyCode.Any(i => i.operand is MethodInfo m && m.Name == "DestroyInner") &&
            destroyCode.Any(i => i.operand is MethodInfo m && m.Name == "DestroyDelay"),
            "Native end/status cleanup/delayed despawn contract changed.");
        var initialize = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.NativeEffectLifetimeGuard", true), "Initialize");
        var shutdown = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.NativeEffectLifetimeGuard", true), "Shutdown");
        var entry = addon.GetType("SephiriaOne.Entry", true);
        Check(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(entry, "OnModLoaded")).Any(i => i.Calls(initialize)) &&
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(entry, "OnModUnloaded")).Any(i => i.Calls(shutdown)),
            "Effect guard is not wired into the addon lifetime.");
        Console.WriteLine("Verified installed FrostTouch FX boundary, unchanged base/caller cleanup, exact one-instruction replacement and Entry lifecycle (static IL only).");
    }
}
