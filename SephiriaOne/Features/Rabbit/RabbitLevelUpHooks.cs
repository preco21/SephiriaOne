using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;
using Random = System.Random;

namespace SephiriaOne
{
    internal static class RabbitLevelUpHooks
    {
        private const string Owner = "preco21.SephiriaOne.RabbitLevelUp";
        private static readonly Random random = new Random();
        private static Harmony harmony;
        private static AccessTools.FieldRef<GridInventory, bool> hasPermission;
        public static bool Available { get; private set; }

        // No level ledger: only the earned-level call site is wrapped. Native save
        // restoration and reward-queue reconstruction cannot replay this operation.
        private sealed class Reward
        {
            public LevelController Controller;
            public PlayerAvatar Player;
            public PlayerSpawner Spawner;
            public NetworkConnectionToClient Connection;
            public GridInventory Inventory;
            public DungeonManager Dungeon;
            public object Run;
            public long Generation;
            public string Floor;
            public int Level;
        }

        public static void Install()
        {
            Uninstall();
            try
            {
                var method = AccessTools.DeclaredMethod(typeof(LevelController), "LocalAddExp", new[] { typeof(int) });
                if (!ValidateEarnedLevelShape(method))
                    throw new InvalidOperationException("Native earned-level call site changed.");
                hasPermission = AccessTools.FieldRefAccess<GridInventory, bool>("writePermission");
                harmony = new Harmony(Owner);
                harmony.Patch(method, transpiler: new HarmonyMethod(typeof(RabbitLevelUpHooks), nameof(WrapEarnedLevel)));
                Available = true;
            }
            catch (Exception error)
            {
                Uninstall();
                Debug.LogWarning("[SephiriaOne] Rabbit level-up hooks unavailable: " + error);
            }
        }

