using System.Runtime.CompilerServices;
using Mirror;
using SephiriaOne;

internal static class ChoiceCleanupLifetimeTests
{
    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start)
    {
        var recovered = ReleaseAfterUntrackedRecovery(start, check);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        check(!recovered.Player.IsAlive, "Completed choice cleanup releases its player without a later scope clear");
        check(!recovered.Dungeon.IsAlive, "Completed choice cleanup releases its dungeon without a later scope clear");

        foreach (string exit in new[] { "stop", "replacement", "retry", "reset" })
        {
            var references = ReleaseAfterCleanup(start, check, exit);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            check(!references.Player.IsAlive, "Choice cleanup releases departed player after " + exit);
            check(!references.Dungeon.IsAlive, "Choice cleanup releases old dungeon after " + exit);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Player, WeakReference Dungeon) ReleaseAfterUntrackedRecovery(
        Func<PlayerSpawner> start, Action<bool, string> check)
    {
        var host = start();
        // Cleanup also removes persisted markers without an enrolled controller scope.
        // Clear that scope before creating the journal, so teardown cannot mask a leak.
        SessionSettings.Stop();
        host.PlayerAvatar.customStats["EXTRAITEMCHOICES"] = 7;
        host.PlayerAvatar.customStats["SEPHIRIAONE_EXTRAITEMCHOICES"] = 5;
        var references = (new WeakReference(host.PlayerAvatar), new WeakReference(DungeonManager.Instance));
        host.PlayerAvatar.customStats.BeforeRemove = key =>
        {
            if (key == "SEPHIRIAONE_EXTRAITEMCHOICES") throw new InvalidOperationException("cleanup marker failure");
        };
        bool failed = false;
        try { ChoicePoints.RemoveContributions(); }
        catch (InvalidOperationException) { failed = true; }
        check(failed, "Untracked cleanup retains a journal after partial marker removal");
        host.PlayerAvatar.customStats.BeforeRemove = null;
        ChoicePoints.RemoveContributions();
        check(host.PlayerAvatar.customStats.GetValueOrDefault("EXTRAITEMCHOICES") == 2,
            "Untracked cleanup recovery restores its native baseline");
        PlayerSpawner.MultiplayerList.Clear();
        DungeonManager.Instance = null;
        return references;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Player, WeakReference Dungeon) ReleaseAfterCleanup(
        Func<PlayerSpawner> start, Action<bool, string> check, string exit)
    {
        var host = start();
        check(SettingsActions.Execute("/choices all 5").Success, "Prepare choice cleanup lifetime: " + exit);
        var references = (new WeakReference(host.PlayerAvatar), new WeakReference(DungeonManager.Instance));
        host.PlayerAvatar.customStats.BeforeRemove = key =>
        {
            if (key == "SEPHIRIAONE_EXTRAITEMCHOICES") throw new InvalidOperationException("cleanup marker failure");
        };
        bool failed = false;
        try { ChoicePoints.RemoveContributions(); }
        catch (InvalidOperationException) { failed = true; }
        check(failed && !SettingsActions.Execute("/stats luck +10").Success,
            "Pending choice cleanup retains same-session recovery protection: " + exit);
        host.PlayerAvatar.customStats.BeforeRemove = null;

        if (exit == "retry") ChoicePoints.RemoveContributions();
        else if (exit == "reset")
            check(SettingsActions.Execute("/choices reset").Success, "Choice reset recovers cleanup before release");
        if (exit == "retry" || exit == "reset")
            check(host.PlayerAvatar.customStats.GetValueOrDefault("EXTRAITEMCHOICES") == 2 &&
                !host.PlayerAvatar.customStats.ContainsKey("SEPHIRIAONE_EXTRAITEMCHOICES"),
                "Recovered cleanup restores the native choice baseline once: " + exit);

        PlayerSpawner.MultiplayerList.Clear();
        DungeonManager.Instance = exit == "replacement" ? new DungeonManager() : null;
        if (exit == "replacement")
        {
            check(SessionSettings.Synchronize() && string.IsNullOrEmpty(SessionSettings.ReadSnapshot().FaultedFeature),
                "Replacing the dungeon abandons the old cleanup fault");
        }
        else
        {
            NetworkServer.active = false;
            SessionSettings.Stop();
        }
        return references;
    }
}
