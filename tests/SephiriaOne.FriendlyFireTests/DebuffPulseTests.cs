using SephiriaOne;
using UnityEngine;

public sealed class CharacterDebuff_Freeze : CharacterDebuff
{
    // Native freeze starts its stun after ApplyDamage, regardless of result.
    public void ApplyDamage()
    {
        NetworkTarget.ApplyDamage(EffectTests.Tick(NetworkAttacker, "Debuff_Freeze", 2));
        NetworkTarget.StartStun(2);
    }
}

internal static class DebuffPulseTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, victim = null;
        void Fresh()
        {
            FriendlyFireParticipants.Clear(); Time.time = 100;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            source = new(); victim = new();
        }
        void Hit(string id, float amount = 2) => victim.ApplyDamage(EffectTests.Tick(source, id, amount));
        foreach (string id in new[] { "Debuff_Electric", "Debuff_Freeze" })
        {
            Fresh(); Hit(id); Time.time = 100.249f; Hit(id);
            check(victim.Hp == 98, id + " cannot damage the same player twice within 0.25 seconds");
            Time.time = 100.25f; Hit(id); check(victim.Hp == 96, id + " permits the exact quarter-second interval boundary");
            Hit(id); check(victim.Hp == 96, id + " does not allow another pulse on the same frame at the boundary");
            victim.ApplyDamage(new() { origin = source, damage = 2, fromType = EDamageFromType.Magic });
            check(victim.Hp == 94, "Direct Bolt/magic damage does not share the debuff pulse cooldown");
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: false); Hit(id); Hit(id);
            check(victim.Hp == 90, id + " restores all pulses immediately when balancing is disabled");
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); Hit(id);
            check(victim.Hp == 88, id + " reenable discards old pulse timing");
            FriendlyFireKda.ResetScores(); Hit(id); check(victim.Hp == 86, "KDA reset retires pulse timing");
            FriendlyFireParticipants.Select(source, victim); Hit(id); check(victim.Hp == 84, "Selection/match epoch resets pulse timing");
            victim.currentFloorGuid = "next"; Hit(id); check(victim.Hp == 82, "Floor transition resets pulse timing");
            FriendlyFireParticipants.Select(); // Default-all; reconnect correctly clears explicit selection.
            FriendlyFireParticipants.Depart(source); Hit(id); check(victim.Hp == 82, "Departed source cannot emit new damage");
            FriendlyFireParticipants.Rejoin(source); Hit(id); check(victim.Hp == 80, "Rejoining source gets a fresh pulse lifetime");
            FriendlyFireParticipants.Depart(victim); FriendlyFireParticipants.Rejoin(victim); Hit(id);
            check(victim.Hp == 78, "Rejoining victim gets a fresh pulse lifetime");
            victim.IsDead = true; victim.Revive(100); Hit(id); check(victim.Hp == 98, "Revival clears old pulse cooldown");
            Time.time = 0; Hit(id); check(victim.Hp == 96, "Clock restart cannot strand pulse timing");
        }
        Fresh(); Hit("Debuff_Electric"); Hit("Debuff_Freeze");
        check(victim.Hp == 96, "Shock and Freeze have independent cadence within the shared damage budget");
        victim.ApplyDamage(EffectTests.Tick(new UnitAvatar { NetworkLeader = source }, "Debuff_Electric", 2));
        check(victim.Hp == 96, "Companions and Plasma-style shock share the owner's same shock cooldown");
        victim.ApplyDamage(EffectTests.Tick(new PlayerAvatar(), "Debuff_Electric", 2));
        check(victim.Hp == 94, "Other attackers retain independent pulses");
        var other = new PlayerAvatar(); other.ApplyDamage(EffectTests.Tick(source, "Debuff_Electric", 2));
        check(other.Hp == 98, "Other victims retain independent pulses");
        Fresh(); Hit("Debuff_Electric", 0); Hit("Debuff_Electric");
        check(victim.Hp == 98, "Initial zero-charge shock cannot consume the first nonzero pulse");
        Fresh(); victim.ApplyDamage(new() { origin = source, damage = float.MaxValue }); Hit("Debuff_Electric");
        Time.time += 0.1f; Hit("Debuff_Electric");
        check(victim.Hp == 78, "Budget-exhausted shock does not reserve a future pulse interval");
        Fresh(); victim.IsMpShield = true; SessionSettings.FriendlyFireForHit = new(true, 25, balance: true);
        Hit("Debuff_Electric", 2); Hit("Debuff_Electric", 8);
        check(victim.Mp == 100, "A pulse rounded away by native MP shields still consumes its cadence interval");
        Time.time += 0.25f; Hit("Debuff_Electric", 8);
        check(victim.Mp == 98, "MP rounding refunds damage tokens without stranding the next timed pulse");
        Fresh(); Hit("Debuff_Electric");
        var freeze = new CharacterDebuff_Freeze(); freeze.InitializeAndSpawn(source, victim); freeze.ApplyDamage();
        check(victim.StunRemaining == 2, "Freeze still immobilizes a player for its native positive duration");
        victim.StunRemaining = 0; freeze.ApplyDamage();
        check(victim.Hp == 96 && victim.StunRemaining == 2, "Suppressing repeated Freeze damage does not skip or shorten its stun");
        var enemy = new UnitAvatar { faction = "enemy" };
        enemy.ApplyDamage(EffectTests.Tick(source, "Debuff_Electric", 2)); enemy.ApplyDamage(EffectTests.Tick(source, "Debuff_Electric", 2));
        check(enemy.Hp == 96, "Repeated outgoing PvE shock remains native");
        Fresh(); victim.ApplyDamage(EffectTests.Tick(enemy, "Debuff_Freeze", 2)); victim.ApplyDamage(EffectTests.Tick(enemy, "Debuff_Freeze", 2));
        check(victim.Hp == 96, "Repeated incoming PvE freeze remains native");
        Fresh(); var hit = EffectTests.Tick(source, "Debuff_Electric", 2);
        for (int i = 0; i < 1000; i++) { Time.time++; victim.ApplyDamage(hit); victim.Hp = 100; }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { Time.time++; victim.ApplyDamage(hit); victim.Hp = 100; }
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed pulse limiting adds no per-hit allocation");
        SessionSettings.FriendlyFireForHit = default;
    }
}