        public static void Uninstall()
        {
            Available = false;
            hasPermission = null;
            try { harmony?.UnpatchAll(Owner); harmony = null; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Rabbit level-up hook removal failed: " + error); }
        }

        private static bool ValidateEarnedLevelShape(MethodInfo method) => method != null &&
            !method.IsStatic && method.ReturnType == typeof(void) &&
            ValidateEarnedLevelCode(PatchProcessor.GetOriginalInstructions(method).ToList());

        private static bool ValidateEarnedLevelCode(List<CodeInstruction> code)
        {
            var native = AccessTools.DeclaredMethod(typeof(LevelController), nameof(LevelController.LevelUpOnServer), Type.EmptyTypes);
            var setter = AccessTools.PropertySetter(typeof(LevelController), nameof(LevelController.NetworkcurrentLevel));
            int call = code.FindIndex(i => i.Calls(native));
            // Require the exact level += 1 write before the call, plus native XP
            // threshold reads. Preserve all labels/exception blocks when replacing.
            return native != null && setter != null && call >= 6 && code.Count(i => i.Calls(native)) == 1 &&
                code[call - 1].opcode == OpCodes.Ldarg_0 && code[call - 2].Calls(setter) &&
                code[call - 3].opcode == OpCodes.Add && code[call - 4].opcode == OpCodes.Ldc_I4_1 &&
                code[call - 5].opcode == OpCodes.Ldfld && code[call - 5].operand is FieldInfo level &&
                level.DeclaringType == typeof(LevelController) && level.Name == nameof(LevelController.currentLevel) &&
                code.Any(i => i.operand is FieldInfo table && table.DeclaringType == typeof(LevelController) &&
                    table.Name == nameof(LevelController.ExpTableByLevel)) &&
                code.Any(i => i.operand is FieldInfo exp && exp.DeclaringType == typeof(LevelController) &&
                    exp.Name == nameof(LevelController.currentExp));
        }

        private static IEnumerable<CodeInstruction> WrapEarnedLevel(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            if (!ValidateEarnedLevelCode(code)) throw new InvalidOperationException("Unsafe earned-level IL shape.");
            var call = code.Single(i => i.Calls(AccessTools.DeclaredMethod(typeof(LevelController), nameof(LevelController.LevelUpOnServer))));
            call.opcode = OpCodes.Call;
            call.operand = AccessTools.DeclaredMethod(typeof(RabbitLevelUpHooks), nameof(CompleteEarnedLevel));
            return code;
        }

        private static void CompleteEarnedLevel(LevelController controller)
        {
            Reward reward = null;
            try { reward = Capture(controller); }
            catch (Exception error) { Fail(error); }
            // Native failures retain their original behavior. No reward is issued
            // until all native level-up callbacks have returned successfully.
            controller.LevelUpOnServer();
            if (reward == null) return;
            try
            {
                if (!Current(reward)) return;
                int id = RabbitLevelUpCatalog.Pick(random);
                int instance = ItemDatabase.GenerateInstanceID(random);
                // Native Permission is not reentrant: disposing an inner scope
                // would close the caller's still-active inventory transaction.
                using (hasPermission(reward.Inventory) ? null : new GridInventory.Permission(reward.Inventory))
                {
                    if (!Current(reward) || !hasPermission(reward.Inventory)) return;
                    if (!reward.Inventory.LocalAddItem(instance, id, 1, 0, notification: true, isReward: false) && Current(reward))
                        reward.Inventory.temporaryInventory.Add(new ItemMetadata(instance, id, 1));
                }
            }
            catch (Exception error)
            {
                // A native write may already have occurred. Never retry or create
                // an overflow copy on exception; disable only this optional hook.
                Fail(error);
            }
        }

        private static Reward Capture(LevelController controller)
        {
            if (!Available || !RabbitLevelUpCatalog.Available || !NetworkServer.active ||
                !SessionSettings.RabbitPotionsForUse.LevelUpPotion || !controller || controller.currentLevel <= 1) return null;
            var player = controller.Avatar;
            var spawner = player ? player.spawner : null;
            var dungeon = DungeonManager.Instance;
            if (!dungeon || !dungeon.isServer || dungeon.netId == 0 || !Ready(controller, player, spawner)) return null;
            return new Reward { Controller = controller, Player = player, Spawner = spawner,
                Connection = spawner.connectionToClient, Inventory = player.Inventory, Dungeon = dungeon,
                Run = SaveManager.CurrentRun, Generation = SessionSettings.ResourceGeneration,
                Floor = player.currentFloorGuid, Level = controller.currentLevel };
        }

        private static bool Ready(LevelController controller, PlayerAvatar player, PlayerSpawner spawner)
        {
            if (!controller || !controller.isServer || controller.netId == 0 || !player || player.IsDead ||
                player.currentCostume != "HolyRabbit" || !spawner || !ReferenceEquals(controller.Avatar, player) ||
                !ReferenceEquals(spawner.PlayerAvatar, player) || !HostStateAdapter.IsReady(spawner) ||
                !PlayerSpawner.MultiplayerList.Contains(spawner)) return false;
            var connection = spawner.connectionToClient;
            if (connection == null || !connection.isReady || connection.identity == null ||
                !connection.identity.TryGetComponent<PlayerSpawner>(out var owner) || !ReferenceEquals(owner, spawner) ||
                !ReferenceEquals(controller.connectionToClient, connection)) return false;
            foreach (var entry in NetworkServer.connections)
                if (ReferenceEquals(entry.Value, connection)) return true;
            return false;
        }

        private static bool Current(Reward reward) => Available && RabbitLevelUpCatalog.Available && NetworkServer.active &&
            SessionSettings.RabbitPotionsForUse.LevelUpPotion &&
            ReferenceEquals(SaveManager.CurrentRun, reward.Run) && SessionSettings.ResourceGeneration == reward.Generation &&
            ReferenceEquals(DungeonManager.Instance, reward.Dungeon) && reward.Dungeon && reward.Dungeon.isServer &&
            Ready(reward.Controller, reward.Player, reward.Spawner) &&
            ReferenceEquals(reward.Spawner.connectionToClient, reward.Connection) &&
            ReferenceEquals(reward.Player.Inventory, reward.Inventory) &&
            reward.Player.currentFloorGuid == reward.Floor && reward.Controller.currentLevel >= reward.Level;

        private static void Fail(Exception error)
        {
            if (!Available) return;
            Available = false;
            Debug.LogWarning("[SephiriaOne] Rabbit level-up rewards disabled after an error; native leveling continues. No grant will be retried: " + error);
        }
    }
}
