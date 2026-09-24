using SephiriaOne;

internal static class DisconnectRuntimeTests
{
    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add)
    {
        void Command(string text)
        {
            var result = SettingsActions.Execute(text);
            check(result.Success, text + ": " + string.Join("; ", result.Messages));
        }
        var party = new List<PlayerSpawner> { start() };
        for (uint id = 2; id <= 5; id++) party.Add(add(id, (int)id * 3, (int)id * 2, (int)id));
        foreach (var spawner in party) spawner.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = (int)spawner.netId * 5;
        Command("/fountain x3"); Command("/choices item 3");
        Command("/stats luck x3"); Command("/stats cooldown x3");
        Command("/resources talents +20"); Command("/resources fruit +2");
        for (int cycle = 0; cycle < 8; cycle++)
        {
            foreach (var spawner in party)
            {
                var player = spawner.PlayerAvatar;
                player.customStats["LUCK"] += 1; // Native costume/passive/item change.
                player.calculatedBonusStats["LUCK"] = cycle % 3;
                player.customStatsAmp["LUCK"] = cycle % 2 * 100;
                player.currentFloorGuid = "floor-" + cycle;
            }
            HorayModAPI.StartSession(cycle % 2 == 0);
            SessionSettings.BeforeNativeRead("Five-player transition fixture");
            foreach (var spawner in party)
            {
                var player = spawner.PlayerAvatar;
                int native = player.customStats["LUCK"] - player.customStats["SEPHIRIAONE_STAT_LUCK"];
                native = (native + player.calculatedBonusStats["LUCK"]) * (100 + player.customStatsAmp["LUCK"]) / 100;
                check(player.GetCustomStatUnsafe("LUCK") == native * 3, "Five players retain individual luck multiplier after native/floor changes");
                check(player.maxPassivePoint == 25 && player.GetCustomStatUnsafe("FRUITCOUNT") == 2,
                    "Talent/fruit maintenance does not compound at stage events");
            }
            int writes = party.Sum(p => p.PlayerAvatar.customStats.Writes);
            for (int tick = 0; tick < 500; tick++) SessionSettings.Synchronize();
            check(writes == party.Sum(p => p.PlayerAvatar.customStats.Writes), "Five-player unchanged ticks do not enqueue repeated stat/marker writes");

            // Two disappear together; reuse their IDs with entirely new avatars.
            foreach (int index in new[] { 1, 3 }) PlayerSpawner.MultiplayerList.Remove(party[index]);
            SessionSettings.Synchronize();
            foreach (int index in new[] { 1, 3 })
            {
                uint id = (uint)index + 1;
                party[index] = add(id, 7 + cycle, 2, 1);
                party[index].PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = 10;
                party[index].connectionToClient.isReady = false;
            }
            SessionSettings.Synchronize();
            foreach (int index in new[] { 1, 3 })
            {
                var replacement = party[index];
                check(replacement.PlayerAvatar.customStats["LUCK"] == 7 + cycle, "Unready replacement is not mistaken for previous connection");
                replacement.connectionToClient.isReady = true;
            }
            SessionSettings.Synchronize();
            foreach (int index in new[] { 1, 3 })
            {
                var p = party[index].PlayerAvatar;
                check(p.GetCustomStatUnsafe("LUCK") == (7 + cycle) * 3 && p.GetCustomStatUnsafe("COOLDOWNRECOVERYSPEED") == 30 &&
                    p.Inventory.dimensionPocket == 6 && p.GetCustomStatUnsafe("EXTRAITEMCHOICES") == 4 &&
                    p.maxPassivePoint == 25 && p.GetCustomStatUnsafe("FRUITCOUNT") == 2,
                    "Returning connection receives every live family from its new native baseline");
            }
        }

        var pending = add(9, 11, 2, 1);
        pending.PlayerAvatar.BeforeRead = _ => throw new Exception("Diagnostic invoked native stat callback");
        int before = pending.PlayerAvatar.customStats.Writes;
        var lines = SessionSettings.DescribeDisconnect(9);
        check(lines.Any(l => l.Contains("not been enrolled")) && lines.Any(l => l.Contains("LUCK: raw=11")),
            "Disconnect snapshot describes unenrolled state without applying policy");
        check(pending.PlayerAvatar.customStats.Writes == before && pending.PlayerAvatar.maxPassivePoint == 5,
            "Disconnect snapshot does not write, initialize or reconcile a departing player");
        check(SessionSettings.DescribeDisconnect(0).Count > 0, "Identityless departure still has session context");
        pending.PlayerAvatar.BeforeRead = null;
    }
}
