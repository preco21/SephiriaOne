using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static class InventoryResourceHooks
    {
        private const string Owner = "preco21.SephiriaOne.ResourceInventory";
        private static Harmony harmony;
        [ThreadStatic] private static Stack<Initialization> initializations;

        private sealed class Initialization
        {
            public readonly PlayerSpawner Spawner;
            public readonly PlayerAvatar Player;
            public readonly GridInventory Inventory;
            public readonly SaveData Run;
            public readonly string Guid;
            public readonly int Slot;
            public readonly uint NetId;
            public readonly long Generation;
            public bool Applied;
            public Initialization(PlayerSpawner spawner)
            {
                Spawner = spawner; Player = spawner.PlayerAvatar; Inventory = Player.Inventory;
                Run = SaveManager.CurrentRun; Guid = spawner.playerGuid; Slot = spawner.currentPlayerIdxForSave; NetId = Player.netId;
                Generation = SessionSettings.ResourceGeneration;
            }
            public bool IsCurrent() => NetworkServer.active && Spawner && Spawner.isServer && Player && Player.isServer && Inventory && Inventory.isServer &&
                Player.netId == NetId && NetId != 0 && ReferenceEquals(Spawner.PlayerAvatar, Player) && ReferenceEquals(Player.Inventory, Inventory) &&
                ReferenceEquals(SaveManager.CurrentRun, Run) && SessionSettings.ResourceGeneration == Generation &&
                Spawner.playerGuid == Guid && Spawner.currentPlayerIdxForSave == Slot;
        }

        public static bool Available { get; private set; }

        public static void Install()
        {
            if (harmony != null) return;
            var instance = new Harmony(Owner);
            try
            {
                var initialize = AccessTools.DeclaredMethod(typeof(PlayerSpawner), "Initialize", new[] { typeof(int), typeof(string), typeof(string), typeof(int) });
                var save = AccessTools.DeclaredMethod(typeof(PlayerSpawner), nameof(PlayerSpawner.SaveCurrentSessionData), Type.EmptyTypes);
                if (initialize == null || initialize.ReturnType != typeof(bool) || save == null || save.ReturnType != typeof(void))
                    throw new MissingMethodException("The native inventory initialization/save boundary changed.");
                instance.Patch(initialize, prefix: new HarmonyMethod(typeof(InventoryResourceHooks), nameof(Begin)),
                    transpiler: new HarmonyMethod(typeof(InventoryResourceHooks), nameof(GuardCapacityRead)),
                    finalizer: new HarmonyMethod(typeof(InventoryResourceHooks), nameof(End)));
                instance.Patch(save, postfix: new HarmonyMethod(typeof(InventoryResourceHooks), nameof(AfterSave)));
                harmony = instance;
                Available = true;
            }
            catch { instance.UnpatchAll(Owner); Available = false; throw; }
        }

        public static void Uninstall()
        {
            if (harmony == null) return;
            ResourceRuntime.TryGetSetting(ResourceKind.Slots, out _);
            if (SessionSettings.ResourceWritesBlocked)
                throw new InvalidOperationException("Resolve the pending state-write recovery before unloading inventory guards.");
            if (initializations != null && initializations.Count != 0)
                throw new InvalidOperationException("Cannot unload resource guards while an inventory is being restored.");
            if (NetworkServer.active)
            {
                var owners = new List<Initialization>();
                foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                {
                    if (!spawner || !spawner.isServer || !spawner.PlayerAvatar || !spawner.PlayerAvatar.isServer) continue;
                    if (!spawner.PlayerAvatar.customStats.TryGetValue(ResourceCatalog.Get(ResourceKind.Slots).Marker, out int owned) || owned == 0) continue;
                    if (!HostStateAdapter.IsReady(spawner))
                        throw new InvalidOperationException("Resource unload must wait until every owned inventory has finished native initialization.");
                    var owner = new Initialization(spawner);
                    RequireKey(owner.Spawner);
                    if (owner.Run == null) throw new InvalidOperationException("The native run save is unavailable; inventory ownership cannot be checkpointed before unload.");
                    owners.Add(owner);
                }
                var batch = new StateWriteBatch(() => owners.TrueForAll(owner => owner.IsCurrent() && HostStateAdapter.IsReady(owner.Spawner)));
                foreach (Initialization owner in owners)
                {
                    ResourceSnapshot snapshot = InventoryResources.Capture(owner.Player);
                    int native = checked(snapshot.Raw - snapshot.Owned);
                    InventoryResources.AddWrites(batch, owner.Player, new ResourceUpdate(snapshot.Definition, native, 0, native));
                }
                // Refresh the native item-position snapshot while the old capacity
                // is still intact. A capacity-only checkpoint could otherwise hide
                // items whose old saved positions are now empty in the live grid.
                foreach (Initialization owner in owners) owner.Spawner.SaveCurrentSessionData();
                if (!batch.TryCommit(out string error))
                {
                    if (batch.MayHaveWritten) SessionSettings.RecordFault("resources", batch, error);
                    throw new InvalidOperationException("Resource unload refused: " + error);
                }
                foreach (Initialization owner in owners) SaveCheckpoint(owner.Spawner, true);
            }
            harmony.UnpatchAll(Owner);
            harmony = null;
            Available = false;
            initializations?.Clear();
        }

        private static void Begin(PlayerSpawner __instance, out Initialization __state)
        {
            __state = null;
            if (!NetworkServer.active || !__instance || !__instance.isServer) return;
            ResourceRuntime.TryGetSetting(ResourceKind.Slots, out _);
            __state = new Initialization(__instance);
            if (initializations == null) initializations = new Stack<Initialization>();
            initializations.Push(__state);
        }

        private static Exception End(Exception __exception, Initialization __state)
        {
            if (__state != null && initializations != null && initializations.Count != 0 && ReferenceEquals(initializations.Peek(), __state))
                initializations.Pop();
            return __exception;
        }

        private static IEnumerable<CodeInstruction> GuardCapacityRead(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var field = AccessTools.Field(typeof(GridInventory), nameof(GridInventory.CurrentInventoryStorage));
            var read = AccessTools.DeclaredMethod(typeof(InventoryResourceHooks), nameof(ReadCapacity));
            int matches = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.opcode != OpCodes.Ldfld || !Equals(instruction.operand, field)) continue;
                instruction.opcode = OpCodes.Call;
                instruction.operand = read;
                matches++;
            }
            if (matches != 1) throw new InvalidOperationException("Expected one native inventory restore capacity read; found " + matches + ".");
            return code;
        }

        private static short ReadCapacity(GridInventory inventory)
        {
            if (!NetworkServer.active || initializations == null || initializations.Count == 0) return inventory.CurrentInventoryStorage;
            Initialization context = initializations.Peek();
            try
            {
                if (!context.IsCurrent() || !ReferenceEquals(inventory, context.Inventory))
                    throw new InvalidOperationException("The inventory restore owner or saved run changed.");
                if (!context.Applied)
                {
                    RestoreBeforeItems(context);
                    context.Applied = true;
                }
                return inventory.CurrentInventoryStorage;
            }
            catch (Exception error)
            {
                string message = "Inventory initialization stopped before saved item enumeration. Earlier native initialization may already have happened; restart/reconnect after correcting the setting. " + error.Message;
                ResourceRuntime.Report(message);
                throw new InvalidOperationException(message, error);
            }
        }

        private static void RestoreBeforeItems(Initialization context)
        {
            string key = Key(context.Spawner);
            SaveData run = context.Run;
            if (run != null && key != null && run.ContainsKey(key + "Version"))
            {
                if (SessionSettings.ResourceWritesBlocked)
                    throw new InvalidOperationException("Resolve the pending state-write recovery before restoring inventory capacity.");
                if (run.GetInt(key + "Version", 0) != 1 || !run.ContainsKey(key + "Total") || !run.ContainsKey(key + "Owned"))
                    throw new InvalidOperationException("The addon inventory checkpoint is incomplete or unsupported.");
                int saved = run.GetInt(key + "Total", -1);
                int savedOwned = run.GetInt(key + "Owned", 0);
                InventoryResources.ValidateCapacity(context.Inventory, saved);
                ResourceSnapshot before = InventoryResources.Capture(context.Player);
                int native = checked(before.Raw - before.Owned);
                if (native < 0) throw new InvalidOperationException("The inventory ownership baseline is invalid.");
                int target = Math.Max(saved, checked(native + savedOwned));
                int owned = checked(target - native);
                var batch = new StateWriteBatch(context.IsCurrent);
                InventoryResources.AddWrites(batch, context.Player, new ResourceUpdate(before.Definition, target, owned, target));
                if (!batch.TryCommit(out string error))
                {
                    if (batch.MayHaveWritten) SessionSettings.RecordFault("resources", batch, error);
                    throw new InvalidOperationException(error);
                }
                ResourceRuntime.AcceptRestored(context.Player, ResourceKind.Slots);
                return;
            }
            if (ResourceRuntime.TryGetSetting(ResourceKind.Slots, out _)) RequireKey(context.Spawner);
            ResourceRuntime.ApplyEarly(context.Player, ResourceKind.Slots);
        }

        private static void AfterSave(PlayerSpawner __instance)
        {
            if (!NetworkServer.active || !__instance || !__instance.isServer) return;
            try { SaveCheckpoint(__instance, false); }
            catch (Exception error)
            {
                ResourceRuntime.Report("Inventory checkpoint could not be saved: " + error.Message);
                throw;
            }
        }

        private static void SaveCheckpoint(PlayerSpawner spawner, bool force)
        {
            string key = Key(spawner);
            SaveData run = SaveManager.CurrentRun;
            bool exists = key != null && run != null && run.ContainsKey(key + "Version");
            spawner.PlayerAvatar.customStats.TryGetValue(ResourceCatalog.Get(ResourceKind.Slots).Marker, out int owned);
            if (!force && owned == 0 && !exists && !ResourceRuntime.TryGetSetting(ResourceKind.Slots, out _)) return;
            ResourceSnapshot snapshot = InventoryResources.Capture(spawner.PlayerAvatar);
            key = RequireKey(spawner);
            if (run == null) throw new InvalidOperationException("The native run save is unavailable.");
            run.SetInt(key + "Total", snapshot.Raw);
            run.SetInt(key + "Owned", snapshot.Owned);
            run.SetInt(key + "Version", 1);
        }

        private static string RequireKey(PlayerSpawner spawner) => Key(spawner) ??
            throw new InvalidOperationException("A resolved native player GUID and save slot are required to preserve expanded inventory capacity.");

        private static string Key(PlayerSpawner spawner)
        {
            if (spawner.currentPlayerIdxForSave < 0 || string.IsNullOrWhiteSpace(spawner.playerGuid)) return null;
            return "SephiriaOne.ResourceSlots.Player" + spawner.currentPlayerIdxForSave.ToString(CultureInfo.InvariantCulture) + "." +
                Convert.ToBase64String(Encoding.UTF8.GetBytes(spawner.playerGuid)) + ".";
        }
    }
}
