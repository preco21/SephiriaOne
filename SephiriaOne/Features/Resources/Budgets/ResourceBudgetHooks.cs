using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static class ResourceBudgetHooks
    {
        private const string Owner = "preco21.SephiriaOne.ResourceBudgets";
        private static Harmony harmony;
        public static bool Available { get; private set; }

        public static void Install()
        {
            if (harmony != null) return;
            var instance = new Harmony(Owner);
            try
            {
                Patch(instance, typeof(PlayerAvatar), nameof(PlayerAvatar.LoadPassiveStatOnServer),
                    new[] { typeof(PlayerAvatar.PassiveStatSaveData[]) }, nameof(BeforeTalentLoad));
                Patch(instance, typeof(DungeonManager), nameof(DungeonManager.LoadStageAndMove),
                    new[] { typeof(string) }, nameof(BeforeFirstDeparture), nameof(GuardFruitRead));
                var save = AccessTools.DeclaredMethod(typeof(PlayerSpawner), nameof(PlayerSpawner.SaveCurrentSessionData), Type.EmptyTypes);
                if (save == null || save.ReturnType != typeof(void))
                    throw new MissingMethodException("PlayerSpawner.SaveCurrentSessionData changed.");
                instance.Patch(save, postfix: new HarmonyMethod(typeof(ResourceBudgetHooks), nameof(AfterSave)));
                harmony = instance;
                Available = true;
            }
            catch
            {
                instance.UnpatchAll(Owner);
                Available = false;
                throw;
            }
        }

        public static void Uninstall()
        {
            Available = false;
            harmony?.UnpatchAll(Owner);
            harmony = null;
        }

        private static void Patch(Harmony instance, Type type, string name, Type[] parameters, string prefix, string transpiler = null)
        {
            var target = AccessTools.DeclaredMethod(type, name, parameters);
            if (target == null || target.ReturnType != typeof(void) || target.IsStatic)
                throw new MissingMethodException(type.Name + "." + name + " changed.");
            instance.Patch(target, prefix: new HarmonyMethod(typeof(ResourceBudgetHooks), prefix),
                transpiler: transpiler == null ? null : new HarmonyMethod(typeof(ResourceBudgetHooks), transpiler));
        }

        private static void BeforeTalentLoad(PlayerAvatar __instance, PlayerAvatar.PassiveStatSaveData[] data)
        {
            if (!NetworkServer.active || !__instance || !__instance.isServer) return;
            try
            {
                // TryGetSetting also establishes the early host/preset scope, before
                // normal player readiness. Unmanaged native selection behavior stays native.
                bool restored = TalentResourceCheckpoint.Restore(__instance);
                if (!restored && !Managed(__instance, ResourceKind.Talents)) return;
                int minimum = ResourceBudgets.RequiredTalentLoad(__instance, data);
                ResourceRuntime.ApplyEarly(__instance, ResourceKind.Talents, minimum);
                if (__instance.maxPassivePoint < minimum)
                    throw new InvalidOperationException("The saved talent selections require " + minimum +
                        " points, but the retained talent budget is " + __instance.maxPassivePoint + ". Raise the budget before retrying the load.");
            }
            catch (Exception error)
            {
                string message = "Talent load stopped before native selection clamping: " + error.Message;
                ResourceRuntime.Report(message);
                // Returning false would let CmdSetDefaultPlayerData continue initialization
                // with an empty/partial talent state, which a later native save can persist.
                throw new InvalidOperationException(message, error);
            }
        }

        private static void AfterSave(PlayerSpawner __instance)
        {
            try { TalentResourceCheckpoint.Save(__instance); }
            catch (Exception error)
            {
                ResourceRuntime.Report("Talent checkpoint could not be saved: " + error.Message);
                throw;
            }
        }

        private static void BeforeFirstDeparture(DungeonManager __instance)
        {
            if (!NetworkServer.active || !__instance || !__instance.isServer || __instance.isRunStarted) return;
            try
            {
                // Use the same connected identity set as the native grant loop. This
                // boundary is before LocalLoadStage or any first-run grants mutate state.
                foreach (var connection in NetworkServer.connections)
                {
                    var identity = connection.Value.identity;
                    if (!identity) continue;
                    PlayerAvatar player = identity.GetComponent<PlayerAvatar>();
                    if (!player) continue;
                    if (!Managed(player, ResourceKind.Fruit)) continue;
                    ResourceSnapshot before = ResourceBudgets.Capture(player, ResourceKind.Fruit);
                    ResourceRuntime.ApplyEarly(player, ResourceKind.Fruit, before.MinimumSafe);
                    if (!Managed(player, ResourceKind.Fruit)) continue;
                    ResourceSnapshot current = ResourceBudgets.Capture(player, ResourceKind.Fruit);
                    if (ResourceBudgets.Total(current) < current.MinimumSafe || ResourceBudgets.Total(current) < 0)
                        throw new InvalidOperationException("Player #" + player.netId +
                            " has more committed fruit-skewer selections than the budget permits. Raise the budget or edit the native selections before leaving.");
                    // A nonempty piece list at zero remaining allowance would otherwise
                    // grant its first piece: native code checks the limit after granting.
                }
            }
            catch (Exception error)
            {
                string message = "First departure stopped before native fruit-skewer grants: " + error.Message;
                ResourceRuntime.Report(message);
                throw new InvalidOperationException(message, error);
            }
        }

        private static IEnumerable<CodeInstruction> GuardFruitRead(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var nativeRead = AccessTools.DeclaredMethod(typeof(UnitAvatar), nameof(UnitAvatar.GetCustomStatUnsafe), new[] { typeof(string) });
            var guardedRead = AccessTools.DeclaredMethod(typeof(ResourceBudgetHooks), nameof(ReadFruitGrantStat));
            int matched = 0;
            for (int i = 1; i < code.Count; i++)
            {
                if (code[i - 1].opcode != OpCodes.Ldstr || !Equals(code[i - 1].operand, "FRUITCOUNT") || !code[i].Calls(nativeRead)) continue;
                code[i].opcode = OpCodes.Call;
                code[i].operand = guardedRead;
                matched++;
            }
            if (matched != 1)
                throw new InvalidOperationException("Expected one native fruit-skewer budget read; found " + matched + ".");
            return code;
        }

        private static int ReadFruitGrantStat(UnitAvatar owner, string key)
        {
            PlayerAvatar player = owner as PlayerAvatar;
            if (!NetworkServer.active || !player || !player.isServer || !Managed(player, ResourceKind.Fruit))
                return owner.GetCustomStatUnsafe(key);
            try
            {
                ResourceSnapshot before = ResourceBudgets.Capture(player, ResourceKind.Fruit);
                ResourceRuntime.ApplyEarly(player, ResourceKind.Fruit, before.MinimumSafe);
                ResourceSnapshot current = ResourceBudgets.Capture(player, ResourceKind.Fruit);
                int total = ResourceBudgets.Total(current);
                if (total < current.MinimumSafe || total < 0)
                    throw new InvalidOperationException("The native starting grants changed player #" + player.netId +
                        "'s fruit-skewer budget below the committed selection cost.");
                return owner.GetCustomStatUnsafe(key);
            }
            catch (Exception error)
            {
                string message = "Fruit-skewer grant stopped at the native budget read; the stage transition and earlier native grants may already have happened. " + error.Message;
                ResourceRuntime.Report(message);
                throw new InvalidOperationException(message, error);
            }
        }

        private static bool Managed(PlayerAvatar player, ResourceKind kind) =>
            ResourceRuntime.TryGetSetting(kind, out _) ||
            (player.customStats.TryGetValue(ResourceCatalog.Get(kind).Marker, out int owned) && owned != 0);
    }
}
