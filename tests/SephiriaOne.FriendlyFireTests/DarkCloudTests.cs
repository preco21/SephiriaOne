using SephiriaOne;
using UnityEngine;

internal static class DarkCloudTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 50, balance: true);
        // Keep these small hits below the new soft knee, isolating strike timing.
        var source = new PlayerAvatar(); var target = new PlayerAvatar { MaxHp = 400 };
        var cloud = new ComboEffect_DarkCloud { NetworkAvatar = source,
            Damage = new() { origin = source, id = "Ability_DarkCloud", damage = 20, targetFactionLayers = -1 } };
        cloud.OnAttackUnit += t => t.ApplyDebuff(new(), source);
        Time.time = 10;
        cloud.FireLightning(target);
        check(target.Hp == 90 && target.DebuffApplications == 1 && cloud.Effects == 1,
            "Dark Cloud first hostile-player strike retains damage scale, burn callback and native FX");
        Time.time = 10.133f;
        cloud.FireLightning(target);
        check(target.Hp == 90 && target.DebuffApplications == 1 && cloud.Effects == 1,
            "Dark Cloud rapid same-player repeat is suppressed before damage, debuff callback and FX");
        Time.time = 10.25f; cloud.FireLightning(target);
        check(target.Hp == 90 && target.DebuffApplications == 1 && cloud.Effects == 1,
            "Dark Cloud rejects the former quarter-second interval including its callback and FX");
        Time.time = 10.499f; cloud.FireLightning(target);
        check(target.Hp == 90, "Dark Cloud remains limited just before the half-second boundary");
        Time.time = 10.5f; cloud.FireLightning(target);
        check(target.Hp == 80 && target.DebuffApplications == 2 && cloud.Effects == 2,
            "Dark Cloud admits the exact half-second boundary without extending cooldown on rejected strikes");
        cloud.FireLightning(target);
        check(target.Hp == 80 && target.DebuffApplications == 2 && cloud.Effects == 2,
            "Dark Cloud rejects a second strike on the same boundary frame");

        var another = new PlayerAvatar { MaxHp = 400 }; cloud.FireLightning(another);
        check(another.Hp == 90, "Another hostile player has an independent strike window");
        var secondOwner = new PlayerAvatar();
        var secondCloud = Cloud(secondOwner);
        secondCloud.FireLightning(target);
        check(target.Hp == 70, "Another attacker can strike the same player independently");
        var replacedCloud = Cloud(source); replacedCloud.FireLightning(target);
        check(target.Hp == 70 && replacedCloud.Strikes == 0, "Replacing the cloud component does not bypass the owner/player limit");
        replacedCloud.NetworkAvatar = secondOwner; replacedCloud.Damage.origin = secondOwner;
        replacedCloud.FireLightning(another);
        check(another.Hp == 80, "Pooled cloud reads its current owner instead of a cached component owner");

        var enemy = new UnitAvatar { faction = "enemy" };
        cloud.FireLightning(enemy); cloud.FireLightning(enemy);
        check(enemy.Hp == 60 && enemy.DebuffApplications == 2, "Native enemy damage and burn are never throttled or rescaled");
        cloud.isPlayerCombo = false; cloud.FireLightning(target); cloud.FireLightning(target);
        check(cloud.NativeNpcCalls == 2, "Native non-player-combo branch is never throttled");
        cloud.isPlayerCombo = true;
        var nativeNpc = new UnitAvatar { faction = "enemy" };
        var npcCloud = Cloud(nativeNpc); npcCloud.FireLightning(another); npcCloud.FireLightning(another);
        check(another.Hp == 40, "Monster-origin lightning remains native even with player-combo metadata");

        foreach (var settings in new[] { default(FriendlyFireSettings), new FriendlyFireSettings(true, 0) })
        {
            SessionSettings.FriendlyFireForHit = settings;
            int before = cloud.Strikes; float hp = target.Hp; int debuffs = target.DebuffApplications;
            cloud.FireLightning(target); cloud.FireLightning(target);
            check(cloud.Strikes == before + 2 && target.Hp == hp && target.DebuffApplications == debuffs,
                "Off/zero leaves native strike scheduling intact while existing friendly-fire guards protect players");
        }
        SessionSettings.FriendlyFireForHit = new(true, 50, balance: true);
        int count = cloud.Strikes;
        cloud.FireLightning(target); cloud.FireLightning(target);
        check(cloud.Strikes == count + 1, "Re-enabling friendly fire starts a fresh limited window");
        Mirror.NetworkServer.active = false;
        count = cloud.Strikes; cloud.FireLightning(target); cloud.FireLightning(target);
        check(cloud.Strikes == count + 2, "Guest clients never apply the host-side rate limit");
        Mirror.NetworkServer.active = true;

        var pair = Fresh();
        FriendlyFireParticipants.Select(pair.Source);
        pair.Cloud.FireLightning(pair.Target); pair.Cloud.FireLightning(pair.Target);
        check(pair.Cloud.Strikes == 2 && pair.Target.Hp == 100, "Excluded player uses existing impact veto without acquiring a cooldown");
        FriendlyFireParticipants.Select(pair.Source, pair.Target);
        pair.Cloud.FireLightning(pair.Target); pair.Cloud.FireLightning(pair.Target);
        check(pair.Cloud.Strikes == 3 && pair.Target.Hp == 90, "Selecting both players admits one fresh strike");
        CombatMatchRuntime.PairRule = (_, _) => false;
        pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 90, "Tournament non-opponents still cannot be damaged");
        CombatMatchRuntime.PairRule = null;
        FriendlyFireParticipants.Select();
        pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 80, "New match/selection epoch discards previous strike windows");
        FriendlyFireKda.ResetScores(); pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 70, "KDA reset invalidates the old strike window");
        pair.Target.currentFloorGuid = "floor-2"; pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 60, "Changed victim floor cannot reuse a previous-floor cooldown");
        pair.Source.currentFloorGuid = "floor-2"; pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 50, "Changed attacker floor cannot reuse a previous-floor cooldown");
        FriendlyFireParticipants.Depart(pair.Target); pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 50, "Disconnected victim remains protected by shared connection checks");
        FriendlyFireParticipants.Rejoin(pair.Target); pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 40, "Same avatar object with a fresh connection receipt gets a fresh window");
        FriendlyFireParticipants.Clear(); pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 30, "New run/session receipts do not inherit old cooldowns");

        pair = Fresh();
        // Model the first strike after preset inheritance, before BeforeHit has
        // enabled score tracking; this must not allow a second immediate bolt.
        FriendlyFireKda.SetEnabled(false);
        pair.Cloud.FireLightning(pair.Target); pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 90 && pair.Cloud.Strikes == 1, "First inherited enabled hit cannot invalidate its own rate-limit epoch");
        Time.time = 0; pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 80, "A reset game clock starts a new window");

        pair = Fresh();
        pair.Cloud.OnAttackUnit += t => t.ApplyDebuff(new(), pair.Source);
        pair.Cloud.FireLightning(pair.Target);
        pair.Source.IsDead = true; Time.time += 0.133f;
        pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 90 && pair.Target.DebuffApplications == 1 && pair.Cloud.Effects == 1,
            "Owner death does not bypass the cap on queued Dark Cloud strikes or burn callbacks");
        Time.time = 10.5f; pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 80 && pair.Target.DebuffApplications == 2,
            "Already-queued strikes from a dead owner retain native behavior at the next interval");

        pair = Fresh();
        pair.Cloud.OnAttackUnit += _ => pair.Cloud.FireLightning(pair.Target);
        pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 90 && pair.Cloud.Strikes == 1 && pair.Cloud.Effects == 1,
            "Rate window is reserved before recursive native strike callbacks");
        pair = Fresh();
        pair.Target.OnHit = (_, _) => throw new InvalidOperationException("cloud fixture");
        try { pair.Cloud.FireLightning(pair.Target); } catch (InvalidOperationException) { }
        pair.Target.OnHit = null;
        pair.Cloud.FireLightning(pair.Target);
        check(pair.Cloud.Strikes == 1, "Failed native strike cannot immediately retry around the rate limit");
        Time.time += 0.5f; pair.Cloud.FireLightning(pair.Target);
        check(pair.Cloud.Strikes == 2, "Native exceptions do not poison the next strike window");

        pair = Fresh();
        pair.Cloud.FireLightning(pair.Target);
        for (int i = 0; i < 100; i++) FriendlyFireRuntime.BeforeDarkCloudStrike(pair.Cloud, pair.Target);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) FriendlyFireRuntime.BeforeDarkCloudStrike(pair.Cloud, pair.Target);
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed suppressed cloud checks allocate no per-strike memory");
        for (int i = 0; i < 100; i++) { Time.time++; FriendlyFireRuntime.BeforeDarkCloudStrike(pair.Cloud, pair.Target); }
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { Time.time++; FriendlyFireRuntime.BeforeDarkCloudStrike(pair.Cloud, pair.Target); }
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed admitted cloud checks allocate no per-strike memory");

        pair = Fresh(); pair.Cloud.FireLightning(pair.Target);
        SessionSettings.FriendlyFireForHit = new(true, 50, balance: false);
        pair.Cloud.FireLightning(pair.Target); pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 70 && pair.Cloud.Strikes == 3, "Balance off removes the Dark Cloud throttle immediately");
        SessionSettings.FriendlyFireForHit = new(true, 50, balance: true);
        pair.Cloud.FireLightning(pair.Target); pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 60 && pair.Cloud.Strikes == 4, "Balance on starts a fresh limited cloud window");
        SessionSettings.FriendlyFireForHit = new(true, 50, balance: false);
        SessionSettings.FriendlyFireForHit = new(true, 50, balance: true);
        pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 50 && pair.Cloud.Strikes == 5, "Off/on with no intervening hit cannot retain a stale cloud window");
        pair = Fresh(); pair.Cloud.OnAttackUnit += t => t.ApplyDebuff(new(), pair.Source);
        pair.Cloud.FireLightning(pair.Target);
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(PvpDotEffect.DarkCloud, 1.5m)));
        Time.time += 0.5f; pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 90 && pair.Cloud.Effects == 1 && pair.Target.DebuffApplications == 1,
            "Lengthened Dark Cloud interval suppresses damage, callbacks and FX using the prior successful strike");
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(PvpDotEffect.DarkCloud, 0.2m)));
        pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 80 && pair.Cloud.Effects == 2 && pair.Target.DebuffApplications == 2,
            "Shortened Dark Cloud interval applies live without restarting its window");
        foreach (var sibling in Enum.GetValues<PvpDotEffect>())
            if (sibling != PvpDotEffect.DarkCloud)
                SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(sibling, 3)));
        Time.time += 0.19f; pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 80, "Editing other effects neither clears nor changes the Dark Cloud interval");
        Time.time += 0.02f; pair.Cloud.FireLightning(pair.Target);
        check(pair.Target.Hp == 70 && pair.Cloud.Effects == 3 && pair.Target.DebuffApplications == 3,
            "Dark Cloud resumes on its own cadence; rejected strikes do not extend it");
        foreach (bool reviveOwner in new[] { false, true })
        {
            pair = Fresh();
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(PvpDotEffect.DarkCloud, 10)));
            pair.Cloud.OnAttackUnit += t => t.ApplyDebuff(new(), pair.Source);
            pair.Cloud.FireLightning(pair.Target);
            var dead = reviveOwner ? pair.Source : pair.Target;
            dead.IsDead = true; Time.time += 3;
            if (reviveOwner)
            {
                pair.Cloud.FireLightning(pair.Target);
                check(pair.Cloud.Strikes == 1, "A dead owner does not bypass the long interval before revival");
            }
            dead.Revive(100);
            pair.Cloud.FireLightning(pair.Target);
            check(pair.Cloud.Strikes == 2 && pair.Cloud.Effects == 2 && pair.Target.DebuffApplications == 2,
                "Successful revival retires Dark Cloud interval from previous life: owner=" + reviveOwner);
            pair.Cloud.FireLightning(pair.Target);
            check(pair.Cloud.Strikes == 2, "Revival grants only one fresh Dark Cloud strike before the new interval");
        }
        pair = Fresh();
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(PvpDotEffect.DarkCloud, 10)));
        check(FriendlyFireRuntime.BeforeDarkCloudStrike(pair.Cloud, pair.Target), "Strike can be admitted before native damage is emitted");
        pair.Target.IsDead = true; pair.Target.Revive(100); Time.time += 1;
        check(FriendlyFireRuntime.BeforeDarkCloudStrike(pair.Cloud, pair.Target), "A strike without a damage budget still retires on revival");
        CombatMatchRuntime.PairRule = null; FriendlyFireParticipants.Clear(); Time.time = 0;
    }

    private static ComboEffect_DarkCloud Cloud(UnitAvatar source) => new() { NetworkAvatar = source,
        Damage = new() { origin = source, id = "Ability_DarkCloud", damage = 20, targetFactionLayers = -1 } };
    private static (PlayerAvatar Source, PlayerAvatar Target, ComboEffect_DarkCloud Cloud) Fresh()
    {
        FriendlyFireParticipants.Clear(); CombatMatchRuntime.PairRule = null;
        SessionSettings.FriendlyFireForHit = new(true, 50, balance: true); Time.time = 10;
        var source = new PlayerAvatar(); var target = new PlayerAvatar { MaxHp = 400 };
        return (source, target, Cloud(source));
    }
}
