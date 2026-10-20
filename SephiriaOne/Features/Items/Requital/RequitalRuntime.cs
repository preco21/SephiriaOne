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
        internal const int HeavenItemId = 2099;
        private const string Marker = "SephiriaOne_RequitalStartingOwner";
        private const string RetiredMarker = "SephiriaOne_RequitalRetiredOwner";
        private const string KindMarker = "SephiriaOne_StartingSlateKind";
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
        private static bool hooks, itemAvailable, heavenAvailable, ticking, writing;
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
            internal readonly List<ItemMetadata> Items = new List<ItemMetadata>();
            internal bool Faulted;
            internal Ownership(string guid) { Guid = guid; }
        }

        internal static bool Available => AvailableFor(SessionSettings.Requital.Kind);
        internal static bool AvailableFor(SlateKind kind) => hooks &&
            (kind == SlateKind.Requital ? itemAvailable : kind == SlateKind.Heaven && heavenAvailable);
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
            try
            {
                itemAvailable = ValidateFields() && ValidItem(SlateKind.Requital);
                heavenAvailable = ValidateFields() && ValidItem(SlateKind.Heaven);
            }
            catch (Exception error) { itemAvailable = heavenAvailable = false; Warn("database unavailable", error); }
            if (!itemAvailable || !heavenAvailable) Debug.LogWarning("[SephiriaOne] A starting Slate identity/inventory contract is unavailable.");
            catalogRevision++;
        }
        private static int ItemIdFor(SlateKind kind) => kind == SlateKind.Heaven ? HeavenItemId : ItemId;
        private static bool ValidItem(SlateKind kind)
        {
            int id = ItemIdFor(kind);
            var item = ItemDatabase.FindItemById(id);
            return item && item.id == id && item.aName?.key ==
                (kind == SlateKind.Heaven ? "Item_StoneTablet_Heaven_Name" : "Item_StoneTablet_Requital_Name") &&
                item.type == EItemType.StoneTablet && item.rarity == EItemRarity.Eternal &&
                item.activeType == (kind == SlateKind.Heaven ? EItemActiveType.Hidden : EItemActiveType.WitchHat) && item.resourcePrefab;
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
            var observed = SessionSettings.Requital;
            long observedScope = scope;
            var result = Reconcile(receipt.Player, false, () => FriendlyFireParticipants.IsCurrent(receipt));
            // The coordinator captures inputs again after Apply. Preserve a
            // settings change made inside a native callback as pending work;
            // the fault latch still forbids replaying ambiguous additions.
            var latest = SessionSettings.Requital;
            if (scope == observedScope && FriendlyFireParticipants.IsCurrent(receipt) && !observed.Equals(latest))
                coordinator.Invalidate(receipt, SyncDomain.Identity);
            return result;
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
                state.Items.RemoveAll(item => IsRetired(state.Dungeon, item.instanceID));
                Recover(state);
                int selectedId = ItemIdFor(settings.Kind);
                for (int i = state.Items.Count - 1; i >= 0; i--)
                {
                    // A selection change retires the old identity; do not turn
                    // a saved Requital instance into Heaven under the same ID.
                    if (state.Items[i].entityID == selectedId && state.Items.Count <= desired) continue;
                    Require(Current());
                    int id = state.Items[i].instanceID;
                    Retire(state.Dungeon, id, state.Guid);
                    RemoveEverywhere(player, id, Current);
                    state.Items.RemoveAt(i);
                }
                if (desired == 0) { state.Faulted = false; return ReconcileResult.Applied(); }
                if (!AvailableFor(settings.Kind) || !ValidItem(settings.Kind)) return ReconcileResult.Suspended("Selected native Slate is unavailable.");
                // Saved inventory restoration does not restore startingItems.
                // Rebuild metadata for proven IDs without delivering them again.
                foreach (var item in state.Items)
                {
                    Require(Current());
                    Mark(state, item);
                    if (!state.Faulted && !Registered(inventory, item)) inventory.startingItems.Add(item);
                }
                // Exact-ID removal above is idempotent and remains possible on
                // an explicit reduction/off. Ambiguous additions never replay.
                if (state.Faulted) return ReconcileResult.Faulted("Requital automatic write paused after an ambiguous native callback.");
                while (state.Items.Count < desired)
                {
                    Require(Current() && ValidItem(settings.Kind));
                    // Allocate the ID before any native callback. Inferring all
                    // new list entries afterward could claim another addon's gift.
                    int id = ItemDatabase.GenerateInstanceID(random);
                    var item = new ItemMetadata(id, selectedId, 1);
                    state.Items.Add(item); Mark(state, item);
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
                            while (state.Items.Count != 0)
                            {
                                int id = state.Items[state.Items.Count - 1].instanceID;
                                Retire(state.Dungeon, id, state.Guid);
                                RemoveEverywhere(player, id, () => IsHost(player) && ReferenceEquals(state.Dungeon, DungeonManager.Instance));
                                state.Items.RemoveAt(state.Items.Count - 1);
                            }
                            state.Faulted = false;
                        }
                        catch (Exception error) { Warn("unload cleanup failed", error); throw; }
                    }
                }
                finally { writing = false; }
            }
            Clear(); itemAvailable = heavenAvailable = false;
            if (hooks) { hooks = false; Uninstall(); }
        }
        private static void Warn(string action, Exception error) =>
            Debug.LogWarning("[SephiriaOne] Requital " + action + ": " + error);
    }
}
