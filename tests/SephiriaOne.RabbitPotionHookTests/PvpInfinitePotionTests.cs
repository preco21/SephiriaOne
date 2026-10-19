using SephiriaOne;

internal static class PvpInfinitePotionTests
{
    internal static void Run(Action<string, Action> check, Action<bool, string> assert,
        Func<int, bool, (PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item)> setup, Action install)
    {
        foreach (string costume in new[] { "HolyRabbit", "Mole", "PinkRabbit" })
        foreach (int id in new[] { 0, 1 })
        foreach (bool local in new[] { false, true })
            check($"PvP infinite HP {costume}/{id}/local={local}", () =>
            {
                var s = setup(id, local); s.player.currentCostume = costume;
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 3 && s.player.Heals.Count == 1 && survival.StatGains == 1 && s.player.MpWrites == 0,
                    "Only quantity changes; native self-heal, talent and MP stay native");
                SessionSettings.FriendlyFireForHit = new() { Enabled = true };
                s.controller.RunDrink();
                assert(s.item.Quantity == 2 && s.player.Heals.Count == 2, "Toggle off resumes consumption immediately");
            });
        foreach (int id in new[] { 2, 37, 28 })
            check("PvP infinite excludes non-HP/Sample " + id, () =>
            {
                var s = setup(id, false); s.player.currentCostume = "Mole";
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 2 && s.player.Heals.Count == 1, "Unsupported potion retains native behavior");
            });
        foreach (string mutation in new[] { "toggle", "end", "spectator", "connection", "run", "costume" })
            check("PvP infinite rejects mid-drink stale state: " + mutation, () =>
            {
                var s = setup(1, false); s.player.currentCostume = "Mole";
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                s.player.OnPotionEvent = () =>
                {
                    if (mutation == "toggle") SessionSettings.FriendlyFireForHit = new() { Enabled = true };
                    if (mutation == "end") SessionSettings.FriendlyFireForHit = default;
                    if (mutation == "spectator") CombatMatchRuntime.ParticipantRule = _ => false;
                    if (mutation == "connection") Mirror.NetworkServer.connections.Clear();
                    if (mutation == "run") SaveManager.CurrentRun = new();
                    if (mutation == "costume") s.player.currentCostume = "Turtle";
                };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 2, "No retention across revoked or stale admission");
            });
        check("Non-Rabbit PvP drink never receives Rabbit mechanics", () =>
        {
            var s = setup(1, false); s.player.currentCostume = "Mole";
            var near = new PlayerAvatar(); _ = new PlayerSpawner(near);
            var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
            SessionSettings.RabbitPotionsForUse = new(true, true, true, true, blockPvpShare: false);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            install(); s.controller.RunDrink();
            assert(s.item.Quantity == 3 && s.player.MpWrites == 0 && survival.StatGains == 1 && near.Heals.Count == 0,
                "Rabbit MP fee, sharing and talent suppression cannot leak through shared context");
        });
        check("Rabbit infinite option remains independent in PvE", () =>
        {
            var s = setup(0, false); SessionSettings.RabbitPotionsForUse = new(true, false);
            install(); s.controller.RunDrink(); assert(s.item.Quantity == 3, "Rabbit still has native feature outside PvP");
            SessionSettings.RabbitPotionsForUse = default; s.controller.RunDrink(); assert(s.item.Quantity == 2, "Both options off consume");
        });
        foreach (bool before in new[] { false, true })
            check("PvP infinite protects death transition, alreadyDead=" + before, () =>
            {
                var s = setup(1, false); s.player.currentCostume = "Mole";
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                if (before) s.player.IsDead = true;
                else s.player.OnPotionEvent = () => { s.player.IsDead = true; s.controller.currentWieldingItem = null; };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 3 && s.player.Heals.Count == (before ? 0 : 1), "Pending dead drink is rejected; admitted drink retains quantity through death cleanup");
            });
        check("PvP does not bypass Tension for other costumes", () =>
        {
            var s = setup(1, false); s.player.currentCostume = "Mole";
            SessionSettings.RabbitPotionsForUse = new(true, false);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            install();
            var filter = HarmonyLib.AccessTools.Method(typeof(RabbitPotionNativeHooks), "FilterTensionBlock");
            assert((bool)filter.Invoke(null, new object[] { true, s.controller, s.item }), "PvP retention must not grant Rabbit Tension exemption");
        });
        check("Completed PvP drink is atomic before the controller notification", () =>
        {
            var s = setup(1, false); s.player.currentCostume = "Mole";
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            s.controller.OnDrinkPotionServerside += _ => SessionSettings.FriendlyFireForHit = default;
            install(); s.controller.RunDrink();
            assert(s.item.Quantity == 3 && s.player.Heals.Count == 1,
                "A later controller notification cannot retroactively consume a completed infinite drink");
            s.controller.RunDrink();
            assert(s.item.Quantity == 2 && s.player.Heals.Count == 2, "The next drink immediately uses native consumption");
        });
    }
}
