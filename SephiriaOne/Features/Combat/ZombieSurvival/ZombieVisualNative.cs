using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Mirror;

namespace SephiriaOne
{
    internal static class ZombieVisualNative
    {
        internal const uint AssetId = 1290287067;
        private const string GameHash = "C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510";
        private const string MirrorHash = "C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440";
        internal static readonly Action<CharacterDebuff> ClientUpdate = (Action<CharacterDebuff>)Delegate.CreateDelegate(
            typeof(Action<CharacterDebuff>), typeof(CharacterDebuff).GetMethod("OnUpdate_Client", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));

        // Client/server separation, disabled-component startup/serialization,
        // prefab spawning and native destruction all form this safety boundary.
        internal static bool ValidateNativeCode() =>
            NativeAssemblyContract.Matches(typeof(CharacterDebuff).Assembly, GameHash) &&
            NativeAssemblyContract.Matches(typeof(NetworkClient).Assembly, MirrorHash);

        internal static CharacterDebuff_Poison ValidateAndGetPrefab()
        {
            var prefab = UnitDatabase.GetDebuff("POISON") as CharacterDebuff_Poison;
            if (!ValidatePrefab(prefab)) throw new InvalidOperationException("Stock Poison presentation prefab changed or is not registered.");
            return prefab;
        }

        private static bool ValidatePrefab(CharacterDebuff_Poison prefab)
        {
            if (!prefab || prefab.GetType() != typeof(CharacterDebuff_Poison) || prefab.ID != "POISON" ||
                prefab.name != "CharacterDebuff_Poison" || !prefab.enabled || !prefab.gameObject.activeSelf ||
                prefab.startFxPrefab || !prefab.startFxEvent.IsNull || !string.IsNullOrEmpty(prefab.HUD_ID) ||
                prefab.Target || prefab.Attacker || prefab.CurrentStack != 0 || prefab.onDestroy != null ||
                !prefab.loopFxPrefab || prefab.loopFxPrefab.name != "Poison_Charm" ||
                !prefab.loopFxPrefab.GetComponent<ElementalParticle>() || (int)prefab.loopFxType != 1) return false;
            var identity = prefab.GetComponent<NetworkIdentity>();
            if (!identity || identity.assetId != AssetId || identity.sceneId != 0 || identity.serverOnly ||
                !NetworkClient.prefabs.TryGetValue(AssetId, out var registered) || !ReferenceEquals(registered, prefab.gameObject) ||
                !NetworkManager.singleton || !NetworkManager.singleton.spawnPrefabs.Contains(prefab.gameObject) ||
                HasCustomHandler("spawnHandlers") || HasCustomHandler("unspawnHandlers")) return false;
            string[] components = prefab.GetComponentsInChildren<Component>(true)
                .Select(c => c ? c.GetType().Name : "missing").OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (!components.SequenceEqual(new[] { "CharacterDebuff_Poison", "NetworkIdentity", "Transform" })) return false;
            // A cosmetic loop must not contain collision, damage or other scripts.
            return prefab.loopFxPrefab.GetComponentsInChildren<Component>(true).All(c => c &&
                (c.GetType().Name == "Transform" || c.GetType().Name == "ParticleSystem" ||
                 c.GetType().Name == "ParticleSystemRenderer" || c.GetType() == typeof(ElementalParticle)));
        }

        private static bool HasCustomHandler(string name) =>
            !(typeof(NetworkClient).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is IDictionary handlers) || handlers.Contains(AssetId);
    }
}
