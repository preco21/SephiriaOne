using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // Server-local accounting only. Guests receive the native spawn/despawn stream.
    internal static class ProjectileLimitRuntime
    {
        internal sealed class Owner
        {
            internal PlayerSpawner Spawner;
            internal NetworkConnectionToClient Connection;
            internal long Scope;
            internal bool Retired;
            internal ConnectionLifetime Lifetime;
            internal long ConnectionGeneration;
        }
        internal sealed class ConnectionLifetime { internal long Generation; internal bool Retired; }
        internal sealed class Admission
        {
            internal Admission Previous;
            internal ProjectileInstanceLedger<Owner>.Lease Lease;
            internal bool Bound;
        }
        internal readonly struct Provenance
        {
            internal readonly Owner Owner;
            internal readonly string Source;
            internal Provenance(Owner owner, string source) { Owner = owner; Source = source; }
        }

        private static readonly ProjectileInstanceLedger<Owner> bullets = new ProjectileInstanceLedger<Owner>();
        private static readonly ProjectileRateLedger<Owner> rates = new ProjectileRateLedger<Owner>();
        // FuryMP controllers are invisible, non-damaging schedulers. Bounding them
        // separately avoids using the last missile slot to block their own output.
        private static readonly ProjectileInstanceLedger<Owner> emitters = new ProjectileInstanceLedger<Owner>();
        private static readonly Dictionary<PlayerSpawner, Owner> owners = new Dictionary<PlayerSpawner, Owner>();
        private static readonly Dictionary<GameObject, bool> emitterPrefabs = new Dictionary<GameObject, bool>();
        private static readonly Dictionary<ProjectileBase, ProjectileLimitLifetime> tracked = new Dictionary<ProjectileBase, ProjectileLimitLifetime>();
        // Survives limiter off/on. Native connection transitions must invalidate
        // old component provenance even while the counter dictionaries are empty.
        private static readonly ConditionalWeakTable<NetworkConnectionToClient, ConnectionLifetime> connections =
            new ConditionalWeakTable<NetworkConnectionToClient, ConnectionLifetime>();
        private static readonly ConditionalWeakTable<NetworkConnectionToClient, ConnectionLifetime>.CreateValueCallback
            createConnectionLifetime = _ => new ConnectionLifetime();
        private static ProjectileLimitSettings settings;
        private static DungeonManager dungeon;
        private static long scope, runGeneration;
        private static Admission pendingBullet, pendingEmitter;
        private static Provenance child, liberation;
        private static bool enabled, refreshing;

        // Failure/teardown diagnostics must not call Refresh/Active/Capture: those
        // can reseed or retire live receipts while a native callback is in progress.
        internal static string DescribeDiagnostics(NetworkConnectionToClient connection)
        {
            int playerBullets = 0, playerEmitters = 0;
            foreach (Owner owner in owners.Values)
                if (!owner.Retired && ReferenceEquals(owner.Connection, connection))
                { playerBullets += bullets.Count(owner); playerEmitters += emitters.Count(owner); }
            return "projectile-limits enabled=" + enabled + "; playerBullets=" + playerBullets +
                "; playerEmitters=" + playerEmitters + "; totalCapPerPool=" + settings.PlayerLimit +
                "; sourceDefault=" + settings.DefaultSourceLimit + "; tracked=" + tracked.Count +
                "; activeCaps=" + settings.Enabled + "; rateEnabled=" + settings.RateEnabled +
                "; creations/player/s=" + settings.PlayerRate + "; creations/source/s=" + settings.SourceRate;
        }

        internal static void Clear()
        {
            enabled = false; scope++;
            bullets.Clear(); emitters.Clear(); rates.Clear(); owners.Clear(); emitterPrefabs.Clear(); tracked.Clear();
            dungeon = null;
            // An in-progress callback owns its pending reservation/finalizer.
            // Scope invalidation prevents it from admitting stale descendants.
        }

        internal static void Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                ProjectileLimitSettings next = SessionSettings.ProjectilesForUse;
                bool active = ProjectileLimitFeature.Available && NetworkServer.active && (next.Enabled || next.RateEnabled);
                DungeonManager current = active ? DungeonManager.Instance : null;
                long generation = SessionSettings.ResourceGeneration;
                bool reseed = active && (!enabled || !ReferenceEquals(current, dungeon) || generation != runGeneration);
                if (!active) { Clear(); return; }
                if (reseed) Clear();
                if (!next.RateEnabled || !settings.RateEnabled) rates.Clear();
                rates.Configure(next.PlayerRate, next.SourceRate, Time.unscaledTimeAsDouble);
                settings = next; dungeon = current; runGeneration = generation; enabled = true;
                if (reseed) SeedExisting();
            }
            finally { refreshing = false; }
        }

        private static bool Active()
        {
            if (!enabled || !NetworkServer.active) return false;
            if (!ReferenceEquals(dungeon, DungeonManager.Instance) || runGeneration != SessionSettings.ResourceGeneration) Refresh();
            return enabled;
        }

        private static Owner Capture(UnitAvatar avatar)
        {
            // Secondary effects owned by companions still count against their player.
            for (int depth = 0; avatar && !(avatar is PlayerAvatar) && depth < 8; depth++)
                avatar = avatar.NetworkLeader;
            var player = avatar as PlayerAvatar;
            if (!player || !player.isServer || !player.spawner) return null;
            PlayerSpawner spawner = player.spawner;
            NetworkConnectionToClient connection = spawner.connectionToClient;
            if (connection == null || !ReferenceEquals(spawner.PlayerAvatar, player) ||
                !NetworkServer.connections.TryGetValue(connection.connectionId, out NetworkConnectionToClient live) ||
                !ReferenceEquals(live, connection) || !ReferenceEquals(connection.identity, spawner.netIdentity)) return null;
            ConnectionLifetime lifetime = connections.GetValue(connection, createConnectionLifetime);
            if (lifetime.Retired) return null;
            if (owners.TryGetValue(spawner, out Owner owner))
            {
                if (ReferenceEquals(owner.Connection, connection)) return owner.Retired ? null : owner;
                Retire(owner);
            }
            owner = new Owner { Spawner = spawner, Connection = connection, Scope = scope,
                Lifetime = lifetime, ConnectionGeneration = lifetime.Generation };
            owners[spawner] = owner;
            return owner;
        }

        private static bool Current(Owner owner) => Connected(owner) && owner.Scope == scope;
        private static bool Connected(Owner owner) => owner != null && !owner.Retired &&
            !owner.Lifetime.Retired && owner.ConnectionGeneration == owner.Lifetime.Generation &&
            owner.Spawner && ReferenceEquals(owner.Spawner.connectionToClient, owner.Connection) &&
            NetworkServer.connections.TryGetValue(owner.Connection.connectionId, out NetworkConnectionToClient live) &&
            ReferenceEquals(live, owner.Connection) && ReferenceEquals(owner.Connection.identity, owner.Spawner.netIdentity);

        private static void Retire(Owner owner)
        { owner.Retired = true; bullets.Forget(owner); emitters.Forget(owner); rates.Forget(owner); }

        internal static void RetireConnection(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            ConnectionLifetime lifetime = connections.GetValue(connection, createConnectionLifetime);
            lifetime.Generation++; lifetime.Retired = true;
            foreach (Owner owner in owners.Values)
                if (ReferenceEquals(owner.Connection, connection)) Retire(owner);
        }
        internal static void ConnectionAdded(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            ConnectionLifetime lifetime = connections.GetValue(connection, createConnectionLifetime);
            lifetime.Generation++; lifetime.Retired = false;
            if (owners.Count == 0) return;
            // NetworkServer normally allocates a fresh connection. Handle literal
            // object reuse as a fresh admission as well; never revive old receipts.
            var removed = new List<PlayerSpawner>();
            foreach (var entry in owners)
                if (ReferenceEquals(entry.Value.Connection, connection))
                { Retire(entry.Value); removed.Add(entry.Key); }
            foreach (var spawner in removed) owners.Remove(spawner);
        }
        internal static void RetireSpawner(PlayerSpawner spawner)
        {
            if (!ReferenceEquals(spawner, null) && owners.TryGetValue(spawner, out Owner owner))
            { Retire(owner); owners.Remove(spawner); }
        }

        internal static Bullet Spawn(ProjectileSpawner<Bullet> pool, GameObject key, Vector3 position,
            bool transparent, EDamageFromType fromType, string damageId, float damage, int stagger, float force,
            UnitAvatar avatar, long targets, float height, Vector2 begin, Vector2 end,
            List<CombatBehaviour> shared, Action<CombatBehaviour, DamageInstance, ProjectileBase> attack,
            float range, EDamageElementalType element, bool showSwing, string source)
        {
            if (!Active() || (source == null && child.Owner == null))
                return pool.Spawn(key, position, transparent, fromType, damageId, damage, stagger, force,
                    avatar, targets, height, begin, end, shared, attack, range, element, showSwing);
            Owner owner = source == null ? child.Owner :
                source == "liberation" && liberation.Owner != null ? liberation.Owner : Capture(avatar);
            source = source ?? child.Source;
            // GreenBat implements several distinct artifacts. Share their setting,
            // not their counters; no per-spawn string construction.
            if (source == "planets" && damageId != null) source = damageId;
            if (owner == null)
            {
                // The audited charm/weapon can also belong to native non-player units.
                // Those are outside per-player accounting.
                if (avatar is PlayerAvatar) return null;
                return pool.Spawn(key, position, transparent, fromType, damageId, damage, stagger, force,
                    avatar, targets, height, begin, end, shared, attack, range, element, showSwing);
            }
            if (!Current(owner)) return null;
            int totalCap = settings.Enabled ? settings.PlayerLimit : int.MaxValue;
            int sourceCap = settings.Enabled ? SourceLimit(source) : int.MaxValue;
            // Check capacity first without allocating a lease or spending rate credit.
            // No native callbacks occur between this check and reservation.
            if (settings.Enabled && (bullets.Count(owner) >= totalCap || bullets.Count(owner, source) >= sourceCap)) return null;
            if (settings.RateEnabled && !rates.TryConsume(owner, source, Time.unscaledTimeAsDouble)) return null;
            if (!bullets.TryReserve(owner, source, totalCap, sourceCap, out var lease)) return null;
            var admission = new Admission { Lease = lease, Previous = pendingBullet };
            pendingBullet = admission;
            try
            {
                return pool.Spawn(key, position, transparent, fromType, damageId, damage, stagger, force,
                    avatar, targets, height, begin, end, shared, attack, range, element, showSwing);
            }
            finally
            {
                pendingBullet = admission.Previous;
                if (!admission.Bound) lease.Release();
            }
        }

        // Local/RPC effects have no stock instance-destruction protocol. Gate the
        // creation sender as a rate-only source; never suppress just the host receiver.
        internal static bool AdmitEffect(UnitAvatar avatar, string source)
        {
            if (!Active() || !settings.RateEnabled) return true;
            Owner owner = Capture(avatar);
            if (owner == null) return !(avatar is PlayerAvatar);
            return Current(owner) && rates.TryConsume(owner, source, Time.unscaledTimeAsDouble);
        }

        private static void Bind(ProjectileBase projectile, Admission admission)
        {
            if (admission == null || admission.Bound || !projectile) return;
            var lifetime = projectile.GetComponent<ProjectileLimitLifetime>();
            if (!lifetime) lifetime = projectile.gameObject.AddComponent<ProjectileLimitLifetime>();
            lifetime.Bind(admission.Lease);
            tracked[projectile] = lifetime;
            admission.Bound = true;
        }
        internal static void Despawned(ProjectileBase projectile)
        {
            if (enabled && !ReferenceEquals(projectile, null) && tracked.TryGetValue(projectile, out var lifetime)) lifetime.Release();
        }
        internal static void BeforeSpawn(Bullet bullet)
        {
            Despawned(bullet);
            // Bind before native OnSpawn callbacks: reentrant children must not
            // accidentally claim their parent's pending reservation.
            Bind(bullet, pendingBullet);
        }
        internal static void Destroyed(ProjectileBase projectile, ProjectileLimitLifetime lifetime)
        {
            if (!ReferenceEquals(projectile, null) && tracked.TryGetValue(projectile, out var current) && ReferenceEquals(current, lifetime))
                tracked.Remove(projectile);
        }

        internal static Provenance BeginChildren(BulletDestroyModule module)
        {
            Provenance previous = child;
            child = default;
            if (Active() && module && module.Bullet)
            {
                if (tracked.TryGetValue(module.Bullet, out var lifetime)) child = lifetime.Provenance;
            }
            return previous;
        }
        internal static void EndChildren(Provenance previous) => child = previous;
        internal static Provenance BeginLiberation(SpecialProjectile_FuryMP emitter)
        {
            Provenance previous = liberation;
            liberation = Active() && emitter && tracked.TryGetValue(emitter, out var lifetime) ? lifetime.Provenance : default;
            return previous;
        }
        internal static void EndLiberation(Provenance previous) => liberation = previous;

        internal static bool BeginEmitter(NewWeaponFireData_SpecialProjectile data, UnitAvatar avatar, out Admission admission)
        {
            admission = null;
            if (!Active() || !data.projectilePrefab) return true;
            if (!emitterPrefabs.TryGetValue(data.projectilePrefab, out bool matches))
            {
                matches = data.projectilePrefab.GetComponent<SpecialProjectile_FuryMP>();
                emitterPrefabs.Add(data.projectilePrefab, matches);
            }
            if (!matches) return true;
            Owner owner = Capture(avatar);
            if (owner == null) return !(avatar is PlayerAvatar);
            if (!Current(owner) || !emitters.TryReserve(owner, "liberation", settings.Enabled ? settings.PlayerLimit : int.MaxValue,
                settings.Enabled ? settings.GetSourceLimit("liberation") : int.MaxValue, out var lease)) return false;
            admission = new Admission { Previous = pendingEmitter, Lease = lease };
            pendingEmitter = admission;
            return true;
        }
        internal static void SpawnEmitter(ProjectileBase projectile)
        {
            if (projectile is SpecialProjectile_FuryMP) Bind(projectile, pendingEmitter);
            projectile.OnSpawn();
        }
        internal static void EndEmitter(Admission admission)
        {
            if (admission == null) return;
            pendingEmitter = admission.Previous;
            if (!admission.Bound) admission.Lease.Release();
        }

        private static void SeedExisting()
        {
            // One inventory per activation/run scope; never an Update or hit-path scan.
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (!identity) continue;
                var bullet = identity.GetComponent<Bullet>();
                var emitter = bullet ? null : identity.GetComponent<SpecialProjectile_FuryMP>();
                if (!bullet && !emitter) continue;
                ProjectileBase projectile = bullet ? (ProjectileBase)bullet : emitter;
                var lifetime = projectile.GetComponent<ProjectileLimitLifetime>();
                var provenance = lifetime ? lifetime.Provenance : default;
                if (provenance.Owner != null && !Connected(provenance.Owner))
                {
                    // Keep a rejection tombstone. Otherwise reseeding could either
                    // charge a rejoined player or let an old parent's children bypass.
                    Retire(provenance.Owner);
                    tracked[projectile] = lifetime;
                    continue;
                }
                string source = provenance.Source ?? (bullet ? SourceOf(bullet.damageId, bullet.spawnedPrefabName) : "liberation");
                if (source == null) continue;
                // A delayed explosion may clear native Owner before it despawns.
                // Retain its recorded connection identity when re-enabling limits.
                UnitAvatar avatar = provenance.Owner != null ? provenance.Owner.Spawner.PlayerAvatar :
                    bullet ? bullet.NetworkOwner : emitter.Networkowner;
                Owner owner = Capture(avatar);
                if (owner == null) continue;
                // Preserve provenance for adopted descendants and delayed destruction.
                if (!lifetime) lifetime = projectile.gameObject.AddComponent<ProjectileLimitLifetime>();
                lifetime.Bind((bullet ? bullets : emitters).Adopt(owner, source));
                tracked[projectile] = lifetime;
            }
        }

        private static int SourceLimit(string source) => settings.GetSourceLimit(
            source.StartsWith("Charm_Planet_", StringComparison.Ordinal) ? "planets" : source);
        internal static string SourceOf(string damageId, string prefab)
        {
            switch (damageId)
            {
                case "Charm_PallasCard": return "pallas-card";
                case "Charm_IcicleVine": return "ice-vine";
                case "Charm_ElectricEarring": return "thunder-earring";
                case "Charm_FireFeather": return "phoenix-wing";
                case "Charm_Meteor": return "meteor";
                case "Charm_FlamePlantRoot": return "flame-root";
                case "Charm_RockElephant": return "rock-elephant";
                case "Charm_Kunai": return "kunai";
                case "Charm_FlameBall": return "flame-ball";
                case "Charm_NearMagicBullet": return "yearning-robe";
                case "Weapon_Spike": return "weapon-spike";
                case "Charm_AirSlash": return "ice-air-slash";
                case "Charm_IceSpear": return "ice-spear";
                case "Charm_IceBow": return "ice-bow";
                case "Charm_IceHammer": return "ice-hammer";
            }
            if (damageId != null && damageId.StartsWith("Charm_Planet_", StringComparison.Ordinal)) return damageId;
            if (damageId == "Weapon_SpecialAttack" && (prefab == "FuryMPBullet_Physical" || prefab == "FuryMPBullet_Fire" ||
                prefab == "FuryMPBullet_Ice" || prefab == "FuryMPBullet_Lightning")) return "liberation";
            return null;
        }
    }

    // Local-only component: no extra network identity, Update, RPC or client addon.
    internal sealed class ProjectileLimitLifetime : MonoBehaviour
    {
        private ProjectileInstanceLedger<ProjectileLimitRuntime.Owner>.Lease lease;
        private ProjectileBase projectile;
        internal ProjectileLimitRuntime.Provenance Provenance { get; private set; }
        internal void Bind(ProjectileInstanceLedger<ProjectileLimitRuntime.Owner>.Lease value)
        {
            Release(); lease = value;
            if (!projectile) projectile = GetComponent<ProjectileBase>();
            Provenance = new ProjectileLimitRuntime.Provenance(value.Owner, value.Source);
        }
        internal void Release() { lease?.Release(); lease = null; }
        private void OnDestroy() { Release(); ProjectileLimitRuntime.Destroyed(projectile, this); }
    }
}
