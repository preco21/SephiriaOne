using SephiriaOne;

internal static class PvpRabbitPolicyTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        void Command(string args) => check(SettingsActions.Execute("/one friendlyfire " + args).Success, "Rabbit PvP command: " + args);
        PlayerAvatar[] New(int count)
        {
            UnityEngine.Time.unscaledTime = 0;
            var players = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint id = 2; id <= count; id++) players.Add(add(id).PlayerAvatar);
            SessionSettings.EnsureResourceScope();
            return players.ToArray();
        }
        var all = New(9);
        Command("infinite-hp off"); Command("on");
        check(all.All(PvpPotionPolicy.BlocksRabbitInfinite), "All nine casual participants receive restriction with balancing off");
        PvpPotionSupplies.Tick();
        int writes = all.Sum(p => p.Inventory.PotionWrites);
        Command("infinite-hp on"); PvpPotionSupplies.Tick();
        Command("infinite-hp off"); PvpPotionSupplies.Tick();
        check(all.Sum(p => p.Inventory.PotionWrites) == writes && SessionSettings.FriendlyFireForHit.AutoPotion,
            "Consumption edits do not retrigger automatic supplies or change the grant option");
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(all[0]), true);
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(all[1]), true);
        check(all.Take(2).All(PvpPotionPolicy.BlocksRabbitInfinite) && all.Skip(2).All(p => !PvpPotionPolicy.BlocksRabbitInfinite(p)),
            "Restriction follows shared participant selection");
        Command("block-rabbit-infinite off");
        check(all.Take(2).All(PvpPotionPolicy.BlocksRabbitInfinite) && !SessionSettings.FriendlyFireForHit.InfiniteHpPotions, "Legacy opt-out cannot bypass finite PvP consumption");
        Command("block-rabbit-infinite reset");
        PvpPotionSupplies.Tick();
        check(all.Sum(p => p.Inventory.PotionWrites) == writes, "Deprecated Rabbit commands do not grant extra potions");
        check(PvpPotionPolicy.BlocksRabbitInfinite(all[0]) && !SessionSettings.FriendlyFireForHit.InfiniteHpPotions, "Reset does not change general option");
        Command("off"); check(all.All(p => !PvpPotionPolicy.BlocksRabbitInfinite(p)), "Casual exit restores costume policy immediately");

        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            all = New(2); Command("infinite-hp off");
            check(SettingsActions.Execute("/one " + mode + " start").Success, mode + " starts");
            check(all.All(p => !PvpPotionPolicy.BlocksRabbitInfinite(p)), mode + " countdown is outside combat admission");
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
            check(all.All(PvpPotionPolicy.BlocksRabbitInfinite), mode + " live participants inherit restriction");
            Command("auto-potion off");
            check(all.All(p => PvpPotionPolicy.RuleFor(p) == HpPotionRule.PvpFinite), mode + " supply opt-out does not change finite consumption");
            Command("block-rabbit-infinite off");
            check(CombatMatchRuntime.IsRunning && all.All(PvpPotionPolicy.BlocksRabbitInfinite), mode + " legacy opt-out changes neither match nor precedence");
            Command("block-rabbit-infinite on");
            check(CombatMatchRuntime.IsRunning && all.All(PvpPotionPolicy.BlocksRabbitInfinite), mode + " live enable retains match");
            Command("infinite-hp on");
            check(all.All(PvpPotionPolicy.Applies), mode + " general infinite retains priority");
            check(SettingsActions.Execute("/one " + mode + " stop").Success, mode + " stops");
            check(all.All(p => !PvpPotionPolicy.BlocksRabbitInfinite(p) && !PvpPotionPolicy.Applies(p)), mode + " cleanup restores native policy");
        }
        all = New(2); Command("on");
        var old = all[1].spawner;
        PlayerSpawner.MultiplayerList.Remove(old); Mirror.NetworkServer.connections.Remove(old.connectionToClient.connectionId);
        var rejoined = add(2).PlayerAvatar;
        check(!PvpPotionPolicy.BlocksRabbitInfinite(all[1]) && PvpPotionPolicy.BlocksRabbitInfinite(rejoined), "New connection re-evaluates live policy; old avatar loses admission");
        RabbitPotionFeature.Available = false;
        check(SettingsActions.Execute("/one friendlyfire block-rabbit-infinite off").Success && PvpPotionPolicy.BlocksRabbitInfinite(rejoined), "Deprecated command is harmless even when hooks are unavailable");
        Command("block-rabbit-infinite on"); Command("block-rabbit-infinite reset");
        RabbitPotionFeature.Available = true;
    }
}
