using SephiriaOne;

internal static class PvpInfinitePotionTests
{
    internal static void Run(Action<string, Action> check, Action<bool, string> assert,
        Func<int, bool, (PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item)> setup, Action install)
    {
        check("Rabbit infinite cannot bypass finite PvP potions by default", () =>
        {
            var s = setup(0, false);
            SessionSettings.RabbitPotionsForUse = new(true, false);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true };
            install(); s.controller.RunDrink();
            assert(s.item.Quantity == 2 && s.player.Heals.Count == 1,
                "Active Rabbit must consume one HP potion when general PvP infinite is off");
        });
        PvpRabbitInfiniteTests.Run(check, assert, setup, install);
        foreach (string costume in new[] { "HolyRabbit", "Mole", "PinkRabbit" })
        foreach (int id in new[] { 0, 1 })
        foreach (bool local in new[] { false, true })
        foreach (bool balance in new[] { false, true })
            check($"PvP infinite HP {costume}/{id}/local={local}/balance={balance}", () =>
            {
                var s = setup(id, local); s.player.currentCostume = costume;
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true, Balance = balance };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 3 && s.player.Heals.Count == 1 && survival.StatGains == 0 && s.player.MpWrites == 0,
                    "Infinite PvP HP drinks retain quantity and heal without MP cost or Survival stat gain");
                SessionSettings.FriendlyFireForHit = new() { Enabled = true };
                s.controller.RunDrink();
                assert(s.item.Quantity == 2 && s.player.Heals.Count == 2 && survival.StatGains == 1,
                    "Toggle off resumes native consumption and Survival immediately");
            });
        foreach (int id in new[] { 2, 37, 28 })
            check("PvP infinite excludes non-HP/Sample " + id, () =>
            {
                var s = setup(id, false);
                SessionSettings.RabbitPotionsForUse = new(true, true, true, true, 10000);
                s.player.mp = 0;
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 2 && s.player.Heals.Count == 1 && survival.StatGains == 1 && s.player.MpWrites == 0,
                    "Sample/other potions retain consumption and Survival even with Rabbit and PvP options enabled");
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
            assert(s.item.Quantity == 3 && s.player.MpWrites == 0 && survival.StatGains == 0 && near.Heals.Count == 0,
                "PvP suppression applies independently of Rabbit; its fee and sharing never leak to other costumes");
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
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                if (before) s.player.IsDead = true;
                else s.player.OnPotionEvent = () => { s.player.IsDead = true; s.controller.currentWieldingItem = null; };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 3 && s.player.Heals.Count == (before ? 0 : 1) && survival.StatGains == 0,
                    "Pending dead drink is rejected; admitted drink retains quantity and Survival suppression through death cleanup");
            });
        foreach (int id in new[] { 0, 1 })
        foreach (bool local in new[] { false, true })
        foreach (bool rabbitSuppress in new[] { false, true })
            check($"PvP waives Rabbit fee with zero MP {id}/local={local}/rabbit-suppress={rabbitSuppress}", () =>
            {
                var s = setup(id, local); s.player.mp = 0;
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.RabbitPotionsForUse = new(true, false, true, rabbitSuppress, 10000);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                int listeners = 0; s.player.OnDrinkPotion += _ => listeners++;
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 3 && s.player.Heals.Count == 1 && survival.StatGains == 0 &&
                    listeners == 1 && s.player.DrinkEvents == 1 && s.player.mp == 0 && s.player.MpWrites == 0,
                    "Zero-MP Rabbit heals freely in PvP without Survival, preserving other listeners");
                assert(s.player.SystemMessages.Count == 0 && s.player.spawner.connectionToClient.Notices.Count == 0,
                    "Eligible PvP drink must not issue a low-MP rejection");
                SessionSettings.FriendlyFireForHit = default;
                s.controller.RunDrink();
                assert(s.player.Heals.Count == 1 && s.item.Quantity == 3,
                    "Leaving PvP immediately restores Rabbit's insufficient-MP rejection");
                s.player.mp = 10000; s.controller.RunDrink();
                assert(s.player.Heals.Count == 2 && s.player.mp == 0 && survival.StatGains == (rabbitSuppress ? 0 : 1),
                    "PvE restores configured Rabbit fee and Survival policy");
            });
        foreach (string excluded in new[] { "selection", "mode", "balance-only", "option-off" })
            check("Ineligible player keeps native potion and Survival: " + excluded, () =>
            {
                var s = setup(0, false); s.player.currentCostume = "Mole";
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                if (excluded == "selection") FriendlyFireParticipants.Selected.Add(new PlayerAvatar());
                if (excluded == "mode") CombatMatchRuntime.ParticipantRule = _ => false;
                if (excluded == "balance-only") SessionSettings.FriendlyFireForHit = new() { Balance = true, InfiniteHpPotions = true };
                if (excluded == "option-off") SessionSettings.FriendlyFireForHit = new() { Enabled = true };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 2 && survival.StatGains == 1 && s.player.Heals.Count == 1,
                    "Native behavior remains outside active infinite-PvP admission");
            });
        check("Direct drink cannot retain a free PvP potion outside guarded controller completion", () =>
        {
            var s = setup(0, false); s.player.currentCostume = "Mole";
            var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            install(); s.potion.Drink(out bool decreased, s.item.InstanceID);
            assert(decreased && survival.StatGains == 1, "Standalone drink cannot retain a potion without suppression scope");
        });
        check("Revoked PvP admission cannot return after Survival has fired", () =>
        {
            var s = setup(1, false); s.player.currentCostume = "Mole";
            var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            s.player.OnPotionEvent = () => SessionSettings.FriendlyFireForHit = default;
            s.player.OnHealed = () => SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            install(); s.controller.RunDrink();
            assert(s.item.Quantity == 2 && survival.StatGains == 1,
                "A drink that allowed native Survival cannot regain PvP retention in a later callback");
        });
        check("Nested direct drink cannot borrow outer PvP retention or Survival suppression", () =>
        {
            var s = setup(0, false); s.player.currentCostume = "Mole";
            var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            bool nestedDecreased = false;
            s.player.OnPotionEvent = () =>
            {
                s.player.OnPotionEvent = null;
                s.potion.Drink(out nestedDecreased, s.item.InstanceID);
            };
            install(); s.controller.RunDrink();
            assert(nestedDecreased && survival.StatGains == 1 && s.item.Quantity == 3 && s.player.Heals.Count == 2,
                "Only the guarded outer drink receives paired retention/suppression");
        });
        foreach (string transient in new[] { "costume", "floor", "slot", "readiness" })
            check("Transient stale drink cannot regain PvP retention after Survival: " + transient, () =>
            {
                var s = setup(1, false); s.player.currentCostume = "Mole";
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(s.player);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
                s.player.OnPotionEvent = () =>
                {
                    if (transient == "costume") s.player.currentCostume = "Turtle";
                    if (transient == "floor") s.player.currentFloorGuid = "elsewhere";
                    if (transient == "slot") s.controller.SelectedQuickSlotIdx = -1;
                    if (transient == "readiness") s.player.Inventory.canBroadcast = 0;
                };
                s.player.OnHealed = () =>
                {
                    s.player.currentCostume = "Mole"; s.player.currentFloorGuid = "floor";
                    s.controller.SelectedQuickSlotIdx = 0; s.player.Inventory.canBroadcast = 1;
                };
                install(); s.controller.RunDrink();
                assert(s.item.Quantity == 2 && survival.StatGains == 1,
                    "Restored native identity cannot turn an already rewarded drink into a free PvP use");
            });
        check("PvP suppression only targets the exact drinker and potion event", () =>
        {
            var s = setup(0, false); s.player.currentCostume = "Mole";
            var other = setup(1, false);
            var sourceSurvival = new PassiveObject_PotionAndRandomStat(); sourceSurvival.Enable(s.player);
            var otherSurvival = new PassiveObject_PotionAndRandomStat(); otherSurvival.Enable(other.player);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true, InfiniteHpPotions = true };
            s.player.OnPotionEvent = () =>
            {
                sourceSurvival.Invoke(other.potion.effect);
                otherSurvival.Invoke(s.potion.effect);
            };
            install(); s.controller.RunDrink();
            assert(sourceSurvival.StatGains == 1 && otherSurvival.StatGains == 1 && s.item.Quantity == 3,
                "Unrelated potion effects and other players' native callbacks stay intact");
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
