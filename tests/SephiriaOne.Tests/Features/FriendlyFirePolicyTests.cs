using SephiriaOne;

internal static class FriendlyFirePolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(!policy.FriendlyFire.Enabled && policy.FriendlyFire.DamagePercent == 100 && !policy.HasChanges, "Combat defaults off and 100%");
        foreach (int percent in new[] { 0, 25, 100, 200, 300 })
        {
            policy.Record(new FriendlyFireCommand(true)); policy.Record(new FriendlyFireCommand(false,percent));
            Check(policy.ToPresetText().StartsWith("SephiriaOne preset v12\n"), "Combat changes require v12");
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(),out var restored,out _) && restored.FriendlyFire.Enabled &&
                restored.FriendlyFire.DamagePercent == percent, "Combat preset round trip: " + percent);
            policy.Record(new FriendlyFireCommand(false));
            Check(!policy.FriendlyFire.Enabled && policy.FriendlyFire.DamagePercent == percent, "Off retains chosen damage percentage");
        }
        foreach (string bad in new[] { "enabled 2", "damage -1", "damage 301", "damage 1.5", "damage NaN", "damage +5", "damage 025", "damage 25\nfriendlyfire damage 50", "enabled 1\nfriendlyfire enabled 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v12\nfriendlyfire " + bad + "\n", out var invalid,out _) && !invalid.HasChanges,
                "Invalid combat preset is atomic: " + bad);
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v11\nfriendlyfire enabled 1\n",out _,out _), "Old schema rejects new syntax");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v11\nitems unlock 1\n",out var old,out _) && !old.FriendlyFire.Enabled && old.FriendlyFire.DamagePercent == 100,
            "Old presets migrate with safe combat defaults");
        policy.Record(new FriendlyFireCommand(false,reset:true));
        Check(!policy.HasChanges && policy.FriendlyFire.DamagePercent == 100, "Combat reset restores defaults and old minimal schema");
        policy.Record(new FriendlyFireCommand(true)); policy.Clear();
        Check(!policy.FriendlyFire.Enabled, "Shared scope clear removes combat policy");
        return checks;
    }
}
