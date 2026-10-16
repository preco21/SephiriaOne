using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireWeaponTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Native(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Native(type), method)).ToList();
        bool Calls(CodeInstruction i, string type, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name;
        bool Field(CodeInstruction i, string name) => i.operand is FieldInfo f && f.Name == name;
        void Require(bool value, string why) { if (!value) throw new Exception("Weapon targeting contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.FriendlyFireWeaponHooks", true)!;
        List<CodeInstruction> Rewrite(string name, List<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, name).Invoke(null, new object[] { code })!).ToList();
        void Reject(string name, List<CodeInstruction> code)
        {
            bool rejected = false;
            try { Rewrite(name, code); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, name + " fails closed on changed native contract");
        }
        var selectors = ((IEnumerable<KeyValuePair<MethodInfo, string>>)AccessTools.Method(hooks, "Selectors").Invoke(null, null)!).ToArray();
        Require(selectors.Length == 5 && selectors.All(p => p.Key != null), "five audited weapon searches exist");
        foreach (var patch in selectors)
        {
            var code = PatchProcessor.GetOriginalInstructions(patch.Key).ToList();
            var mapped = Rewrite(patch.Value, code);
            string helper = patch.Value == "RewriteNearest" ? "OffensiveNearestPoint" : patch.Value == "RewriteMine" ? "WeaponMineTarget" : "ItemTarget";
            Require(mapped.Count == code.Count && mapped.Count(i => Calls(i, "FriendlyFireRuntime", helper)) == 1,
                patch.Key + " changes only its audited selector");
            int anchor = code.FindIndex(i => patch.Value == "RewriteNearest" ? Calls(i, "PlayerInputController", "SearchTargetNearestPoint") :
                Calls(i, "CombatManager", "ContainsAttackableFaction"));
            Require(anchor >= 0, patch.Key + " has native selector anchor");
            var missing = code.Select(i => new CodeInstruction(i)).ToList(); missing.RemoveAt(anchor); Reject(patch.Value, missing);
            if (patch.Value == "RewriteMine")
            {
                var changed = code.Select(i => new CodeInstruction(i)).ToList(); changed[anchor - 2] = new(OpCodes.Ldarg_0); Reject(patch.Value, changed);
                changed = code.Select(i => new CodeInstruction(i)).ToList(); changed[anchor - 3] = new(OpCodes.Ldc_I8, 1L); Reject(patch.Value, changed);
                changed = code.Select(i => new CodeInstruction(i)).ToList(); changed[anchor - 1] = new(OpCodes.Nop); Reject(patch.Value, changed);
                foreach (string fieldName in new[] { "checkTimer", "canBeTarget", "IsDead" })
                    Require(code.Any(i => Field(i, fieldName)), patch.Key.DeclaringType + " retains " + fieldName);
                Require(code.Any(i => Field(i, "YPos") || i.operand is MethodInfo m && m.Name == "get_YPos"), patch.Key.DeclaringType + " retains height check");
                Require(code.Any(i => Calls(i, "GameObject", "get_activeSelf")), patch.Key.DeclaringType + " retains active check");
                Require(code.Any(i => i.operand is MethodInfo m && m.Name == "DestroySelf"), patch.Key.DeclaringType + " retains native explosion handling");
            }
        }
        var blade = Code("WeaponSimple_Katana", "OnCreateMagicServerside");
        Require(blade.Any(i => Field(i, "useMagicBlade")) && blade.Any(i => Field(i, "magicBladeDamageId")) &&
            blade.Any(i => Field(i, "magicBladePrefab")) && blade.Any(i => i.LoadsConstant(5f)) && blade.Any(i => Calls(i, "Bullet", "SetHomingTarget")),
            "Adamah initial targeting retains native feature/range/assignment");
        var shield = Code("WeaponSimple_SwordAndShield", "Update");
        Require(shield.Any(i => Calls(i, "NetworkBehaviour", "get_isServer")) && shield.Any(i => Field(i, "solisMissioType")) &&
            shield.Any(i => Field(i, "throwSwordIntervalTimer")) && shield.Any(i => i.LoadsConstant("FLAMESWORD")) &&
            shield.Any(i => i.LoadsConstant("solisMissioThrowSwordDistance")) && shield.Any(i => Calls(i, "ComboEffect_FlameSword", "FireSword")),
            "Solis Missio native host, combo, timer and range gates remain");
        foreach (string mine in new[] { "BulletMoveModule_CrossbowMine", "BulletMoveModule_CrossbowMineHoming" })
            Require(Code(mine, "SetMotionData").Any(i => Field(i, "hostileLayers")), mine + " caches native mask at spawn");
        var mover = Code("BulletMoveModule_CrossbowMineHoming", "OnMove");
        Require(mover.Any(i => Calls(i, "Bullet", "get_HomingTarget")) && mover.Any(i => Calls(i, "BulletMoveModule_CrossbowMineHoming", "set_Networkvelocity")),
            "drone movement consumes current shared target and replicates native velocity");
        Require(AccessTools.PropertyGetter(Native("BulletMoveModule"), "Bullet").ReturnType == Native("Bullet"), "module exposes its current bullet");

        var ballista = Code("Unit_MiniBallista", "Attack");
        Require(ballista.Any(i => i.LoadsConstant("BasicAttack")) && ballista.Any(i => i.LoadsConstant("ENHANCEDMINIBALLISTA")) &&
            ballista.Any(i => i.LoadsConstant(0.125f)) && ballista.Count(i => Calls(i, "Bullet", "TurnOnHoming")) == 1,
            "Mini Ballista keeps enhancement gate and delayed homing");
        foreach (var (method, id) in new[] { ("CreateBasicAttackProjectile", "Weapon_BasicAttack"), ("CreateDashAttackProjectile", "Weapon_DashAttack"), ("CreateSpecialAttackProjectile", "Weapon_SpecialAttack") })
        {
            var code = Code("WeaponSimple", method);
            Require(code.Any(i => i.LoadsConstant(id)) && code.Any(i => Calls(i, "NewWeaponFireData", "CreateAttack")), "weapon emitter retains " + id);
        }
        foreach (string fire in new[] { "NewWeaponFireData_Bullet", "NewWeaponFireData_BulletBurst", "NewWeaponFireData_BulletSpread", "NewWeaponFireData_BulletSpread_ElementalScaled" })
        {
            var code = fire == "NewWeaponFireData_BulletBurst" ? PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(
                Native(fire).GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name.StartsWith("<AAA>")), "MoveNext")).ToList() : Code(fire, "InstantiateProjectile");
            Require(code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "ProjectileSpawner`1" && m.Name == "Spawn"),
                fire + " uses native pooled bullet identity/transport");
        }
        Console.WriteLine("Verified five weapon selectors, mine cached-mask pairing/rejection, native weapon emission, enhanced Ballista gate and drone synchronization (not live multiplayer).");
    }
}
