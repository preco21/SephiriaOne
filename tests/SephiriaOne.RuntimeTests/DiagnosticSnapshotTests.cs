using SephiriaOne;

internal static class DiagnosticSnapshotTests
{
    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        var host = start();
        check(SettingsActions.Execute("/stats luck +10").Success && SessionSettings.EnsureFresh(), "Prepare diagnostic formatting fixture");
        var before = SessionSettings.ReadSnapshot();
        const string rule = "Player #1 luck / relative-stat: Applied (revision 1).";
        const string values = "Player #1: luck=15 (base adjustment +10), defense=0, attackspeed=100, critical=0, criticaldamage=50, evasion=0, cooldown=0, mpregen=0, negotiation=0, truedamage=0, toughness=0, dashrecovery=100, expdrop=100, leafdrop=100, thorns=0, normaldamage=100, dashdamage=100, specialdamage=100, weapondamage=100, grimoiredamage=100, alldamage=100, hpsteal=0, mpsteal=0, ignoredefense=0, debuffduration=0, debuffdamage=0, crossbowreload=100. Units: /stats list.";
        check(before.Lines.Contains(rule) && before.Lines.Contains(values), "Status text preserves rule, stat ordering, units and contribution formatting");
        check(SessionSettings.DescribeDisconnect(1).Contains(rule), "Disconnect diagnostics use the same rule formatting");
        host.PlayerAvatar.customStats["LUCK"] += 7;
        int writes = 0;
        host.PlayerAvatar.customStats.BeforeWrite = (_, _) => writes++;
        var updated = SessionSettings.ReadSnapshot();
        var repeated = SessionSettings.ReadSnapshot();
        check(updated.Players[0].Stats["luck"] == 22 && updated.Lines.Contains(rule) && writes == 0,
            "Diagnostic refresh reads fresh values without reconciling or advancing rule revision");
        check(updated.Lines.SequenceEqual(repeated.Lines) && before.Lines.Contains(values) && before.Players[0].Stats["luck"] == 15,
            "Report buffers cannot change earlier snapshots or carry text across captures");
        check(SessionSettings.DescribeDisconnect(1).Contains(rule) && writes == 0, "Disconnect diagnostic read never applies native changes");
        host.PlayerAvatar.customStats.BeforeWrite = null;
        PlayerSpawner.MultiplayerList.Clear();
        SessionSettings.Synchronize();
        check(!SessionSettings.ReadSnapshot().Lines.Any(line => line.Contains("Player #1")), "Forgotten subject diagnostics do not leak into next report");
    }
}
