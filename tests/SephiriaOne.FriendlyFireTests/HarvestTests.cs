using SephiriaOne;
using UnityEngine;

internal static class HarvestTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        DamageInstance hit = null;
        bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;
        void Fresh()
        {
            FriendlyFireParticipants.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
            Mirror.NetworkServer.active = true; Time.time = 0;
            source = new() { Hp = 10, Mp = 0, HpHarvest = 1000, MpHarvest = 1000 };
            target = new(); hit = new() { origin = source, damage = 20 };
        }
        bool Restored(float hp, float mp) => Near(source.Hp, 10 + hp) && Near(source.Mp + source.MpStack, mp);

        Fresh(); target.ApplyDamage(hit);
        check(target.Hp == 87.5f && source.Hp == 10 && source.Mp == 2 && source.MpStack == 0.5f,
            "Balanced PvP disables HP Harvest and retains 20 percent fractional MP recovery");
        Fresh(); hit.hpSteal = 500; target.ApplyDamage(hit);
        check(Restored(0, 2.5f) && source.HpHarvest == 1000 && source.MpHarvest == 1000 && hit.hpSteal == 500 && hit.damage == 20,
            "Stat and per-hit HP Harvest are suppressed without writing either rate or pooled damage");
        Fresh(); source.HpHarvest = 0; hit.hpSteal = 500; target.ApplyDamage(hit);
        check(Restored(0, 2.5f), "Per-hit HP Harvest is suppressed even without a native HP Steal stat");
        Fresh(); int healingCallbacks = 0; source.OnHeal = () => healingCallbacks++; target.ApplyDamage(hit);
        check(Restored(0, 2.5f) && healingCallbacks == 0, "Suppressed HP Harvest does not invoke native healing callbacks");
        Fresh(); hit.damage = 1;
        for (int i = 0; i < 6; i++) target.ApplyDamage(hit);
        check(Restored(0, 1.2f) && source.Mp == 1, "Sub-one MP recovery retains native fractional accumulation across hits");
        Fresh(); source.HealingPenalty = 50; source.MpRegenMultiple = 100; target.ApplyDamage(hit);
        check(Restored(0, 5), "HP Harvest remains disabled with healing penalties while native MP modifiers still apply");
        Fresh(); target.HarvestReceivedIncrease = 100; target.ApplyDamage(hit);
        check(Restored(0, 1.25f), "Native received-damage-increase correction occurs before MP Harvest reduction");
        Fresh(); source.HpHarvest = source.MpHarvest = 1; target.ApplyDamage(hit);
        check(Restored(0, 0.0025f), "Low MP Harvest rates are scaled as amounts instead of truncating integer stats to zero");

        Fresh(); target.Shield = 10; target.ApplyDamage(hit);
        check(Restored(0, 0.5f), "MP Harvest uses actual HP overflow, not shield damage");
        Fresh(); target.IsMpShield = true; target.Mp = 10; target.ApplyDamage(hit);
        check(Restored(0, 0.5f), "MP shield overflow suppresses HP Harvest and reduces MP Harvest");
        Fresh(); target.Shield = 100; target.ApplyDamage(hit);
        check(Restored(0, 0), "Fully shielded hits yield no Harvest");
        Fresh(); target.IsGuarding = true; target.ApplyDamage(hit);
        check(Restored(0, 0), "Guarded hits retain native no-Harvest behavior");
        Fresh(); target.Hp = 2; target.ClampHpDamage = true; target.ApplyDamage(hit);
        check(Restored(0, 0.4f) && target.IsDead, "Lethal HP Harvest is disabled while MP uses clamped HP damage before death");
        Fresh(); target.SuperArmor = true; target.ApplyDamage(hit);
        check(Restored(0, 1.625f), "Native armor reduction is reflected in MP Harvest amounts");

        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(Restored(0, 20), "Balance off still disables casual PvP HP Harvest and preserves native MP Harvest");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); source.HpHarvest = 0; hit.hpSteal = 500; target.ApplyDamage(hit);
        check(Restored(0, 20), "Balance off also suppresses the per-hit HP Steal rate");
        Fresh(); var enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(hit);
        check(Restored(20, 20), "Balance on preserves outgoing PvE Harvest");
        Fresh(); enemy = new UnitAvatar { faction = "enemy", Hp = 10, Mp = 0, HpHarvest = 1000, MpHarvest = 1000 };
        target.ApplyDamage(new() { origin = enemy, damage = 20, targetFactionLayers = -1 });
        check(enemy.Hp == 30 && enemy.Mp == 20, "Normal enemy Harvest against a player is unchanged");
        Fresh(); new UnitAvatar { NetworkLeader = target }.ApplyDamage(hit);
        check(Restored(0, 20), "Player-owned companion victims cannot grant HP Harvest, with MP on its existing native path");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); new UnitAvatar { NetworkLeader = target }.ApplyDamage(hit);
        check(Restored(0, 20), "Companion victims cannot bypass HP Harvest suppression with balancing off");
        Fresh(); var companion = new UnitAvatar { NetworkLeader = source, Hp = 10, Mp = 0, HpHarvest = 1000, MpHarvest = 1000 };
        target.ApplyDamage(new() { origin = companion, damage = 20 });
        check(companion.Hp == 10 && companion.Mp == 2 && companion.MpStack == 0.5f && Restored(0, 0),
            "Owned companion hits suppress HP and reduce MP recovery on the actual attacker, not its owner");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100);
        companion = new UnitAvatar { NetworkLeader = source, Hp = 10, Mp = 0, HpHarvest = 1000, MpHarvest = 1000 };
        target.ApplyDamage(new() { origin = companion, damage = 20 });
        check(companion.Hp == 10 && companion.Mp == 20 && Restored(0, 0), "Owned companion HP Harvest stays disabled with balancing off");
        Fresh(); target.monsterType = EMonsterType.Dummy; target.ApplyDamage(hit);
        check(Restored(0, 2.5f), "Native training-dummy HP Harvest exclusion is preserved");
        Fresh(); source.OnAttackUnitBeforeOperation = null; hit.isSystemDamage = true; hit.targetFactionLayers = -1; target.ApplyDamage(hit);
        check(Restored(20, 20), "System damage retains native Harvest rather than borrowing PvP scope");
        Fresh(); source.OnAttackUnitBeforeOperation = null; source.Hp = 50; hit.damage = 5; hit.targetFactionLayers = -1; source.ApplyDamage(hit);
        check(source.Hp == 50 && source.Mp == 5, "Self damage retains native Harvest");

        Fresh(); target.OnHit = (_, _) => { source.Heal(10); source.HealMpFloat(10); }; target.ApplyDamage(hit);
        check(Restored(10, 12.5f), "Unrelated recovery in an on-hit callback is not treated as Harvest");
        Fresh(); enemy = new UnitAvatar { faction = "enemy" }; target.OnHit = (_, _) => enemy.ApplyDamage(hit); target.ApplyDamage(hit);
        check(Restored(20, 22.5f), "Nested PvE Harvest stays native, then outer PvP suppresses only its own HP recovery");
        Fresh(); source.Hp = 50; target.HpHarvest = target.MpHarvest = 1000; target.Mp = 0;
        target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, damage = 20, id = "Ability_Thorns" }); target.ApplyDamage(hit);
        check(source.Hp == 37.5f && source.Mp == 2 && target.Hp == 87.5f && target.Mp == 2,
            "Reflected PvP damage suppresses HP and reduces MP Harvest independently in both directions");
        Fresh(); target.ApplyDamage(EffectTests.Tick(source));
        check(Restored(0, 2.5f), "Ongoing PvP debuff damage suppresses HP Harvest and reduces MP Harvest");
        Fresh(); var other = new PlayerAvatar();
        var artifact = new Charm_FrostiumRing { NetworkAvatar = source, Target = other, Damage = hit };
        target.OnHit = (_, _) => artifact.HandleAttackUnit(); target.ApplyDamage(hit);
        check(Restored(0, 5), "Nested offensive artifact and parent hit each suppress HP and reduce their own MP Harvest once");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); source.Hp = 50;
        target.HpHarvest = target.MpHarvest = 1000; target.Mp = 0;
        target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, damage = 20, id = "Ability_Thorns" }); target.ApplyDamage(hit);
        check(source.Hp == 30 && source.Mp == 20 && target.Hp == 80 && target.Mp == 20,
            "Unbalanced reflected hits cannot recover HP in either direction, while MP remains native");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(EffectTests.Tick(source));
        check(Restored(0, 20), "Unbalanced ongoing PvP debuff damage cannot grant HP Harvest");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); other = new PlayerAvatar();
        artifact = new Charm_FrostiumRing { NetworkAvatar = source, Target = other, Damage = hit };
        target.OnHit = (_, _) => artifact.HandleAttackUnit(); target.ApplyDamage(hit);
        check(Restored(0, 40), "Unbalanced nested artifact hits cannot grant HP Harvest");

        Fresh(); target.OnHit = (_, _) => SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(Restored(0, 12.5f), "Turning balance off before Harvest restores only MP effectiveness immediately");
        Fresh(); target.OnHit = (_, _) => SessionSettings.FriendlyFireForHit = new(false, 100, balance: true); target.ApplyDamage(hit);
        check(Restored(12.5f, 12.5f), "Ending effective PvP before Harvest leaves no reduced-recovery override");
        Fresh(); target.ApplyDamage(hit); SessionSettings.FriendlyFireForHit = default;
        source.Hp = 10; source.Mp = 0; source.MpStack = 0; new UnitAvatar { faction = "enemy" }.ApplyDamage(hit);
        check(Restored(20, 20) && source.HpHarvest == 1000 && source.MpHarvest == 1000,
            "Stopping PvP immediately restores native PvE HP Harvest without restoring any stat");
        Fresh(); source.OnHeal = () => SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(Restored(0, 2.5f) && SessionSettings.FriendlyFireForHit.Balance,
            "Disabled HP Harvest cannot invoke a callback that changes subsequent MP policy");
        Fresh(); source.OnHeal = () => SessionSettings.FriendlyFireForHit = new(true, 100);
        target.OnHit = (_, _) => source.Heal(1); target.ApplyDamage(hit);
        check(Restored(1, 12.5f), "An unrelated native HP callback still changes subsequent MP Harvest policy immediately");
        Fresh(); target.OnHit = (_, _) => throw new InvalidOperationException("Harvest fixture failure");
        try { target.ApplyDamage(hit); } catch (InvalidOperationException) { }
        check(Restored(0, 0), "Exception before recovery never invents Harvest");
        target.OnHit = null; new UnitAvatar { faction = "enemy" }.ApplyDamage(hit);
        check(Restored(20, 20), "Exception restores scope before a later PvE hit");

        Fresh(); FriendlyFireParticipants.Select(source); target.ApplyDamage(hit);
        check(Restored(0, 0), "Unselected players neither take damage nor grant Harvest");
        FriendlyFireParticipants.Select(source, target); target.ApplyDamage(hit);
        check(Restored(0, 2.5f), "Selected participants immediately suppress HP Harvest and share balanced MP Harvest");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false; target.ApplyDamage(hit);
        check(Restored(0, 0), "Tournament bystanders cannot grant Harvest");
        CombatMatchRuntime.PairRule = (a, b) => a == source && b == target; target.ApplyDamage(hit);
        check(Restored(0, 2.5f), "Current Tournament pair uses the same Harvest policy");
        Fresh(); CombatMatchRuntime.Chaser = target; target.ApplyDamage(hit);
        check(Restored(0, 1.875f), "Play Tag chaser incoming reduction applies before MP Harvest calculation");
        foreach (bool balanced in new[] { false, true })
        {
            Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: balanced);
            var zombieRound = new ZombieSurvivalRound(3, 0);
            var participants = new[] { source, target, new PlayerAvatar() };
            CombatMatchRuntime.PairRule = (a, b) => zombieRound.AllowsPair(Array.IndexOf(participants, a), Array.IndexOf(participants, b));
            target.ApplyDamage(hit);
            check(Restored(0, balanced ? 2.5f : 20), "Zombie opposing teams disable HP Harvest regardless of balance: " + balanced);
            var teammate = participants[2]; teammate.Hp = 10; teammate.HpHarvest = 1000;
            check(target.ApplyDamage(new() { origin = teammate, damage = 20 }) == EApplyDamageResult.Fail_Absolute && teammate.Hp == 10,
                "Zombie teammates cannot create Harvest through a rejected hit: " + balanced);
            Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: balanced);
            CombatMatchRuntime.PairRule = (a, b) => a == source && b == target;
            target.ApplyDamage(hit);
            check(Restored(0, balanced ? 2.5f : 20), "Current Tournament pair disables HP Harvest regardless of balance: " + balanced);
            Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: balanced); CombatMatchRuntime.Chaser = target;
            target.ApplyDamage(hit);
            check(Restored(0, balanced ? 1.875f : 15), "Play Tag disables HP Harvest regardless of balance: " + balanced);
        }
        Fresh(); FriendlyFireParticipants.Depart(target); target.ApplyDamage(hit);
        check(Restored(0, 0), "Retired connection cannot grant Harvest");
        FriendlyFireParticipants.Rejoin(target); target.ApplyDamage(hit);
        check(Restored(0, 2.5f), "Rejoined player receives current Harvest policy without retained per-player state");
        Fresh(); Mirror.NetworkServer.active = false; target.ApplyDamage(hit);
        check(Restored(0, 0), "Clients do not perform authoritative PvP Harvest");

        Fresh(); var result = EApplyDamageResult.Success;
        FriendlyFireRuntime.BeforeHit(target, hit, ref result, out var previous);
        try
        {
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1, 0 })
                check(FriendlyFireRuntime.HarvestMpAmount(invalid) == 0, "Invalid balanced Harvest cannot poison native MP: " + invalid);
            check(float.IsFinite(FriendlyFireRuntime.HarvestMpAmount(float.MaxValue)), "Extreme finite MP Harvest scales without overflow");
        }
        finally { FriendlyFireRuntime.AfterHit(null, previous); }
        check(FriendlyFireRuntime.HarvestMpAmount(10) == 10, "No MP adjustment survives the native hit scope");

        Fresh();
        void Strike() { Time.time++; target.Hp = 100; source.Hp = 10; source.Mp = 0; source.MpStack = 0; target.ApplyDamage(hit); }
        for (int i = 0; i < 1000; i++) Strike();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Strike();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        check(bytes == 0, "Warmed balanced HP/MP Harvest adds no per-hit allocations");
        Console.WriteLine("Balanced Harvest: " + bytes + " bytes / 1000 warmed hits");
        CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null; FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = default;
    }
}
