using SephiriaOne;
using UnityEngine;

internal static class PlasmaDurationTests
{
    // Subclasses must inherit the family rule too; the installed blue variants
    // are properties on Burn/Plasma themselves (verified against native IL).
    private sealed class DerivedPlasma : CharacterDebuff_Plasma { }

    internal static void Run(Action<bool, string> check)
    {
        void Fresh()
        {
            FriendlyFireParticipants.Clear(); CombatMatchRuntime.PairRule = null;
            Mirror.NetworkServer.active = true; Time.time = 100; Time.deltaTime = 0;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        }
        foreach (var create in new Func<CharacterDebuff_Plasma>[] { () => new(), () => new DerivedPlasma() })
        {
            CharacterDebuff_Plasma Plasma(UnitAvatar source = null, UnitAvatar target = null, float duration = 30)
            {
                var effect = create(); effect.NativeDuration = duration;
                effect.InitializeAndSpawn(source ?? new PlayerAvatar(), target ?? new PlayerAvatar());
                effect.OnTick = effect.OnUpdate_Server;
                return effect;
            }
            Fresh(); var plasma = Plasma(); int expiries = 0;
            plasma.OnExpire = () => { expiries++; plasma.NetworkTarget.ApplyDamage(EffectTests.Tick(plasma.NetworkAttacker, "Debuff_Electric", 2)); };
            plasma.Step(0.75f); plasma.Step(0.75f); plasma.Step(0.499f);
            check(!plasma.IsEndBuff && plasma.Ticks == 2, "Plasma retains its periodic ticks before the capped deadline");
            float hp = plasma.NetworkTarget.Hp;
            Time.time = 102; Time.deltaTime = 0.001f; plasma.Update();
            check(plasma.IsEndBuff && plasma.Ticks == 2 && expiries == 1 && plasma.NetworkTarget.Hp < hp && plasma.NativeDuration == 30,
                "Plasma cap retains one native expiry attack without a late periodic tick or timer rewrite");
            plasma.Step(1); SessionSettings.FriendlyFireForHit = new(true, 100); plasma.Step(1);
            check(plasma.Ticks == 2 && expiries == 1, "Plasma cannot tick or expire twice during despawn, even after balancing is disabled");

            Fresh(); plasma = Plasma(); plasma.Step(1.5f); int refreshAttacks = 0;
            plasma.OnRefreshAttack = () => refreshAttacks++;
            plasma.RefreshDurationTimer(999, 999);
            plasma.Step(0.499f); plasma.OnTick = null; plasma.AddStack(); plasma.OnTick = plasma.OnUpdate_Server;
            float refreshedAt = Time.time;
            Time.time = 102; plasma.Update();
            check(!plasma.IsEndBuff && refreshAttacks == 2, "Plasma override and renewing stacks retain native refresh attacks and reset the shared deadline");
            Time.time = refreshedAt + 1.999f; plasma.Update();
            check(!plasma.IsEndBuff, "Plasma refresh grants up to two more seconds");
            int ticks = plasma.Ticks; Time.time = refreshedAt + 2; plasma.Update();
            check(plasma.IsEndBuff && plasma.Ticks == ticks, "Plasma refresh bonuses cannot extend the cap or add a late tick");
            plasma.RefreshDurationTimer(999, 999); plasma.Step(1);
            check(plasma.IsEndBuff && plasma.Ticks == ticks, "A native refresh cannot restart a retired Plasma lifetime");
            plasma.InitializeAndSpawn(plasma.NetworkAttacker, plasma.NetworkTarget); plasma.Step(0.75f);
            check(!plasma.IsEndBuff && plasma.Ticks == ticks + 1, "New Plasma initialization replaces the retired receipt");

            Fresh(); plasma = Plasma(); plasma.renewDurationOnStacked = false; plasma.Step(1.5f);
            plasma.OnTick = null; plasma.AddStack(); plasma.OnTick = plasma.OnUpdate_Server; plasma.Step(0.5f);
            check(plasma.IsEndBuff, "Non-renewing Plasma stacks cannot reset the lifetime");
            Fresh(); plasma = Plasma(duration: 1); plasma.Step(1);
            check(plasma.IsEndBuff && plasma.NativeDuration == 1, "Short native Plasma lifetimes stay short");

            Fresh(); plasma = Plasma(); plasma.Step(1);
            SessionSettings.FriendlyFireForHit = new(true, 100); plasma.Step(2);
            check(!plasma.IsEndBuff, "Disabling balancing immediately restores Plasma's native lifetime");
            plasma.RefreshDurationTimer(999, 999);
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); plasma.Step(1.999f);
            check(!plasma.IsEndBuff, "Plasma refresh while disabled is respected when balancing resumes");
            plasma.Step(0.01f); check(plasma.IsEndBuff, "Reenabled balancing still caps refreshed Plasma at two seconds");
            Fresh(); plasma = Plasma(); SessionSettings.FriendlyFireForHit = new(true, 100); plasma.Step(3);
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); plasma.Step(0);
            check(plasma.IsEndBuff && plasma.Ticks == 1, "Enabling balancing retires old Plasma before another tick");

