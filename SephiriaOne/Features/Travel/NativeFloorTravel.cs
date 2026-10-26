using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SephiriaOne
{
    internal enum FloorTravelResult { Requested, Busy, DestinationUnavailable, Incompatible }

    // Native travel owns the asynchronous work. This adapter only reads its queue
    // to prevent duplicate transactions; it never removes or rewrites one.
    internal static class NativeFloorTravel
    {
        private static bool checkedContract, warned;
        private static FieldInfo transactions, avatar;

        internal static FloorTravelResult Request(DungeonManager dungeon, PlayerAvatar player, PlayerAvatar host,
            string floorGuid, string spawnPoint, Vector3 position)
        {
            if (!Bind()) return FloorTravelResult.Incompatible;
            var floor = FloorGenerator.FindByGuid(floorGuid);
            if (!floor || !floor.isServer || floor.netId == 0 || !floor.GenerateSuccess ||
                !dungeon.generatedFloors.TryGetValue(floorGuid, out var data) || data == null ||
                !ReferenceEquals(floor.DataOnServer, data)) return FloorTravelResult.DestinationUnavailable;
            try
            {
                if (!(transactions.GetValue(dungeon) is IList pending)) throw new InvalidOperationException("Native travel queue is unavailable.");
                for (int i = 0; i < pending.Count; i++)
                {
                    object traveler = avatar.GetValue(pending[i]);
                    if (ReferenceEquals(traveler, player) || ReferenceEquals(traveler, host)) return FloorTravelResult.Busy;
                }
            }
            catch (Exception error) { Warn(error); return FloorTravelResult.Incompatible; }

            // Server overload retains overridePosition (the client Cmd overload does not).
            // Loading=0 uses the normal short travel wait. Do not rewrite host saves
            // or stage progression when recovering a guest. Native completion records
            // their floor history, occupancy and spawn identity and releases unused floors.
            dungeon.MoveFloor(player, floorGuid, spawnPoint, 0, recordHistory: true, allowSave: false,
                keepPrevFloor: false, randomPosition: false, leaveTrainingSchool: false, overridePosition: position);
            return FloorTravelResult.Requested;
        }

        private static bool Bind()
        {
            if (checkedContract) return transactions != null && avatar != null;
            checkedContract = true;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var queue = typeof(DungeonManager).GetField("travelingTransactions", flags);
                if (queue == null || !queue.FieldType.IsGenericType ||
                    queue.FieldType.GetGenericTypeDefinition() != typeof(List<>)) throw new MissingFieldException("Native travel queue changed.");
                var subject = queue.FieldType.GetGenericArguments()[0].GetField("avatar", flags);
                if (subject == null || subject.FieldType != typeof(PlayerAvatar)) throw new MissingFieldException("Native travel owner changed.");
                transactions = queue; avatar = subject;
                return true;
            }
            catch (Exception error) { Warn(error); return false; }
        }

        private static void Warn(Exception error)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[SephiriaOne] Cross-floor teleport unavailable; native travel queue compatibility failed: " + error);
        }
    }
}
