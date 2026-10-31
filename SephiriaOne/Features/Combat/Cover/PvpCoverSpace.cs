using System;
using UnityEngine;

namespace SephiriaOne
{
    internal sealed partial class PvpCoverRooms
    {
        // Largest audited prop is 1.35 x 1; retain 0.1 clearance on each side.
        internal static readonly Vector2 CoverSize = new Vector2(1.55f, 1.2f);
        private readonly Collider2D[] overlaps = new Collider2D[64];
        private readonly ContactFilter2D occupancyFilter = new ContactFilter2D { useLayerMask = true, useTriggers = true,
            layerMask = CombatManager.BlockCharacterLayerMask | (1 << LayerMask.NameToLayer("TopdownWorld_1F")) |
                (1 << LayerMask.NameToLayer("Blockable")) };

        internal bool ClearSpace(Vector2 point, Vector2 size, bool ignorePlayers)
        {
            if (PvpCoverMoveRequests.Blocks(point, size) || !Grounded(point, size)) return false;
            int count = Physics2D.OverlapBox(point, size, 0, occupancyFilter, overlaps);
            bool blocked = count == overlaps.Length; // never accept a truncated query
            for (int i = 0; i < count; i++)
            {
                var collider = overlaps[i]; overlaps[i] = null;
                if (!collider) continue;
                var player = collider.GetComponentInParent<PlayerAvatar>();
                if (ignorePlayers && player && KnownPlayer(player)) continue;
                // Portal/menu triggers are not physical obstacles. Actor/prop
                // hitboxes still count so cover cannot overlap native entities.
                if (collider.isTrigger && (CombatManager.BlockCharacterLayerMask & (1 << collider.gameObject.layer)) == 0 &&
                    !collider.GetComponentInParent<UnitAvatar>() &&
                    !collider.GetComponentInParent<BreakableProp>()) continue;
                RecordBlocker(collider); blocked = true;
                if ((CombatManager.BlockCharacterLayerMask & (1 << collider.gameObject.layer)) != 0) colliderRejected++;
                else occupiedRejected++;
            }
            return !blocked;
        }

        internal static bool Overlaps(Vector2 a, Vector2 sizeA, Vector2 b, Vector2 sizeB) =>
            Math.Abs(a.x - b.x) < (sizeA.x + sizeB.x) * .5f &&
            Math.Abs(a.y - b.y) < (sizeA.y + sizeB.y) * .5f;

        internal static bool Body(PlayerAvatar player, out Vector2 center, out Vector2 size)
        {
            center = player.transform.position; size = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            if (!Finite(center.x) || !Finite(center.y)) { center = Vector2.zero; return false; }
            var actor = player.TopdownRigidbody;
            var collider = actor ? actor.MovementCollider : null;
            if (!collider || !collider.enabled) return false;
            var bounds = collider.bounds;
            Vector2 candidate = bounds.center, dimensions = bounds.size;
            if (!Finite(candidate.x) || !Finite(candidate.y) || !Finite(dimensions.x) || !Finite(dimensions.y) ||
                dimensions.x <= 0 || dimensions.y <= 0) return false;
            center = candidate; size = dimensions + new Vector2(.1f, .1f);
            return dimensions.x <= 4 && dimensions.y <= 4;
        }

        private bool KnownPlayer(PlayerAvatar player)
        {
            if (player.currentFloorGuid != floor.guid) return false;
            foreach (var spawner in PlayerSpawner.MultiplayerList)
                if (spawner && ReferenceEquals(spawner.PlayerAvatar, player)) return true;
            return false;
        }

        internal bool PlayersClear(Vector2 point, Vector2 size, PlayerAvatar except = null)
        {
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.PlayerAvatar || spawner.PlayerAvatar == except || spawner.PlayerAvatar.currentFloorGuid != floor.guid) continue;
                Body(spawner.PlayerAvatar, out var center, out var bodySize);
                if (Overlaps(point, size, center, bodySize)) return false;
            }
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
