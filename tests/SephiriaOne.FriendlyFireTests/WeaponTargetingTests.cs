using SephiriaOne;
using UnityEngine;

internal static class WeaponTargetingTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        var owner = new PlayerAvatar();
        var player = new PlayerAvatar { transform = { position = new(1, 0) } };
        var enemy = new UnitAvatar { faction = "enemy", transform = { position = new(0.5f, 0) } };
        var katana = new WeaponSimple_Katana { Owner = owner };
        var shield = new WeaponSimple_SwordAndShield { Owner = owner };
        var nastrond = new WeaponAddonKatana_Nastrond();
        PlayerInputController.Candidates.Clear();
        PlayerInputController.Candidates.Add(player);
        foreach (var select in new Func<UnitAvatar>[] {
            () => { katana.OnCreateMagicServerside(null); return katana.Target; },
            () => { shield.Update(); return shield.Target; },
            () => { nastrond.Candidate = PlayerInputController.Candidates[0]; return nastrond.SearchTarget(owner); }
        })
        {
            SessionSettings.FriendlyFireForHit = new(true, 50);
            check(select() == player, "Weapon selector admits eligible player");
            SessionSettings.FriendlyFireForHit = default;
            check(select() == null, "Weapon selector obeys disabled friendly fire");
            SessionSettings.FriendlyFireForHit = new(true, 0);
            check(select() == null, "Weapon selector obeys zero scale");
            SessionSettings.FriendlyFireForHit = new(true, 50);
            FriendlyFireParticipants.Select(owner);
            check(select() == null, "Weapon selector obeys participant selection");
            FriendlyFireParticipants.Clear();
            CombatMatchRuntime.PairRule = (_, _) => false;
            check(select() == null, "Weapon selector obeys current match pair/countdown");
            CombatMatchRuntime.PairRule = null;
            FriendlyFireParticipants.Depart(player);
            check(select() == null, "Weapon selector rejects old player connection");
            FriendlyFireParticipants.Clear();
            player.IsDead = true; check(select() == null, "Weapon selector keeps dead rejection"); player.IsDead = false;
            player.canBeTarget.Value = false; check(select() == null, "Weapon selector keeps targetability"); player.canBeTarget.Value = true;
            Mirror.NetworkServer.active = false;
            check(select() == null, "Weapon selector keeps stock guest behavior");
            Mirror.NetworkServer.active = true;
            PlayerInputController.Candidates[0] = enemy;
            check(select() == enemy, "Weapon selector retains native enemy targeting");
            PlayerInputController.Candidates[0] = player;
        }
        check(PlayerInputController.SearchTargetNearestPoint(owner, new(), 5) == null, "Weapon selector does not leak into global aim");
        katana.useMagicBlade = false; katana.OnCreateMagicServerside(null);
        shield.Ready = false; shield.Update();
        check(katana.Target == null && shield.Target == null, "Native weapon feature and readiness gates remain in force");
        katana.useMagicBlade = true; shield.Ready = true;
        PlayerInputController.Candidates.Add(enemy);
        katana.OnCreateMagicServerside(null); shield.Update();
        check(katana.Target == enemy && shield.Target == enemy, "Native nearest ranking remains in force");
        PlayerInputController.Candidates.Remove(enemy); player.transform.position = new(3, 0);
        katana.OnCreateMagicServerside(null); shield.Update();
        check(katana.Target == null && shield.Target == null, "Native squared range remains in force");
        player.transform.position = new(1, 0);

        foreach (var mover in new BulletMoveModule[] { new BulletMoveModule_CrossbowMine(), new BulletMoveModule_CrossbowMineHoming() })
        foreach (string prefab in new[] { "CrossbowMine", "CrossbowMine_Fire", "CrossbowMine_Poison", "CrossbowMine_Drone", "CrossbowMine_DroneEnhanced" })
        {
            string attack = prefab.StartsWith("CrossbowMine_Drone") ? "Weapon_SpecialAttack" : "Weapon_DashAttack";
            var bullet = new Bullet { NetworkOwner = owner, FromType = EDamageFromType.DirectAttack, damageId = attack, spawnedPrefabName = prefab };
            mover.Bullet = bullet; mover.Candidate = player;
            bool Trigger() { mover.OnMove(); return mover.Triggered; }
            SessionSettings.FriendlyFireForHit = new(true, 50);
            check(Trigger(), prefab + " proximity triggers on an eligible player");
            SessionSettings.FriendlyFireForHit = default;
            check(!Trigger(), prefab + " does not retain stale friendly hostility");
            SessionSettings.FriendlyFireForHit = new(true, 0);
            check(!Trigger(), prefab + " respects zero damage");
            SessionSettings.FriendlyFireForHit = new(true, 50);
            FriendlyFireParticipants.Select(owner); check(!Trigger(), prefab + " respects selection");
            FriendlyFireParticipants.Clear();
            CombatMatchRuntime.PairRule = (_, _) => false; check(!Trigger(), prefab + " respects match transitions");
            CombatMatchRuntime.PairRule = null;
            FriendlyFireParticipants.Depart(player); check(!Trigger(), prefab + " rejects old connections"); FriendlyFireParticipants.Clear();
            bullet.NetworkOwner = new UnitAvatar { NetworkLeader = player }; check(!Trigger(), prefab + " protects companion owner");
            bullet.NetworkOwner = owner; mover.Candidate = owner; check(!Trigger(), prefab + " preserves self exclusion"); mover.Candidate = player;
            mover.Ready = false; check(!Trigger(), prefab + " preserves native cadence/readiness gate"); mover.Ready = true;
            player.IsDead = true; check(!Trigger(), prefab + " preserves dead exclusion"); player.IsDead = false;
            player.canBeTarget.Value = false; check(!Trigger(), prefab + " preserves targetability"); player.canBeTarget.Value = true;
            mover.RangeSquared = 1; check(!Trigger(), prefab + " preserves strict proximity radius"); mover.RangeSquared = 4;
            bullet.FromType = EDamageFromType.None; check(!Trigger(), prefab + " rejects mismatched damage type"); bullet.FromType = EDamageFromType.DirectAttack;
            foreach (string other in new[] { "Weapon_BasicAttack", attack == "Weapon_SpecialAttack" ? "Weapon_DashAttack" : "Weapon_SpecialAttack" })
            { bullet.damageId = other; check(!Trigger(), prefab + " rejects mismatched attack ID: " + other); }
            bullet.damageId = attack;
            bullet.spawnedPrefabName = "OtherMine"; check(!Trigger(), prefab + " rejects unaudited pooled prefab"); bullet.spawnedPrefabName = prefab;
            check(Trigger(), prefab + " reads current pooled identity");
            Mirror.NetworkServer.active = false; check(!Trigger(), prefab + " preserves stock guest targeting"); Mirror.NetworkServer.active = true;
            mover.Candidate = enemy; check(Trigger(), prefab + " preserves native enemy mask");
            mover.Candidate = player;
            long allocated = Measure(mover);
            check(allocated == 0, prefab + " warmed proximity policy allocated " + allocated + " bytes (expected zero)");
        }
        FriendlyFireParticipants.Clear();
        PlayerInputController.Candidates.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 50);
    }

    // Keep the allocation probe outside the test's captured selector/Trigger
    // locals, so tiered-JIT closure materialization is not measured as gameplay.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long Measure(BulletMoveModule mover)
    {
        for (int i = 0; i < 1000; i++) mover.OnMove();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) mover.OnMove();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