            foreach (var pair in new[] {
                (new PlayerAvatar() as UnitAvatar, new UnitAvatar { faction = "enemy" }),
                (new UnitAvatar { faction = "enemy" }, new PlayerAvatar() as UnitAvatar),
                (new PlayerAvatar() as UnitAvatar, new UnitAvatar { faction = "friends" }) })
            {
                Fresh(); plasma = Plasma(pair.Item1, pair.Item2); plasma.Step(3);
                check(!plasma.IsEndBuff && plasma.Ticks == 1, "Incoming/outgoing PvE and non-player victims retain native Plasma lifetime");
            }
            Fresh(); var self = new PlayerAvatar(); plasma = Plasma(self, self); plasma.Step(3);
            check(!plasma.IsEndBuff, "Self-origin Plasma retains native lifetime");
            foreach (var settings in new[] { new FriendlyFireSettings(true, 100), new FriendlyFireSettings(false, 100, balance: true),
                new FriendlyFireSettings(true, 0, balance: true) })
            {
                Fresh(); SessionSettings.FriendlyFireForHit = settings; plasma = Plasma(); plasma.Step(3);
                check(!plasma.IsEndBuff, "Balance off, FF off and zero damage retain native Plasma lifetime");
            }
            Fresh(); plasma = Plasma(); FriendlyFireParticipants.Select((PlayerAvatar)plasma.NetworkAttacker); plasma.Step(3);
            check(!plasma.IsEndBuff && plasma.NetworkTarget.Hp == 100, "Unselected players are outside the Plasma cap and damage policy");
            Fresh(); plasma = Plasma(); CombatMatchRuntime.PairRule = (_, _) => false; plasma.Step(3);
            check(!plasma.IsEndBuff && plasma.NetworkTarget.Hp == 100, "Match bystanders are outside the Plasma cap and damage policy");
            CombatMatchRuntime.PairRule = null; plasma.Step(0);
            check(plasma.IsEndBuff, "Current match opponents apply the cap to the existing Plasma age");
            Fresh(); var owner = new PlayerAvatar(); var companion = new UnitAvatar { NetworkLeader = owner };
            plasma = Plasma(companion); companion.IsDead = true; companion.NetworkLeader = null; plasma.Step(2);
            check(plasma.IsEndBuff && plasma.Ticks == 0, "Companion Plasma keeps the owner cap after native leader cleanup");
            Fresh(); plasma = Plasma();
            plasma.OnExpire = () => plasma.NetworkTarget.ApplyDamage(EffectTests.Tick(plasma.NetworkAttacker, "Debuff_Electric", 2));
            FriendlyFireParticipants.Depart((PlayerAvatar)plasma.NetworkAttacker);
            FriendlyFireParticipants.Rejoin((PlayerAvatar)plasma.NetworkAttacker); plasma.Step(0.1f);
            check(plasma.IsEndBuff && plasma.Ticks == 0 && plasma.NetworkTarget.Hp == 100,
                "Reconnecting retires stale Plasma without leaking its native expiry attack");
            Fresh(); plasma = Plasma(); Mirror.NetworkServer.active = false; plasma.Step(3);
            check(!plasma.IsEndBuff, "Guests do not apply host-only Plasma destruction");

            Fresh(); plasma = Plasma(); plasma.Step(0);
            for (int i = 0; i < 100; i++) plasma.Update();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) plasma.Update();
            check(GC.GetAllocatedBytesForCurrentThread() == allocated && !plasma.IsEndBuff,
                "Paused/warmed Plasma lifetime checks do not advance or allocate");
        }
        foreach (var effect in new PeriodicDebuffFixture[] { new CharacterDebuff_Frostbite(), new CharacterDebuff_Poison(), new CharacterDebuff_Wound() })
        {
            Fresh(); effect.NativeDuration = 30; effect.InitializeAndSpawn(new PlayerAvatar(), new PlayerAvatar());
            effect.OnTick = effect.OnUpdate_Server; effect.Step(3);
            check(!effect.IsEndBuff && effect.Ticks == 1, "Unrelated elemental debuffs retain their native lifetime");
        }
        Fresh();
    }
}
