using System;
using System.Collections.Generic;
using UnityEngine;

namespace SephiriaOne
{
    // Choice branches share a depth. Town-board missions instead share a finite
    // number of visit slots: their native nodeProgress is not chronological.
    internal sealed class MerchantRoute
    {
        private const string BoardKey = "SephiriaOne.MerchantRoute.Board.";
        private readonly DungeonManager dungeon;
        private readonly SaveData run;
        private readonly Dictionary<string, Stage> stages = new Dictionary<string, Stage>(StringComparer.Ordinal);
        private readonly List<int> opportunities = new List<int>();
        private int next;

        private sealed class Stage
        {
            internal int Start, Length, BoardVisits;
        }

        internal IReadOnlyList<int> Opportunities => opportunities;
        internal int LatestPosition { get; private set; } = -1;

        internal MerchantRoute(DungeonManager dungeon, SaveData run)
        {
            this.dungeon = dungeon;
            this.run = run;
            if (!dungeon || !dungeon.Race || run == null) return;
            Add(dungeon.Race.lobbyStage, onlyPlayable: true);
            if (dungeon.Race.stages != null)
                foreach (StageEntity stage in dungeon.Race.stages) Add(stage, onlyPlayable: false);
        }

        internal int Position(FloorData data)
        {
            if (data == null || data.isHidden || data.pocketDimension || string.IsNullOrEmpty(data.stageName) ||
                !stages.TryGetValue(data.stageName, out Stage stage)) return -1;
            int depth = data.nodeProgress;
            if (stage.BoardVisits > 0)
            {
                if (!string.IsNullOrEmpty(data.questBoardEventId))
                {
                    if (string.IsNullOrEmpty(data.guid) || depth < 1) return -1;
                    string prefix = BoardKey + Uri.EscapeDataString(data.stageName) + ".";
                    string key = prefix + "Floor." + data.guid;
                    depth = run.GetInt(key, 0);
                    if (depth <= 0)
                    {
                        int count = Math.Max(0, run.GetInt(prefix + "Count", 0));
                        depth = count < stage.BoardVisits ? count + 1 : stage.BoardVisits + 1;
                        run.SetInt(key, depth);
                        run.SetInt(prefix + "Count", depth);
                    }
                    depth = Math.Min(depth, stage.BoardVisits + 1);
                }
                else if (depth == 1) depth = stage.BoardVisits + 1; // Native board boss uses nodeProgress=1.
                else if (depth != 0) return -1;
            }
            if (depth < 0 || depth >= stage.Length) return -1;
            int position = stage.Start + depth;
            LatestPosition = Math.Max(LatestPosition, position);
            return position;
        }

        internal void ObserveHistory()
        {
            if (!dungeon) return;
            // A late guest can have only a suffix of the route. Recover the host's
            // chronology first, then add any missing GUIDs from other current players.
            for (int pass = 0; pass < 2; pass++)
                foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                {
                    if (!spawner || !spawner.PlayerAvatar || spawner.PlayerAvatar.isLocalPlayer != (pass == 0)) continue;
                    foreach (string guid in spawner.PlayerAvatar.floorTravelHistory)
                        if (!string.IsNullOrEmpty(guid) && dungeon.generatedFloors.TryGetValue(guid, out FloorData data)) Position(data);
                }
        }

