using SephiriaOne;

internal static class CompanionTests
{
    internal static void Run(Action<bool, string> check)
    {
        var owner = new PlayerAvatar { Name = "Owner" };
        var guest = new PlayerAvatar { Name = "Guest" };
        var companion = new UnitAvatar { NetworkLeader = owner };
        var ai = new UnitAI_NewBasic { Avatar = companion, CurrentTarget = guest };
        SessionSettings.FriendlyFireForHit = new(true, 50);
        check(ai.ShouldAttack, "Enabled companion AI actively targets another player");
        guest.ApplyDamage(new() { origin = companion, damage = 10 });
        check(guest.Hp == 95, "Companion hits apply the host scale to other players' HP");
        ai.CurrentTarget = owner;
        owner.ApplyDamage(new() { origin = companion, damage = 10, targetFactionLayers = -1 });
        check(!ai.ShouldAttack && owner.Hp == 100 && owner.Hits == 0, "Companion cannot target or damage its owner even with a broad attack mask");

        ai.CurrentTarget = guest;
        var inFlight = new DamageInstance { origin = companion, damage = 10, targetFactionLayers = -1 };
        SessionSettings.FriendlyFireForHit = default;
        guest.ApplyDamage(inFlight);
        check(!ai.ShouldAttack && guest.Hp == 95, "Off restores native targeting and faction admission for an existing companion");
        SessionSettings.FriendlyFireForHit = new(true, 50);
        check(ai.ShouldAttack, "Re-enabling restores companion targeting without respawning it");
        SessionSettings.FriendlyFireForHit = default;
        guest.IsGuarding = true;
        guest.ApplyDamage(new() { origin = companion, damage = 10, targetFactionLayers = -1 });
        check(guest.Mp == 100 && guest.GuardHits == 0, "Off blocks an in-flight companion hit before MP guard costs or other hit side effects");
        guest.IsGuarding = false;
        SessionSettings.FriendlyFireForHit = new(true, 0);
        guest.ApplyDamage(new() { origin = companion, damage = 10 });
        check(!ai.ShouldAttack && guest.Hp == 95, "Zero allied damage does not provoke companions into useless attacks");
        SessionSettings.FriendlyFireForHit = new(true, 50);
        Mirror.NetworkServer.active = false;
        check(!ai.ShouldAttack, "Guest clients do not change companion hostility");
        Mirror.NetworkServer.active = true;

        companion.NetworkLeader = guest;
        check(!ai.ShouldAttack, "New owner is immediately protected without a cached owner identity");
        ai.CurrentTarget = owner;
        check(ai.ShouldAttack, "Former owner becomes a valid rival after ownership transfer");
        companion.NetworkLeader = null;
        check(!ai.ShouldAttack, "Released companion resumes native relations");
        companion.NetworkLeader = owner;
        ai.CurrentTarget = new PlayerAvatar { Name = "Guest" };
        check(ai.ShouldAttack, "A rejoined player's new avatar gets current policy without stale connection state");
        ai.CurrentTarget.IsDead = true;
        check(!ai.ShouldAttack, "Dead targets retain native exclusion");
        ai.CurrentTarget.IsDead = false; ai.CurrentTarget.IsInvulnerable = true;
        check(!ai.ShouldAttack, "Invulnerable targets retain native exclusion");

        var enemy = new UnitAvatar { faction = "enemy" };
        ai.CurrentTarget = enemy;
        enemy.ApplyDamage(new() { origin = companion, damage = 10 });
        check(ai.ShouldAttack && enemy.Hp == 90, "Companions retain native enemy targeting and full enemy damage");
        var npc = new UnitAvatar();
        ai.CurrentTarget = npc;
        npc.ApplyDamage(new() { origin = companion, damage = 10 });
        check(!ai.ShouldAttack && npc.Hp == 100, "Companions do not gain hostility to ordinary friendly NPCs");
        var otherPet = new UnitAvatar { NetworkLeader = guest };
        ai.CurrentTarget = otherPet;
        check(!ai.ShouldAttack, "New companion target exception covers players only");

        guest = new PlayerAvatar { Shield = 2 };
        guest.ApplyDamage(new() { origin = companion, damage = 10 });
        check(guest.Shield == 0 && guest.Hp == 97, "Companion shield overflow reaches HP with one scale application");
        guest.OnHit = (_, _) => owner.ApplyDamage(new() { origin = guest, damage = 10 });
        guest.ApplyDamage(new() { origin = companion, damage = 10 });
        check(owner.Hp == 100, "Companion attacks share recursive allied-retaliation protection");
        guest.OnHit = null;
        int notices = DungeonManager.Instance.Messages.Count;
        guest.ApplyDamage(new() { origin = companion, damage = 1000 });
        check(guest.IsDead && DungeonManager.Instance.Messages.Count == notices + 1 &&
            DungeonManager.Instance.Messages[^1].Contains("Owner"), "Companion kills credit their player owner through stock chat");

        foreach (bool searched in new[] { true, false })
        foreach (string transition in new[] { "off", "zero", "owner" })
        {
            guest = new PlayerAvatar(); companion.NetworkLeader = owner;
            SessionSettings.FriendlyFireForHit = new(true, 50);
            var archer = new ArcherFixture { Avatar = companion };
            if (searched) archer.SearchForFixture(guest);
            else archer.SetTarget(guest); // Native OnDamaged can set a target before any search.
            archer.Tick();
            check(archer.TriggerHeld, "Archer begins native held attack before " + transition);
            if (transition == "off") SessionSettings.FriendlyFireForHit = default;
            else if (transition == "zero") SessionSettings.FriendlyFireForHit = new(true, 0);
            else companion.NetworkLeader = guest;
            archer.Tick();
            check(!archer.TriggerHeld && archer.CurrentTarget == null && archer.LostTargets == 1,
                "Hostility removal runs native target-loss cleanup exactly once: " + transition);
            archer.SearchForFixture(companion.NetworkLeader);
            archer.Tick(); archer.Tick();
            check(archer.CurrentTarget == companion.NetworkLeader && archer.LostTargets == 1,
                "Ordinary friendly follow targets are not repeatedly cleared: " + transition);
        }
        companion.NetworkLeader = owner;
        var enemyArcher = new ArcherFixture { Avatar = companion };
        SessionSettings.FriendlyFireForHit = new(true, 50);
        enemyArcher.SearchForFixture(enemy); enemyArcher.Tick();
        SessionSettings.FriendlyFireForHit = default;
        enemyArcher.Tick();
        check(enemyArcher.TriggerHeld && enemyArcher.CurrentTarget == enemy && enemyArcher.LostTargets == 0,
            "Turning off does not cancel native attacks against monsters");
    }
}
