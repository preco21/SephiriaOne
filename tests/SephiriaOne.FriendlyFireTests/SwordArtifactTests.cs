using SephiriaOne;

internal static class SwordArtifactTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        var source = new PlayerAvatar(); var target = new PlayerAvatar();
        var charm = new Charm_IceSword { NetworkAvatar = source };
        var sword = new IceSword(charm) { Target = target };
        var combo = new ComboEffect_FlameSword { NetworkAvatar = source, Target = target };
        SessionSettings.FriendlyFireForHit = new(true, 50);
        sword.Update();
        check(target.Hp == 90 && sword.HitEffects == 1, "Plitvice orbiting sword reaches hostile player damage, scales once and keeps successful-hit FX");
        check(combo.SearchTarget(), "Flame Sword auto-search includes hostile players");
        sword.Now = 0.5f; sword.Update();
        check(target.Hp == 90, "Plitvice native per-target cooldown remains intact");
        sword.Now = 0.6f; sword.Update();
        check(target.Hp == 80 && sword.HitEffects == 2, "Plitvice can damage the same target again after its native interval");

        void Blocked(string reason)
        {
            float hp = target.Hp; int effects = sword.HitEffects;
            sword.Now += 1; sword.Update();
            check(target.Hp == hp && sword.HitEffects == effects && !combo.SearchTarget(), reason);
        }
        SessionSettings.FriendlyFireForHit = default; Blocked("Existing swords and combo stop for FF off");
        SessionSettings.FriendlyFireForHit = new(true, 0); Blocked("Existing swords and combo stop for zero scale");
        SessionSettings.FriendlyFireForHit = new(true, 50);
        FriendlyFireParticipants.Select(source); Blocked("Secondary swords exclude unselected players");
        FriendlyFireParticipants.Select(source, target);
        CombatMatchRuntime.PairRule = (_, _) => false; Blocked("Secondary swords respect match countdown/opponent restrictions");
        CombatMatchRuntime.PairRule = null;
        FriendlyFireParticipants.Depart(target); Blocked("Secondary swords reject retired target connections");
        FriendlyFireParticipants.Rejoin(target); Blocked("Rejoined targets do not inherit a prior selection");
        FriendlyFireParticipants.Select(source, target);
        sword.Now += 1; sword.Update();
        check(target.Hp == 70 && combo.SearchTarget(), "Existing swords resume for freshly selected participants");
        FriendlyFireParticipants.Depart(source); Blocked("Secondary swords reject a retired caster");
        FriendlyFireParticipants.Clear();
        Mirror.NetworkServer.active = false; sword.IsServerObject = false;
        Blocked("Guest copies retain native targeting and cannot apply sword damage");
        Mirror.NetworkServer.active = true; sword.IsServerObject = true;
        sword.Target = source; combo.Target = source;
        sword.Now += 1; sword.Update();
        check(source.Hp == 100 && !combo.SearchTarget(), "Secondary swords never admit their own caster");

        sword.Target = target; combo.Target = target; sword.Overlapping = false; sword.Now += 1;
        sword.Update(); check(target.Hp == 70, "Plitvice preserves native collision overlap requirement");
        sword.Overlapping = true; sword.EffectEnabled = false; sword.Update();
        check(target.Hp == 70, "Disabled artifact does not deal damage");
        sword.EffectEnabled = true; source.IsDead = true; sword.Update();
        check(target.Hp == 70, "Dead caster's swords do not deal damage"); source.IsDead = false;
        target.transform.position = new(5.01f, 0);
        check(!combo.SearchTarget(), "Flame Sword retains its native search radius");
        target.transform.position = new(5, 0);
        check(combo.SearchTarget(), "Flame Sword retains inclusive search boundary");
        target.IsDead = true; check(!combo.SearchTarget(), "Flame Sword excludes dead candidates"); target.IsDead = false;
        target.canBeTarget.Value = false; check(!combo.SearchTarget(), "Flame Sword excludes untargetable candidates"); target.canBeTarget.Value = true;
        target.gameObject.activeSelf = false; check(!combo.SearchTarget(), "Flame Sword excludes inactive candidates"); target.gameObject.activeSelf = true;

        var enemy = new UnitAvatar { faction = "enemy" };
        sword.Target = enemy; combo.Target = enemy; SessionSettings.FriendlyFireForHit = default;
        sword.Now += 1; sword.Update();
        check(enemy.Hp == 80 && combo.SearchTarget(), "Secondary swords keep native PvE targeting and damage while FF is off");
        FriendlyFireParticipants.Clear();
    }
}
