using SephiriaOne;

internal static class ActiveDefenseTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
        CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
        Mirror.NetworkServer.active = true;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 8);
        var attacker = new PlayerAvatar();
        var defender = new PlayerAvatar { IsGuarding = true, MaxMp = 1000, Mp = 1000, GuardResistance = 40 };
        var damage = new DamageInstance { origin = attacker, damage = 20 };
        defender.ApplyDamage(damage);
        check(defender.Mp == 920, "Iron Wall 40 percent efficiency cannot bypass the eight percent PvP guard floor");
        defender.PerfectGuard = true; defender.ApplyDamage(damage);
        check(defender.Mp == 840, "Perfect guard cannot reduce the guaranteed MP floor");
        defender.GuardResistance = 100; defender.ApplyDamage(damage);
        check(defender.Mp == 760, "Stacked guard efficiency cannot create a free PvP block");
        SessionSettings.FriendlyFireForHit = new(true, 100);
        defender.ApplyDamage(damage);
        check(defender.Mp == 760, "Balancing off restores native free guard without editing the stat");

        void Fresh(string mode)
        {
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear(); CombatMatchRuntime.PairRule = null;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            attacker = new PlayerAvatar();
            defender = new PlayerAvatar { MaxMp = 1000, Mp = 1000 };
            damage = new DamageInstance { origin = attacker, damage = 20 };
            if (mode == "parry") defender.IsParrying = true;
            if (mode == "mp") defender.IsMpShield = true;
            if (mode == "dash") defender.Shield = 100;
            if (mode == "sub")
            {
                var shield = new UnitSubShield { owner = defender };
                defender.OnCalculateDamage = shield.OnCalculateDamage;
            }
        }
        foreach (string mode in new[] { "parry", "mp", "dash", "sub" })
        {
            Fresh(mode); defender.ApplyDamage(damage);
            check(defender.Mp == 950 && defender.Hp == 100, mode + " successful defense pays default five percent");
            Fresh(mode); defender.Mp = 13; defender.ApplyDamage(damage);
            check(defender.Mp == 0 && defender.Hp == (mode == "mp" ? 93 : 100), mode + " spends available MP and preserves native absorption overflow");
            Fresh(mode); defender.Mp = 13; int reported = 0;
            defender.OnMpUsedServerside = amount => reported += amount; defender.ApplyDamage(damage);
            check(reported == 13, mode + " MP-spend rewards receive only the available debit");
            Fresh(mode); defender.InfiniteMp = true; defender.ApplyDamage(damage);
            check(defender.Mp == 950, mode + " native infinite MP cannot bypass the minimum");
            Fresh(mode); SessionSettings.FriendlyFireForHit = new(true, 100); defender.ApplyDamage(damage);
            check(defender.Mp == (mode == "mp" ? 980 : 1000), mode + " balance-off keeps native cost");
            Fresh(mode); damage.origin = new UnitAvatar { faction = "enemy" }; damage.targetFactionLayers = -1;
            defender.ApplyDamage(damage);
            check(defender.Mp == (mode == "mp" ? 980 : 1000), mode + " incoming PvE keeps native cost");
            Fresh(mode); FriendlyFireParticipants.Select(attacker); defender.ApplyDamage(damage);
            check(defender.Mp == 1000, mode + " unselected player cannot be charged");
            Fresh(mode); CombatMatchRuntime.PairRule = (_, _) => false; defender.ApplyDamage(damage);
            check(defender.Mp == 1000, mode + " same-team or tournament-bystander hit cannot be charged");
            Fresh(mode); defender.OnMpUsedServerside = _ => defender.Mp += 100; defender.ApplyDamage(damage);
            check(defender.Mp == 950, mode + " synchronous MP refunds cannot erase the floor");
            Fresh(mode); defender.OnMpUsedServerside = _ => SessionSettings.FriendlyFireForHit = new(true, 100);
            defender.InfiniteMp = true; defender.ApplyDamage(damage);
            check(defender.Mp == 1000, mode + " ending balancing inside MP callback prevents extra writes");
            Fresh(mode); FriendlyFireParticipants.Depart(defender); defender.ApplyDamage(damage);
            check(defender.Mp == 1000, mode + " departed connection cannot be charged");
            FriendlyFireParticipants.Rejoin(defender); damage.failed = EDamageFailType.None; defender.ApplyDamage(damage);
            check(defender.Mp == 950, mode + " fresh connection uses current policy without retained shield state");
        }
        Fresh("parry"); defender.OnParry = _ => defender.Mp += 3; defender.ApplyDamage(damage);
        check(defender.Mp == 950, "Dagger parry refund cannot reduce the net floor");
        Fresh("parry"); defender.OnParry = _ => SessionSettings.FriendlyFireForHit = new(true, 100); defender.ApplyDamage(damage);
        check(defender.Mp == 1000, "Parry callback can disable balance before settlement");
        Fresh("parry"); defender.OnParry = _ => throw new InvalidOperationException("parry");
        try { defender.ApplyDamage(damage); } catch (InvalidOperationException) { }
        check(defender.Mp == 1000 && FriendlyFireRuntime.ParryAllowed(1) == 1, "Failed parry callback restores outer scope without charge");
        Fresh("parry"); defender.Mp = 0; defender.ApplyDamage(damage);
        check(defender.Hp < 100, "Empty MP cannot repeatedly start free parry blocks");
        Fresh("dash"); defender.Mp = 0; defender.ApplyDamage(damage);
        check(defender.Hp == 100 && defender.Shield < 100 && defender.Mp == 0, "Empty MP preserves finite dash-shield absorption");
        Fresh("dash"); defender.Mp = 0; int freeRewards = 0;
        defender.OnMpUsedServerside = amount => { freeRewards += amount; defender.Shield += amount; };
        defender.ApplyDamage(damage);
        check(freeRewards == 0 && defender.Shield < 100, "Empty MP cannot report fictitious spending to regenerate shields or buffs");
        Fresh("sub"); defender.Mp = 0; defender.ApplyDamage(damage);
        check(defender.Hp == 100 && defender.Mp == 0, "Empty MP preserves native subshield blocking");
        Fresh("mp"); defender.Mp = 3; defender.ApplyDamage(damage);
        check(defender.Mp == 0 && defender.Hp == 83, "Partial MP absorption retains native HP spill from funded balanced damage");
        Fresh("mp"); defender.Mp = 3; defender.InfiniteMp = true; defender.ApplyDamage(damage);
        check(defender.Mp == 0 && defender.Hp == 83, "Partial absorption spends remaining MP even with infinite-MP artifact");
        Fresh("mp"); defender.Shield = 5; defender.ApplyDamage(damage);
        check(defender.Mp == 950 && defender.Hp == 100 && defender.Shield == 0, "Ordinary and MP shield layers pay one floor total");
        Fresh("mp"); defender.Shield = 100; int layeredCallbacks = 0;
        defender.OnMpUsedServerside = _ => layeredCallbacks++; defender.ApplyDamage(damage);
        check(defender.Mp == 950 && defender.Hp == 100 && layeredCallbacks == 1,
            "Full ordinary absorption and an unused MP shield share one charge and MP-use callback");
        Fresh("dash"); damage.damage = 0; defender.ApplyDamage(damage);
        check(defender.Mp == 1000, "Zero damage that consumes no shield adds no MP cost");
        Fresh("sub"); defender.OnCalculateDamage = new UnitSubShield { owner = defender, Block = false }.OnCalculateDamage;
        defender.ApplyDamage(damage); check(defender.Mp == 1000, "Missed subshield block cannot charge MP");
        Fresh("dash"); defender.Shield = 0; defender.ApplyDamage(damage);
        check(defender.Mp == 1000, "Plain HP damage has no defensive MP charge");
        Fresh("mp"); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0); defender.ApplyDamage(damage);
        check(defender.Mp == 980, "Zero minimum leaves native MP-shield absorption cost");
        foreach (string mode in new[] { "dash", "sub" })
        {
            Fresh(mode); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0);
            int calls = 0; defender.OnMpUsedServerside = _ => calls++; defender.ApplyDamage(damage);
            check(calls == 0 && defender.Mp == 1000, mode + " zero floor never restarts native MP-use or regeneration timers");
        }
        Fresh("dash");
        for (int i = 0; i < 1000; i++) { defender.Mp = 1000; defender.Shield = 100; defender.ApplyDamage(damage); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { defender.Mp = 1000; defender.Shield = 100; defender.ApplyDamage(damage); }
        check(GC.GetAllocatedBytesForCurrentThread() == before, "Automatic shield minimum adds no warmed per-hit allocations");
        SessionSettings.FriendlyFireForHit = default;
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
    }
}
