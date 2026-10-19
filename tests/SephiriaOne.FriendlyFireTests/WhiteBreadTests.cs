using SephiriaOne;

internal static class WhiteBreadTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, victim = null;
        Charm_HitInvincible bread = null;
        bool Hit() => victim.ApplyDamage(new() { origin = source, damage = 10 }) == EApplyDamageResult.Success;
        void Fresh()
        {
            FriendlyFireParticipants.Clear(); FriendlyFireFeature.Available = true;
            CombatMatchRuntime.ParticipantRule = null; CombatMatchRuntime.PairRule = null;
            SessionSettings.FriendlyFireForHit = new(true, 100);
            Mirror.NetworkServer.active = true;
            ItemDatabase.Bread = new(); source = new(); victim = new() { invincibleTime = 1.5f };
            bread = new() { NetworkAvatar = victim, IsEffectEnabled = true, limitedEffectEnabledLevel = 2 };
            victim.Inventory.Bread = bread; victim.SetHitWindow(0.4f);
        }
        Fresh(); check(Hit() && victim.Hp == 90, "White Bread extra invulnerability does not reject an otherwise valid PvP hit");
        check(victim.invincibleTime == 1.5f && bread.IsEffectEnabled && bread.limitedEffectEnabledLevel == 2,
            "Native artifact duration, enabled state and level stay intact");
        Fresh(); victim.SetHitWindow(0.1f); check(!Hit(), "Native base hit-invulnerability remains");
        Fresh(); victim.IsInvulnerable = true; check(!Hit(), "Unrelated revive/general invulnerability remains");
        Fresh(); victim.SetHitWindow(2.1f, EInvincibleType.Fall); check(!Hit(), "Fall recovery grace remains");
        victim.SetHitWindow(2.4f, EInvincibleType.Fall); check(Hit(), "Only Bread extension is removed after fall grace");
        foreach (int level in new[] { 0, 1, 2, 99 })
        {
            Fresh(); bread.limitedEffectEnabledLevel = level;
            victim.invincibleTime = 0.3f + bread.addTimeByLevel[Math.Min(level, 2)];
            check(Hit(), "Current effective artifact level is used: " + level);
            victim.SetHitWindow(0.2f); check(!Hit(), "Every level retains base duration: " + level);
        }
        Fresh(); victim.invincibleTime = 1.9f;
        check(!Hit(), "An unrelated added duration remains intact");
        victim.SetHitWindow(0.8f); check(Hit(), "Only Bread is subtracted from combined duration");
        Fresh(); bread.IsEffectEnabled = false; check(!Hit(), "Disabled Bread cannot suppress another protection");
        Fresh(); victim.Inventory.Bread = null; check(!Hit(), "Unequipped Bread cannot suppress another protection");
        Fresh(); victim.Inventory.Bread = new Charm_Basic { IsEffectEnabled = true }; check(!Hit(), "Unexpected unique-effect type retains native protection");
        Fresh(); bread.Destroyed = true; check(!Hit(), "Destroyed artifact cannot contribute");
        Fresh(); bread.NetworkAvatar = source; check(!Hit(), "Artifact from an old owner cannot contribute");
        Fresh(); victim.Inventory = null; check(!Hit(), "Missing inventory keeps native behavior");
        Fresh(); ItemDatabase.Bread = null; check(!Hit(), "Unavailable database keeps native behavior");
        Fresh(); bread.addTimeByLevel = Array.Empty<float>(); check(!Hit(), "Empty level configuration keeps native behavior");
        Fresh(); bread.addTimeByLevel = new[] { 1.2f }; check(Hit(), "Short level arrays use native final-element clamping");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f, 0f })
        {
            Fresh(); bread.addTimeByLevel[2] = invalid; check(!Hit(), "Invalid artifact contribution is not used: " + invalid);
        }
        Fresh(); victim.SetHitWindow(float.NaN); check(!Hit(), "Invalid native timer cannot drop invulnerability");
        Fresh(); victim.SetHitWindow(0.4f, (EInvincibleType)99); check(!Hit(), "Unknown native invulnerability kind is left alone");
        Fresh(); CombatMatchRuntime.ParticipantRule = p => p != victim; check(!Hit(), "Match spectators retain native protection");
        Fresh(); FriendlyFireParticipants.Select(source); check(!Hit(), "Unselected players retain protection and cannot be attacked");
        Fresh(); FriendlyFireParticipants.Depart(victim); check(!Hit(), "Departed lifetime is excluded immediately");
        Fresh(); FriendlyFireFeature.Available = false; check(!Hit(), "Unavailable feature does not suppress protection");
        Fresh(); victim.isServer = false; check(!Hit(), "Nonauthoritative avatars are unchanged");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); check(Hit(), "Optional balancing still allows the same artifact suppression");
        // Use an NPC hit to observe native behavior while friendly fire is off.
        Fresh(); var enemyHit = new DamageInstance { origin = new UnitAvatar { faction = "enemy" }, targetFactionLayers = -1, damage = 1 };
        bool EnemyHit() => victim.ApplyDamage(enemyHit) == EApplyDamageResult.Success;
        SessionSettings.FriendlyFireForHit = default;
        check(!EnemyHit(), "Native PvE protection is unchanged with PvP off");
        SessionSettings.FriendlyFireForHit = new(true, 100);
        check(EnemyHit(), "Enabling PvP removes an already-running Bread extension immediately");
        SessionSettings.FriendlyFireForHit = default;
        check(!EnemyHit(), "Disabling PvP restores the remaining native window without a frame or equipment refresh");
        victim.AdvanceHitWindow(2);
        check(EnemyHit(), "Native window continues to expire normally");
        Fresh(); var npc = new UnitAvatar { faction = "enemy", invincibleTime = 1.5f };
        npc.Inventory.Bread = new Charm_HitInvincible { NetworkAvatar = npc, IsEffectEnabled = true, limitedEffectEnabledLevel = 2 };
        npc.SetHitWindow(0.4f);
        check(npc.ApplyDamage(new() { origin = source, damage = 1 }) != EApplyDamageResult.Success, "NPC artifact protection is unchanged during PvP");
        Fresh();
        for (int i = 0; i < 100; i++) { victim.Hp = 100; Hit(); }
        // Reuse a native-like damage packet to measure only the adapter and hit path.
        var packet = new DamageInstance { origin = source, damage = 1 };
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { victim.Hp = 100; victim.ApplyDamage(packet); }
        check(GC.GetAllocatedBytesForCurrentThread() == bytes, "White Bread adapter allocates no memory on warmed hit path");
        Fresh(); SessionSettings.FriendlyFireForHit = default;
    }
}
