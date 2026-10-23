using SephiriaOne;

internal static class PvpRabbitInfiniteTests
{
    internal static void Run(Action<string, Action> check, Action<bool, string> assert,
        Func<int, bool, (PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item)> setup,
        Action install)
    {
        foreach (int id in new[] { 0, 1, 37 })
        foreach (bool local in new[] { false, true })
        foreach (bool general in new[] { false, true })
        foreach (bool block in new[] { false, true })
        foreach (bool balance in new[] { false, true })
            check($"Rabbit PvP precedence {id}/{local}/{general}/{block}/{balance}", () =>
            {
                var s = setup(id, local);
                SessionSettings.RabbitPotionsForUse = new(true, false, true, true, 10);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = general,
                    BlockRabbitInfinite = block, Balance = balance };
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                install(); s.controller.RunDrink();
                bool retained = id != 37 && (general || !block);
                assert(s.item.Quantity == (retained ? 3 : 2) && s.player.Heals.Count == 1,
                    "General infinite takes priority; Sample always consumes; restricted Rabbit drinks consume natively");
                assert(s.player.mp == (id == 37 || general ? 30 : 20) && survival.StatGains == (id == 37 ? 1 : 0),
                    "The override does not alter separate Rabbit MP/Survival rules; general PvP infinite remains free and suppressed");
            });
        foreach (string transition in new[] { "death", "end", "disable-restriction", "spectator" })
            check("Finite Rabbit drink remains finite across callback: " + transition, () =>
            {
                var s = setup(1, false);
                SessionSettings.RabbitPotionsForUse = new(true, false);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true };
                s.player.OnPotionEvent = () =>
                {
                    if (transition == "death") { s.player.IsDead = true; s.controller.currentWieldingItem = null; }
                    if (transition == "end") SessionSettings.FriendlyFireForHit = default;
                    if (transition == "disable-restriction") SessionSettings.FriendlyFireForHit = new() { Enabled = true, BlockRabbitInfinite = false };
                    if (transition == "spectator") CombatMatchRuntime.ParticipantRule = _ => false;
                };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 2 && s.player.Heals.Count == 1, "Callback cannot retroactively retain a finite drink");
            });
        check("Restriction takes effect at completion and restores only ability on exit", () =>
        {
            var s = setup(0, false);
            SessionSettings.RabbitPotionsForUse = new(true, false);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, BlockRabbitInfinite = false };
            s.player.OnPotionEvent = () => SessionSettings.FriendlyFireForHit = new() { Enabled = true };
            install(); s.controller.RunDrink();
            assert(s.item.Quantity == 2, "Live enable blocks a drink admitted before restriction");
            s.player.OnPotionEvent = null;
            SessionSettings.FriendlyFireForHit = default;
            s.controller.RunDrink();
            assert(s.item.Quantity == 2 && s.player.Heals.Count == 2, "Exit restores Rabbit retention without restoring spent potions");
        });
        foreach (string exclusion in new[] { "selection", "mode", "off" })
            check("Nonparticipant Rabbit retains costume ability: " + exclusion, () =>
            {
                var s = setup(0, false);
                SessionSettings.RabbitPotionsForUse = new(true, false);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true };
                if (exclusion == "selection") FriendlyFireParticipants.Selected.Add(new PlayerAvatar());
                if (exclusion == "mode") CombatMatchRuntime.ParticipantRule = _ => false;
                if (exclusion == "off") SessionSettings.FriendlyFireForHit = default;
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 3, "Outside live combat admission, costume policy stays native");
            });
    }
}
