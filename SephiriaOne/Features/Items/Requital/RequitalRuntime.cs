using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class RequitalRuntime
    {
        internal const int ItemId = 2100;
        private const string Marker = "SephiriaOne_RequitalStartingOwner";
        private const string RetiredMarker = "SephiriaOne_RequitalRetiredOwner";
        private const string Rule = "requital-starting-items";
        private static readonly FieldInfo permission = typeof(GridInventory).GetField("writePermission", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo pending = typeof(GridInventory).GetField("additionFailedStartingItems", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly System.Random random = new System.Random();
        private static readonly ConditionalWeakTable<GridInventory, Ownership> ownership = new ConditionalWeakTable<GridInventory, Ownership>();
        private static readonly ReconciliationCoordinator<FriendlyFireParticipants.Receipt> coordinator = Create();
        private static readonly HashSet<FriendlyFireParticipants.Receipt> tracked = new HashSet<FriendlyFireParticipants.Receipt>();
        private static readonly HashSet<FriendlyFireParticipants.Receipt> current = new HashSet<FriendlyFireParticipants.Receipt>();
        private static readonly List<FriendlyFireParticipants.Receipt> roster = new List<FriendlyFireParticipants.Receipt>();
        private static readonly List<FriendlyFireParticipants.Receipt> departed = new List<FriendlyFireParticipants.Receipt>();
        private static bool hooks, itemAvailable, ticking, writing;
        private static long scope;
        private static long catalogRevision;
        private static DungeonManager retirementDungeon;
        private static RequitalSettings retirementSettings;
        private static bool retirementObserved;

        // Native restart erases global metadata before RestockStartingItem, but
        // retains the native inventory and starting registrations. Weak receipts
        // bridge only that lifetime; reconnects recover saved exact-ID markers.
        private sealed class Ownership
        {
            internal readonly DungeonManager Dungeon = DungeonManager.Instance;
            internal readonly string Guid;
            internal readonly List<int> Ids = new List<int>();
            internal bool Faulted;
            internal Ownership(string guid) { Guid = guid; }
        }

        internal static bool Available => hooks && itemAvailable;
        internal static bool CanExecute => Available && NetworkServer.active && !writing &&
            SessionSettings.EnsureResourceScope() && ReviveAllAction.IsRunOpen(DungeonManager.Instance, SaveManager.CurrentRun);

        internal static void Initialize()
        {
            if (hooks) return;
            try { HarmonyRuntime.EnsureLoaded(); Install(); hooks = true; }
            catch (Exception error) { Warn("hooks unavailable", error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => RequitalHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => RequitalHooks.Uninstall();

        internal static void Load()
        {
            try { itemAvailable = ValidateFields() && ValidItem(); }
            catch (Exception error) { itemAvailable = false; Warn("database unavailable", error); }
            if (!itemAvailable) Debug.LogWarning("[SephiriaOne] Requital identity/inventory contract unavailable.");
            catalogRevision++;
        }
        private static bool ValidItem()
        {
            var item = ItemDatabase.FindItemById(ItemId);
            return item && item.id == ItemId && item.aName?.key == "Item_StoneTablet_Requital_Name" &&
                item.type == EItemType.StoneTablet && item.rarity == EItemRarity.Eternal &&
                item.activeType == EItemActiveType.WitchHat && item.resourcePrefab;
        }
        internal static bool ValidateFields() => permission?.FieldType == typeof(bool) &&
            pending?.FieldType == typeof(List<GridInventory.StartingItemAdditionFailedInfo>);

        private static ReconciliationCoordinator<FriendlyFireParticipants.Receipt> Create()
        {
            var result = new ReconciliationCoordinator<FriendlyFireParticipants.Receipt>();
            result.Register(ReconciliationRule<FriendlyFireParticipants.Receipt>.ObserveValue(Rule,
                SyncDomain.Identity, SyncDomain.None, ReconcileMode.OnChange, receipt => true,
                receipt => (SessionSettings.Requital, receipt.Player.Inventory, catalogRevision), Apply));
            return result;
        }

        internal static void Clear()
        {
            coordinator.Clear(); tracked.Clear(); retirementObserved = false; scope++;
            if (!ticking) { current.Clear(); roster.Clear(); departed.Clear(); }
        }

        internal static void Tick()
        {
            if (ticking || writing || !NetworkServer.active || !DungeonManager.Instance || permission == null ||
                !SessionSettings.EnsureResourceScope()) return;
            ticking = true;
            try
            {
                foreach (var spawner in PlayerSpawner.MultiplayerList)
                {
                    var receipt = spawner ? FriendlyFireParticipants.Capture(spawner.PlayerAvatar) : null;
                    if (receipt != null && current.Add(receipt)) roster.Add(receipt);
                }
                foreach (var receipt in tracked) if (!current.Contains(receipt)) departed.Add(receipt);
                foreach (var receipt in departed) { coordinator.Forget(receipt); tracked.Remove(receipt); }
                long observedScope = scope;
                foreach (var receipt in roster)
                {
                    if (scope != observedScope) break;
                    tracked.Add(receipt); coordinator.Reconcile(receipt);
                }
            }
            finally { current.Clear(); roster.Clear(); departed.Clear(); ticking = false; }
        }

        private static Ownership GetOwnership(PlayerAvatar player)
        {
            var inventory = player.Inventory;
            string guid = player.spawner.playerGuid;
            if (ownership.TryGetValue(inventory, out var value) &&
                (!ReferenceEquals(value.Dungeon, DungeonManager.Instance) || value.Guid != guid))
                ownership.Remove(inventory);
            return ownership.GetValue(inventory, _ => new Ownership(guid));
        }

        private static ReconcileResult Apply(FriendlyFireParticipants.Receipt receipt)
        {
            // Readiness must not be the rule predicate: a later loading frame
            // would reset an applied/faulted cursor and replay a one-shot write.
            if (writing || !FriendlyFireParticipants.IsCurrent(receipt) || !HostStateAdapter.IsReady(receipt.Spawner) ||
                !receipt.Player.Inventory || string.IsNullOrWhiteSpace(receipt.Spawner.playerGuid))
                return ReconcileResult.Waiting("Waiting for Requital inventory readiness.");
            return Reconcile(receipt.Player, false, () => FriendlyFireParticipants.IsCurrent(receipt));
        }

        internal static void BeforeRestock(GridInventory inventory)
        {
            try
            {
                var player = inventory ? inventory.UnitAvatar as PlayerAvatar : null;
                if (!IsHost(player) || !ReferenceEquals(player.Inventory, inventory) || writing) return;
                // Player initialization can temporarily retire shared participant
                // receipts. This exact native boundary uses its live owner instead.
                Reconcile(player, true);
            }
            catch (Exception error) { Warn("restock boundary failed", error); }
        }

        private static bool IsHost(PlayerAvatar player) => NetworkServer.active && player && player.isServer &&
            player.Inventory && player.Inventory.isServer && player.spawner && DungeonManager.Instance &&
            DungeonManager.Instance.isServer && ReferenceEquals(player.spawner.PlayerAvatar, player) &&
            PlayerSpawner.MultiplayerList.Contains(player.spawner) && !string.IsNullOrWhiteSpace(player.spawner.playerGuid);

        private static ReconcileResult Reconcile(PlayerAvatar player, bool restock, Func<bool> admitted = null)
        {
            if (!IsHost(player)) return ReconcileResult.Waiting("Waiting for Requital owner.");
            var inventory = player.Inventory;
            var state = GetOwnership(player);
            var settings = SessionSettings.Requital;
            var dungeon = DungeonManager.Instance;
            var run = SaveManager.CurrentRun;
            long observedScope = scope, generation = SessionSettings.ResourceGeneration;
            bool Current() => scope == observedScope && IsHost(player) &&
                ReferenceEquals(player.Inventory, inventory) && ReferenceEquals(dungeon, DungeonManager.Instance) &&
                ReferenceEquals(run, SaveManager.CurrentRun) && player.spawner.playerGuid == state.Guid &&
                generation == SessionSettings.ResourceGeneration && SessionSettings.Requital.Equals(settings) &&
                (admitted == null || admitted());
            writing = true;
            try
            {
                int desired = settings.Automatic ? settings.Count : 0;
                RetireExcess(settings, Current);
                RemoveRetired(inventory, Current);
                state.Ids.RemoveAll(id => IsRetired(state.Dungeon, id));
                Recover(state);
                while (state.Ids.Count > desired)
                {
                    Require(Current());
                    int id = state.Ids[state.Ids.Count - 1];
                    Retire(state.Dungeon, id, state.Guid);
                    RemoveEverywhere(player, id, Current);
                    state.Ids.RemoveAt(state.Ids.Count - 1);
                }
                if (desired == 0) { state.Faulted = false; return ReconcileResult.Applied(); }
                if (!Available || !ValidItem()) return ReconcileResult.Suspended("Requital native item is unavailable.");
                // Saved inventory restoration does not restore startingItems.
                // Rebuild metadata for proven IDs without delivering them again.
                foreach (int id in state.Ids)
                {
                    Require(Current());
                    Mark(state, id);
                    if (!state.Faulted && !Registered(inventory, id)) inventory.startingItems.Add(new ItemMetadata(id, ItemId, 1));
                }
                // Exact-ID removal above is idempotent and remains possible on
                // an explicit reduction/off. Ambiguous additions never replay.
                if (state.Faulted) return ReconcileResult.Faulted("Requital automatic write paused after an ambiguous native callback.");
                while (state.Ids.Count < desired)
                {
                    Require(Current() && ValidItem());
                    // Allocate the ID before any native callback. Inferring all
                    // new list entries afterward could claim another addon's gift.
                    int id = ItemDatabase.GenerateInstanceID(random);
                    var item = new ItemMetadata(id, ItemId, 1);
                    state.Ids.Add(id); Mark(state, id);
                    inventory.startingItems.Add(item);
                    Require(Current());
                    if (!restock && !HorayNetworkAuthenticator.AccessDeny_InDungeon)
                        DeliverStarting(player, inventory, item, true, Current);
                    Require(Current());
                }
                return ReconcileResult.Applied();
            }
            catch (Exception error)
            {
                state.Faulted = true; Warn("automatic write paused", error);
                return ReconcileResult.Faulted(error.Message);
            }
            finally { writing = false; }
        }

        internal static void Shutdown()
        {
            if (writing) throw new InvalidOperationException("Requital cleanup cannot interrupt an active native write.");
            // Explicit unload cleanup is safe to retry by exact ID even after a
            // failed addition. Leave markers intact if native cleanup fails.
            if (!writing && NetworkServer.active && DungeonManager.Instance && permission != null)
            {
                var players = new List<PlayerAvatar>();
                foreach (var spawner in PlayerSpawner.MultiplayerList)
                    if (spawner && IsHost(spawner.PlayerAvatar)) players.Add(spawner.PlayerAvatar);
                writing = true;
                try
                {
                    var dungeon = DungeonManager.Instance;
                    RetireExcess(new RequitalSettings(false), () => NetworkServer.active && ReferenceEquals(dungeon, DungeonManager.Instance));
                    foreach (var player in players)
                    {
                        var state = GetOwnership(player);
                        try
                        {
                            RemoveRetired(player.Inventory, () => IsHost(player) && ReferenceEquals(state.Dungeon, DungeonManager.Instance));
                            Recover(state);
                            while (state.Ids.Count != 0)
                            {
                                int id = state.Ids[state.Ids.Count - 1];
                                Retire(state.Dungeon, id, state.Guid);
                                RemoveEverywhere(player, id, () => IsHost(player) && ReferenceEquals(state.Dungeon, DungeonManager.Instance));
                                state.Ids.RemoveAt(state.Ids.Count - 1);
                            }
                            state.Faulted = false;
                        }
                        catch (Exception error) { Warn("unload cleanup failed", error); throw; }
                    }
                }
                finally { writing = false; }
            }
            Clear(); itemAvailable = false;
            if (hooks) { hooks = false; Uninstall(); }
        }
        private static void Warn(string action, Exception error) =>
            Debug.LogWarning("[SephiriaOne] Requital " + action + ": " + error);
    }
}
