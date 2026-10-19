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
        foreach (string id in new[] { "Debuff_Burn", "Debuff_Plasma", "Debuff_Frostbite", "Debuff_Poison", "Debuff_Wound" })
        {
            Fresh(); Hit(id); Time.time += 0.01f; Hit(id);
            check(victim.Hp == 98, id + " keeps the first hit but blocks reapplication inside its own interval");
        }
        foreach (string id in new[] { "Debuff_Electric", "Debuff_Freeze", "Debuff_Chim" })
        {
            Fresh(); float interval = id == "Debuff_Electric" ? 0.5f : 0.25f;
            Hit(id); Time.time = 100 + interval - 0.001f; Hit(id);
            check(victim.Hp == 98, id + " cannot damage the same player twice within its own default interval");
            Time.time = 100 + interval; Hit(id); check(victim.Hp == 96, id + " permits the exact default interval boundary");
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
        check(victim.Hp == 96, "Companions share their owner's Lightning cooldown");
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
        check(victim.Mp == 99, "A minimum pulse damages MP shields and still consumes its cadence interval");
        Time.time += 0.5f; Hit("Debuff_Electric", 8);
        check(victim.Mp == 97, "The next timed pulse still receives its full scaled damage");
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
        foreach (var (kind, id) in new[] { (PvpDotEffect.Lightning, "Debuff_Electric"),
            (PvpDotEffect.Freeze, "Debuff_Freeze"), (PvpDotEffect.Chim, "Debuff_Chim") })
        {
            Fresh(); Hit(id);
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(kind, 1)));
            Time.time += 0.5f; Hit(id);
            check(victim.Hp == 98, kind + " live lengthening reads the last admitted pulse, not a reset clock");
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(kind, 0.5m)));
            Hit(id); check(victim.Hp == 96, kind + " live shortening admits the elapsed boundary immediately");
            foreach (var sibling in Enum.GetValues<PvpDotEffect>())
                if (sibling != kind) SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(sibling, 3)));
            Time.time += 0.499f; Hit(id); check(victim.Hp == 96, kind + " sibling edits neither reset nor replace its interval");
            Time.time = 101; Hit(id); check(victim.Hp == 94, kind + " rejected attempts never postpone its next pulse");
            var direct = EffectTests.Tick(source, id, 2); direct.fromType = EDamageFromType.Magic;
            victim.ApplyDamage(direct); victim.ApplyDamage(direct);
            check(victim.Hp == 90, kind + " similarly named direct magic never borrows a debuff cooldown");
            Fresh(); Hit(id, 0); Hit(id); check(victim.Hp == 98, kind + " zero damage cannot reserve an interval");
            Fresh(); victim.ApplyDamage(new() { origin = source, damage = float.MaxValue }); Hit(id);
            Time.time += 0.1f; Hit(id); check(victim.Hp == 78, kind + " exhausted damage budget cannot reserve an interval");
        }
        Fresh();
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(PvpDotEffect.Lightning, 1)));
        Hit("Debuff_Electric");
        var plasma = new CharacterDebuff_Plasma(); plasma.InitializeAndSpawn(source, victim);
        plasma.OnRefreshAttack = () => Hit("Debuff_Electric");
        plasma.OnExpire = () => Hit("Debuff_Electric");
        Time.time += 0.5f; plasma.RefreshDurationTimer(1, 0);
        check(victim.Hp == 98, "Plasma refresh and normal Lightning share the configured shock interval");
        Time.time += 0.5f; plasma.Destroy();
        check(victim.Hp == 96, "Plasma expiry uses the same interval without delaying its native destruction");
        Fresh();
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(PvpDotEffect.Lightning, 1)));
        void PlasmaShock()
        {
            var damage = EffectTests.Tick(source, "Debuff_Plasma", 2); damage.flag = "DEBUFF_ELECTRIC";
            victim.ApplyDamage(damage);
        }
        plasma = new CharacterDebuff_Plasma(); plasma.InitializeAndSpawn(source, victim);
        plasma.OnRefreshAttack = PlasmaShock; plasma.OnExpire = PlasmaShock;
        Hit("Debuff_Electric"); PlasmaShock(); PlasmaShock();
        check(victim.Hp == 96, "Serialized Plasma shock has its own first hit and repeat gate, separate from Lightning");
        Time.time += 0.5f; plasma.RefreshDurationTimer(1, 0);
        check(victim.Hp == 96, "Plasma shock refresh waits for its 0.9-second default");
        Hit("Debuff_Plasma"); Hit("Debuff_Plasma");
        check(victim.Hp == 94, "Plasma burn has its own schedule, independent of both shock clocks");
        Time.time = 100.9f; plasma.Destroy();
        check(victim.Hp == 92, "Serialized Plasma expiry shock resumes at its own default boundary");
        Hit("Debuff_Electric"); check(victim.Hp == 92, "Plasma shock does not shorten Lightning's configured 1-second interval");
        Time.time = 101; Hit("Debuff_Electric"); check(victim.Hp == 90, "Lightning retains its independent boundary");
        var spell = EffectTests.Tick(source, "Debuff_Plasma", 2); spell.flag = "DEBUFF_ELECTRIC"; spell.fromType = EDamageFromType.Magic;
        victim.ApplyDamage(spell); victim.ApplyDamage(spell);
        check(victim.Hp == 86, "Shock-like metadata on direct magic still cannot borrow the debuff pulse clock");
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, balance: false));
        PlasmaShock(); PlasmaShock(); check(victim.Hp == 82, "Disabling balancing immediately restores serialized Plasma shock damage");
        Fresh(); Hit("Debuff_Chim");
        var chim = new CharacterDebuff { OnExpire = () => Hit("Debuff_Chim") };
        chim.InitializeAndSpawn(source, victim); Time.time += 0.1f; chim.Destroy();
        check(victim.Hp == 98, "Chim expiry damage obeys its interval without periodic conversion");
        Fresh();
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(PvpDotEffect.Freeze, 5)));
        freeze = new CharacterDebuff_Freeze(); freeze.InitializeAndSpawn(source, victim); freeze.ApplyDamage();
        Time.time += 1; victim.StunRemaining = 0; freeze.ApplyDamage();
        check(victim.Hp == 98 && victim.StunRemaining == 2, "A long custom Freeze damage interval preserves the native stun refresh");
        Fresh(); var hit = EffectTests.Tick(source, "Debuff_Electric", 2);
        for (int i = 0; i < 1000; i++) { Time.time++; victim.ApplyDamage(hit); victim.Hp = 100; }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { Time.time++; victim.ApplyDamage(hit); victim.Hp = 100; }
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed pulse limiting adds no per-hit allocation");
        SessionSettings.FriendlyFireForHit = default;
    }
}
