using SephiriaOne;
using UnityEngine;

internal static class ChaserBalanceTests
{
    internal static void Run(Action<bool, string> check)
    {
        bool Near(double a, double b) => Math.Abs(a - b) < 0.001;
        var source = new PlayerAvatar(); var target = new PlayerAvatar();
        void Fresh(bool balance = false, bool attackerChases = false)
        {
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: balance, refillPercent: 10);
            SessionSettings.PlayTagChaser = default;
            source = new(); target = new(); Time.time = 0;
            CombatMatchRuntime.Chaser = attackerChases ? source : target;
            CombatMatchRuntime.RoleSettingsChanged();
        }
        void Strike(float damage = 20) => target.ApplyDamage(new() { origin = source, damage = damage });
        try
        {
            Fresh(); Strike();
            check(target.Hp == 85, "Current chaser takes 75 percent of resolved PvP damage even with balancing off");
            CombatMatchRuntime.Chaser = source; CombatMatchRuntime.RoleSettingsChanged(); Strike();
            check(target.Hp == 65, "Previous carrier immediately loses incoming protection");
            CombatMatchRuntime.Chaser = null; CombatMatchRuntime.RoleSettingsChanged(); Strike();
            check(target.Hp == 45, "Ending Tag restores ordinary PvP damage");
            Fresh(); Strike(float.MaxValue);
            check(Math.Abs(-target.Hp - 2147483520d * 0.75) < 256,
                "Extreme unbalanced hits reduce the same safely capped original damage by 25 percent");

            Fresh(true); for (int i = 0; i < 50; i++) Strike(float.MaxValue);
            check(Near(target.Hp, 36), "Chaser reduction applies to funded damage; exhausted hits retain their final floor");
            for (int i = 1; i <= 10; i++) { Time.time = i / 10f; Strike(float.MaxValue); }
            check(Near(target.Hp, 26), "Sub-one role-reduced refill remains responsive without floor debt");

            Fresh(true, true); Strike(float.MaxValue); Time.time = 0.1f; Strike(float.MaxValue);
            check(Near(target.Hp, 78), "Chaser global 10-percent refill becomes 20 percent per second");
            CombatMatchRuntime.Chaser = null; CombatMatchRuntime.RoleSettingsChanged(); Time.time = 0.2f; Strike(float.MaxValue);
            check(Near(target.Hp, 77), "Losing chaser role immediately restores base refill without resetting tokens");
            CombatMatchRuntime.Chaser = source; CombatMatchRuntime.RoleSettingsChanged(); Time.time = 0.3f; Strike(float.MaxValue);
            check(Near(target.Hp, 76), "Role gain never retroactively grants boosted elapsed refill");
            Time.time = 0.4f; Strike(float.MaxValue);
            check(Near(target.Hp, 74), "Steady current carrier receives multiplied refill");
            CombatMatchRuntime.Chaser = target; CombatMatchRuntime.RoleSettingsChanged();
            CombatMatchRuntime.Chaser = source; CombatMatchRuntime.RoleSettingsChanged();
            Time.time = 0.5f; Strike(float.MaxValue);
            check(Near(target.Hp, 73), "Unobserved A-to-B-to-A transfer cannot earn boosted credit for the interval");

            Fresh(); SessionSettings.PlayTagChaser = new(false, 4, false, 25); Strike();
            check(target.Hp == 80, "Disabled incoming option keeps its stored percentage without applying it");
            SessionSettings.PlayTagChaser = SessionSettings.PlayTagChaser.WithIncoming(enabled: true); Strike();
            check(target.Hp == 75, "Incoming option independently applies its configured percentage");
            Fresh(true, true); SessionSettings.PlayTagChaser = new(false, 4, true, 75); Strike(float.MaxValue);
            Time.time = 0.1f; Strike(float.MaxValue);
            check(Near(target.Hp, 79), "Refill toggle off uses global rate even when its saved multiplier differs");
            SessionSettings.PlayTagChaser = SessionSettings.PlayTagChaser.WithRefill(enabled: true);
            CombatMatchRuntime.RoleSettingsChanged(); Time.time = 0.2f; Strike(float.MaxValue);
            Time.time = 0.3f; Strike(float.MaxValue);
            check(Near(target.Hp, 74), "Live x4 edit preserves credits then refills at four times global value");

            Fresh(); target.Shield = 10; Strike();
            check(target.Shield == 0 && target.Hp == 95, "Incoming reduction precedes native shield and HP overflow");
            Fresh(true); target.IsMpShield = true; Strike(); Strike(float.MaxValue);
            check(target.Mp == 85 && target.Hp == 100, "MP integer rounding refunds only undelivered reduced damage");
            Fresh(true); target.SuperArmor = true; Strike(float.MaxValue);
            check(Near(target.Hp, 90.25), "Native armor stacks after chaser reduction on an individual hit");
            for (int i = 0; i < 30; i++) Strike(float.MaxValue);
            float armorBefore = target.Hp; Strike(float.MaxValue);
            check(Near(armorBefore - target.Hp, .65), "Minimum-only armor hits cannot refund free role-adjusted credit");

            Fresh(true); target.OnHit = (_, _) =>
            {
                target.OnHit = null;
                SessionSettings.PlayTagChaser = SessionSettings.PlayTagChaser.WithIncoming(enabled: false);
                CombatMatchRuntime.RoleSettingsChanged();
            };
            Strike(); Strike(float.MaxValue);
            check(Near(target.Hp, 83.125), "Refund uses the hit's captured factor despite a callback editing settings");
            Fresh(true); SessionSettings.PlayTagChaser = SessionSettings.PlayTagChaser.WithIncoming(percent: 0);
            target.ApplyDebuff(new() { OnApply = (caster, receiver) => receiver.ApplyDamage(EffectTests.Tick(caster, "Debuff_Electric")) }, source);
            check(target.Hp == 100 && target.DebuffApplications == 1, "Zero incoming scale preserves native effect application without damage");
            SessionSettings.PlayTagChaser = SessionSettings.PlayTagChaser.WithIncoming(percent: 100); CombatMatchRuntime.RoleSettingsChanged();
            target.ApplyDebuff(new() { OnApply = (caster, receiver) => receiver.ApplyDamage(EffectTests.Tick(caster, "Debuff_Electric")) }, source);
            check(Near(target.Hp, 87.5), "Zero-damage protection never consumes the next Shock pulse or budget");

            Fresh(); target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, id = "Ability_Thorns", damage = 20 }); Strike();
            check(source.Hp == 80 && target.Hp == 85, "Reflection reduces only damage received by the actual chaser");
            Fresh(true, true);
            var companionHit = new DamageInstance { origin = new UnitAvatar { NetworkLeader = source }, damage = float.MaxValue };
            target.ApplyDamage(companionHit); Time.time = 0.1f; target.ApplyDamage(companionHit);
            check(Near(target.Hp, 78), "Chaser-owned companion attacks share the chaser's multiplied refill");
            Fresh(); target.ApplyDamage(new() { origin = new UnitAvatar { NetworkLeader = source }, damage = 20 });
            check(target.Hp == 85, "Other players' companions respect incoming chaser protection");
            var enemy = new UnitAvatar { faction = "enemy" };
            target.ApplyDamage(new() { origin = enemy, damage = 20, targetFactionLayers = -1 });
            enemy.ApplyDamage(new() { origin = target, damage = 20 });
            check(target.Hp == 65 && enemy.Hp == 80, "Chaser options cannot affect incoming or outgoing PvE");
            Fresh(); source.OnAttackUnitBeforeOperation += (_, _) => { CombatMatchRuntime.Chaser = source; CombatMatchRuntime.RoleSettingsChanged(); };
            Strike();
            check(target.Hp == 80, "Role is resolved at damage scaling after native before-attack callbacks");

            Fresh(true, true);
            var reusable = new DamageInstance { origin = source, damage = 20 };
            for (int i = 0; i < 100; i++) { Time.time++; target.ApplyDamage(reusable); target.Hp = 100; }
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { Time.time++; target.ApplyDamage(reusable); target.Hp = 100; }
            check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Current-chaser combat modifiers allocate nothing per warmed hit");
        }
        finally { CombatMatchRuntime.Chaser = null; SessionSettings.PlayTagChaser = default; SessionSettings.FriendlyFireForHit = default; FriendlyFireRuntime.Clear(); }
    }
}
