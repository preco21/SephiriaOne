using SephiriaOne;
using UnityEngine;

internal static class ThornsTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Toggle(string value)
        {
            check(FriendlyFireCommand.Parse("/one friendlyfire suppress-thorns " + value, out var command) == FriendlyFireParseResult.Valid,
                "Thorns suppression command is supported");
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(command);
        }
        DamageInstance Return(UnitAvatar source, string id = "Ability_Thorns") => new()
            { origin = source, id = id, fromType = EDamageFromType.None, damage = 10, targetFactionLayers = -1 };
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear(); Time.time = 100;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        Toggle("on");
        var attacker = new PlayerAvatar(); var defender = new PlayerAvatar();
        int callbacks = 0;
        attacker.OnHit = (_, _) => callbacks++;
        defender.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(Return(defender));
        defender.ApplyDamage(new() { origin = attacker, damage = 10 });
        check(attacker.Hp == 100 && callbacks == 0 && defender.Hp == 90, "Only the Thorns return is suppressed, before callbacks");
        attacker.IsGuarding = true;
        check(attacker.ApplyDamage(Return(defender)) == EApplyDamageResult.Fail_Absolute && attacker.Mp == 100 && attacker.GuardHits == 0,
            "Suppressed independent Thorns cannot spend guard MP");
        attacker.IsGuarding = false;
        attacker.ApplyDamage(Return(defender, "Weapon_Reflect"));
        attacker.ApplyDamage(Return(defender, "Charm_VenomSporePouch"));
        check(attacker.Hp == 80 && callbacks == 2, "Weapon reflection and Spore Pouch retain their own behavior");
        var enemy = new UnitAvatar { faction = "enemy" };
        enemy.ApplyDamage(Return(defender));
        attacker.ApplyDamage(Return(enemy));
        check(enemy.Hp == 90 && attacker.Hp == 70, "Outgoing and incoming PvE Thorns remain native");
        var companion = new UnitAvatar { NetworkLeader = defender };
        attacker.ApplyDamage(Return(companion));
        check(attacker.Hp == 70, "Player-owned source obeys the same player-victim policy");
        companion.ApplyDamage(Return(attacker));
        check(companion.Hp == 90, "The player-only suppression does not change Thorns against NPCs");
        Toggle("off"); attacker.ApplyDamage(Return(defender));
        check(attacker.Hp == 60, "Live off restores Thorns immediately");
        Toggle("on");
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, balance: false));
        attacker.ApplyDamage(Return(defender));
        check(attacker.Hp == 50, "Balance off restores Thorns with preference retained");
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, balance: true));
        Toggle("reset"); attacker.ApplyDamage(Return(defender));
        check(attacker.Hp == 40, "Reset restores off without disabling friendly fire");

        // An earlier native callback may change the live setting inside ApplyDamage.
        attacker.OnCalculateDamage = _ => Toggle("on");
        attacker.ApplyDamage(Return(defender));
        check(attacker.Hp == 40, "Late policy check stops an already-admitted return after live enable");
        attacker.OnCalculateDamage = null;
        Toggle("off");
        defender.OnHit = (_, d) => { Toggle("on"); ((UnitAvatar)d.origin).ApplyDamage(Return(defender)); };
        defender.ApplyDamage(new() { origin = attacker, damage = 10 });
        check(attacker.Hp == 40 && defender.Hp == 80, "Nested returns read current policy, not the outer-hit snapshot");
        var pooled = Return(defender);
        for (int i = 0; i < 20; i++) attacker.ApplyDamage(pooled);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) attacker.ApplyDamage(pooled);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        check(allocated == 0 && attacker.Hp == 40, "Suppressed Thorns path has no warmed per-hit allocation");
        Console.WriteLine($"Thorns suppression: {allocated} bytes/1000 warmed returns");
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = default;
    }
}
