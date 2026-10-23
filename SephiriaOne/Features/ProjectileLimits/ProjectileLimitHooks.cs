using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static class ProjectileLimitHooks
    {
        private const string Id = "preco.sephiriaone.projectile-limits";
        internal sealed class Emitter
        {
            internal readonly MethodInfo Method;
            internal readonly string Source;
            internal readonly int Calls;
            internal readonly MemberInfo GuardTail;
            internal Emitter(Type type, string name, string source, int calls = 1, MemberInfo tail = null, bool iterator = false)
            {
                MethodInfo method = AccessTools.DeclaredMethod(type, name);
                Method = iterator ? AccessTools.DeclaredMethod(method?.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType, "MoveNext") : method;
                Source = source; Calls = calls; GuardTail = tail;
            }
        }
        internal static IEnumerable<Emitter> Emitters()
        {
            var fakeTarget = AccessTools.DeclaredMethod(typeof(BulletMoveModule_FireworkHoming), "TurnOnFakeTarget");
            yield return new Emitter(typeof(Charm_PallasCard), "OnBeginAttackAnimation", "pallas-card", tail: fakeTarget);
            yield return new Emitter(typeof(SpecialProjectile_FuryMP), "OnUpdateServer", "liberation",
                tail: AccessTools.Field(typeof(ProjectileBase), "swingId"));
            yield return new Emitter(typeof(Charm_NearMagicBullet), "AddBulletAttackCallback", "yearning-robe", tail: fakeTarget);
            yield return new Emitter(typeof(Charm_CreateBulletOnSweep), "CreateBullet", "ice-vine");
            yield return new Emitter(typeof(Charm_AirSlash), "AirSlashCoroutine", "ice-air-slash",
                tail: AccessTools.Field(typeof(ProjectileBase), "swingId"), iterator: true);
            yield return new Emitter(typeof(Charm_IceSpear), "FireSpearCoroutine", "ice-spear",
                tail: AccessTools.Field(typeof(ProjectileBase), "swingId"), iterator: true);
            yield return new Emitter(typeof(Charm_IceBow), "FireArrow", "ice-bow",
                tail: AccessTools.Field(typeof(ProjectileBase), "swingId"));
            // A rejected hammer must also skip activeBullets.Add and its throw RPC.
            yield return new Emitter(typeof(Charm_IceHammer), "HammerCoroutine", "ice-hammer",
                tail: AccessTools.DeclaredMethod(typeof(Charm_IceHammer), "RpcCreateHammer"), iterator: true);
            yield return new Emitter(typeof(Charm_ElectricEarring), "AttackCoroutine", "thunder-earring", iterator: true);
            yield return new Emitter(typeof(Charm_FireFeather), "OnUpdate", "phoenix-wing");
            yield return new Emitter(typeof(Charm_FlameGround_Meteor), "MeteorCoroutine", "meteor", iterator: true);
            yield return new Emitter(typeof(Charm_FlamePlantRoot), "CreateFlame", "flame-root");
            yield return new Emitter(typeof(Charm_RockElephant), "FireStoneBulletCoroutine", "rock-elephant", iterator: true);
            yield return new Emitter(typeof(Charm_Kunai), "CreateKunai", "kunai");
            yield return new Emitter(typeof(GreenBat), "CreateBullet", "planets", 4);
            yield return new Emitter(typeof(WeaponAddonGreatsword_FireNeedleBullet), "HandleAttackUnit", "weapon-spike");
            yield return new Emitter(typeof(Charm_FlameBall), "HandleSwing", "flame-ball");
            foreach (Type type in ChildTypes())
                yield return new Emitter(type, "OnDestroyRequestReceived", null);
        }
        private static IEnumerable<Type> ChildTypes()
        {
            yield return typeof(BulletDestroyModule_CreateOtherBullet);
            yield return typeof(BulletDestroyModule_Explode);
            yield return typeof(BulletDestroyModule_HeavyExplode);
            yield return typeof(BulletDestroyModule_CreateBulletSpread);
        }
        internal static MethodInfo Spawn => AccessTools.DeclaredMethod(typeof(ProjectileSpawner<Bullet>), "Spawn");
        internal static MethodInfo SpecialSpawn => AccessTools.DeclaredMethod(typeof(NewWeaponFireData_SpecialProjectile), "InstantiateProjectile");

        internal static void Install()
        {
            var harmony = new Harmony(Id);
            try
            {
                ValidateSpawnSignature();
                foreach (Emitter emitter in Emitters())
                {
                    if (emitter.Method == null) throw new MissingMethodException("Secondary emitter entry point missing: " + emitter.Source);
                    ValidateEmitter(PatchProcessor.GetOriginalInstructions(emitter.Method), emitter);
                }
                ValidateSpecial(PatchProcessor.GetOriginalInstructions(SpecialSpawn));
                ProjectileEffectRateHooks.Install(harmony);
                foreach (Emitter emitter in Emitters())
                    harmony.Patch(emitter.Method, transpiler: Hook(nameof(Rewrite)));
                foreach (Type type in ChildTypes())
                    harmony.Patch(Require(type, "OnDestroyRequestReceived"), prefix: Hook(nameof(BeforeChildren)), finalizer: Hook(nameof(AfterChildren)));
                harmony.Patch(Require(typeof(SpecialProjectile_FuryMP), "OnUpdateServer"),
                    prefix: Hook(nameof(BeforeLiberation)), finalizer: Hook(nameof(AfterLiberation)));
                harmony.Patch(Require(typeof(Bullet), "OnSpawn"), prefix: Hook(nameof(BeforeBullet)));
                harmony.Patch(Require(typeof(Bullet), "OnDespawn"), prefix: Hook(nameof(BeforeDespawn)));
                harmony.Patch(Require(typeof(SpecialProjectile_FuryMP), "OnDespawn"), prefix: Hook(nameof(BeforeDespawn)));
                harmony.Patch(SpecialSpawn, prefix: Hook(nameof(BeforeEmitter)), finalizer: Hook(nameof(AfterEmitter)), transpiler: Hook(nameof(RewriteSpecial)));
                harmony.Patch(Require(typeof(PlayerSpawner), "OnDestroy"), prefix: Hook(nameof(BeforePlayerDestroy)));
                harmony.Patch(Require(typeof(HorayNetworkManager), "OnServerDisconnect"), prefix: Hook(nameof(BeforeDisconnect)));
                harmony.Patch(Require(typeof(NetworkServer), "RemoveConnection"), prefix: Hook(nameof(BeforeRemoveConnection)));
                harmony.Patch(Require(typeof(NetworkServer), "AddConnection"), postfix: Hook(nameof(AfterAddConnection)));
                harmony.Patch(Require(typeof(HorayNetworkManager), "OnStopServer"), prefix: Hook(nameof(BeforeStop)));
            }
            catch { harmony.UnpatchAll(Id); throw; }
        }
        internal static void Uninstall() => new Harmony(Id).UnpatchAll(Id);
        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(ProjectileLimitHooks), name);
        private static MethodInfo Require(Type type, string name) =>
            AccessTools.DeclaredMethod(type, name) ?? throw new MissingMethodException(type.Name, name);

        internal static void ValidateSpawnSignature()
        {
            MethodInfo replacement = AccessTools.DeclaredMethod(typeof(ProjectileLimitRuntime), nameof(ProjectileLimitRuntime.Spawn));
            if (Spawn == null || Spawn.ReturnType != typeof(Bullet) ||
                !replacement.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    new[] { typeof(ProjectileSpawner<Bullet>) }.Concat(Spawn.GetParameters().Select(p => p.ParameterType)).Concat(new[] { typeof(string) })))
                throw new InvalidOperationException("Native secondary bullet spawn signature changed.");
        }

        internal static void ValidateEmitter(IEnumerable<CodeInstruction> instructions, Emitter emitter)
        {
            var code = instructions.ToList();
            var calls = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(Spawn)).ToArray();
            if (calls.Length != emitter.Calls)
                throw new InvalidOperationException("Native secondary spawn count changed: " + emitter.Method);
            if (calls.Any(i => i + 1 >= code.Count))
                throw new InvalidOperationException("Native secondary spawn has no result consumer: " + emitter.Method);
            if (emitter.GuardTail != null)
            {
                int tail = FindTail(code, emitter);
                if (calls.Length != 1 || tail <= calls[0] || tail + 1 >= code.Count ||
                    code.Skip(calls[0]).Take(tail - calls[0] + 1).Any(i => i.blocks.Count != 0))
                    throw new InvalidOperationException("Native secondary post-spawn guard changed: " + emitter.Method);
                if (emitter.Method.DeclaringType == typeof(SpecialProjectile_FuryMP))
                {
                    var progress = AccessTools.Field(typeof(SpecialProjectile_FuryMP), "spawnedBulletCount");
                    if (progress == null || !code.Skip(tail + 1).Any(i => i.StoresField(progress)))
                        throw new InvalidOperationException("Native Liberation emission progress changed.");
                }
            }
            else
            {
                foreach (int call in calls)
                {
                    // Only a discarded result or native null-guarded orbit admission.
                    if (code[call + 1].opcode != OpCodes.Pop && emitter.Method.DeclaringType != typeof(Charm_CreateBulletOnSweep))
                        throw new InvalidOperationException("Unverified secondary spawn result consumer: " + emitter.Method);
                    if (emitter.Method.DeclaringType == typeof(Charm_CreateBulletOnSweep) &&
                        !code.Skip(call + 1).Take(6).Any(i => i.operand is MethodInfo m && m.Name == "op_Implicit" && m.DeclaringType == typeof(UnityEngine.Object)))
                        throw new InvalidOperationException("Native Ice Vine null guard changed.");
                }
            }
        }
        private static int FindTail(List<CodeInstruction> code, Emitter emitter)
        {
            int[] tails = Enumerable.Range(0, code.Count).Where(i =>
                emitter.GuardTail is MethodInfo method ? code[i].Calls(method) :
                emitter.GuardTail is FieldInfo field && code[i].StoresField(field)).ToArray();
            if (tails.Length != 1) throw new InvalidOperationException("Native secondary guard tail changed: " + emitter.Method);
            return tails[0];
        }
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
        {
            Emitter emitter = Emitters().Single(e => e.Method == __originalMethod);
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            ValidateEmitter(code, emitter);
            Label continuation = default;
            if (emitter.GuardTail != null)
            {
                continuation = generator.DefineLabel();
                code[FindTail(code, emitter) + 1].labels.Add(continuation);
            }
            var replacement = AccessTools.DeclaredMethod(typeof(ProjectileLimitRuntime), nameof(ProjectileLimitRuntime.Spawn));
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i];
                if (!instruction.Calls(Spawn)) { yield return instruction; continue; }
                // All original arguments including the spawner remain on the stack.
                var source = emitter.Source == null ? new CodeInstruction(OpCodes.Ldnull) : new CodeInstruction(OpCodes.Ldstr, emitter.Source);
                source.MoveLabelsFrom(instruction); source.MoveBlocksFrom(instruction);
                yield return source;
                yield return new CodeInstruction(OpCodes.Call, replacement);
                if (emitter.GuardTail == null) continue;
                Label accepted = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Brtrue, accepted);
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Br, continuation);
                var resume = new CodeInstruction(OpCodes.Nop); resume.labels.Add(accepted);
                yield return resume;
            }
        }

        internal static void ValidateSpecial(IEnumerable<CodeInstruction> instructions)
        {
            var spawn = AccessTools.DeclaredMethod(typeof(ProjectileBase), "OnSpawn");
            if (spawn == null || instructions.Count(i => i.Calls(spawn)) != 1)
                throw new InvalidOperationException("Native Liberation controller initialization changed.");
        }
        internal static IEnumerable<CodeInstruction> RewriteSpecial(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            ValidateSpecial(code);
            var spawn = AccessTools.DeclaredMethod(typeof(ProjectileBase), "OnSpawn");
            foreach (var instruction in code)
            {
                if (instruction.Calls(spawn))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.DeclaredMethod(typeof(ProjectileLimitRuntime), nameof(ProjectileLimitRuntime.SpawnEmitter));
                }
            }
            return code;
        }

        private static void BeforeBullet(Bullet __instance) => ProjectileLimitRuntime.BeforeSpawn(__instance);
        private static void BeforeDespawn(ProjectileBase __instance) => ProjectileLimitRuntime.Despawned(__instance);
        private static void BeforeChildren(BulletDestroyModule __instance, out ProjectileLimitRuntime.Provenance __state) =>
            __state = ProjectileLimitRuntime.BeginChildren(__instance);
        private static Exception AfterChildren(Exception __exception, ProjectileLimitRuntime.Provenance __state)
        { ProjectileLimitRuntime.EndChildren(__state); return __exception; }
        private static void BeforeLiberation(SpecialProjectile_FuryMP __instance, out ProjectileLimitRuntime.Provenance __state) =>
            __state = ProjectileLimitRuntime.BeginLiberation(__instance);
        private static Exception AfterLiberation(Exception __exception, ProjectileLimitRuntime.Provenance __state)
        { ProjectileLimitRuntime.EndLiberation(__state); return __exception; }
        private static bool BeforeEmitter(NewWeaponFireData_SpecialProjectile __instance, UnitAvatar owner, out ProjectileLimitRuntime.Admission __state) =>
            ProjectileLimitRuntime.BeginEmitter(__instance, owner, out __state);
        private static Exception AfterEmitter(Exception __exception, ProjectileLimitRuntime.Admission __state)
        { ProjectileLimitRuntime.EndEmitter(__state); return __exception; }
        private static void BeforePlayerDestroy(PlayerSpawner __instance) => ProjectileLimitRuntime.RetireSpawner(__instance);
        private static void BeforeDisconnect(NetworkConnectionToClient conn) => ProjectileLimitRuntime.RetireConnection(conn);
        private static void BeforeRemoveConnection(int connectionId)
        {
            if (NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient connection))
                ProjectileLimitRuntime.RetireConnection(connection);
        }
        private static void BeforeStop() => ProjectileLimitRuntime.Clear();
        private static void AfterAddConnection(NetworkConnectionToClient conn, bool __result)
        { if (__result) ProjectileLimitRuntime.ConnectionAdded(conn); }
    }
}
