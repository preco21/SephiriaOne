using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirror;

namespace SephiriaOne
{
    // Selections belong to native object lifetimes, never accounts or save slots.
    // Retirement is separate: clearing the last selection enables other players,
    // but must not enable the actor whose teardown callbacks are still running.
    internal static class FriendlyFireParticipants
    {
        internal sealed class Lifetime
        {
            internal long Generation;
            internal bool Retired;
            internal int Initializing;
        }

        internal sealed class Receipt
        {
            internal readonly PlayerAvatar Player;
            internal readonly PlayerSpawner Spawner;
            internal readonly NetworkIdentity Identity;
            internal readonly NetworkConnectionToClient Connection;
            internal readonly uint NetId;
            internal readonly int ConnectionId;
            internal readonly long Scope, PlayerGeneration, ConnectionGeneration;
            internal readonly Lifetime PlayerLifetime, ConnectionLifetime;

            internal Receipt(PlayerAvatar player, Lifetime playerLifetime, Lifetime connectionLifetime)
            {
                Player = player; Spawner = player.spawner; Identity = Spawner.netIdentity;
                Connection = Spawner.connectionToClient; NetId = player.netId; ConnectionId = Connection.connectionId;
                Scope = scope; PlayerLifetime = playerLifetime; ConnectionLifetime = connectionLifetime;
                PlayerGeneration = playerLifetime.Generation; ConnectionGeneration = connectionLifetime.Generation;
            }
        }

        internal readonly struct Initialization
        {
            internal readonly Lifetime Lifetime;
            internal readonly long Generation;
            internal Initialization(Lifetime lifetime) { Lifetime = lifetime; Generation = lifetime.Generation; }
        }

        private static ConditionalWeakTable<PlayerAvatar, Receipt> receipts = new ConditionalWeakTable<PlayerAvatar, Receipt>();
        private static readonly ConditionalWeakTable<PlayerSpawner, Lifetime> playerLifetimes = new ConditionalWeakTable<PlayerSpawner, Lifetime>();
        private static readonly ConditionalWeakTable<NetworkConnectionToClient, Lifetime> connectionLifetimes = new ConditionalWeakTable<NetworkConnectionToClient, Lifetime>();
        private static readonly ConditionalWeakTable<PlayerSpawner, Lifetime>.CreateValueCallback createPlayerLifetime = NewPlayerLifetime;
        private static readonly ConditionalWeakTable<NetworkConnectionToClient, Lifetime>.CreateValueCallback createConnectionLifetime = NewConnectionLifetime;
        private static readonly HashSet<Receipt> selected = new HashSet<Receipt>();
        private static readonly List<Receipt> stale = new List<Receipt>();
        private static DungeonManager dungeon;
        private static SaveData run;
        private static uint dungeonId;
        private static long runGeneration, scope, epoch;
        private static bool stopped;

        internal static long Epoch { get { EnsureScope(); Prune(); return epoch; } }
        internal static int SelectedCount { get { EnsureScope(); Prune(); return selected.Count; } }

        internal static Receipt Capture(PlayerAvatar player)
        {
            if (!EnsureScope() || !CurrentPlayer(player)) return null;
            PlayerSpawner spawner = player.spawner;
            Lifetime playerLifetime = playerLifetimes.GetValue(spawner, createPlayerLifetime);
            Lifetime connectionLifetime = connectionLifetimes.GetValue(spawner.connectionToClient, createConnectionLifetime);
            if (playerLifetime.Retired || playerLifetime.Initializing != 0 || connectionLifetime.Retired) return null;
            if (receipts.TryGetValue(player, out Receipt current))
            {
                if (Valid(current)) return current;
                receipts.Remove(player);
            }
            var receipt = new Receipt(player, playerLifetime, connectionLifetime);
            receipts.Add(player, receipt);
            return receipt;
        }

        internal static bool IsCurrent(Receipt receipt) => EnsureScope() && Valid(receipt);

        internal static bool Includes(PlayerAvatar player, bool requireReady = true)
        {
            Receipt receipt = Capture(player);
            Prune();
            return receipt != null && (!requireReady || HostStateAdapter.IsReady(receipt.Spawner)) &&
                (selected.Count == 0 || selected.Contains(receipt));
        }

        internal static bool IsSelected(PlayerAvatar player)
        {
            Receipt receipt = Capture(player);
            Prune();
            return receipt != null && selected.Contains(receipt);
        }

        internal static bool Set(Receipt receipt, bool value)
        {
            if (!IsCurrent(receipt)) return false;
            Prune();
            if (value) selected.Add(receipt); else selected.Remove(receipt);
            Changed(true);
            return true;
        }

        internal static bool SetAll(IReadOnlyList<Receipt> values)
        {
            if (values == null || !EnsureScope()) return false;
            for (int i = 0; i < values.Count; i++) if (!Valid(values[i])) return false;
            selected.Clear();
            for (int i = 0; i < values.Count; i++) selected.Add(values[i]);
            Changed(true);
            return true;
        }

        internal static void ResetSelection()
        {
            EnsureScope(); selected.Clear(); Changed(true);
        }

        internal static void Clear()
        {
            receipts = new ConditionalWeakTable<PlayerAvatar, Receipt>();
            selected.Clear(); stale.Clear(); dungeon = null; run = null; dungeonId = 0; runGeneration = 0;
            scope++; Changed(false);
        }

