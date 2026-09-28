using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class MerchantRuntime
    {
        private const string FloorKey = "SephiriaOne.MerchantFloor.";
        private const string EncounterKey = "SephiriaOne.MerchantEncounter";
        private static readonly Dictionary<UnitAI_NewBasic, SpawnedMerchant> owned =
            new Dictionary<UnitAI_NewBasic, SpawnedMerchant>(ReferenceComparer<UnitAI_NewBasic>.Instance);
        private static DungeonManager dungeon;
        private static SaveData run;
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
                bool enabled = SessionSettings.MerchantSpawnsForUse;
                DungeonManager current = DungeonManager.Instance;
                SaveData currentRun = SaveManager.CurrentRun;
                if (!current || !current.isServer || current.netId == 0 || currentRun == null) return;
                if (!ReferenceEquals(dungeon, current) || !ReferenceEquals(run, currentRun))
                { Clear(); dungeon = current; run = currentRun; }
                Prune();
                if (!enabled) return;
                if (only) TrySpawn(only);
                else foreach (FloorGenerator floor in FloorGenerator.FloorGenerators.ToArray()) TrySpawn(floor);
            }
            catch (Exception error)
            { Debug.LogWarning("[SephiriaOne] Merchant refresh failed; native floors continue: " + error); }
            finally { refreshing = false; }
        }

        private static void TrySpawn(FloorGenerator floor)
        {
            if (!floor || !floor.isServer || !floor.GenerateSuccess || floor.isSafeFloor || floor.isTrainingFloor ||
                string.IsNullOrEmpty(floor.guid) || floor.DataOnServer == null || floor.DataOnServer.isHidden ||
                floor.DataOnServer.pocketDimension ||
                !dungeon.generatedFloors.TryGetValue(floor.guid, out FloorData data) ||
                !ReferenceEquals(data, floor.DataOnServer) || run.GetBool(FloorKey + floor.guid, false)) return;
            if (!MerchantRooms.TryChoose(floor, out Vector2 position)) return;
            SocialIDEntity template = SocialIDDatabase.FindByName("Merchant_Papa");
            GameObject stockPrefab = PropDatabase.FindPropById("Mat_TravelerMerchant")?.propPrefab;
            string faction = HostileFaction();
            if (!template || !template.avatarPrefab || !template.avatarPrefab.GetComponent<Unit_BabaMerchantHard>() ||
                !template.avatarPrefab.GetComponent<UnitAI_NewBasic>() ||
                template.proceduralMerchantType == EProceduralMerchantType.None ||
                !stockPrefab || !stockPrefab.GetComponent<Safe>() || faction == null)
            {
                Debug.LogWarning("[SephiriaOne] Merchant skipped: native merchant, stock prefab or hostile faction unavailable.");
                return;
            }
            // SetSocialID chooses ANY nearby safe. Never let it bind a natural merchant's stock.
            if (Safe.Find(position)) return;
            run.SetBool(FloorKey + floor.guid, true);
            // The roll is consumed even on a miss. Setting changes and revisits cannot farm it.
            if (run.GetBool(EncounterKey, false) &&
                new System.Random(floor.seed ^ 0x43484E43).Next(100) >= SessionSettings.MerchantSpawnChanceForUse) return;
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
                avatar.SetRandomID(floor.seed ^ 0x534F4D);
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
                ai.SetSocialID("SephiriaOne_Merchant_" + floor.guid, template.aName.key,
                    EPersonality.Aggressive, template.alignment, "", template.proceduralMerchantType,
                    template.startingMoney, template.startingItems);
                if (!ReferenceEquals(ai.NetworkMySafe, record.Stock) || !ReferenceEquals(record.Stock.NetworkconnectedMerchant, ai))
                    throw new InvalidOperationException("Native merchant stock did not bind to its new owner.");
                ai.CanTalk = false;
                avatar.ChangeAttackableTargetSelector(EPersonality.Aggressive);
                run.SetBool(EncounterKey, true);
                Debug.Log("[SephiriaOne] Spawned hostile Wandering Merchant on floor " + floor.guid + ".");
            }
            catch (Exception error)
            {
                Destroy(record);
                if (!ReferenceEquals(ai, null)) owned.Remove(ai);
                Debug.LogWarning("[SephiriaOne] Merchant spawn aborted on " + floor.guid +
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
            owned.Clear(); dungeon = null; run = null;
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
