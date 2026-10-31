using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // One bounded placement transaction. Native owner movement is asynchronous;
    // never publish cover until current server-observed bodies are clear.
    internal sealed class PvpCoverDisplacement
    {
        internal enum Result { Waiting, Ready, Cancelled }
        private struct Move
        {
            internal PlayerSpawner Spawner;
            internal PlayerAvatar Player;
            internal NetworkConnectionToClient Connection;
            internal Vector2 Center, Size, Destination;
            internal Vector3 Position;
            internal PvpCoverMoveRequests.Request Request;
        }
        private readonly List<Move> moves = new List<Move>(16);
        private string floor;
        private float deadline;
        private int probesLeft = 64;
        internal void BeginFrame() => probesLeft = 64;
        internal bool Active { get; private set; }
        internal Vector2 Point { get; private set; }
        internal void Cancel() { Active = false; moves.Clear(); floor = null; }

        internal bool Begin(PvpCoverRooms rooms, string floorGuid, Vector2 point, Func<bool> currentScope = null)
        {
            Cancel(); floor = floorGuid; Point = point;
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.PlayerAvatar || spawner.PlayerAvatar.currentFloorGuid != floor) continue;
                var player = spawner.PlayerAvatar;
                bool body = PvpCoverRooms.Body(player, out var center, out var size);
                if (!PvpCoverRooms.Overlaps(point, PvpCoverRooms.CoverSize, center, size)) continue;
                var move = new Move { Spawner = spawner, Player = player, Connection = spawner.connectionToClient,
                    Center = center, Size = size, Position = player.transform.position };
                if (!body || moves.Count >= 32 || PvpCoverMoveRequests.Pending(player) || !Current(move)) { Cancel(); return false; }
                moves.Add(move);
            }
            // All destinations are planned before moving anyone. At most 64
            // probes for the whole transaction, not 64 for every player.
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i]; bool found = false;
                for (int ring = 0; ring < 4 && !found; ring++) for (int direction = 0; direction < 8 && !found; direction++)
                {
                    if (probesLeft-- <= 0) break;
                    int dx = direction == 0 || direction == 1 || direction == 7 ? 1 : direction >= 3 && direction <= 5 ? -1 : 0;
                    int dy = direction >= 1 && direction <= 3 ? 1 : direction >= 5 ? -1 : 0;
                    Vector2 destination = point + new Vector2(dx * ((PvpCoverRooms.CoverSize.x + move.Size.x) * .5f + .15f + ring * .5f),
                        dy * ((PvpCoverRooms.CoverSize.y + move.Size.y) * .5f + .15f + ring * .5f));
                    bool reserved = false;
                    for (int j = 0; j < i; j++)
                        if (PvpCoverRooms.Overlaps(destination, move.Size, moves[j].Destination, moves[j].Size)) { reserved = true; break; }
                    if (reserved || !rooms.PlayersClear(destination, move.Size, move.Player)) continue;
                    // A swept bounding rectangle also avoids crossing walls,
                    // pits or existing props on the short relocation path.
                    Vector2 delta = destination - move.Center;
                    if (!rooms.ClearSpace((destination + move.Center) * .5f,
                        move.Size + new Vector2(Math.Abs(delta.x), Math.Abs(delta.y)), true)) continue;
                    move.Destination = destination; moves[i] = move; found = true;
                }
                if (!found) { Cancel(); return false; }
            }
            Active = true; deadline = Time.realtimeSinceStartup + 2f;
            for (int i = 0; Active && i < moves.Count; i++)
            {
                var move = moves[i];
                if (!Current(move) || (currentScope != null && !currentScope())) { Cancel(); return false; }
                Vector2 delta = move.Destination - move.Center;
                var request = PvpCoverMoveRequests.Send(move.Spawner, floor,
                    move.Position + new Vector3(delta.x, delta.y, 0), move.Destination, move.Size);
                if (!Active || (currentScope != null && !currentScope())) { Cancel(); return false; }
                if (request == null) { Cancel(); return false; }
                move.Request = request; moves[i] = move;
            }
            return Active;
        }

        internal Result Poll(PvpCoverRooms rooms)
        {
            if (!Active) return Result.Cancelled;
            foreach (var move in moves)
            {
                if (!Current(move)) return Result.Cancelled;
                if (move.Request == null || !move.Request.Completed)
                    return Time.realtimeSinceStartup >= deadline ? Result.Cancelled : Result.Waiting;
            }
            if (!rooms.PlayersClear(Point, PvpCoverRooms.CoverSize))
                return Time.realtimeSinceStartup >= deadline ? Result.Cancelled : Result.Waiting;
            // Includes followers moved by the native teleport callback and any
            // new non-player occupants. Synchronize physics before this call.
            return rooms.Safe(Point.x, Point.y) ? Result.Ready : Result.Cancelled;
        }

        private bool Current(Move move) => PvpCoverMoveRequests.IsCurrent(move.Spawner, move.Player, move.Connection, floor);
    }
}