        private static bool EnsureScope()
        {
            DungeonManager current = !stopped && NetworkServer.active ? DungeonManager.Instance : null;
            if (!current || !current.isServer || current.netId == 0 || SaveManager.CurrentRun == null) current = null;
            if (!current)
            {
                if (!ReferenceEquals(dungeon, null) || selected.Count != 0) Clear();
                return false;
            }
            if (!ReferenceEquals(dungeon, current) || dungeonId != current.netId ||
                !ReferenceEquals(run, SaveManager.CurrentRun) || runGeneration != SessionSettings.ResourceGeneration)
            {
                Clear(); dungeon = current; dungeonId = current.netId;
                run = SaveManager.CurrentRun; runGeneration = SessionSettings.ResourceGeneration;
            }
            return true;
        }

        private static bool CurrentPlayer(PlayerAvatar player)
        {
            if (!player || !player.isServer || player.netId == 0) return false;
            PlayerSpawner spawner = player.spawner;
            if (!spawner || !spawner.isServer || spawner.netId != player.netId ||
                !ReferenceEquals(spawner.PlayerAvatar, player) || !PlayerSpawner.MultiplayerList.Contains(spawner)) return false;
            NetworkConnectionToClient connection = spawner.connectionToClient;
            NetworkIdentity identity = spawner.netIdentity;
            return connection != null && identity && identity.netId == player.netId &&
                ReferenceEquals(player.netIdentity, identity) && ReferenceEquals(player.connectionToClient, connection) &&
                ReferenceEquals(connection.identity, identity) &&
                NetworkServer.connections.TryGetValue(connection.connectionId, out NetworkConnectionToClient live) &&
                ReferenceEquals(live, connection) &&
                NetworkServer.spawned.TryGetValue(player.netId, out NetworkIdentity spawned) && ReferenceEquals(spawned, identity);
        }

        private static bool Valid(Receipt receipt) => receipt != null && receipt.Scope == scope &&
            !receipt.PlayerLifetime.Retired && receipt.PlayerLifetime.Initializing == 0 && !receipt.ConnectionLifetime.Retired &&
            receipt.PlayerGeneration == receipt.PlayerLifetime.Generation && receipt.ConnectionGeneration == receipt.ConnectionLifetime.Generation &&
            CurrentPlayer(receipt.Player) && ReferenceEquals(receipt.Player.spawner, receipt.Spawner) &&
            ReferenceEquals(receipt.Spawner.netIdentity, receipt.Identity) && ReferenceEquals(receipt.Spawner.connectionToClient, receipt.Connection) &&
            receipt.Player.netId == receipt.NetId && receipt.Connection.connectionId == receipt.ConnectionId;

        private static void Prune()
        {
            foreach (Receipt receipt in selected) if (!Valid(receipt)) stale.Add(receipt);
            if (stale.Count == 0) return;
            foreach (Receipt receipt in stale) selected.Remove(receipt);
            stale.Clear(); Changed(false);
        }

        private static Lifetime NewPlayerLifetime(PlayerSpawner _) => new Lifetime();
        private static Lifetime NewConnectionLifetime(NetworkConnectionToClient _) => new Lifetime();

        private static void Changed(bool resetScores)
        {
            epoch++;
            if (resetScores) FriendlyFireKda.ResetScores(); else FriendlyFireKda.ClearLives();
        }

        internal static void Retire(PlayerSpawner spawner)
        {
            if (ReferenceEquals(spawner, null)) return;
            Lifetime lifetime = playerLifetimes.GetValue(spawner, createPlayerLifetime);
            lifetime.Retired = true; lifetime.Generation++;
            Prune(); Changed(false);
        }

        internal static void Retire(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            Lifetime lifetime = connectionLifetimes.GetValue(connection, createConnectionLifetime);
            lifetime.Retired = true; lifetime.Generation++;
            Prune(); Changed(false);
        }

        internal static void Connected(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            Lifetime lifetime = connectionLifetimes.GetValue(connection, createConnectionLifetime);
            lifetime.Retired = false; lifetime.Generation++;
            Prune(); Changed(false);
        }

        internal static void Spawned(PlayerSpawner spawner)
        {
            if (ReferenceEquals(spawner, null)) return;
            Lifetime lifetime = playerLifetimes.GetValue(spawner, createPlayerLifetime);
            lifetime.Retired = false; lifetime.Generation++;
            Prune(); Changed(false);
        }

        internal static Initialization Initializing(PlayerSpawner spawner)
        {
            if (!NetworkServer.active || !spawner) return default;
            Lifetime lifetime = playerLifetimes.GetValue(spawner, createPlayerLifetime);
            lifetime.Generation++; lifetime.Initializing++; lifetime.Retired = true;
            Prune(); Changed(false);
            return new Initialization(lifetime);
        }

        internal static void Initialized(Initialization initialization, bool success)
        {
            Lifetime lifetime = initialization.Lifetime;
            if (lifetime == null) return;
            lifetime.Initializing--;
            if (lifetime.Generation == initialization.Generation) lifetime.Retired = !success;
        }

        internal static void ServerStopping() { stopped = true; Clear(); }
        internal static void ServerStarted() { stopped = false; Clear(); }
    }
}
