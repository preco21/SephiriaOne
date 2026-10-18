using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // A stock network prefab is only a presentation carrier. Never register it
    // with UnitAvatar.Debuffs or invoke InitializeAndSpawn/ApplyStatus.
    internal static class ZombieRoleVisuals
    {
        private const string PatchId = "SephiriaOne.ZombieRoleVisuals";
        private sealed class Marker
        {
            internal CharacterDebuff_Poison Effect;
            internal FriendlyFireParticipants.Receipt Receipt;
            internal long Match;
            internal bool Spawning, Retired;
        }
        private static readonly Dictionary<PlayerAvatar, Marker> markers =
            new Dictionary<PlayerAvatar, Marker>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly HashSet<CharacterDebuff> owned =
            new HashSet<CharacterDebuff>(ReferenceComparer<CharacterDebuff>.Instance);
        private static Harmony harmony;
        private static Action<CharacterDebuff> updateClient;
        private static bool checkedNative, failed, clearing;

        internal static void Update(PlayerAvatar player)
        {
            if (clearing || failed || !player) return;
            var receipt = FriendlyFireParticipants.Capture(player);
            long match = CombatMatchRuntime.MatchToken;
            bool current = CombatMatchRuntime.IsZombieVisualCurrent(player, receipt, match);
            if (markers.TryGetValue(player, out var marker) &&
                (!current || !marker.Effect || marker.Match != match || !ReferenceEquals(marker.Receipt, receipt)))
            {
                markers.Remove(player); Retire(marker); marker = null;
            }
            if (!current) return;
            try
            {
                if (!EnsureNative()) return;
                if (marker == null)
                {
                    // Registration is session-owned: recheck it on each creation,
                    // while expensive code checks and binding stay once per load.
                    var prefab = ZombieVisualNative.ValidateAndGetPrefab();
                    marker = new Marker { Receipt = receipt, Match = match };
                    // Publish ownership before native spawning can invoke callbacks.
                    marker.Effect = UnityEngine.Object.Instantiate(prefab, player.transform.position, Quaternion.identity);
                    marker.Effect.enabled = false;
                    marker.Effect.damage = 0; marker.Effect.debuffResistDecrease = 0;
                    owned.Add(marker.Effect); markers.Add(player, marker);
                    marker.Effect.NetworkTarget = player;
                    marker.Effect.NetworkAttacker = null;
                    marker.Effect.NetworkCurrentStack = 0;
                    marker.Spawning = true;
                    try { NetworkServer.Spawn(marker.Effect.gameObject); }
                    finally { marker.Spawning = false; }
                    // Native callbacks may have ended/replaced the match mid-spawn.
                    if (marker.Retired || !CombatMatchRuntime.IsZombieVisualCurrent(player, receipt, match))
                    {
                        if (markers.TryGetValue(player, out var live) && ReferenceEquals(live, marker)) markers.Remove(player);
                        Retire(marker); return;
                    }
                }
                // Disabled only on the host. Stock guests instantiate the original
                // enabled prefab; Mirror does not serialize Behaviour.enabled.
                if (marker.Effect && marker.Effect.isClient) updateClient(marker.Effect);
            }
            catch (Exception error)
            {
                // Do not replay ambiguous failed spawns every frame.
                if (marker != null) Retire(marker);
                failed = true; Clear();
                Debug.LogWarning("[SephiriaOne] Zombie role visual unavailable; match gameplay continues: " + error);
            }
        }

        private static bool EnsureNative()
        {
            if (checkedNative) return !failed;
            checkedNative = true;
            if (!ZombieVisualNative.ValidateNativeCode()) throw new InvalidOperationException("Native Zombie visual contract changed.");
            updateClient = ZombieVisualNative.ClientUpdate;
            harmony = new Harmony(PatchId);
            harmony.Patch(AccessTools.DeclaredMethod(typeof(CharacterDebuff), "OnStartServer"),
                prefix: new HarmonyMethod(typeof(ZombieRoleVisuals), nameof(BeforeStartServer)));
            return true;
        }

        private static bool BeforeStartServer(CharacterDebuff __instance) => !owned.Contains(__instance);

        internal static void Clear()
        {
            if (clearing || markers.Count == 0) return;
            clearing = true;
            try
            {
                var previous = new List<Marker>(markers.Values);
                markers.Clear();
                foreach (var marker in previous) Retire(marker);
            }
            finally { clearing = false; }
        }

        private static void Retire(Marker marker)
        {
            marker.Retired = true;
            // Destroying during Spawn can leave Mirror re-registering a destroyed
            // identity. Keep the startup guard until that call has unwound.
            if (marker.Spawning) return;
            var effect = marker.Effect;
            try
            {
                if (!effect) return;
                effect.enabled = false;
                var identity = effect.netIdentity;
                if (NetworkServer.active && identity && identity.netId != 0 &&
                    NetworkServer.spawned.TryGetValue(identity.netId, out var live) && ReferenceEquals(identity, live))
                    NetworkServer.Destroy(effect.gameObject);
                else UnityEngine.Object.Destroy(effect.gameObject);
            }
            catch (Exception error)
            {
                if (effect) UnityEngine.Object.Destroy(effect.gameObject);
                Debug.LogWarning("[SephiriaOne] Zombie role visual cleanup: " + error);
            }
            finally { owned.Remove(effect); marker.Effect = null; }
        }

        internal static void Shutdown()
        {
            Clear();
            if (owned.Count != 0)
                throw new InvalidOperationException("Zombie visual spawn is finishing a native callback; retry unload after it completes.");
            harmony?.UnpatchAll(PatchId); harmony = null;
            updateClient = null; checkedNative = failed = false;
        }
    }
}
