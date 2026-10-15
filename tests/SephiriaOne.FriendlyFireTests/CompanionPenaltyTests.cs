using SephiriaOne;

internal static class CompanionPenaltyTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar attacker = null, owner = null;
        var factions = RuntimeFactionManager.Instance;
        var dungeon = DungeonManager.Instance;
        void Fresh()
        {
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100);
            Mirror.NetworkServer.active = true; CombatMatchRuntime.PairRule = null;
            attacker = new() { faction = "Player" }; owner = new() { faction = "Player" };
            PlayerSpawner.MultiplayerList.Clear();
            PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = attacker });
            PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = owner });
            SaveManager.CurrentRun = new();
            factions.TempHostilityChanges = factions.ReputationChanges = 0;
            dungeon.MerchantNotices = dungeon.WandererNotices = dungeon.CrimeChecks = 0;
            dungeon.dungeonEnvironment.Clear();
        }
        UnitAI_NewBasic Companion(PlayerAvatar leader, string faction = "Merchant", Action<DamageInstance> beforeDeath = null)
        {
            var unit = new UnitAvatar { NetworkLeader = leader, faction = faction, OnDeath = beforeDeath };
            var ai = new UnitAI_NewBasic { Avatar = unit }; ai.BindPenaltyCallbacks(); return ai;
        }
        DamageInstance Hit(float amount = 1000) => new() { origin = attacker, damage = amount, targetFactionLayers = -1 };
        int Crimes() => SaveManager.CurrentRun.GetInt("CrimeCount", 0);

        Fresh(); var npc = Companion(owner); var companion = npc.Avatar;
        companion.ApplyDamage(new() { origin = attacker, damage = 1000 });
        check(companion.IsDead && attacker.CrimeBuffs == 0 && owner.CrimeBuffs == 0 &&
            SaveManager.CurrentRun.GetInt("CrimeCount", 0) == 0,
            "A friendly-fire companion death must not punish any player or persist a crime");
        check(RuntimeFactionManager.Instance.TempHostilityChanges == 0 && RuntimeFactionManager.Instance.ReputationChanges == 0 &&
            DungeonManager.Instance.MerchantNotices == 0 && !DungeonManager.Instance.dungeonEnvironment.ContainsKey("RedMerchant"),
            "Companion friendly fire must not change shared faction relations or merchant state");
        check(npc.CurrentTarget == null && npc.LostTargets == 1 && companion.BattleStops == 1 && companion.NetworkLeader == null,
            "Exemption preserves native targeting, death cleanup and ownership release");

        Fresh(); npc = Companion(attacker); npc.Avatar.ApplyDamage(Hit());
        check(npc.Avatar.IsDead && Crimes() == 0 && dungeon.CrimeChecks == 0,
            "Killing one's own companion uses the same exact friendly-fire exemption");
        Fresh(); npc = Companion(owner, "Wanderer"); npc.Avatar.ApplyDamage(Hit(10));
        check(npc.Avatar.Hp == 90 && npc.CurrentTarget == attacker && factions.TempHostilityChanges == 0 && dungeon.WandererNotices == 0,
            "Admitted nonlethal companion hits preserve personal targeting without global hostility or notices");
        npc.Avatar.ApplyDamage(Hit());
        check(npc.Avatar.IsDead && npc.Avatar.BattleStops == 1 && Crimes() == 0,
            "Wanderer companion death retains cleanup without any crime");

        Fresh(); SaveManager.CurrentRun.SetInt("CrimeCount", 4); attacker.CrimeBuffs = owner.CrimeBuffs = 4;
        npc = Companion(owner); npc.Avatar.ApplyDamage(Hit());
        check(Crimes() == 4 && attacker.CrimeBuffs == 4 && owner.CrimeBuffs == 4,
            "Exemption never removes existing crime history or party penalties");

        foreach (string change in new[] { "off", "selection", "disconnect", "pool" })
        {
            Fresh(); npc = Companion(owner, beforeDeath: damage =>
            {
                if (change == "off") SessionSettings.FriendlyFireForHit = default;
                if (change == "selection") FriendlyFireParticipants.Select(new PlayerAvatar());
                if (change == "disconnect") FriendlyFireParticipants.Depart(owner);
                if (change == "pool") damage.origin = new PlayerAvatar { faction = "Player" };
            });
            npc.Avatar.ApplyDamage(Hit());
            check(npc.Avatar.IsDead && Crimes() == 0 && dungeon.CrimeChecks == 0,
                "Already-delivered companion death retains exemption through callback " + change);
        }

        Fresh(); npc = Companion(owner);
        var burning = new CharacterDebuff(); npc.Avatar.ApplyDebuff(burning, attacker);
        burning.OnTick = () => npc.Avatar.ApplyDamage(EffectTests.Tick(attacker, "Debuff_Burn", 1000));
        burning.Update();
        check(npc.Avatar.IsDead && Crimes() == 0, "Owned ongoing Burn damage uses the same companion exemption");

        Fresh(); npc = Companion(owner);
        attacker.OnHit = (_, damage) => npc.Avatar.ApplyDamage(new() { origin = attacker, id = "Ability_Thorns", damage = 1000 });
        attacker.ApplyDamage(new() { origin = npc.Avatar, damage = 1 });
        check(npc.Avatar.IsDead && Crimes() == 0 && dungeon.CrimeChecks == 0,
            "An admitted player reflection can kill a companion without punishing its owner or party");

        foreach (bool otherIsCompanion in new[] { false, true })
        {
            Fresh(); var other = Companion(otherIsCompanion ? owner : null);
            npc = Companion(owner, beforeDeath: damage => other.Avatar.Die(5, damage));
            npc.Avatar.ApplyDamage(Hit());
            check(Crimes() == 1 && attacker.CrimeBuffs == 1 && dungeon.CrimeChecks == 1,
                "Nested non-attributed death cannot borrow the outer companion exemption: " + otherIsCompanion);
        }
        Fresh(); npc = Companion(owner); var pooled = Hit(); npc.Avatar.ApplyDamage(pooled);
        var ordinary = Companion(null); ordinary.Avatar.Die(5, pooled);
        check(Crimes() == 1 && dungeon.CrimeChecks == 1 && factions.ReputationChanges == 2 &&
            dungeon.MerchantNotices == 1 && dungeon.dungeonEnvironment["RedMerchant"] == 1,
            "Reused damage after the hit retains all ordinary merchant penalties");

        Fresh(); npc = Companion(owner, beforeDeath: _ => throw new InvalidOperationException("death callback"));
        try { npc.Avatar.ApplyDamage(Hit()); } catch (InvalidOperationException) { }
        ordinary = Companion(null); ordinary.Avatar.Die(5, Hit());
        check(Crimes() == 1, "Exceptional death cannot leak an exemption into later native deaths");

        foreach (bool enabled in new[] { false, true })
        {
            Fresh(); SessionSettings.FriendlyFireForHit = new(enabled, enabled ? 0 : 100);
            npc = Companion(owner); npc.Avatar.ApplyDamage(Hit());
            check(!npc.Avatar.IsDead && npc.Avatar.Hp == 100 && dungeon.CrimeChecks == 0,
                "Off/zero still blocks ordinary companion team damage: " + enabled);
            npc.Avatar.Die(5, Hit());
            check(Crimes() == 1, "Death outside an admitted hit retains native penalties with off/zero: " + enabled);
        }
        Fresh(); npc = Companion(owner); var system = Hit(); system.isSystemDamage = true;
        npc.Avatar.ApplyDamage(system);
        check(npc.Avatar.IsDead && Crimes() == 1, "System damage does not receive a friendly-fire crime exemption");
        Fresh(); npc = Companion(owner); npc.Avatar.ApplyDamage(new() { origin = new UnitAvatar { faction = "enemy" }, damage = 1000, targetFactionLayers = -1 });
        check(npc.Avatar.IsDead && dungeon.CrimeChecks == 1 && Crimes() == 0,
            "Ordinary enemy kills still execute the native noncriminal death check");
        Fresh(); npc = Companion(null, "Wanderer"); npc.Avatar.ApplyDamage(Hit());
        check(dungeon.CrimeChecks == 1 && Crimes() == 1 && factions.TempHostilityChanges == 1 && dungeon.WandererNotices == 1,
            "Natural wanderers retain native crime, hostility and notification paths");

        Fresh(); npc = Companion(owner); FriendlyFireParticipants.Select(attacker);
        npc.Avatar.ApplyDamage(Hit());
        check(npc.Avatar.Hp == 100, "Nonparticipating owners' companions retain selection protection");
        Fresh(); npc = Companion(owner); CombatMatchRuntime.PairRule = (_, _) => false;
        npc.Avatar.ApplyDamage(Hit());
        check(npc.Avatar.Hp == 100, "Match countdown/nonpair rules still gate companion damage");
        CombatMatchRuntime.PairRule = null;

        Fresh(); npc = Companion(owner); var reusable = Hit(1);
        for (int i = 0; i < 1000; i++) { npc.Avatar.ApplyDamage(reusable); npc.Avatar.Hp = 100; }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { npc.Avatar.ApplyDamage(reusable); npc.Avatar.Hp = 100; }
        check(GC.GetAllocatedBytesForCurrentThread() == allocated,
            "Warm companion penalty wrappers add no per-hit allocations");
        PlayerSpawner.MultiplayerList.Clear();
        SessionSettings.FriendlyFireForHit = default;
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
    }
}
