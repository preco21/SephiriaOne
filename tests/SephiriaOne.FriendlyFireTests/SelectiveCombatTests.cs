using SephiriaOne;

internal static class SelectiveCombatTests
{
    private static DamageInstance Hit(UnitAvatar source, float amount = 20) => new() { origin = source, damage = amount, targetFactionLayers = -1 };

    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var source = new PlayerAvatar(); var target = new PlayerAvatar(); var excluded = new PlayerAvatar();
        FriendlyFireParticipants.Select(source);
        target.IsGuarding = true;
        check(target.ApplyDamage(Hit(source)) == EApplyDamageResult.Fail_Absolute && target.Hp == 100 && target.Mp == 100 && target.GuardHits == 0,
            "Selective combat rejects an unselected target before native guard costs");
        target.IsGuarding = false; FriendlyFireParticipants.Select(source, target);
        target.ApplyDamage(Hit(source));
        check(target.Hp == 90, "Selected pair retains fractional allied damage scaling");
        excluded.ApplyDamage(Hit(source)); source.ApplyDamage(Hit(excluded));
        check(source.Hp == 100 && excluded.Hp == 100, "Both directions require selected participants");
        FriendlyFireParticipants.Select(); excluded.ApplyDamage(Hit(source));
        check(excluded.Hp == 90, "Empty explicit selection restores all current players");
        Consumers(check);
        Callbacks(check);
        Lifetimes(check);
        FriendlyFireParticipants.Clear();
    }

    private static void Consumers(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        var source = new PlayerAvatar(); var target = new PlayerAvatar(); var excluded = new PlayerAvatar();
        var companion = new UnitAvatar { NetworkLeader = source };
        var ai = new UnitAI_NewBasic { Avatar = companion, CurrentTarget = target };
        FriendlyFireParticipants.Select(source);
        check(!ai.ShouldAttack, "Companion AI excludes an unselected target");
        FriendlyFireParticipants.Select(source, target);
        check(ai.ShouldAttack, "Companion AI admits a selected pair");
        companion.ApplyDamage(Hit(excluded)); target.ApplyDamage(Hit(companion));
        check(companion.Hp == 100 && target.Hp == 90, "Companion victims and sources use their owner selection");
        var npc = new UnitAvatar { faction = "friends" };
        npc.ApplyDamage(Hit(excluded));
        check(npc.Hp == 100, "Unselected source cannot use the addon allied-NPC exception");
        PlayerSpawner.MultiplayerList.Clear(); PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = excluded });
        check(!FriendlyFireRuntime.ArtifactInBattle(source), "Excluded nearby player cannot activate an offensive artifact");
        PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = target });
        check(FriendlyFireRuntime.ArtifactInBattle(source), "Selected nearby player activates an offensive artifact");
        FriendlyFireParticipants.Select(target);
        check(!FriendlyFireRuntime.ArtifactInBattle(source), "Unselected source cannot gain artifact battle activation");
        source.IsInBattle = true;
        check(FriendlyFireRuntime.ArtifactInBattle(source), "Native monster battle activation is unchanged by selection");
        source.IsInBattle = false;
        FriendlyFireParticipants.Select(source, target);
        var bullet = new Bullet { NetworkOwner = source, Candidate = target, damageId = "Charm_IceBow" };
        bullet.Update(); check(bullet.HomingTarget == target, "Selected homing target is acquired");
        FriendlyFireParticipants.Select(source); bullet.Update();
        check(bullet.HomingTarget == null, "Changing selection releases the active homing target");
        target.ApplyDebuff(new(), source);
        check(target.DebuffApplications == 0 && !FriendlyFireRuntime.ItemTarget(target, EDamageFromType.None, source) &&
            FriendlyFireRuntime.ItemTargetMask(target, EDamageFromType.None, -1, source) == 0,
            "Excluded pair cannot apply debuffs or pass audited target masks");
        PlayerSpawner.MultiplayerList.Clear();
    }

    private static void Callbacks(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear(); SessionSettings.FriendlyFireForHit = new(true, 50);
        foreach (bool viaCompanion in new[] { false, true })
        {
            var beforeSource = new PlayerAvatar(); var guarded = new PlayerAvatar { IsGuarding = true };
            var pet = new UnitAvatar { NetworkLeader = beforeSource };
            FriendlyFireParticipants.Select(beforeSource, guarded);
            beforeSource.OnAttackUnitBeforeOperation += (_, _) => FriendlyFireParticipants.Select(beforeSource);
            beforeSource.OnAttackUnitBeforeOperation_AsLeader += (_, _, _) => FriendlyFireParticipants.Select(beforeSource);
            check(guarded.ApplyDamage(Hit(viaCompanion ? pet : beforeSource)) == EApplyDamageResult.Fail_Absolute && guarded.Mp == 100 && guarded.GuardHits == 0,
                "Before-attack callback selection change cancels the native guard branch, companion=" + viaCompanion);
        }
        var source = new PlayerAvatar(); var target = new PlayerAvatar { Shield = 10, IsMpShield = true };
        FriendlyFireParticipants.Select(source, target);
        target.OnCalculateDamage = _ => FriendlyFireParticipants.Select(source);
        target.ApplyDamage(Hit(source));
        check(target.Hp == 100 && target.Shield == 10 && target.Mp == 100,
            "Selection changed by damage callbacks blocks later HP and shield writes");
        target = new PlayerAvatar(); var third = new PlayerAvatar();
        FriendlyFireParticipants.Select(source, target, third);
        target.OnHit = (_, _) =>
        {
            FriendlyFireParticipants.Select(source);
            third.ApplyDamage(Hit(source));
            source.ApplyDamage(new() { origin = target, damage = 20, id = "Ability_Thorns" });
        };
        target.ApplyDamage(Hit(source));
        check(target.Hp == 90 && third.Hp == 100 && source.Hp == 100,
            "On-hit selection change preserves delivered damage but blocks nested procs and reflection");
        var spawner = new PlayerSpawner(); target = new PlayerAvatar(); spawner.BindDeath(target);
        FriendlyFireParticipants.Select(source, target);
        target.OnHit = (_, _) => FriendlyFireParticipants.Select(source);
        int notices = DungeonManager.Instance.Messages.Count;
        target.ApplyDamage(Hit(source, 1000));
        check(target.IsDead && spawner.GameOvers == 0 && DungeonManager.Instance.Messages.Count == notices &&
            FriendlyFireKda.For(source).Kills == 0, "Lethal hit remains recoverable but loses attribution after on-hit selection change");
        target = new PlayerAvatar(); FriendlyFireParticipants.Select(source, target);
        target.OnDeath = _ => FriendlyFireParticipants.Select(source);
        notices = DungeonManager.Instance.Messages.Count; target.ApplyDamage(Hit(source, 1000));
        check(FriendlyFireKda.For(source).Kills == 0 && DungeonManager.Instance.Messages.Count == notices,
            "Selection changed during death invalidates an already captured KDA receipt and notice");
        target = new PlayerAvatar(); FriendlyFireParticipants.Select(source, target);
        target.OnHit = (_, _) =>
        {
            FriendlyFireParticipants.Select();
            source.ApplyDamage(new() { origin = target, damage = 20, id = "Ability_Thorns" });
        };
        target.ApplyDamage(Hit(source));
        check(source.Hp == 100, "Empty-selection fallback cannot reauthorize reflection from an invalidated parent hit");
        target = new PlayerAvatar(); FriendlyFireParticipants.Select();
        target.OnDeath = _ => FriendlyFireParticipants.Capture(source).Current = false;
        notices = DungeonManager.Instance.Messages.Count; target.ApplyDamage(Hit(source, 1000));
        check(FriendlyFireKda.For(source).Kills == 0 && DungeonManager.Instance.Messages.Count == notices,
            "Death finalizer validates captured current lifetimes even before an epoch notification");
    }

    private static void Lifetimes(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear(); SessionSettings.FriendlyFireForHit = new(true, 50);
        var source = new PlayerAvatar(); var target = new PlayerAvatar();
        FriendlyFireParticipants.Select(source); FriendlyFireParticipants.Depart(source);
        target.ApplyDamage(Hit(source));
        check(target.Hp == 100, "Last-selected departure fallback does not admit a stale living source");
        source.OnAttackUnitBeforeOperation = null; source.Destroyed = true; target.ApplyDamage(Hit(source));
        check(target.Hp == 100, "Unity fake-null player source cannot fall through to native all-faction damage");
        FriendlyFireParticipants.Clear(); source = new PlayerAvatar(); target = new PlayerAvatar();
        var companion = new UnitAvatar { NetworkLeader = source };
        FriendlyFireParticipants.Select(source, target);
        companion.Die(1, Hit(target));
        target.ApplyDamage(Hit(companion));
        check(target.Hp == 90, "Dead companion projectile retains its owner and allied damage scale");
        FriendlyFireParticipants.Select(target); target.ApplyDamage(Hit(companion));
        check(target.Hp == 90, "Dead companion projectile remains subject to owner selection");
        FriendlyFireParticipants.Clear(); source = new PlayerAvatar(); target = new PlayerAvatar();
        var effect = new CharacterDebuff(); target.ApplyDebuff(effect, source);
        effect.OnTick = () => target.ApplyDamage(EffectTests.Tick(source)); effect.OnExpire = effect.OnTick;
        FriendlyFireParticipants.Depart(source); effect.Update();
        check(effect.IsEndBuff && target.Hp == 100, "Lingering effect expires safely when its living owner connection departs");
        FriendlyFireParticipants.Clear(); source = new PlayerAvatar(); target = new PlayerAvatar();
        companion = new UnitAvatar { NetworkLeader = target };
        effect = new CharacterDebuff(); companion.ApplyDebuff(effect, source);
        effect.OnTick = () => companion.ApplyDamage(EffectTests.Tick(source)); effect.OnExpire = effect.OnTick;
        companion.NetworkLeader = new PlayerAvatar(); effect.Update();
        check(effect.IsEndBuff && companion.Hp == 100, "Debuff target ownership transfer cannot borrow a new player's selection");
        FriendlyFireParticipants.Clear(); source = new PlayerAvatar(); target = new PlayerAvatar();
        companion = new UnitAvatar { NetworkLeader = source };
        target.ApplyDamage(Hit(companion));
        effect = new CharacterDebuff(); target.ApplyDebuff(effect, companion);
        effect.OnTick = () => target.ApplyDamage(EffectTests.Tick(companion)); effect.OnExpire = effect.OnTick;
        FriendlyFireParticipants.Clear();
        target.ApplyDamage(Hit(companion));
        check(target.Hp == 80, "Living companion reads its live owner's new-run lifetime");
        effect.Update();
        check(effect.IsEndBuff && target.Hp == 80, "Old-run debuff cannot borrow the surviving companion's fresh owner receipt");
        foreach (bool owned in new[] { false, true })
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear(); SessionSettings.FriendlyFireForHit = new(true, 50);
            source = new PlayerAvatar(); target = new PlayerAvatar();
            UnitAvatar retiredSource = owned ? new UnitAvatar { NetworkLeader = source } : source;
            if (owned) retiredSource.Die(1, Hit(target));
            retiredSource.OnAttackUnitBeforeOperation = null; retiredSource.Destroyed = true;
            var monster = new UnitAvatar { faction = "enemy" };
            check(monster.ApplyDamage(Hit(retiredSource)) == EApplyDamageResult.Success && monster.Hp == 80,
                "Destroyed source's fired projectile retains native unscaled monster damage, companion=" + owned);
            check(target.ApplyDamage(Hit(retiredSource)) == EApplyDamageResult.Fail_Absolute && target.Hp == 100,
                "Destroyed source's fired projectile still rejects allied players, companion=" + owned);
        }
    }
}
