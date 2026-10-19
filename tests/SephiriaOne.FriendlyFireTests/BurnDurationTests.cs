using SephiriaOne;
using UnityEngine;

internal static class BurnDurationTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Fresh()
        {
            FriendlyFireParticipants.Clear(); CombatMatchRuntime.PairRule = null;
            Mirror.NetworkServer.active = true; Time.time = 100; Time.deltaTime = 0;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        }
        CharacterDebuff_Burn Burn(UnitAvatar source = null, UnitAvatar target = null, float duration = 30)
        {
            var burn = new CharacterDebuff_Burn { NativeDuration = duration };
            burn.InitializeAndSpawn(source ?? new PlayerAvatar(), target ?? new PlayerAvatar());
            burn.OnTick = burn.OnUpdate_Server;
            return burn;
        }
        Fresh(); var burn = Burn(); burn.Step(0.9f); burn.Step(0.9f); burn.Step(0.199f);
        check(!burn.IsEndBuff && burn.Ticks == 2, "Balanced Burn retains ticks before its two-second lifetime");
        Time.time = 102; Time.deltaTime = 0.001f; burn.Update();
        check(burn.IsEndBuff && burn.Ticks == 2 && burn.NativeDuration == 30,
            "Balanced Burn ends at exactly two seconds despite native extension, without a late tick or timer rewrite");
        int ended = 0; burn.OnExpire = () => ended++; burn.Step(1);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: false); burn.Step(1);
        check(burn.Ticks == 2 && ended == 0, "Expired Burn cannot tick or destroy twice during delayed despawn, even after balance off");

        Fresh(); burn = Burn(); burn.Step(1.5f);
        burn.RefreshDurationTimer(999, 999); burn.Step(0.49f);
        // Native Burn's AddStack changes stacks/blue appearance, not periodic ticks.
        burn.OnTick = null; burn.AddStack(); burn.OnTick = burn.OnUpdate_Server;
        float refreshedAt = Time.time;
        Time.time = 102; burn.Update();
        check(!burn.IsEndBuff, "Repeated application refreshes Burn beyond its original deadline");
        Time.time = refreshedAt + 1.999f; burn.Update();
        check(!burn.IsEndBuff, "Refreshed Burn remains active until two seconds after the latest application");
        int refreshedTicks = burn.Ticks;
        Time.time = refreshedAt + 2; burn.Update();
        check(burn.IsEndBuff && burn.Ticks == refreshedTicks,
            "Refreshed Burn still ends at two seconds regardless of duration bonuses, before another tick");
        burn.RefreshDurationTimer(999, 999); burn.Step(0.75f);
        check(burn.IsEndBuff && burn.Ticks == refreshedTicks, "Refreshing a retired Burn cannot revive it during delayed despawn");
        var replacement = Burn(burn.NetworkAttacker, burn.NetworkTarget); replacement.Step(0.9f);
        check(!replacement.IsEndBuff && replacement.Ticks == 1, "A fresh application after expiry receives an independent lifetime");
        burn.InitializeAndSpawn(new PlayerAvatar(), new PlayerAvatar()); burn.Step(0.75f);
        check(!burn.IsEndBuff, "Reinitializing an effect replaces its old receipt and ended flag");
        Fresh(); burn = Burn(); burn.Step(1.5f); burn.RefreshDurationTimer(999, 999); burn.Step(0.5f);
        check(!burn.IsEndBuff, "Direct native refresh also grants up to two more seconds");
        burn.Step(1.5f);
        check(burn.IsEndBuff, "Direct refresh cannot grant an extended native duration");
        Fresh(); burn = Burn(); burn.renewDurationOnStacked = false; burn.Step(1.5f);
        burn.OnTick = null; burn.AddStack(); burn.OnTick = burn.OnUpdate_Server; burn.Step(0.5f);
        check(burn.IsEndBuff, "Non-renewing native stacks do not extend Burn lifetime");
        Fresh(); burn = Burn(duration: 1); burn.Step(1);
        check(burn.IsEndBuff && burn.NativeDuration == 1, "Native durations below two seconds are never extended");
        int ticks = burn.Ticks; burn.Step(0.75f);
        check(burn.Ticks == ticks, "Native short expiry also blocks balanced Burn's delayed-despawn ticks");

        Fresh(); burn = Burn(); burn.Step(1);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: false); burn.Step(2);
        check(!burn.IsEndBuff && burn.Ticks == 2, "Disabling balancing restores untouched native lifetime immediately");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); burn.Step(0);
        check(burn.IsEndBuff && burn.Ticks == 2, "Enabling balancing expires an already-old Burn before another tick");
        Fresh(); burn = Burn(); burn.Step(1);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: false); burn.Step(2);
        burn.RefreshDurationTimer(999, 999);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); burn.Step(0.75f);
        check(!burn.IsEndBuff, "Live balance enable uses the latest native refresh, including refreshes made while disabled");
        burn.Step(1.25f);
        check(burn.IsEndBuff, "Latest refresh before balance enable still receives only two seconds");
        Fresh(); burn = Burn(); var later = Burn(); burn.Step(1);
        later.InitializeAndSpawn(later.NetworkAttacker, later.NetworkTarget);
        burn.Step(1); later.Update();
        check(burn.IsEndBuff && !later.IsEndBuff, "Separate applications keep independent deadlines");
        Fresh(); burn = Burn(); burn.OnExpire = () => throw new InvalidOperationException("expiry callback");
        try { burn.Step(2); } catch (InvalidOperationException) { }
        burn.OnExpire = null; burn.Step(1);
        var enemyHit = new PlayerAvatar(); enemyHit.ApplyDamage(EffectTests.Tick(new UnitAvatar { faction = "enemy" }, amount: 2));
        check(burn.IsEndBuff && burn.Ticks == 0 && enemyHit.Hp == 98,
            "Throwing native expiry callbacks cannot resume ticks or leak the debuff scope");

        foreach (var pair in new[] {
            (new PlayerAvatar() as UnitAvatar, new UnitAvatar { faction = "enemy" }),
            (new UnitAvatar { faction = "enemy" }, new PlayerAvatar() as UnitAvatar),
            (new PlayerAvatar() as UnitAvatar, new UnitAvatar { faction = "friends" }) })
        {
            Fresh(); burn = Burn(pair.Item1, pair.Item2); burn.Step(3);
            check(!burn.IsEndBuff && burn.Ticks == 1, "Incoming/outgoing PvE and non-player victims keep native Burn lifetime");
        }
        Fresh(); var self = new PlayerAvatar(); burn = Burn(self, self); burn.Step(3);
        check(!burn.IsEndBuff, "Self-origin Burn keeps native lifetime");
        foreach (var settings in new[] { new FriendlyFireSettings(true, 100), new FriendlyFireSettings(false, 100, balance: true),
            new FriendlyFireSettings(true, 0, balance: true) })
        {
            Fresh(); SessionSettings.FriendlyFireForHit = settings; burn = Burn(); burn.Step(3);
            check(!burn.IsEndBuff, "Balance off, FF off and zero damage keep native Burn lifetime");
        }
        Fresh(); burn = Burn(); FriendlyFireParticipants.Select((PlayerAvatar)burn.NetworkAttacker); burn.Step(3);
        check(!burn.IsEndBuff && burn.NetworkTarget.Hp == 100, "Unselected players retain native expiry while damage stays blocked");
        Fresh(); burn = Burn(); CombatMatchRuntime.PairRule = (_, _) => false; burn.Step(3);
        check(!burn.IsEndBuff && burn.NetworkTarget.Hp == 100, "Match bystanders are outside the Burn cap/damage policy");
        CombatMatchRuntime.PairRule = null; burn.Step(0);
        check(burn.IsEndBuff, "Current match opponents immediately apply the cap to original effect age");
        Fresh(); var owner = new PlayerAvatar(); var companion = new UnitAvatar { NetworkLeader = owner };
        burn = Burn(companion); companion.IsDead = true; companion.NetworkLeader = null; burn.Step(2);
        check(burn.IsEndBuff && burn.Ticks == 0, "Companion Burn retains the owner's cap after native leader cleanup");
        Fresh(); burn = Burn(); FriendlyFireParticipants.Depart((PlayerAvatar)burn.NetworkAttacker);
        FriendlyFireParticipants.Rejoin((PlayerAvatar)burn.NetworkAttacker); burn.Step(0.1f);
        check(burn.IsEndBuff && burn.Ticks == 0, "Reconnection cannot reuse old Burn origin or deadline");
        Fresh(); burn = Burn(); Mirror.NetworkServer.active = false; burn.Step(3);
        check(!burn.IsEndBuff, "Guests do not apply host-only Burn destruction"); Mirror.NetworkServer.active = true;
        Fresh(); var plasma = new CharacterDebuff_Plasma { NativeDuration = 30 };
        plasma.InitializeAndSpawn(new PlayerAvatar(), new PlayerAvatar()); plasma.OnTick = plasma.OnUpdate_Server; plasma.Step(3);
        check(plasma.IsEndBuff && plasma.Ticks == 0, "Plasma Burn expires before an overdue tick despite native duration extensions");
        Fresh(); burn = Burn(); burn.Step(0);
        for (int i = 0; i < 100; i++) burn.Update();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) burn.Update();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated && !burn.IsEndBuff,
            "Paused/warmed Burn lifetime checks neither advance nor allocate");
        Fresh();
    }
}
