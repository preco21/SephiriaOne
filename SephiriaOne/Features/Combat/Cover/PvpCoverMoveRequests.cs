using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // A sent native RPC cannot be recalled. Retain its landing reservation after
    // a placement timeout/cancel, until acknowledgement AND observed arrival,
    // or connection/avatar invalidation. No repeated teleport requests.
    internal static class PvpCoverMoveRequests
    {
        internal sealed class Request
        {
            internal readonly PlayerSpawner Spawner;
            internal readonly PlayerAvatar Player;
            internal readonly NetworkConnectionToClient Connection;
            internal readonly string Floor;
            internal readonly Vector3 Position;
            internal readonly Vector2 Center, Size;
            internal bool Acknowledged, Completed;
            internal Request(PlayerSpawner spawner, string floor, Vector3 position, Vector2 center, Vector2 size)
            { Spawner = spawner; Player = spawner.PlayerAvatar; Connection = spawner.connectionToClient;
                Floor = floor; Position = position; Center = center; Size = size; }
            internal bool Current => IsCurrent(Spawner, Player, Connection, Floor);
            internal bool Alive => SameConnection(Spawner, Player, Connection);
            internal void Acknowledge(Vector3 position, bool teleport)
            { if (teleport && (position - Position).sqrMagnitude < .0001f) Acknowledged = true; }
            internal void Detach() { if (Player) Player.OnSetPositionServerside -= Acknowledge; }
        }
        private static readonly List<Request> outstanding = new List<Request>(16);
        internal static int Count => outstanding.Count;
        internal static bool Pending(PlayerAvatar player)
        { foreach (var request in outstanding) if (ReferenceEquals(request.Player, player)) return true; return false; }
        internal static bool Blocks(Vector2 point, Vector2 size)
        {
            foreach (var request in outstanding)
                if (PvpCoverRooms.Overlaps(point, size, request.Center, request.Size)) return true;
            return false;
        }
        internal static Request Send(PlayerSpawner spawner, string floor, Vector3 position, Vector2 center, Vector2 size)
        {
            if (outstanding.Count >= 32 || Pending(spawner.PlayerAvatar)) return null;
            var request = new Request(spawner, floor, position, center, size);
            if (!request.Current) return null;
            outstanding.Add(request);
            request.Player.OnSetPositionServerside += request.Acknowledge;
            request.Player.ReqSetPosition(position, teleport: true);
            return request;
        }
        internal static void Reconcile()
        {
            for (int i = outstanding.Count - 1; i >= 0; i--)
            {
                var request = outstanding[i];
                if (request.Alive)
                {
                    // Ignore Z (native air movement) for ground footprint arrival.
                    Vector2 delta = (Vector2)request.Player.transform.position - (Vector2)request.Position;
                    if (!request.Acknowledged || delta.sqrMagnitude > .0625f) continue;
                    request.Completed = true;
                }
                request.Detach(); outstanding.RemoveAt(i);
            }
        }
        // Unload has removed every cover object, so late native arrival cannot
        // collide with this addon's cover. Detach callbacks before unloading.
        internal static void Shutdown()
        { foreach (var request in outstanding) request.Detach(); outstanding.Clear(); }
        // Loading, readiness and floor changes abort placement, but cannot
        // recall a sent TargetRpc. They must not retire its landing reservation.
        internal static bool IsCurrent(PlayerSpawner spawner, PlayerAvatar player, NetworkConnectionToClient connection, string floor) =>
            SameConnection(spawner, player, connection) && player.currentFloorGuid == floor && player.loadingScreenType < 0 && connection.isReady;
        private static bool SameConnection(PlayerSpawner spawner, PlayerAvatar player, NetworkConnectionToClient connection) =>
            NetworkServer.active && spawner && player && PlayerSpawner.MultiplayerList.Contains(spawner) &&
            ReferenceEquals(spawner.PlayerAvatar, player) && connection != null && ReferenceEquals(spawner.connectionToClient, connection) &&
            ReferenceEquals(player.connectionToClient, connection) && NetworkServer.connections.TryGetValue(connection.connectionId, out var connected) &&
            ReferenceEquals(connected, connection);
    }
}
