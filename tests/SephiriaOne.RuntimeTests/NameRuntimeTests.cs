using SephiriaOne;

internal static class NameRuntimeTests
{
    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add)
    {
        var host = start();
        var guest = add(2, 7, 2, 0);
        var name = new MultiplayerNameColor();
        host.PlayerAvatar.playerNameSource = "Alice";
        SaveManager.Current = new();
        SaveManager.Current.SetString("PlayerName", "Alice");
        UnityEngine.Time.unscaledTime = 0;
        name.Update(host);
        check(host.PlayerAvatar.NameRequests.SequenceEqual(new[] { NameGradient.Format("Alice") }), "Native name publishes once before acknowledgment");
        for (int i = 0; i < 1000; i++) name.Update(host);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 5000; i++) name.Update(host);
        check(GC.GetAllocatedBytesForCurrentThread() - allocated < 1024 && host.PlayerAvatar.NameRequests.Count == 1,
            "Waiting native name does not allocate diagnostics or spam commands between retry deadlines");
        UnityEngine.Time.unscaledTime = 2;
        name.Update(host);
        check(host.PlayerAvatar.NameRequests.Count == 2, "Typed name observation preserves retry deadline");
        host.PlayerAvatar.playerNameSource = host.PlayerAvatar.NameRequests.Last();
        name.Update(host);
        check(name.Status.Contains("local readback matches"), "Acknowledgment replaces cached waiting status");
        for (int i = 0; i < 1000; i++) name.Update(host);
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 5000; i++) name.Update(host);
        check(GC.GetAllocatedBytesForCurrentThread() - allocated < 1024 && host.PlayerAvatar.NameRequests.Count == 2,
            "Acknowledged native name has no repeated allocation or writes");

        SaveManager.Current.SetString("PlayerName", "Bob");
        name.Update(host);
        check(host.PlayerAvatar.NameRequests.Last() == NameGradient.Format("Bob"), "Cached normalization sees profile edits immediately");
        PlayerSpawner.MultiplayerList.Remove(guest);
        name.Update(host);
        check(host.PlayerAvatar.NameRequests.Last() == "Bob", "Leaving multiplayer restores latest plain name");
        host.PlayerAvatar.playerNameSource = "Bob";
        name.Update(host);
        name.Restore();
        host = start(); guest = add(2, 7, 2, 0);
        name.Update(host);
        check(host.PlayerAvatar.NameRequests.Last() == NameGradient.Format("Bob"), "New avatar discards all prior profile/diagnostic observations");
        SaveManager.Current = null;
        name.Update(host);
        check(name.Status.Contains("not ready"), "Missing profile still gates native name publication");
        SaveManager.Current = new(); SaveManager.Current.SetString("PlayerName", "Carol");
        name.Update(host);
        check(host.PlayerAvatar.NameRequests.Last() == NameGradient.Format("Carol"), "Profile returning after absence refreshes plain name");
        name.Restore();
        NameLocalizationRuntimeTests.Run(check, start, add);
    }
}