        private void Add(StageEntity asset, bool onlyPlayable)
        {
            if (!asset || string.IsNullOrEmpty(asset.name) || stages.ContainsKey(asset.name)) return;
            Type type = asset.GetType();
            if (type != typeof(StageEntity) && type != typeof(StageEntity_Lobby) && type != typeof(StageEntity_Choice) &&
                type != typeof(StageEntity_Infinity) && type != typeof(StageEntity_GrasslandTown)) return;
            var eligible = new List<bool> { Supports(asset.firstFloor) };
            int boardVisits = 0;
            if (asset is StageEntity_Choice choice)
            {
                if (choice.steps == null) return;
                foreach (var step in choice.steps)
                    eligible.Add(ChoiceStep(choice, step));
            }
            else if (asset is StageEntity_Infinity infinity)
            {
                if (infinity.steps == null) return;
                foreach (var step in infinity.steps)
                    eligible.Add(InfinityStep(infinity, step));
            }
            else if (asset is StageEntity_GrasslandTown board)
            {
                if (board.eventCountToClear <= 0 || board.eventCountToClear > board.eventCount) return;
                boardVisits = board.eventCountToClear;
                bool possible = false;
                if (board.nodeEvents != null)
                    foreach (var mission in board.nodeEvents)
                        if (mission && Supports(mission.floorPrefab)) { possible = true; break; }
                for (int i = 0; i < boardVisits; i++) eligible.Add(possible);
                eligible.Add(false); // Boss follows the required visits, regardless of its native depth.
            }
            if (onlyPlayable && !eligible.Contains(true)) return;
            stages.Add(asset.name, new Stage { Start = next, Length = eligible.Count, BoardVisits = boardVisits });
            for (int i = 0; i < eligible.Count; i++) if (eligible[i]) opportunities.Add(next + i);
            next = checked(next + eligible.Count);
        }

        private bool ChoiceStep(StageEntity_Choice stage, StageEntity_Choice.EachStep step)
        {
            if (step == null || step.choiceMax <= 0) return false;
            if (step.matchEvents != null)
                foreach (var node in step.matchEvents)
                    if (node.fixedPool ? Pool(stage.fixedEventFloorPrefabs, node.threat, node.ev) :
                        RandomPool(stage.randomEventFloorPrefabs, node.threat, stage.battleZoneFloorChance)) return true;
            if (step.possibleMainEvents != null && step.possibleMainEvents.Length > 0 && step.possibleThreats != null)
                foreach (var threat in step.possibleThreats)
                    if (RandomPool(stage.randomEventFloorPrefabs, threat, stage.battleZoneFloorChance)) return true;
            return false;
        }

        private bool InfinityStep(StageEntity_Infinity stage, StageEntity_Infinity.EachStep step)
        {
            if (step == null || step.choiceMax <= 0) return false;
            if (step.matchEvents != null)
                foreach (var node in step.matchEvents)
                    if (node.fixedPool ? Pool(stage.fixedEventFloorPrefabs, node.threat, node.ev) :
                        RandomPool(stage.randomEventFloorPrefabs, node.threat, 0)) return true;
            if (step.possibleMainEvents != null && step.possibleMainEvents.Length > 0 && step.possibleThreats != null)
                foreach (var threat in step.possibleThreats)
                    if (RandomPool(stage.randomEventFloorPrefabs, threat, 0)) return true;
            return false;
        }

        private bool RandomPool(GameObject[] prefabs, EFloorThreatType threat, float battleZoneChance)
        {
            if (dungeon.Race.enhancedDisturbance && threat == EFloorThreatType.Battle) threat = EFloorThreatType.HardBattle;
            if (threat == EFloorThreatType.Battle && battleZoneChance > 0 && prefabs != null)
            {
                bool replacementExists = false;
                foreach (GameObject prefab in prefabs)
                {
                    FloorGenerator floor = prefab ? prefab.GetComponent<FloorGenerator>() : null;
                    if (!floor || floor.floorThreatType != EFloorThreatType.BattleFloor) continue;
                    replacementExists = true;
                    if (Supports(floor)) return true;
                }
                if (replacementExists && battleZoneChance >= 1) return false;
            }
            return Pool(prefabs, threat, null);
        }

        private static bool Pool(GameObject[] prefabs, EFloorThreatType threat, EFloorMainEventType? mainEvent)
        {
            if (prefabs == null || threat == EFloorThreatType.Boss) return false;
            foreach (GameObject prefab in prefabs)
            {
                FloorGenerator floor = prefab ? prefab.GetComponent<FloorGenerator>() : null;
                if (Supports(floor) && floor.floorThreatType == threat && (!mainEvent.HasValue || floor.floorMainEventType == mainEvent.Value)) return true;
            }
            return false;
        }

        private static bool Supports(FloorGenerator floor) => floor && !floor.isSafeFloor && !floor.isTrainingFloor &&
            floor.floorThreatType != EFloorThreatType.Boss &&
            (floor is EnhancedProceduralFloorGenerator || floor is LibraryFloorGenerator || floor is FixedFloorGenerator);
    }
}
