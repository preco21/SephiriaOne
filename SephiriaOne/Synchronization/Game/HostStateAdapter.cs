using System;
using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    internal static class HostStateAdapter
    {
        public static bool IsReady(PlayerSpawner spawner)
        {
            if (!spawner || !spawner.isServer || spawner.netId == 0 ||
                spawner.connectionToClient == null || !spawner.connectionToClient.isReady) return false;
            PlayerAvatar player = spawner.PlayerAvatar;
            if (!player || !player.isServer || player.netId == 0 || !player.Race ||
                string.IsNullOrEmpty(player.playerNameSource) || string.IsNullOrEmpty(player.currentFloorGuid)) return false;
            GridInventory inventory = player.Inventory;
            return inventory && inventory.isServer && inventory.netId != 0 && inventory.canBroadcast > 0;
        }

        public static bool TryCollect(out HostCommandContext context, out string error)
        {
            context = null;
            error = "Host session data is not ready. Enter town or a run first.";
            DungeonManager dungeon = NetworkServer.active ? DungeonManager.Instance : null;
            if (!dungeon || !dungeon.isServer || dungeon.netId == 0) return false;
            var subjects = new List<HostPlayer>();
            var seen = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.isServer || spawner.netId == 0) continue;
                if (!IsReady(spawner))
                { error = "A player is still initializing. Retry in a moment; no command writes were made."; return false; }
                if (seen.Add(spawner.PlayerAvatar)) subjects.Add(new HostPlayer(spawner));
            }
            if (subjects.Count == 0) { error = "No ready players found. Enter town or a run first."; return false; }
            context = new HostCommandContext(dungeon, subjects);
            error = "";
            return true;
        }
    }

    internal sealed class HostPlayer
    {
        public PlayerSpawner Spawner { get; }
        public PlayerAvatar Player { get; }
        public uint Id { get; }
        public HostPlayer(PlayerSpawner spawner) { Spawner = spawner; Player = spawner.PlayerAvatar; Id = Player.netId; }
        public bool IsReady => HostStateAdapter.IsReady(Spawner) && ReferenceEquals(Spawner.PlayerAvatar, Player) && Player.netId == Id;
    }

    internal sealed class HostCommandContext
    {
        public DungeonManager Dungeon { get; }
        public IReadOnlyList<HostPlayer> Subjects { get; }
        public List<PlayerAvatar> Players { get; } = new List<PlayerAvatar>();
        private readonly List<HostPlayerSnapshot> snapshots = new List<HostPlayerSnapshot>();
        private readonly Dictionary<string, int> constants;
        private readonly object constantsObject;
        private readonly object run;
        private readonly long runGeneration;

        public HostCommandContext(DungeonManager dungeon, IReadOnlyList<HostPlayer> subjects)
        {
            Dungeon = dungeon; Subjects = subjects;
            run = SaveManager.CurrentRun; runGeneration = SessionSettings.ResourceGeneration;
            foreach (HostPlayer subject in subjects)
            { Players.Add(subject.Player); snapshots.Add(new HostPlayerSnapshot(subject.Player)); }
            constantsObject = dungeon.constValueDictionary;
            constants = new Dictionary<string, int>(dungeon.constValueDictionary);
        }

        public bool IsCurrent()
        {
            if (!NetworkServer.active || !Dungeon || !ReferenceEquals(Dungeon, DungeonManager.Instance) || !Dungeon.isServer) return false;
            if (!ReferenceEquals(constantsObject, Dungeon.constValueDictionary)) return false;
            if (!ReferenceEquals(run, SaveManager.CurrentRun) || runGeneration != SessionSettings.ResourceGeneration) return false;
            foreach (HostPlayer subject in Subjects)
                if (!subject.IsReady || !PlayerSpawner.MultiplayerList.Contains(subject.Spawner)) return false;
            foreach (HostPlayerSnapshot snapshot in snapshots) if (!snapshot.SameObjects()) return false;
            return true;
        }

        public StateWriteBatch CreateBatch()
        {
            var batch = new StateWriteBatch(IsCurrent);
            foreach (HostPlayerSnapshot snapshot in snapshots)
            { batch.Require(snapshot.Matches); batch.RequireAfter(snapshot.NativeInputsMatch); }
            batch.Require(() => HostPlayerSnapshot.MapMatches(constants, Dungeon.constValueDictionary));
            return batch;
        }
    }

    internal sealed class HostPlayerSnapshot
    {
        private readonly PlayerAvatar player;
        private readonly GridInventory inventory;
        private readonly int points;
        private readonly Dictionary<string, int> raw, bonus, amp;
        private readonly object rawObject, bonusObject, ampObject;
        public HostPlayerSnapshot(PlayerAvatar player)
        {
            this.player = player; inventory = player.Inventory; points = inventory.dimensionPocket;
            rawObject = player.customStats; bonusObject = player.calculatedBonusStats; ampObject = player.customStatsAmp;
            raw = new Dictionary<string, int>(player.customStats);
            bonus = new Dictionary<string, int>(player.calculatedBonusStats);
            amp = new Dictionary<string, int>(player.customStatsAmp);
        }
        public bool SameObjects() => player && ReferenceEquals(player.Inventory, inventory) &&
            ReferenceEquals(rawObject, player.customStats) && ReferenceEquals(bonusObject, player.calculatedBonusStats) && ReferenceEquals(ampObject, player.customStatsAmp);
        public bool NativeInputsMatch() => SameObjects() && MapMatches(bonus, player.calculatedBonusStats) && MapMatches(amp, player.customStatsAmp);
        public bool Matches() => SameObjects() && inventory.dimensionPocket == points &&
            MapMatches(raw, player.customStats) && MapMatches(bonus, player.calculatedBonusStats) && MapMatches(amp, player.customStatsAmp);
        internal static bool MapMatches(IDictionary<string, int> expected, IDictionary<string, int> current)
        {
            if (expected.Count != current.Count) return false;
            foreach (var entry in expected)
                if (!current.TryGetValue(entry.Key, out int value) || value != entry.Value) return false;
            return true;
        }
    }
}
