using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameElementalStatTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var avatar = game.GetType("UnitAvatar", true)!;
        var stat = game.GetType("ECustomStat", true)!;
        var hooks = addon.GetType("SephiriaOne.FriendlyFireElementalHooks", true)!;
        var runtime = addon.GetType("SephiriaOne.PvpElementalRuntime", true)!;
        var converter = AccessTools.DeclaredMethod(avatar, "GetConvertedElementalDamage", new[] { typeof(int) });
        var code = PatchProcessor.GetOriginalInstructions(converter).ToList();
        bool Validate(IEnumerable<CodeInstruction> input) => (bool)AccessTools.Method(hooks, "ValidateConverter").Invoke(null, new object[] { input })!;
        void Require(bool value, string reason) { if (!value) throw new Exception("Elemental stat contract: " + reason); }
        bool Call(CodeInstruction i, string owner, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name;
        Require(converter.ReturnType == typeof(int) && !converter.IsStatic && Validate(code), "final converted integer boundary is recognized");
        Require(!Validate(code.Where(i => !Call(i, "UnitAvatar", "SelectElementalConversion"))), "changed conversion is rejected");
        Require(!Validate(code.Concat(new[] { new CodeInstruction(OpCodes.Stsfld, AccessTools.Field(avatar, "ElementalDamageKeys")) })),
            "new converter state mutation is rejected");
        var unsafeRead = AccessTools.DeclaredMethod(avatar, "GetCustomStatUnsafe", new[] { typeof(string) });
        var enumRead = AccessTools.DeclaredMethod(avatar, "GetCustomStat", new[] { stat });
        foreach (Type key in new[] { typeof(string), stat })
            Require(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "GetCustomStat", new[] { key })).Count(i => i.Calls(unsafeRead)) == 1,
                "both public readers reach the shared native conversion");
        Require(PatchProcessor.GetOriginalInstructions(unsafeRead).Count(i => i.Calls(converter)) == 1, "unsafe reader converts once");

        var index = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "ElementalIndexOf")).ToList();
        Require((bool)AccessTools.Method(hooks, "ValidateIndices").Invoke(null, new object[] { index })!, "physical/fire/ice/lightning map to indices 0/1/2/3");
        foreach (string key in new[] { "PHYSICALDAMAGE", "FIREDAMAGE", "ICEDAMAGE", "LIGHTNINGDAMAGE" })
            Require(index.Count(i => i.LoadsConstant(key)) == 1, "native element key " + key);
        var readers = ((IEnumerable<MethodInfo>)AccessTools.Method(hooks, "NativeReaders").Invoke(null, null)!).ToArray();
        Require(readers.Length == 11 && readers.Distinct().Count() == 11, "all audited derivation, progression and C-menu readers are preserved");
        Require(readers.Any(m => m.DeclaringType?.FullName == "MiniscriptGlobal+<>c"), "deferred script body is preserved, not registration only");
        var rewrite = AccessTools.Method(hooks, "RewriteNativeReads");
        foreach (var reader in readers)
        {
            var native = PatchProcessor.GetOriginalInstructions(reader).ToList();
            var changed = ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { native })!).ToList();
            Require(changed.Count == native.Count, reader + " retains native instruction count");
            Require(changed.Any(i => i.operand is MethodInfo m && m.DeclaringType == runtime), reader + " adapts native reads");
            for (int n = 0; n < native.Count; n++)
            {
                var a = native[n]; var b = changed[n];
                bool adapted = a.Calls(unsafeRead) || a.Calls(enumRead);
                Require(adapted ? b.opcode == OpCodes.Call && b.operand is MethodInfo helper && helper.DeclaringType == runtime &&
                    helper.Name == (a.Calls(unsafeRead) ? "ReadNativeUnsafe" : "ReadNativeEnum") :
                    a.opcode == b.opcode && Equals(a.operand, b.operand), reader + " changes only exact stat reads");
                Require(a.labels.SequenceEqual(b.labels) && a.blocks.SequenceEqual(b.blocks), reader + " retains branch and exception metadata");
            }
        }
        bool rejected = false;
        try { ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { new[] { new CodeInstruction(OpCodes.Ret) } })!).ToList(); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
        Require(rejected, "missing stat-read adapter cannot silently install");
        var install = PatchProcessor.GetOriginalInstructions(AccessTools.Method(addon.GetType("SephiriaOne.FriendlyFireHooks", true)!, "Install"));
        Require(install.Any(i => Call(i, "FriendlyFireElementalHooks", "Install")), "shared owned installation includes elemental limits");
        Console.WriteLine("Verified final elemental conversion, targeted native derivation/progression/UI reads, preserved IL and hook ownership (not live Unity).");
    }
}
