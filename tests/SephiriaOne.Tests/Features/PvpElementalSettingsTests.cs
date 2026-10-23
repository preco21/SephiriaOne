using SephiriaOne;
internal static class PvpElementalSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        void Apply(string operation) { Check(FriendlyFireCommand.Parse("/one friendlyfire elements " + operation, out var command) == FriendlyFireParseResult.Valid && !command.TouchesMasterToggle, "Element command preserves master toggle: " + operation); policy.Record(command); }
        Check(!policy.HasChanges && policy.FriendlyFire.Elements.Enabled, "Default elemental limits on without preset changes");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v43\nfriendlyfire amplification 1", out var old, out _) && old.FriendlyFire.Elements.Enabled, "Old presets default elemental limits on");
        Apply("physical limits 0 0"); Apply("flame limits 10.25 20.75"); Apply("ice limits 200 400"); Apply("lightning limits 1000 10000"); Apply("off");
        var saved = policy.ToPresetText();
        Check(saved.StartsWith("SephiriaOne preset v52\n") && saved.Contains("friendlyfire elements 0"), "Explicit opt-out saved in v52");
        Check(SessionPolicy.TryReadPreset(saved, out var roundtrip, out _) && roundtrip.FriendlyFire.Equals(policy.FriendlyFire), "Four independent pairs and disabled master round trip exactly");
        foreach (var bad in new[] { "physical limits 90 80", "ice limits 0.001 100", "ice limits 50 100.000", "fire limits 50 100", "flame limits 50", "on extra", "ice reset extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire elements " + bad, out _) == FriendlyFireParseResult.Invalid, "Reject invalid atomic command: " + bad);
        foreach (var bad in new[] { saved.Replace("elements-ice 200 400", "elements-ice 500 400"), saved + "\nfriendlyfire elements 0", saved.Replace("v52", "v51") })
            Check(!SessionPolicy.TryReadPreset(bad, out _, out _), "Reject invalid, duplicate, and version-incompatible elemental preset");
        Apply("on"); Check(policy.FriendlyFire.Elements.GetLimit(PvpElement.Ice).Soft == 200, "On preserves pairs");
        Apply("ice reset"); Check(policy.FriendlyFire.Elements.GetLimit(PvpElement.Ice).Soft == 50 && policy.FriendlyFire.Elements.GetLimit(PvpElement.Flame).Soft == 10.25m, "Per-element reset preserves siblings");
        var configured = policy.FriendlyFire.Elements;
        policy.Record(new FriendlyFireCommand(true)); policy.Record(new FriendlyFireCommand(false, balance: true));
        Check(policy.FriendlyFire.Elements.Equals(configured) && policy.FriendlyFire.WithEnabled(false).Elements.Equals(configured), "Sibling settings and mode overlays preserve elements");
        Apply("reset"); Check(!policy.FriendlyFire.Elements.HasChanges && policy.FriendlyFire.Elements.Enabled, "Global reset restores on/50/100");
        return checks;
    }
}
