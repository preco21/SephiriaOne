using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class MerchantRuntime
    {
        private static readonly Dictionary<UnitAI_NewBasic, SpawnedMerchant> owned =
            new Dictionary<UnitAI_NewBasic, SpawnedMerchant>(ReferenceComparer<UnitAI_NewBasic>.Instance);
        private static DungeonManager dungeon;
        private static SaveData run;
        private static MerchantRoute route;
        private static readonly Dictionary<string, MerchantSchedule> schedules = new Dictionary<string, MerchantSchedule>();
        private static bool refreshing;

        private sealed class SpawnedMerchant
        {
            internal GameObject Actor;
            internal Safe Stock;
            internal FloorGenerator Floor;
        }

        // Provenance survives setting changes and death callbacks; never infer it from
        // social ID, prefab, netId, or the current option (all can change or be reused).
        internal static bool Owns(UnitAI_NewBasic npc) =>
            NetworkServer.active && npc && owned.ContainsKey(npc);

        internal static void OnFloorReady(string guid, string name, FloorGenerator floor) => RefreshFloor(floor);

        internal static void Refresh() => RefreshFloor(null);

        private static void RefreshFloor(FloorGenerator only)
        {
            if (refreshing || !NetworkServer.active || !MerchantFeature.Available) return;
            refreshing = true;
            try
            {
                // Resolve shared session policy before reading any cached runtime state.
                bool enabled = SessionSettings.AnyMerchantSpawnsForUse;
                DungeonManager current = DungeonManager.Instance;
                SaveData currentRun = SaveManager.CurrentRun;
                if (!current || !current.isServer || current.netId == 0 || currentRun == null) return;
                if (!ReferenceEquals(dungeon, current) || !ReferenceEquals(run, currentRun))
                { Clear(); dungeon = current; run = currentRun; }
                Prune();
                if (!enabled) return;
                if (route == null)
                {
                    // Commands can refresh before native LoadDungeon has assigned the
                    // scenario. Do not cache an empty plan for the rest of that run.
                    if (!dungeon.Race) return;
                    route = new MerchantRoute(dungeon, run);
                }
                route.ObserveHistory();
                FloorGenerator[] floors = only ? new[] { only } : FloorGenerator.FloorGenerators.ToArray();
                // Establish current progress before processing anything. Reversed list
                // order and settings changes must not spawn on historical loaded floors.
                foreach (FloorGenerator floor in floors)
                    if (Ready(floor)) route.Position(floor.DataOnServer);
                foreach (MerchantDefinition definition in MerchantCatalog.All)
                {
                    MerchantSettings settings = SessionSettings.GetMerchantSettingsForUse(definition.Id);
                    if (!settings.Enabled) continue;
                    if (!schedules.TryGetValue(definition.Id, out MerchantSchedule schedule))
                    {
                        schedule = new MerchantSchedule(run, route.Opportunities, dungeon.DestinySeed, definition);
                        schedules.Add(definition.Id, schedule);
                    }
                    schedule.State.InitializeCount(dungeon.generatedFloors.Keys);
                    schedule.Advance(route.LatestPosition, settings.FirstFloor, settings.Guarantee);
                    foreach (FloorGenerator floor in floors)
                    {
                        // A broken template or condition must not suppress another type.
                        try { TrySpawn(floor, definition, settings, schedule); }
                        catch (Exception error)
                        { Debug.LogWarning("[SephiriaOne] Merchant " + definition.Id + " skipped: " + error); }
                    }
                }
            }
            catch (Exception error)
            { Debug.LogWarning("[SephiriaOne] Merchant refresh failed; native floors continue: " + error); }
            finally { refreshing = false; }
        }

        private static bool Ready(FloorGenerator floor) =>
            floor && floor.isServer && floor.GenerateSuccess && !string.IsNullOrEmpty(floor.guid) &&
            floor.DataOnServer != null && dungeon.generatedFloors.TryGetValue(floor.guid, out FloorData data) &&
            ReferenceEquals(data, floor.DataOnServer);

        private static void TrySpawn(FloorGenerator floor, MerchantDefinition definition, MerchantSettings settings, MerchantSchedule schedule)
        {
            if (!Ready(floor) || floor.isSafeFloor || floor.isTrainingFloor || floor.DataOnServer.isHidden ||
                floor.DataOnServer.pocketDimension ||
                schedule.State.Consumed(floor.guid)) return;
            int positionInRun = route.Position(floor.DataOnServer);
            // Optional/unknown stages cannot host the scheduled guarantee, but keep
            // their existing chance-only behavior under the normal room safety gates.
            if (positionInRun >= 0 && positionInRun < schedule.Progress) return;
            var context = new MerchantSpawnContext(route.FloorNumber(route.LatestPosition), schedule.State.Count,
                floor.DataOnServer.difficulty, floor.DataOnServer.stageName);
            if (!MerchantSpawnRules.Allows(definition, settings, context)) return;
            bool guaranteed = schedule.IsDue(positionInRun, settings.Guarantee);
            // Keep one slot available for the scheduled encounter. A 100% chance and
            // cap of one therefore produces only the guarantee, at its chosen floor.
            if (!guaranteed && settings.MaxPerRun > 0 && schedule.HasPendingOpportunity(settings.FirstFloor, settings.Guarantee) &&
                schedule.State.Count >= settings.MaxPerRun - 1) return;
            if (!MerchantRooms.TryChoose(floor, definition.SeedSalt, out Vector2 position)) return;
            SocialIDEntity template = SocialIDDatabase.FindByName(definition.SocialId);
            GameObject stockPrefab = PropDatabase.FindPropById("Mat_TravelerMerchant")?.propPrefab;
            string faction = HostileFaction();
            if (!template || !template.avatarPrefab || !template.avatarPrefab.GetComponent<UnitAvatar>() ||
                template.avatarPrefab.GetComponent<UnitAvatar>().GetType().Name != definition.UnitTypeName ||
                !template.avatarPrefab.GetComponent<UnitAI_NewBasic>() ||
                template.proceduralMerchantType == EProceduralMerchantType.None ||
                !stockPrefab || !stockPrefab.GetComponent<Safe>() || faction == null)
            {
                Debug.LogWarning("[SephiriaOne] Merchant " + definition.Id + " skipped: native merchant, stock prefab or hostile faction unavailable.");
                return;
            }
            // SetSocialID chooses ANY nearby safe. Never let it bind a natural merchant's stock.
            if (Safe.Find(position)) return;
            schedule.State.Reserve(floor.guid);
            // The roll is consumed even on a miss. Setting changes and revisits cannot farm it.
            if (!guaranteed &&
                new System.Random(floor.seed ^ 0x43484E43 ^ definition.SeedSalt).Next(100) >= settings.Chance) return;
            var record = new SpawnedMerchant { Floor = floor };
            UnitAI_NewBasic ai = null;
            try
            {
                record.Actor = UnityEngine.Object.Instantiate(template.avatarPrefab, position, Quaternion.identity);
                ai = record.Actor.GetComponent<UnitAI_NewBasic>();
                UnitAvatar avatar = record.Actor.GetComponent<UnitAvatar>();
                owned.Add(ai, record);
                floor.floorRelatedNetworkObjects.Add(record.Actor);
                NetworkServer.Spawn(record.Actor);
                avatar.SetRandomID(floor.seed ^ 0x534F4D ^ definition.SeedSalt);
                avatar.ChangeFaction(faction);
                ApplyNativeScaling(avatar, position);
                if (Safe.Find(position)) throw new InvalidOperationException("Another safe appeared before merchant setup.");
                // Own the container before native initialization can throw. SetSocialID
                // reuses this known new safe, so rollback never claims a preexisting one.
                GameObject stockObject = UnityEngine.Object.Instantiate(stockPrefab, position, Quaternion.identity);
                record.Stock = stockObject.GetComponent<Safe>();
                floor.floorRelatedNetworkObjects.Add(stockObject);
                NetworkServer.Spawn(stockObject);
                if (!ReferenceEquals(Safe.Find(position), record.Stock))
                    throw new InvalidOperationException("The new stock container is not the nearest safe.");
                // A unique ID prevents talk/quest/global lookup collisions with natural NPCs.
                ai.SetSocialID("SephiriaOne_Merchant_" + definition.Id + "_" + floor.guid, template.aName.key,
                    EPersonality.Aggressive, template.alignment, "", template.proceduralMerchantType,
                    template.startingMoney, template.startingItems);
                if (!ReferenceEquals(ai.NetworkMySafe, record.Stock) || !ReferenceEquals(record.Stock.NetworkconnectedMerchant, ai))
                    throw new InvalidOperationException("Native merchant stock did not bind to its new owner.");
                ai.CanTalk = false;
                avatar.ChangeAttackableTargetSelector(EPersonality.Aggressive);
                schedule.State.RecordSpawn(guaranteed);
                Debug.Log("[SephiriaOne] Spawned hostile " + definition.Id + " merchant on floor " + floor.guid + ".");
            }
            catch (Exception error)
            {
                Destroy(record);
                if (!ReferenceEquals(ai, null)) owned.Remove(ai);
                Debug.LogWarning("[SephiriaOne] Merchant " + definition.Id + " spawn aborted on " + floor.guid +
                    "; this floor will not retry to avoid duplicate stock: " + error);
            }
        }

        private static string HostileFaction()
        {
            RuntimeFactionManager factions = RuntimeFactionManager.Instance;
            if (!factions) return null;
            foreach (string candidate in new[] { "Undead", "Pillagers" })
                if (factions.FindFactionLayer(candidate) != 0 &&
                    factions.GetRelationBehaviour(candidate, "Player", EPersonality.Aggressive) == ERelationBehaviour.Hostile)
                    return candidate;
            return null;
        }

        private static void ApplyNativeScaling(UnitAvatar avatar, Vector2 position)
        {
            dungeon.GetStageStatBonusAtPosition(position, out int hp, out int attack, out int defense);
            avatar.AddMaxHpPercent(hp * 1.5f);
            avatar.AddCustomStat(ECustomStat.AllDamageBonus, (int)(attack * 1.5f));
            avatar.AddCustomStat(ECustomStat.DamageReduction, (int)(defense * 1.5f));
            int extraPlayers = NetworkServer.connections.Count - 1;
            if (extraPlayers > 0)
            {
                string kind = avatar.monsterType == EMonsterType.Normal ? "enemy" : "miniboss";
                avatar.AddMaxHpPercent(extraPlayers * KeywordDatabase.GetConstValue(kind + "BonusHpByPlayerNumber"));
                avatar.AddCustomStat(ECustomStat.AllDamageBonus,
                    extraPlayers * KeywordDatabase.GetConstValue(kind + "BonusDamageByPlayerNumber"));
            }
            float nativeHp = avatar.MaxHp;
            if (avatar.isHPCursed > 0 || nativeHp <= 0 || float.IsNaN(nativeHp) || float.IsInfinity(nativeHp))
                throw new InvalidOperationException("Native merchant HP is invalid.");
            avatar.HealPercent(100f);
        }

        private static void Prune()
        {
            var gone = new List<UnitAI_NewBasic>();
            foreach (var pair in owned)
                if (!pair.Value.Actor && !pair.Value.Stock) gone.Add(pair.Key);
            foreach (var npc in gone) owned.Remove(npc);
        }

        internal static void Clear()
        {
            foreach (var pair in owned) Destroy(pair.Value);
            owned.Clear(); dungeon = null; run = null; route = null; schedules.Clear();
        }

        private static void Destroy(SpawnedMerchant record)
        {
            GameObject stock = record.Stock ? record.Stock.gameObject : null;
            // Destroy is deliberately not ForceDie: cleanup must not award kills or loot.
            if (record.Actor)
            {
                if (NetworkServer.active) NetworkServer.Destroy(record.Actor);
                else UnityEngine.Object.Destroy(record.Actor);
            }
            // Keep the native teardown reference if destruction throws; it is a second
            // cleanup path while our provenance/hook remains active for the surviving NPC.
            if (record.Floor) record.Floor.floorRelatedNetworkObjects.Remove(record.Actor);
            if (stock)
            {
                if (NetworkServer.active) NetworkServer.Destroy(stock);
                else UnityEngine.Object.Destroy(stock);
            }
            if (record.Floor && !ReferenceEquals(stock, null)) record.Floor.floorRelatedNetworkObjects.Remove(stock);
        }
    }
}
