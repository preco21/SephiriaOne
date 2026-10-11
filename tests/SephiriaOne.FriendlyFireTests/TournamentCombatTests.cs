using SephiriaOne;

internal static class TournamentCombatTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var a = new PlayerAvatar(); var b = new PlayerAvatar(); var waiting = new PlayerAvatar();
        CombatMatchRuntime.PairRule = (source, target) => source == a && target == b || source == b && target == a;
        DamageInstance Hit(UnitAvatar source, string id = "") => new() { origin = source, damage = 20, targetFactionLayers = -1, id = id };
        waiting.IsGuarding = true;
        check(waiting.ApplyDamage(Hit(a)) == EApplyDamageResult.Fail_Absolute && waiting.Mp == 100 && waiting.GuardHits == 0,
            "Tournament spectator blocks broad-mask player damage before guard/MP costs");
        check(a.ApplyDamage(Hit(waiting)) == EApplyDamageResult.Fail_Absolute, "Spectator cannot attack a fighter");
        b.ApplyDamage(Hit(a)); check(b.Hp == 90, "Active pair uses shared damage scale");
        var ally = new UnitAvatar { NetworkLeader = a };
        check(new UnitAI_NewBasic { Avatar = ally, CurrentTarget = b }.ShouldAttack &&
            !new UnitAI_NewBasic { Avatar = ally, CurrentTarget = waiting }.ShouldAttack,
            "Companions acquire only the opposing fighter");
        b.ApplyDamage(Hit(ally)); waiting.ApplyDamage(Hit(ally));
        check(b.Hp == 80 && waiting.Hp == 100, "Companion impacts preserve active-pair ownership");
        var burn = new CharacterDebuff();
        waiting.ApplyDebuff(burn, a);
        check(burn.NetworkTarget == null, "Debuff application cannot bypass tournament spectator protection");
        check(FriendlyFireRuntime.ItemTarget(b, EDamageFromType.Magic, a) &&
            !FriendlyFireRuntime.ItemTarget(waiting, EDamageFromType.Magic, a), "Artifact targeting uses the same active pair");
        b.OnHit = (_, _) => a.ApplyDamage(Hit(b, "Ability_Thorns"));
        b.ApplyDamage(Hit(a));
        check(a.Hp == 90 && b.Hp == 70, "Reflected damage applies the shared scale back to the opposing fighter");
        b.OnHit = null;
        a.OnAttackUnitBeforeOperation += (_, _) => CombatMatchRuntime.PairRule = (_, _) => false;
        b.IsGuarding = true; b.ApplyDamage(Hit(a));
        check(b.Mp == 100 && b.Hp == 70, "A round ending inside an attack callback revokes the hit before guard and HP writes");
        CombatMatchRuntime.KeepScores = true;
        var score = FriendlyFireKda.For(a); score.Kills = 2;
        // Model the effective countdown policy without using the fixture setter,
        // which intentionally simulates a manual normal-mode toggle.
        typeof(SessionSettings).GetField("settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .SetValue(null, new FriendlyFireSettings(false, 50));
        new UnitAvatar { faction = "enemy" }.ApplyDamage(Hit(a));
        check(ReferenceEquals(score, FriendlyFireKda.For(a)) && score.Kills == 2,
            "Normal enemy attacks during a round countdown cannot erase tournament KDA");
        CombatMatchRuntime.PairRule = null; CombatMatchRuntime.KeepScores = false;
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear(); SessionSettings.FriendlyFireForHit = default;
    }
}
