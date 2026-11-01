using SephiriaOne;

internal static class PvpRabbitMpTests
{
    internal static void Run(Action<string, Action> check, Action<bool, string> assert,
        Func<int, bool, (PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item)> setup,
        Action install)
    {
        foreach (int id in new[] { 0, 1 })
        foreach (bool local in new[] { false, true })
        foreach (bool infinite in new[] { false, true })
        foreach (int mp in new[] { 49, 50, 100 })
            check($"PvP Rabbit 50 MP admission {id}/{local}/{infinite}/{mp}", () =>
            {
                var s = setup(id, local); s.player.mp = mp;
                SessionSettings.RabbitPotionsForUse = new(true, false, true, true, 50);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = infinite };
                install(); s.controller.RunDrink();
                bool accepted = infinite || mp >= 50;
                assert(s.player.mp == (accepted && !infinite ? mp - 50 : mp) &&
                    s.player.Heals.Count == (accepted ? 1 : 0) &&
                    s.item.Quantity == (accepted && !infinite ? 2 : 3),
                    "Only infinite PvP waives Rabbit's fee; insufficient MP must reject both healing and consumption");
            });
        foreach (string invalid in new[] { "inventory-initializing", "controller-owner", "disconnected" })
            check("Required Rabbit policy cannot fall through to a free native drink: " + invalid, () =>
            {
                var s = setup(1, false); s.player.mp = 100;
                SessionSettings.RabbitPotionsForUse = new(true, false, true, true, 50);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = false };
                if (invalid == "inventory-initializing") s.player.Inventory.canBroadcast = 0;
                if (invalid == "controller-owner") s.controller.connectionToClient = null;
                if (invalid == "disconnected") Mirror.NetworkServer.connections.Clear();
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                install(); s.controller.RunDrink();
                assert(s.player.Heals.Count == 0 && s.player.mp == 100 && s.item.Quantity == 3 &&
                    survival.StatGains == 0 && s.controller.CleanupCalls == 1,
                    "An incomplete policy context must cancel without granting free healing or changing stale state");
                s.controller.RunDrink();
                string reason = invalid == "inventory-initializing" ? "Readiness" : "Ownership";
                assert(UnityEngine.Debug.Warnings.Count == 1 && UnityEngine.Debug.Warnings[0].Contains(reason),
                    "Rejected use must explain the relevant cause without logging each attempt");
            });
        check("Live infinite toggle returns to the configured 50 MP cost", () =>
        {
            var s = setup(1, false); s.player.mp = 100;
            SessionSettings.RabbitPotionsForUse = new(true, false, true, true, 50);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            install(); s.controller.RunDrink();
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = false };
            s.controller.RunDrink();
            assert(s.player.mp == 50 && s.item.Quantity == 2 && s.player.Heals.Count == 2,
                "Turning infinite off must restore charging without a new session or potion grant");
        });
        foreach (int id in new[] { 0, 1, 37 })
            check("Incomplete inventory without an applicable policy remains native: " + id, () =>
            {
                var s = setup(id, false); s.player.Inventory.canBroadcast = 0;
                if (id == 37) SessionSettings.RabbitPotionsForUse = new(true, true, true, true, 50);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = false };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 2 && s.player.Heals.Count == 1 && s.player.MpWrites == 0,
                    "Policy validation must not block unmodified native or Sample drinks");
            });
        check("Required policy resumes after inventory initialization", () =>
        {
            var s = setup(1, false); s.player.mp = 100;
            SessionSettings.RabbitPotionsForUse = new(true, false, true, true, 50);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = false };
            s.player.Inventory.canBroadcast = 0;
            install(); s.controller.RunDrink();
            s.player.Inventory.canBroadcast = 1;
            s.controller.RunDrink();
            assert(s.player.mp == 50 && s.player.MpWrites == 1 && s.item.Quantity == 2 && s.player.Heals.Count == 1,
                "Failed completion must leave no fee reservation or stale state on the next valid use");
        });
    }
}
