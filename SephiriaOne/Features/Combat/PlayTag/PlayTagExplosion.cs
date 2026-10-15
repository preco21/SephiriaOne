using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // This is the main Leaf Explosion, not the similarly named successful-hit FX.
    // The stock prefab remains unchanged. Only an owned server instance is isolated;
    // guest safety comes from the audited stock code/layers, not unsynchronized flags.
    internal static class PlayTagExplosion
    {
        private const string PrefabName = "MoneyWhirlwind";
        private const uint AssetId = 2358477907;
        private const string GameHash = "C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510";
        private const string MirrorHash = "C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440";
        private static readonly FieldInfo AttackCallback = typeof(Bullet).GetField("onAttack", BindingFlags.Instance | BindingFlags.NonPublic);
        private static GameObject owned;
        private static DungeonManager dungeon;
        private static SaveData run;
        private static PlayerAvatar origin;
        private static string floor;
        private static long generation;
        private static float expires;
        private static bool validated, supported, warned;

        internal static void Show(PlayerAvatar carrier)
        {
            try
            {
                Reset();
                var current = DungeonManager.Instance;
                if (!NetworkServer.active || !NetworkClient.active || !current || !current.isServer || current.netId == 0 ||
                    !carrier || !carrier.isServer || carrier.netId == 0 || SaveManager.CurrentRun == null ||
                    string.IsNullOrEmpty(carrier.currentFloorGuid)) return;
                Vector3 position = carrier.transform.position;
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return;
                if (!validated) { supported = ValidateNativeCode(); validated = true; }
                if (!supported) throw new InvalidOperationException("Leaf Explosion native code has changed; re-audit the stock client collision and spawn paths.");
                Bullet prefab = Bullet.Pool?.GetPrefab(PrefabName);
                if (!ValidatePrefab(prefab)) throw new InvalidOperationException("Leaf Explosion stock prefab, registration or animation has changed.");

                // Instantiate directly: ProjectileSpawner would call OnSpawn (camera
                // shake/collision reset) and Initialize (weapon/owner attack setup).
                owned = CreateCosmeticInstance(prefab, position);
                dungeon = current; run = SaveManager.CurrentRun; origin = carrier;
                floor = carrier.currentFloorGuid; generation = SessionSettings.ResourceGeneration;
                expires = Time.unscaledTime + 3f;
                NetworkServer.Spawn(owned);
                var identity = owned.GetComponent<NetworkIdentity>();
                if (!identity || identity.netId == 0 || !NetworkServer.spawned.TryGetValue(identity.netId, out var spawned) ||
                    !ReferenceEquals(spawned, identity)) throw new InvalidOperationException("Leaf Explosion network spawn did not complete.");
                // Native Animator2D starts once on each peer, including the host.
                // Its bombDemonExplosion and three tradeMoney layers are sufficient.
            }
            catch (Exception error) { DestroyOwned(); Warn(error); }
        }

        private static GameObject CreateCosmeticInstance(Bullet prefab, Vector3 position)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab.gameObject, position, Quaternion.identity);
            try
            {
                var bullet = instance.GetComponent<Bullet>();
                bullet.enabled = false;
                bullet.attackingCollider.enabled = false;
                bullet.isCollisionEnabled = false;
                bullet.collosionType = Bullet.ECollisionTiming.None;
                bullet.canAttackEvenIfOwnerIsNull = false;
                bullet.canAttackOwner = false;
                bullet.damageApplieable = false;
                bullet.damageDealtType = Bullet.EDamageDealtType.None;
                bullet.turnOnChainLightning = false;
                bullet.isHomingTargetEnabled = bullet.autoDetectHomingTarget = false;
                bullet.shakeCameraOnSpawn = false;
                bullet.onSpawn = bullet.onDespawn = null;
                AttackCallback.SetValue(bullet, null);
                bullet.SetOwner(null);
                bullet.ClearTargetFaction();
                bullet.SetDamage(0);
                bullet.NetworkcanBeTransparentOnMultiplayer = false;
                bullet.spawnedPrefabName = PrefabName;
                // Keep all native NetworkBehaviours in their original order. The
                // animation's frame-16 Destroy still calls the empty native destroy
                // module, sending normal network destruction without a damage event.
                return instance;
            }
            catch { UnityEngine.Object.Destroy(instance); throw; }
        }

        // Run even after automatic match completion. Recovery must not erase the
        // presentation in the same frame it was created. Native animation normally
        // destroys it at frame 16; this deadline also covers paused/broken animation.
        internal static void Tick()
        {
            if (ReferenceEquals(owned, null)) return;
            if (!owned || !NetworkServer.active || !NetworkClient.active || !dungeon || !dungeon.isServer ||
                !ReferenceEquals(dungeon, DungeonManager.Instance) || !ReferenceEquals(run, SaveManager.CurrentRun) ||
                generation != SessionSettings.ResourceGeneration || !origin || origin.currentFloorGuid != floor ||
                Time.unscaledTime >= expires) DestroyOwned();
        }

        // Explicit interruption, a new match/run and unload cancel the owned effect.
        // The automatic bomb outcome deliberately leaves it to Tick/native expiry.
        internal static void Reset() => DestroyOwned();

        private static void DestroyOwned()
        {
            GameObject instance = owned;
            owned = null; dungeon = null; run = null; origin = null; floor = null;
            if (!instance) return;
            try
            {
                var identity = instance.GetComponent<NetworkIdentity>();
                if (NetworkServer.active && identity && identity.netId != 0 &&
                    NetworkServer.spawned.TryGetValue(identity.netId, out var current) && ReferenceEquals(identity, current))
                    NetworkServer.Destroy(instance);
                else UnityEngine.Object.Destroy(instance);
            }
            catch (Exception error)
            {
                // Contain a partially completed spawn/destruction without leaving a
                // local enabled object behind. Do not replay an ambiguous spawn.
                UnityEngine.Object.Destroy(instance);
                Warn(error);
            }
        }

        internal static bool ValidateNativeCode() => AttackCallback != null &&
            MatchesAssembly(typeof(Bullet).Assembly, GameHash) && MatchesAssembly(typeof(NetworkClient).Assembly, MirrorHash);

        internal static bool MatchesAssembly(Assembly assembly, string expected)
        {
            // A whole-assembly contract is intentional here: stock-client safety
            // depends on movement, melee, trigger, network and animation consumers.
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == expected;
        }

        private static bool ValidatePrefab(Bullet prefab)
        {
            if (!prefab || prefab.GetType() != typeof(Bullet) || prefab.name != PrefabName || !prefab.gameObject.activeSelf) return false;
            // NetworkBehaviour.netIdentity is assigned by NetworkIdentity.Awake;
            // an uninstantiated asset needs the serialized component lookup.
            var identity = prefab.GetComponent<NetworkIdentity>();
            if (!identity || identity.assetId != AssetId || identity.sceneId != 0 ||
                identity.serverOnly || !NetworkClient.prefabs.TryGetValue(AssetId, out var registered) ||
                !ReferenceEquals(registered, prefab.gameObject) || !NetworkManager.singleton ||
                !NetworkManager.singleton.spawnPrefabs.Contains(prefab.gameObject) || HasCustomHandler("spawnHandlers") || HasCustomHandler("unspawnHandlers")) return false;
            // No body, hitbox, physics body, interaction component or unexpected
            // script may join this hierarchy. Default-layer colliders are excluded
            // from stock player movement and server-only melee/bullet-break queries.
            string[] components = prefab.GetComponentsInChildren<Component>(true).Select(c => c ? c.GetType().Name : "missing").OrderBy(n => n, StringComparer.Ordinal).ToArray();
            string[] expected = { "Animator2D_SpriteRenderer", "Animator2D_SpriteRenderer", "Bullet", "BulletAnimationTransionController",
                "BulletDestroyModule_DestroyImmediate", "BulletMoveModule_BlinkToTargetPosition", "BulletRenderer_TopdownActor", "CircleCollider2D",
                "NetworkIdentity", "NetworkTransformReliable", "SortingGroup", "SpriteRenderer", "SpriteRenderer", "TopdownActorRenderingMetadata",
                "Transform", "Transform", "Transform", "Transform" };
            if (!components.SequenceEqual(expected) || prefab.GetComponentsInChildren<Transform>(true).Any(t => t.gameObject.layer != 0 || !t.gameObject.activeSelf)) return false;
            int observedMasks = CombatManager.TileLayerMask | CombatManager.CliffLayerMask | CombatManager.PitLayerMask |
                CombatManager.TopdownWorldLayer | CombatManager.BlockCharacterLayerMask | CombatManager.ClickableLayerMask;
            if ((observedMasks & 1) != 0 || LayerMask.NameToLayer("Default") != 0 || LayerMask.NameToLayer("InteractableChecker") == 0) return false;
            if (!(prefab.attackingCollider is CircleCollider2D circle) || !ReferenceEquals(circle.gameObject, prefab.gameObject) ||
                circle.isTrigger || !circle.enabled || Mathf.Abs(circle.radius - 2.45f) > 0.0001f ||
                Mathf.Abs(circle.offset.x) > 0.0001f || Mathf.Abs(circle.offset.y - 0.83f) > 0.0001f) return false;
            if (prefab.Owner || prefab.canAttackEvenIfOwnerIsNull || prefab.createFxPrefab || prefab.createSoundEvent ||
                prefab.bulletChildParticle || prefab.trailRenderer || prefab.isHomingTargetEnabled || prefab.autoDetectHomingTarget ||
                prefab.onSpawn != null || prefab.onDespawn != null || AttackCallback.GetValue(prefab) != null) return false;
            var move = prefab.MoveModule;
            var destroy = prefab.DestroyModule;
            if (!move || move.GetType() != typeof(BulletMoveModule_BlinkToTargetPosition) || move.destroyOnGrounded || move.destroyOnPitFalling || move.isFallingWithDelay ||
                !destroy || destroy.GetType() != typeof(BulletDestroyModule_DestroyImmediate) || !destroy.destroySound.IsNull || destroy.destroyFxPrefab ||
                destroy.destroyFxPrefabList.Count != 0 || destroy.breakFxFragments.Count != 0) return false;
            var actor = prefab.GetComponent<TopdownActorRenderingMetadata>();
            if (!actor || !actor.createCustomShadow || actor.customShadowSprite || actor.shadowInstance || !actor.animator) return false;
            var animation = prefab.GetComponent<BulletAnimationTransionController>();
            var animators = prefab.GetComponentsInChildren<Animator2D_Basic>(true);
            if (!animation || animation.GetType() != typeof(BulletAnimationTransionController) || animation.animators.Count != 2 ||
                !animation.animators.SequenceEqual(animators) && !animation.animators.SequenceEqual(animators.Reverse())) return false;
            return animators.Length == 2 && animators.All(a => a.enabled && a.eventPerformer == prefab.gameObject &&
                a.frameMoveType == EAnimator2DFrameMoveType.SCALED && a.entryStateDefaultFrameOffset == 0) &&
                ValidateAnimation(actor.animator.currentSet, true) && ValidateAnimation(animators.Single(a => a != actor.animator).currentSet, false);
        }

        private static bool HasCustomHandler(string name) =>
            !(typeof(NetworkClient).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is IDictionary handlers) || handlers.Contains(AssetId);

        private static bool ValidateAnimation(AnimationSet set, bool main)
        {
            if (!set || set.name != (main ? PrefabName : "Lightning") || set.sprites.Count != 1) return false;
            var state = set.sprites[0];
            if (state.state != "IDLE" || state.repeat || state.fps != 20 || state.transformAttributes.Count != 0 || state.timeline.Count != (main ? 15 : 8)) return false;
            for (int i = 0; i < state.timeline.Count; i++)
            {
                var key = state.timeline[i];
                string spriteName = main ? "DokkaeviExplosionFX" + i.ToString("00") : "SmallLightning_GroundFX" + i;
                if (!key.sprite || key.sprite.name != spriteName || key.frameIdx != (main && i >= 2 ? i + 1 : i)) return false;
            }
            if (!main) return state.frameEvents.Count == 0 && state.soundEvents.Count == 0;
            return state.frameEvents.Count == 2 && FrameEvent(state.frameEvents[0], 5, "DeactivateCollision") &&
                FrameEvent(state.frameEvents[1], 16, "Destroy") && state.soundEvents.Count == 3 &&
                SoundFrame(state.soundEvents[0], 0, true) && SoundFrame(state.soundEvents[1], 2, false) && SoundFrame(state.soundEvents[2], 5, false);
        }

        private static bool FrameEvent(AnimationSet.StateInfo.FrameEvent frame, int index, string method) =>
            frame.frame == index && frame.events.Count == 1 && frame.events[0].componentName == "Bullet" && frame.events[0].methodName == method &&
            frame.events[0].priority == AnimationSet.StateInfo.Event.EPriority.Essential;

        private static bool SoundFrame(AnimationSet.StateInfo.FrameSoundEvent frame, int index, bool explosion)
        {
            if (frame.frame != index || frame.events.Count != (explosion ? 2 : 1)) return false;
            for (int i = 0; i < frame.events.Count; i++)
            {
                var sound = frame.events[i]; var guid = sound.path.Guid;
                if (!sound.attachToPerformer) return false;
                if (explosion && i == 0)
                { if (guid.Data1 != -1489583142 || guid.Data2 != 1090527874 || guid.Data3 != 1827617978 || guid.Data4 != 739549238) return false; }
                else if (guid.Data1 != -1768456097 || guid.Data2 != 1214824152 || guid.Data3 != -1036708714 || guid.Data4 != -221910872) return false;
            }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Warn(Exception error)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[SephiriaOne] Exact Leaf Explosion presentation unavailable: " + error);
        }
    }
}
