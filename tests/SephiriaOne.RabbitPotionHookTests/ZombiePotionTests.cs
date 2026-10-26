using SephiriaOne;

internal static class ZombiePotionTests
{
    internal static void Run(Action<string, Action> check, Action<bool, string> assert,
        Func<int, bool, (PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item)> setup,
        Action install)
    {
        foreach (bool local in new[] { false, true })
        foreach (int id in new[] { 0, 1 })
        foreach (bool infinite in new[] { false, true })
            check($"Zombie HP admission/completion: potion {id}, host {local}, infinite {infinite}", () =>
            {
                var s = setup(id, local);
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.RabbitPotionsForUse = new(true, true, true, true, 10);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = infinite };
                CombatMatchRuntime.ZombiePotionRule = p => ReferenceEquals(p, s.player);
                install();
                if (local) s.controller.UseItemKeyDown(); else s.controller.GuestUseItemKeyDown();
                assert(s.controller.StartedDrinks == 0, "Zombie should not start drinking");
                s.controller.RunDrink(); // A pending animation must also be rejected.
                assert(s.player.Heals.Count == 0 && s.player.mp == 30 && s.item.Quantity == 3 &&
                    survival.StatGains == 0 && s.player.DrinkEvents == 0 && s.controller.CleanupCalls == 1,
                    "Blocked completion must have no gameplay effects and retain native cleanup");
            });
        foreach (bool local in new[] { false, true })
        foreach (int id in new[] { 0, 1 })
        foreach (bool infinite in new[] { false, true })
        foreach (bool growth in new[] { false, true })
            check($"Starting Zombie potion grace: host {local}, potion {id}, infinite {infinite}, growth {growth}", () =>
            {
                var s = setup(id, local); s.player.currentCostume = "Mole";
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = infinite };
                int population = 2;
                CombatMatchRuntime.ZombiePotionRule = _ => population > 2;
                install();
                if (local) s.controller.UseItemKeyDown(); else s.controller.GuestUseItemKeyDown();
                assert(s.controller.StartedDrinks == 1, "Initial Zombie population may begin drinking");
                if (growth) population++;
                s.controller.RunDrink();
                assert(s.player.Heals.Count == (growth ? 0 : 1) && s.item.Quantity == (growth || infinite ? 3 : 2) &&
                    s.player.mp == 30 && s.controller.CleanupCalls == 1,
                    "Completion follows live population; otherwise the existing finite/infinite PvP rule remains authoritative");
            });
        foreach (bool local in new[] { false, true })
        foreach (bool infinite in new[] { false, true })
        foreach (bool converted in new[] { false, true })
            check($"Zombie Sample remains native: host {local}, infinite {infinite}, conversion {converted}", () =>
            {
                var s = setup(37, local);
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.RabbitPotionsForUse = new(true, true, true, true, 10);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = infinite };
                bool zombie = !converted;
                CombatMatchRuntime.ZombiePotionRule = _ => zombie;
                install();
                if (local) s.controller.UseItemKeyDown(); else s.controller.GuestUseItemKeyDown();
                assert(s.controller.StartedDrinks == 1, "Sample admission must not be blocked for Zombies");
                zombie = true;
                s.controller.RunDrink();
                assert(s.player.Heals.Count == 1 && s.item.Quantity == 2 && s.player.mp == 30 &&
                    survival.StatGains == 1 && s.player.DrinkEvents == 1 && s.controller.CleanupCalls == 1,
                    "Sample heals and is consumed normally, without addon MP charge or Survival suppression, even after conversion");
            });
        foreach (bool infinite in new[] { false, true })
            check("Human conversion and live release preserve PvP potion policy: " + infinite, () =>
            {
                var s = setup(0, false); s.player.currentCostume = "Mole";
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = infinite };
                bool zombie = false;
                CombatMatchRuntime.ZombiePotionRule = _ => zombie;
                install(); s.controller.GuestUseItemKeyDown();
                assert(s.controller.StartedDrinks == 1, "Human can start drinking");
                zombie = true; s.player.IsDead = true;
                s.controller.RunDrink();
                assert(s.item.Quantity == 3 && s.player.Heals.Count == 0 && s.controller.CleanupCalls == 1,
                    "Conversion during animation blocks even while dead/pending respawn");
                s.player.IsDead = false; zombie = false; // Toggle off, role removed, or match ended.
                s.controller.RunDrink();
                assert(s.player.Heals.Count == 1 && s.item.Quantity == (infinite ? 3 : 2),
                    "Human/current PvP rule resumes immediately without cached faction");
            });
        foreach (int id in new[] { 2, 99 })
            check("Zombie restriction excludes unrelated potions: " + id, () =>
            {
                var s = setup(id, false);
                CombatMatchRuntime.ZombiePotionRule = _ => true;
                install(); s.controller.GuestUseItemKeyDown(); s.controller.RunDrink();
                assert(s.controller.StartedDrinks == 1 && s.item.Quantity == 2, "Non-HP potion behavior remains native");
            });
        check("Zombie restriction unload restores native potion use", () =>
        {
            var s = setup(0, false); CombatMatchRuntime.ZombiePotionRule = _ => true;
            install(); RabbitPotionFeature.Shutdown(); s.controller.GuestUseItemKeyDown(); s.controller.RunDrink();
            assert(s.controller.StartedDrinks == 1 && s.item.Quantity == 2, "No potion restriction after unload");
        });
    }
}
