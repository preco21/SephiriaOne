using SephiriaOne;

internal static class ZombieSurvivalControlsTests
{
    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        start();
        check(SessionSettings.ZombieBlockHpPotions, "Zombie HP restriction defaults on");
        check(SettingsActions.Execute("/one zombie block-hp off").Success && !SessionSettings.ZombieBlockHpPotions,
            "Command disables Zombie HP restriction");
        check(SettingsActions.Execute("/one save").Success, "Zombie HP preference saves");
        string potionPreset = File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
        bool potionRead = SessionPolicy.TryReadPreset(potionPreset, out var potionPolicy, out _);
        check(potionPreset.StartsWith("SephiriaOne preset v54\n") &&
            potionRead && !potionPolicy.ZombieBlockHpPotions &&
            potionPolicy.ToPresetText() == potionPreset, "Zombie HP preference round-trips with v54 gating");
        potionPolicy.Clear();
        check(potionPolicy.ZombieBlockHpPotions && potionPolicy.ToPresetText() == new SessionPolicy().ToPresetText(), "Policy clear restores HP restriction");
        for (int version = 1; version <= 53; version++)
        {
            check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out var legacy, out _) && legacy.ZombieBlockHpPotions,
                "Legacy preset inherits HP restriction default: " + version);
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\nzombie block-hp 0\n", out _, out _),
                "Legacy header cannot smuggle HP restriction setting: " + version);
        }
        foreach (string value in new[] { "2", "-1", "0.0", "false", "0\nzombie block-hp 1" })
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v54\nzombie block-hp " + value + "\n", out _, out _),
                "Malformed/duplicate HP restriction row is rejected: " + value);
        check(!SettingsActions.Execute("/one zombie block-hp 1").Success, "HP restriction only accepts boolean commands");
        Mirror.NetworkServer.active = false;
        check(!SettingsActions.Execute("/one zombie block-hp on").Success && !SessionSettings.ZombieBlockHpPotions,
            "Guests cannot change host potion policy");
        Mirror.NetworkServer.active = true;
        check(SettingsActions.Execute("/one zombie block-hp reset").Success && SessionSettings.ZombieBlockHpPotions,
            "HP restriction reset restores default");
        check(SessionSettings.ZombieDuration == 40 && !SessionSettings.ZombieAnyDeath &&
            SessionSettings.ZombieModifiers.Equals(new CombatRoleSettings(true, 3m, true, 30m)), "Zombie defaults match confirmed settings");
        foreach (string op in new[] { "duration 37", "any-death on", "refill x3.25", "damage 45.5%", "refill off", "damage off" })
            check(SettingsActions.Execute("/one zombie " + op).Success, "Zombie command accepts " + op);
        check(!SessionSettings.ZombieModifiers.RefillEnabled && SessionSettings.ZombieModifiers.RefillMultiplier == 3.25m &&
            !SessionSettings.ZombieModifiers.IncomingEnabled && SessionSettings.ZombieModifiers.IncomingPercent == 45.5m, "Independent disabled options retain values");
        check(SettingsActions.Execute("/one save").Success, "Zombie settings save through shared presets");
        string text = File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
        check(text.StartsWith("SephiriaOne preset v31\n") && SessionPolicy.TryReadPreset(text, out var policy, out _) &&
            policy.ToPresetText() == text && policy.ZombieAnyDeath && policy.ZombieDuration == 37 &&
            policy.ZombieModifiers.Equals(SessionSettings.ZombieModifiers), "Zombie settings round-trip without runtime team state");
        foreach (string op in new[] { "duration reset", "any-death reset", "refill reset", "damage reset" })
            check(SettingsActions.Execute("/ONE ZOMBIE " + op).Success, "Zombie reset accepts " + op);
        check(!SessionSettings.ZombieAnyDeath && SessionSettings.ZombieDuration == 40 &&
            SessionSettings.ZombieModifiers.Equals(new CombatRoleSettings(true, 3m, true, 30m)), "Resets restore defaults");
        foreach (string op in new[] { "duration 2", "duration 3601", "duration 1.5", "duration -3", "duration +3", "duration x3",
            "any-death 1", "refill 0", "refill 10.01", "refill 1.001", "refill 2.000", "refill +2", "refill NaN",
            "damage -1", "damage 100.01", "damage x2", "damage 75.000", "damage 75%%", "start extra", "stop extra", "status extra", "unknown" })
            check(!SettingsActions.Execute("/one zombie " + op).Success, "Zombie rejects malformed input: " + op);
        Mirror.NetworkServer.active = false;
        check(!SettingsActions.Execute("/one zombie any-death on").Success && !SessionSettings.ZombieAnyDeath &&
            SettingsActions.Execute("/one zombie help").Success, "Guests can read help but cannot edit Zombie policy");
        Mirror.NetworkServer.active = true;
        for (int i = 1; i <= 30; i++)
        {
            check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + i + "\n", out var old, out _) && old.ZombieDuration == 40 &&
                !old.ZombieAnyDeath && old.ZombieModifiers.Equals(new CombatRoleSettings(true, 3m, true, 30m)), "Older preset defaults Zombie policy: v" + i);
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + i + "\nzombie any-death 1\n", out _, out _), "Older preset cannot smuggle Zombie command: v" + i);
        }
        check(SessionPolicy.TryReadPreset("SephiriaOne preset v31\nzombie refill 2\nzombie damage 75\n", out var explicitOld, out _) &&
            explicitOld.ZombieModifiers.Equals(new CombatRoleSettings(true, 2m, true, 75m)) &&
            explicitOld.ToPresetText().Contains("zombie refill 2\n") && explicitOld.ToPresetText().Contains("zombie damage 75\n"),
            "Explicit saved modifiers remain custom values instead of becoming new defaults");
        check(SessionPolicy.TryReadPreset("SephiriaOne preset v31\nzombie damage 50\n", out var explicitPrevious, out _) &&
            explicitPrevious.ZombieModifiers.Equals(new CombatRoleSettings(true, 3m, true, 50m)) &&
            explicitPrevious.ToPresetText().Contains("zombie damage 50\n"), "Explicit previous 50-percent default remains a saved override");
        check(SessionPolicy.TryReadPreset("SephiriaOne preset v31\nzombie refill 4\n", out var omittedDamage, out _) &&
            omittedDamage.ZombieModifiers.Equals(new CombatRoleSettings(true, 4m, true, 30m)) &&
            !omittedDamage.ToPresetText().Contains("zombie damage "), "Omitted incoming damage inherits 30 percent without creating an override");
        check(new SessionPolicy().ToPresetText() == "SephiriaOne preset v1\n", "New Zombie defaults do not create preset overrides");
        check(SessionSettings.PlayTagChaser.Equals(new CombatRoleSettings(true, 2m, true, 75m)), "Zombie tuning preserves independent Tag defaults");
        foreach (string entry in new[] { "duration 2", "duration 3.0", "duration 3601", "any-death 2", "any-death 1.0", "refill 0", "damage 100.1",
            "refill-enabled -1", "damage-enabled 3", "duration 10\nzombie duration 20", "carrier 1", "role 1", "remaining 15" })
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v31\nzombie " + entry + "\n", out _, out _), "Zombie preset rejects invalid/runtime state: " + entry);
        var p = new SessionPolicy(); p.RecordZombieAnyDeath(true); p.Clear();
        check(!p.HasChanges && p.ToPresetText() == new SessionPolicy().ToPresetText(), "Policy clear resets all Zombie settings and preserves oldest header");
        start();
    }
}
