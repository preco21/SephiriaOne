using SephiriaOne;
using UnityEngine;

internal static class LowerBoundCombatTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        void Edit(string tail)
        {
            check(FriendlyFireCommand.Parse("/one friendlyfire " + tail, out var edit) == FriendlyFireParseResult.Valid,
                "Lower-bound live edit accepted: " + tail);
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(edit);
        }
        void Fresh(bool enabled = true, bool frequency = false, decimal scale = 100, float hp = 150)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
            CombatMatchRuntime.Zombies.Clear(); CombatMatchRuntime.Humans.Clear();
            SessionSettings.ZombieDynamicBalance = false; CombatMatchRuntime.ResetDynamicZombieBalance();
            Mirror.NetworkServer.active = true; Time.time = Time.unscaledTime = 10;
            SessionSettings.FriendlyFireForHit = new(true, scale, balance: true, hitFrequency: frequency);
            source = new(); target = new() { Hp = hp, MaxHp = hp };
            if (enabled) Edit("lower-bound on");
        }
        DamageInstance Hit(float damage = 1, string id = "Weapon_Test", EDamageFromType type = EDamageFromType.DirectAttack) =>
            new() { origin = source, damage = damage, id = id, fromType = type, damageType = EDamageType.Projectile };
        double Strike(DamageInstance hit)
        { float before = target.Hp; target.ApplyDamage(hit); return before - target.Hp; }
        bool Near(double a, double b) => Math.Abs(a - b) < .002;

        Fresh(); check(Near(Strike(Hit()), 6.5), "Weak hit receives HP-relative assistance before remaining limits");
        Fresh(); check(Near(Strike(Hit(2)), 7), "Stronger weak hit retains more damage than the weaker hit");
        foreach (decimal scale in new[] { .3m, .5m })
        { Fresh(scale: scale); check(Near(Strike(Hit((float)(100m / scale))), 6.5), "Tiny percentage uses post-scale relative damage once"); }
        Fresh(hp: 300); check(Near(Strike(Hit(2)), 13), "Same relative strength scales proportionally with opponent HP");
        Fresh(); check(Strike(Hit(20)) == 20, "Strong original hit is unchanged");
        Fresh(false); check(Strike(Hit()) == 1, "Lower-bound default off preserves damage");
        Fresh(); Edit("balance off"); check(Strike(Hit()) == 1, "Balancing off removes assistance immediately");
        Fresh(); Edit("lower-bound off"); check(Strike(Hit()) == 1, "Feature off removes assistance immediately");
        Fresh(); Edit("damage 0"); check(Strike(Hit()) == 0, "Zero configured damage cannot be promoted");
        foreach (float invalid in new[] { 0f, -1, float.NaN, float.PositiveInfinity })
        {
            Fresh(); var result = EApplyDamageResult.Success;
            FriendlyFireRuntime.BeforeHit(target, Hit(), ref result, out var scope);
            check(FriendlyFireRuntime.Scale(invalid) == 0, "Invalid resolved hit cannot borrow assistance");
            FriendlyFireRuntime.AfterHit(null, scope);
        }

        Fresh();
        double burst = Strike(Hit());
        burst += Strike(Hit(1, "Charm_Planet_Blue", EDamageFromType.None));
        burst += Strike(Hit(1, "Charm_Planet_Red", EDamageFromType.None));
        burst += Strike(Hit(1, "Charm_PlitviceWaterDrop", EDamageFromType.None));
        check(Near(burst, 10), "Direct hits and separate Planet types share one six-HP assistance pool");
        check(Strike(Hit()) == 1, "Exhausted assistance never discards the base hit");
        Time.time += .1f;
        check(Near(Strike(Hit()), 2.2), "Assistance continuously refills at eight percent HP per second");

        Fresh(frequency: true);
        for (int i = 0; i < 4; i++) Strike(Hit(20)); // Strong hits prime frequency without spending bonus.
        check(Near(Strike(Hit(2)), 7 * Math.Sqrt(4d / 5)), "Rapid-hit reduction applies to original AND bonus damage");
        Fresh(); Edit("rapid burst 1");
        check(Near(Strike(Hit()), 1.5) && Strike(Hit()) == 1, "Lower-bound cannot bypass an exhausted upper budget");
        Time.time += .1f;
        check(Near(Strike(Hit()), 1.5), "Upper reserve continues capping assisted damage after refill");

        // A rejected late native write returns only the credit actually reserved.
        Fresh();
        var nativeResult = EApplyDamageResult.Success;
        FriendlyFireRuntime.BeforeHit(target, Hit(), ref nativeResult, out var late);
        check(Near(FriendlyFireRuntime.Scale(1), 6.5), "Scoped hit reserves bonus before native callbacks");
        FriendlyFireRuntime.AfterHit(null, late);
        check(Near(Strike(Hit()), 6.5), "Native cancellation refunds funded assistance exactly once");
        Fresh(); target.Shield = 100;
        check(Strike(Hit()) == 0 && Near(target.Shield, 93.5), "Shields absorb assisted damage normally");
        target.Shield = 0;
        check(Near(Strike(Hit()), 1.5), "Shield absorption consumes assistance and cannot refund a fresh bonus");
        Fresh();
        var nested = new Charm_FrostiumRing { NetworkAvatar = source, Target = target, Damage = Hit(1, "Charm_FrostiumRing", EDamageFromType.None) };
        target.OnHit = (_, _) => { target.OnHit = null; nested.HandleAttackUnit(); };
        check(Near(Strike(Hit()), 8), "Nested attack cannot reuse its parent's reserved assistance");

        Fresh(); Strike(Hit()); Strike(Hit());
        Edit("lower-bound off"); Edit("lower-bound on");
        check(Strike(Hit()) == 1, "Off/on does not refill spent assistance");
        Edit("lower-bound reference 10");
        check(Strike(Hit()) == 1, "Increasing reference does not create immediate bonus credit");
        Edit("lower-bound refill 16"); Time.time += .1f;
        check(Near(Strike(Hit()), 2.2), "Higher refill is not retroactive across an unobserved edit");
        Time.time += .1f;
        check(Near(Strike(Hit()), 3.4), "New refill applies after observation");

        for (int reset = 0; reset < 6; reset++)
        {
            Fresh(); Strike(Hit()); Strike(Hit());
            switch (reset)
            {
                case 0: FriendlyFireKda.ResetScores(); break;
                case 1: source.IsDead = true; source.Revive(100); break;
                case 2: target.currentFloorGuid = "next"; break;
                case 3: FriendlyFireParticipants.Depart(source); FriendlyFireParticipants.Rejoin(source); break;
                case 4: FriendlyFireParticipants.Select(source, target); break;
                case 5: FriendlyFireRuntime.Clear(); break;
            }
            check(Near(Strike(Hit()), 6.5), "Assistance follows shared session/life/selection reset " + reset);
        }
        Fresh(); CombatMatchRuntime.Chaser = target; SessionSettings.PlayTagChaser = new(true, 2, true, 50);
        CombatMatchRuntime.RoleSettingsChanged();
        check(Near(Strike(Hit()), 3.25), "Role incoming reduction applies after assistance");
        Fresh();
        SessionSettings.ZombieDynamicBalance = true; SessionSettings.ZombieModifiers = default;
        CombatMatchRuntime.Zombies.Add(source); CombatMatchRuntime.Humans.Add(target);
        CombatMatchRuntime.DynamicZombieRatio(5, 1); CombatMatchRuntime.RoleSettingsChanged();
        Time.time = Time.unscaledTime = 40;
        check(Near(Strike(Hit()), 6.5 * CombatMatchRuntime.IncomingRoleScale(target)), "Dynamic Human defense is not undone by assistance");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false;
        check(Strike(Hit()) == 0, "Same-team or inactive-match pairs cannot receive assisted damage");
        Fresh(); FriendlyFireParticipants.Select(source);
        check(Strike(Hit()) == 0, "Unselected players remain protected");
        Fresh();
        var enemy = new UnitAvatar { faction = "enemy" };
        enemy.ApplyDamage(Hit()); check(enemy.Hp == 99, "Outgoing PvE stays native");
        target.ApplyDamage(new() { origin = enemy, damage = 1, targetFactionLayers = -1 }); check(target.Hp == 149, "Incoming PvE stays native");

        Fresh(frequency: true);
        var cached = Hit();
        void Storm()
        {
            for (int i = 0; i < 2000; i++)
            { target.Hp = target.MaxHp; Time.time += .001f; target.ApplyDamage(cached); }
        }
        Storm(); long allocations = GC.GetAllocatedBytesForCurrentThread(); Storm();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocations;
        check(bytes == 0, "Warmed assisted rapid-hit processing allocates nothing");
        Console.WriteLine($"PvP lower-bound assistance: {bytes} bytes/2000 warmed hits");
        SessionSettings.FriendlyFireForHit = default; SessionSettings.ZombieDynamicBalance = false;
        SessionSettings.PlayTagChaser = default; CombatMatchRuntime.Chaser = null;
        CombatMatchRuntime.Zombies.Clear(); CombatMatchRuntime.Humans.Clear(); CombatMatchRuntime.ResetDynamicZombieBalance();
        CombatMatchRuntime.RoleSettingsChanged(); FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
    }
}
