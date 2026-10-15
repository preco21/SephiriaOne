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
        check(target.Hp == 87.5f && source.Hp == 12.5f && source.Mp == 2 && source.MpStack == 0.5f,
            "Balanced PvP Harvest recovers 20 percent of the native amount for both HP and fractional MP");
        Fresh(); hit.hpSteal = 500; target.ApplyDamage(hit);
        check(Restored(3.75f, 2.5f) && source.HpHarvest == 1000 && source.MpHarvest == 1000 && hit.hpSteal == 500 && hit.damage == 20,
            "Per-hit HP Harvest is reduced with the native stat, without writing either rate or pooled damage");
        Fresh(); hit.damage = 1;
        for (int i = 0; i < 6; i++) target.ApplyDamage(hit);
        check(Restored(1.2f, 1.2f) && source.Mp == 1, "Sub-one MP recovery retains native fractional accumulation across hits");
        Fresh(); source.HealingPenalty = 50; source.MpRegenMultiple = 100; target.ApplyDamage(hit);
        check(Restored(1.25f, 5), "Native HP healing penalties and MP recovery modifiers compose with Harvest reduction");
        Fresh(); target.HarvestReceivedIncrease = 100; target.ApplyDamage(hit);
        check(Restored(1.25f, 1.25f), "Native received-damage-increase correction occurs before Harvest reduction");
        Fresh(); source.HpHarvest = source.MpHarvest = 1; target.ApplyDamage(hit);
        check(Restored(0.0025f, 0.0025f), "Low Harvest rates are scaled as amounts instead of truncating integer stats to zero");

        Fresh(); target.Shield = 10; target.ApplyDamage(hit);
        check(Restored(0.5f, 0.5f), "Harvest uses actual HP overflow, not shield damage");
        Fresh(); target.IsMpShield = true; target.Mp = 10; target.ApplyDamage(hit);
        check(Restored(0.5f, 0.5f), "MP shield overflow uses the same reduced Harvest");
        Fresh(); target.Shield = 100; target.ApplyDamage(hit);
        check(Restored(0, 0), "Fully shielded hits yield no Harvest");
        Fresh(); target.IsGuarding = true; target.ApplyDamage(hit);
        check(Restored(0, 0), "Guarded hits retain native no-Harvest behavior");
        Fresh(); target.Hp = 2; target.ClampHpDamage = true; target.ApplyDamage(hit);
        check(Restored(0.4f, 0.4f) && target.IsDead, "Lethal Harvest is reduced before death and uses clamped HP damage");
        Fresh(); target.SuperArmor = true; target.ApplyDamage(hit);
        check(Restored(1.625f, 1.625f), "Native armor reduction is reflected in Harvest amounts");

        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(Restored(20, 20), "Balance off leaves casual friendly-fire Harvest fully native");
        Fresh(); var enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(hit);
        check(Restored(20, 20), "Balance on preserves outgoing PvE Harvest");
        Fresh(); enemy = new UnitAvatar { faction = "enemy", Hp = 10, Mp = 0, HpHarvest = 1000, MpHarvest = 1000 };
        target.ApplyDamage(new() { origin = enemy, damage = 20, targetFactionLayers = -1 });
        check(enemy.Hp == 30 && enemy.Mp == 20, "Normal enemy Harvest against a player is unchanged");
        Fresh(); new UnitAvatar { NetworkLeader = target }.ApplyDamage(hit);
        check(Restored(20, 20), "Non-player companion victims retain the existing non-normalized combat path");
        Fresh(); var companion = new UnitAvatar { NetworkLeader = source, Hp = 10, Mp = 0, HpHarvest = 1000, MpHarvest = 1000 };
        target.ApplyDamage(new() { origin = companion, damage = 20 });
        check(companion.Hp == 12.5f && companion.Mp == 2 && companion.MpStack == 0.5f && Restored(0, 0),
            "Owned companion hits on players reduce recovery on the actual native attacker, not its owner");
        Fresh(); target.monsterType = EMonsterType.Dummy; target.ApplyDamage(hit);
        check(Restored(0, 2.5f), "Native training-dummy HP Harvest exclusion is preserved");
        Fresh(); source.OnAttackUnitBeforeOperation = null; hit.isSystemDamage = true; hit.targetFactionLayers = -1; target.ApplyDamage(hit);
        check(Restored(20, 20), "System damage retains native Harvest rather than borrowing PvP scope");
        Fresh(); source.OnAttackUnitBeforeOperation = null; source.Hp = 50; hit.damage = 5; hit.targetFactionLayers = -1; source.ApplyDamage(hit);
        check(source.Hp == 50 && source.Mp == 5, "Self damage retains native Harvest");

        Fresh(); target.OnHit = (_, _) => { source.Heal(10); source.HealMpFloat(10); }; target.ApplyDamage(hit);
        check(Restored(12.5f, 12.5f), "Unrelated recovery in an on-hit callback is not treated as Harvest");
        Fresh(); enemy = new UnitAvatar { faction = "enemy" }; target.OnHit = (_, _) => enemy.ApplyDamage(hit); target.ApplyDamage(hit);
        check(Restored(22.5f, 22.5f), "Nested PvE Harvest stays native, then outer PvP recovery restores its reduced scope");
        Fresh(); source.Hp = 50; target.HpHarvest = target.MpHarvest = 1000; target.Mp = 0;
        target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, damage = 20, id = "Ability_Thorns" }); target.ApplyDamage(hit);
        check(source.Hp == 40 && source.Mp == 2 && target.Hp == 90 && target.Mp == 2,
            "Reflected PvP damage reduces Harvest independently in both directions");
        Fresh(); target.ApplyDamage(EffectTests.Tick(source));
        check(Restored(2.5f, 2.5f), "Ongoing PvP debuff damage uses the same Harvest reduction");
        Fresh(); var other = new PlayerAvatar();
        var artifact = new Charm_FrostiumRing { NetworkAvatar = source, Target = other, Damage = hit };
        target.OnHit = (_, _) => artifact.HandleAttackUnit(); target.ApplyDamage(hit);
        check(Restored(5, 5), "Nested offensive artifact and parent hit each reduce their own Harvest once");

        Fresh(); target.OnHit = (_, _) => SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(Restored(12.5f, 12.5f), "Turning balance off before Harvest restores native effectiveness immediately");
        Fresh(); target.OnHit = (_, _) => SessionSettings.FriendlyFireForHit = new(false, 100, balance: true); target.ApplyDamage(hit);
        check(Restored(12.5f, 12.5f), "Ending effective PvP before Harvest leaves no reduced-recovery override");
        Fresh(); source.OnHeal = () => SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(Restored(2.5f, 12.5f), "HP callback disabling balance is observed immediately by subsequent MP Harvest");
        Fresh(); target.OnHit = (_, _) => throw new InvalidOperationException("Harvest fixture failure");
        try { target.ApplyDamage(hit); } catch (InvalidOperationException) { }
        check(Restored(0, 0), "Exception before recovery never invents Harvest");
        target.OnHit = null; new UnitAvatar { faction = "enemy" }.ApplyDamage(hit);
        check(Restored(20, 20), "Exception restores scope before a later PvE hit");

        Fresh(); FriendlyFireParticipants.Select(source); target.ApplyDamage(hit);
        check(Restored(0, 0), "Unselected players neither take damage nor grant Harvest");
        FriendlyFireParticipants.Select(source, target); target.ApplyDamage(hit);
        check(Restored(2.5f, 2.5f), "Selected participants immediately share balanced Harvest");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false; target.ApplyDamage(hit);
        check(Restored(0, 0), "Tournament bystanders cannot grant Harvest");
        CombatMatchRuntime.PairRule = (a, b) => a == source && b == target; target.ApplyDamage(hit);
        check(Restored(2.5f, 2.5f), "Current Tournament pair uses the same Harvest policy");
        Fresh(); CombatMatchRuntime.Chaser = target; target.ApplyDamage(hit);
        check(Restored(1.875f, 1.875f), "Play Tag chaser incoming reduction applies before Harvest calculation");
        Fresh(); FriendlyFireParticipants.Depart(target); target.ApplyDamage(hit);
        check(Restored(0, 0), "Retired connection cannot grant Harvest");
        FriendlyFireParticipants.Rejoin(target); target.ApplyDamage(hit);
        check(Restored(2.5f, 2.5f), "Rejoined player receives current Harvest policy without retained per-player state");
        Fresh(); Mirror.NetworkServer.active = false; target.ApplyDamage(hit);
        check(Restored(0, 0), "Clients do not perform authoritative PvP Harvest");

        Fresh(); var result = EApplyDamageResult.Success;
        FriendlyFireRuntime.BeforeHit(target, hit, ref result, out var previous);
        try
        {
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1, 0 })
                check(FriendlyFireRuntime.HarvestAmount(invalid) == 0, "Invalid balanced Harvest cannot poison native HP/MP: " + invalid);
            check(float.IsFinite(FriendlyFireRuntime.HarvestAmount(float.MaxValue)), "Extreme finite Harvest scales without overflow");
        }
        finally { FriendlyFireRuntime.AfterHit(null, previous); }
        check(FriendlyFireRuntime.HarvestAmount(10) == 10, "No adjustment survives the native hit scope");

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
