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
        internal const uint AssetId = 4098245047;
        private const string GameHash = "C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510";
        private const string MirrorHash = "C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440";
        internal static readonly Action<CharacterDebuff> ClientUpdate = (Action<CharacterDebuff>)Delegate.CreateDelegate(
            typeof(Action<CharacterDebuff>), typeof(CharacterDebuff).GetMethod("OnUpdate_Client", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));

        // Client/server separation, disabled-component startup/serialization,
        // prefab spawning and native destruction all form this safety boundary.
        internal static bool ValidateNativeCode() =>
            NativeAssemblyContract.Matches(typeof(CharacterDebuff).Assembly, GameHash) &&
            NativeAssemblyContract.Matches(typeof(NetworkClient).Assembly, MirrorHash);

        internal static CharacterDebuff_ChimDamage ValidateAndGetPrefab()
        {
            var prefab = UnitDatabase.GetDebuff("CHIM_DAMAGE") as CharacterDebuff_ChimDamage;
            if (!ValidatePrefab(prefab)) throw new InvalidOperationException("Stock Chim presentation prefab changed or is not registered.");
            return prefab;
        }

        private static bool ValidatePrefab(CharacterDebuff_ChimDamage prefab)
        {
            if (!prefab || prefab.GetType() != typeof(CharacterDebuff_ChimDamage) || prefab.ID != "CHIM_DAMAGE" ||
                prefab.name != "CharacterDebuff_ChimDamage" || !prefab.enabled || !prefab.gameObject.activeSelf ||
                prefab.startFxPrefab || !prefab.startFxEvent.IsNull || !string.IsNullOrEmpty(prefab.HUD_ID) ||
                prefab.buffStackUIPrefab ||
                prefab.Target || prefab.Attacker || prefab.CurrentStack != 0 || prefab.onDestroy != null ||
                !prefab.loopFxPrefab || prefab.loopFxPrefab.name != "BuffFx_Chim" ||
                !prefab.loopFxPrefab.activeSelf || (int)prefab.loopFxType != 0) return false;
            var identity = prefab.GetComponent<NetworkIdentity>();
            if (!identity || identity.assetId != AssetId || identity.sceneId != 0 || identity.serverOnly ||
                !NetworkClient.prefabs.TryGetValue(AssetId, out var registered) || !ReferenceEquals(registered, prefab.gameObject) ||
                !NetworkManager.singleton || !NetworkManager.singleton.spawnPrefabs.Contains(prefab.gameObject) ||
                HasCustomHandler("spawnHandlers") || HasCustomHandler("unspawnHandlers")) return false;
            string[] components = prefab.GetComponentsInChildren<Component>(true)
                .Select(c => c ? c.GetType().Name : "missing").OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (!components.SequenceEqual(new[] { "CharacterDebuff_ChimDamage", "NetworkIdentity", "Transform" })) return false;
            // Exact visual-only hierarchy; do not admit colliders, gameplay
            // scripts, nested renderers or animation callbacks from other mods.
            var loop = prefab.loopFxPrefab;
            components = loop.GetComponentsInChildren<Component>(true)
                .Select(c => c ? c.GetType().Name : "missing").OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (!components.SequenceEqual(new[] { "Animator2D_SpriteRenderer", "SpriteRenderer", "Transform" })) return false;
            var renderer = loop.GetComponent<SpriteRenderer>();
            var animator = loop.GetComponent<Animator2D_SpriteRenderer>();
            if (!renderer || !renderer.enabled || !animator || !animator.enabled ||
                animator.spriteRenderer != renderer || animator.eventPerformer || animator.onEnter != null || animator.onExit != null)
                return false;
            return ValidateAnimation(animator.currentSet);
        }

        private static readonly string[] FrameSprites =
            { "ChilrmFX02", "ChilrmFX01", "ChilrmFX02", "ChilrmFX03", "ChilrmFX04",
              "ChilrmFX03", "ChilrmFX02", "ChilrmFX03", "ChilrmFX04", "ChilrmFX03" };

        private static bool ValidateAnimation(AnimationSet animation)
        {
            if (!animation || animation.name != "ChimFx" || animation.sprites == null || animation.sprites.Count != 1) return false;
            var state = animation.sprites[0];
            if (state == null || state.state != "IDLE" || !state.repeat || state.fps != 10 ||
                state.frameEvents == null || state.frameEvents.Count != 0 ||
                state.soundEvents == null || state.soundEvents.Count != 0 ||
                state.transformAttributes == null || state.transformAttributes.Count != 0 ||
                state.timeline == null || state.timeline.Count != FrameSprites.Length) return false;
            for (int i = 0; i < FrameSprites.Length; i++)
            {
                var frame = state.timeline[i];
                if (frame == null || frame.frameIdx != i || !frame.sprite || frame.sprite.name != FrameSprites[i]) return false;
            }
            return true;
        }

        private static bool HasCustomHandler(string name) =>
            !(typeof(NetworkClient).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is IDictionary handlers) || handlers.Contains(AssetId);
    }
}
