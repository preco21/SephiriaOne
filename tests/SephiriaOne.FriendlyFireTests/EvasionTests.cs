using SephiriaOne;
using UnityEngine;

internal static class EvasionTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        DamageInstance hit = null;
        void Fresh()
        {
            FriendlyFireParticipants.Clear(); CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
            Mirror.NetworkServer.active = true;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            UnityEngine.Random.NextValue = 60; UnityEngine.Random.Calls = 0;
            source = new(); target = new() { Evasion = 10000 };
            hit = new() { origin = source, damage = 1 };
        }
        Fresh();
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success && target.Hp < 100,
            "Balanced PvP caps max-stat Evasion below a 60 percent dodge roll");
        check(target.GetCustomStat(ECustomStat.Evasion) == 10000 && UnityEngine.Random.Calls == 1,
            "Evasion cap does not change native stat values or add RNG draws");
        Fresh(); UnityEngine.Random.NextValue = 50;
        int evades = 0; target.OnEvade = _ => evades++;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block && evades == 1 && target.Hp == 100,
            "50 percent boundary still dodges and fires native OnEvade once");
        Fresh(); UnityEngine.Random.NextValue = 50.01f;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Chance cannot exceed 50 percent");
        Fresh(); target.Evasion = int.MaxValue;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Extreme stats still respect the native input clamp and PvP cap");
        Fresh(); target.Evasion = 1000; UnityEngine.Random.NextValue = 10;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Lower native Evasion remains useful");
        UnityEngine.Random.NextValue = 15;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Cap never raises a lower native chance");
        Fresh(); target.Evasion = -100; UnityEngine.Random.NextValue = 1;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Negative Evasion retains native zero-chance behavior");
        Fresh(); source.IgnoreEvasion = 50; UnityEngine.Random.NextValue = 30;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Anti-Evasion reduces capped 50 percent to 25, not native 77 to 38");
        UnityEngine.Random.NextValue = 24;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Anti-Evasion preserves the remaining defensive chance");
        Fresh(); source.IgnoreEvasion = 100; UnityEngine.Random.NextValue = 1;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Full anti-Evasion remains effective");
        Fresh(); hit.absoluteEvasionPercent = 100;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block && UnityEngine.Random.Calls == 2,
            "Separate per-hit guaranteed item dodge keeps its native roll");
        Fresh(); target.AbsoluteEvasion = 100;
        target.OnEvade = damage => { if (damage.UsedAbsoluteEvasion) target.AbsoluteEvasion = 0; };
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block && target.AbsoluteEvasion == 0 && hit.UsedAbsoluteEvasion,
            "Guaranteed dodge barrier consumes through its native flag and callback");
        hit.UsedAbsoluteEvasion = false;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success && !hit.UsedAbsoluteEvasion,
            "Consumed barrier does not retain guaranteed dodge state");
        Fresh(); target.EvasionDisabled = true; UnityEngine.Random.NextValue = 20;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Native Evasion-disable condition still bypasses dodges");
        Fresh(); target.IsMpShield = true; UnityEngine.Random.NextValue = 20;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success && target.Mp < 100, "Native MP-shield exclusion is preserved");
        foreach (var type in new[] { EDamageType.ElementalEffectDamage, EDamageType.Fall })
        {
            Fresh(); hit.damageType = type; UnityEngine.Random.NextValue = 20;
            check(target.ApplyDamage(hit) == EApplyDamageResult.Success && UnityEngine.Random.Calls == 0,
                type + " retains native Evasion bypass");
        }
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Balancing off immediately restores native 77 percent chance");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Live balancing enable applies without changing stats");
        Fresh(); hit.origin = new UnitAvatar { faction = "enemy" }; hit.targetFactionLayers = -1;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Incoming PvE remains native while balancing is on");
        Fresh(); var enemy = new UnitAvatar { faction = "enemy", Evasion = 10000 };
        check(enemy.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Enemy Evasion stays native against player attacks");
        Fresh(); var companion = new UnitAvatar { NetworkLeader = source, Evasion = 10000 };
        check(companion.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Companion victims keep native Evasion");
        Fresh(); hit.origin = new UnitAvatar { NetworkLeader = source };
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Owned companion attacks obey player-victim Evasion cap");
        Fresh(); hit.isSystemDamage = true; hit.targetFactionLayers = -1; source.OnAttackUnitBeforeOperation = null;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "System damage cannot borrow PvP Evasion scope");
        Fresh(); hit.origin = target; hit.targetFactionLayers = -1; target.OnAttackUnitBeforeOperation = null;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Self damage retains native Evasion");
        Fresh(); SessionSettings.FriendlyFireForHit = new(false, 100, balance: true);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute && UnityEngine.Random.Calls == 0, "FF off rejects before Evasion");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 0, balance: true);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute && UnityEngine.Random.Calls == 0, "Zero scale rejects before Evasion");
        Fresh(); Mirror.NetworkServer.active = false;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute && UnityEngine.Random.Calls == 0, "Guests cannot make authoritative Evasion rolls");
        Fresh(); FriendlyFireParticipants.Select(source);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute, "Excluded participant is not admitted");
        FriendlyFireParticipants.Select(source, target);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Selected pair uses capped Evasion");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute, "Countdowns and other Tournament pairs stay protected");
        CombatMatchRuntime.PairRule = (_, _) => true;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Current match pair uses the same balancing cap");
        Fresh(); CombatMatchRuntime.Chaser = target;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Play Tag chaser uses the same Evasion cap");
        Fresh(); FriendlyFireParticipants.Depart(target);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute, "Departed connection cannot receive balanced combat");
        FriendlyFireParticipants.Rejoin(target);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Rejoined player receives current cap without retained stat state");
        Fresh(); target.OnCalculateDamage = _ => SessionSettings.FriendlyFireForHit = new(true, 100);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Disabling balancing inside a native callback restores native chance");
        Fresh(); target.OnCalculateDamage = _ => FriendlyFireParticipants.Select(source);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Changed participant receipt cannot borrow old cap");
        Fresh(); enemy = new UnitAvatar { faction = "enemy", Evasion = 10000 };
        EApplyDamageResult nested = EApplyDamageResult.Success;
        target.OnCalculateDamage = _ => nested = enemy.ApplyDamage(hit);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success && nested == EApplyDamageResult.Fail_Block,
            "Nested PvE Evasion stays native and outer PvP cap resumes");
        Fresh(); target.OnCalculateDamage = _ => throw new InvalidOperationException("Evasion fixture failure");
        try { target.ApplyDamage(hit); } catch (InvalidOperationException) { }
        target.OnCalculateDamage = null; hit.origin = new UnitAvatar { faction = "enemy" }; hit.targetFactionLayers = -1;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Exception leaves no cap scope in the next PvE hit");
        Fresh();
        void Strike() { Time.time++; target.Hp = 100; target.ApplyDamage(hit); }
        for (int i = 0; i < 1000; i++) Strike();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Strike();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "Balanced Evasion adds no warmed per-hit allocation");
        Console.WriteLine("Balanced Evasion: " + allocated + " bytes / 1000 warmed hits");
        FriendlyFireParticipants.Clear(); CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
        Mirror.NetworkServer.active = true; UnityEngine.Random.NextValue = 100;
        SessionSettings.FriendlyFireForHit = default;
    }
}
