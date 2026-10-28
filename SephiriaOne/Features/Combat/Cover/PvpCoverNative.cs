using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class PvpCoverNative
    {
        private const string PatchId = "SephiriaOne.PvpCover";
        internal static readonly FieldInfo AllProps = typeof(FloorGenerator).GetField("allProps", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static List<GameObject> Props(FloorGenerator floor) => AllProps.GetValue(floor) as List<GameObject>;
        private static readonly HashSet<BreakableProp> owned = new HashSet<BreakableProp>(ReferenceComparer<BreakableProp>.Instance);

        internal static void Install()
        {
            if (AllProps?.FieldType != typeof(List<GameObject>) || !PvpCoverRooms.Validate())
                throw new InvalidOperationException("Cover floor contract changed.");
            var harmony = new Harmony(PatchId);
            try
            {
                harmony.Patch(AccessTools.Method(typeof(BreakableProp), nameof(BreakableProp.BreakForce)),
                    prefix: new HarmonyMethod(typeof(PvpCoverNative), nameof(AllowBreak)));
                harmony.Patch(AccessTools.Method(typeof(BreakableProp), nameof(BreakableProp.SetCanBeBroken)),
                    prefix: new HarmonyMethod(typeof(PvpCoverNative), nameof(AllowBreak)));
            }
            catch { harmony.UnpatchAll(PatchId); throw; }
        }
        internal static void Uninstall() => new Harmony(PatchId).UnpatchAll(PatchId);
        private static bool AllowBreak(BreakableProp __instance) => !owned.Contains(__instance);

        internal static GameObject[] Prefabs()
        {
            var entries = PvpCoverCatalog.Entries;
            var prefabs = new GameObject[entries.Length];
            for (int i = 0; i < entries.Length; i++) prefabs[i] = Prefab(entries[i]);
            return prefabs;
        }
        private static GameObject Prefab(PvpCoverCatalog.Entry entry)
        {
            var entity = PropDatabase.FindPropById(entry.Id);
            var prefab = entity ? entity.propPrefab : null;
            var identity = prefab ? prefab.GetComponent<NetworkIdentity>() : null;
            var prop = prefab ? prefab.GetComponent<BreakableProp>() : null;
            var collider = prop ? prop.connectCollider as BoxCollider2D : null;
            // Fail closed on asset updates: only the audited compact, registered
            // Tile-layer prop. Never edit the shared prefab or its network layout.
            if (!NetworkManager.singleton || !prefab || !prefab.activeSelf || !NetworkManager.singleton.spawnPrefabs.Contains(prefab) ||
                !identity || identity.assetId != entry.AssetId || identity.sceneId != 0 || identity.serverOnly || !prop || prop.GetType() != typeof(BreakableProp) ||
                prop.IsBroken || !collider || collider.isTrigger || !collider.enabled || collider.gameObject.layer != LayerMask.NameToLayer("Tile") ||
                collider.transform != prefab.transform || collider.size != new Vector2(entry.Width, entry.Height) || collider.offset != new Vector2(0, entry.OffsetY) ||
                prefab.transform.localScale != Vector3.one || prefab.transform.localRotation != Quaternion.identity ||
                !string.IsNullOrEmpty(prop.id) || prop.exp != 0 || prop.sapphire != 0 ||
                prop.droppableItems.Length != 0 || prefab.GetComponentInChildren<UpdateAstarProp>(true) || prefab.GetComponentInChildren<Interactable>(true))
                throw new InvalidOperationException("Audited cover prop asset/collision contract changed: " + entry.Id);
            return prefab;
        }

        internal static GameObject Spawn(FloorGenerator floor, GameObject prefab, Vector2 point, Action<GameObject> track)
        {
            Vector2 offset = prefab.GetComponent<BreakableProp>().connectCollider.offset;
            GameObject instance = UnityEngine.Object.Instantiate(prefab, (Vector3)(point - offset), Quaternion.identity);
            track(instance);
            var prop = instance.GetComponent<BreakableProp>();
            owned.Add(prop);
            // Both native floor lists contain these roots even if generation is
            // cancelled. Retain ownership before Spawn can publish the identity.
            Props(floor).Add(instance);
            floor.floorRelatedNetworkObjects.Add(instance);
            prop.propType = PropEntity.PropType.Breakable;
            prop.NetworkCanBeBroken = false;
            foreach (var randomId in instance.GetComponents<IRandomID>()) randomId.SetRandomID(UnityEngine.Random.Range(1, int.MaxValue));
            if (instance.TryGetComponent<IFloorProp>(out var floorProp)) floorProp.SetFloorGeneratorServer(floor);
            NetworkServer.Spawn(instance);
            return instance;
        }
        internal static Vector3 Center(GameObject instance) => instance.transform.position +
            (Vector3)instance.GetComponent<BreakableProp>().connectCollider.offset;
        internal static void Remove(FloorGenerator floor, GameObject instance)
        {
            var prop = instance ? instance.GetComponent<BreakableProp>() : null;
            if (instance)
            {
                // Destroy is deferred locally; remove collision before updating
                // pathfinding. No BreakForce / RpcBreak => no detached debris.
                if (prop && prop.connectCollider) prop.connectCollider.enabled = false;
                if (NetworkServer.active) NetworkServer.Destroy(instance);
                else UnityEngine.Object.Destroy(instance);
            }
            if (!ReferenceEquals(prop, null)) owned.Remove(prop);
            if (floor) { Props(floor)?.Remove(instance); floor.floorRelatedNetworkObjects.Remove(instance); }
        }
        internal static void PruneDestroyed() => owned.RemoveWhere(prop => !prop);
    }
}
