using SephiriaOne;

if (args.Length == 1 && args[0] == "--live-check")
{
    using var client = new GitHubReleaseClient();
    var release = await client.CheckAsync(CancellationToken.None);
    Console.WriteLine(release == null ? "No stable release." : $"Verified live GitHub response: {release.Tag}, {release.Size} bytes, SHA-256 present. No install.");
    return;
}
if (args.Length == 3 && args[0] == "--package-fixture")
{
    PackageFixture.Run(args[1], args[2]); return;
}
int checks = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
var root = Path.Combine(Path.GetTempPath(), "SephiriaOne-update-flow-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
UpdateRelease Release(int patch) => new(new Version(0, 38, patch), "v0.38." + patch,
    "https://github.com/preco21/SephiriaOne/releases/download/v0.38." + patch + "/SephiriaOne.zip", new string('a', 64), 1024);
async Task Settle(UpdateCoordinator service)
{
    for (int i = 0; i < 3000 && service.Snapshot.Busy; i++) { await Task.Delay(2); service.Tick(); }
    Check(!service.Snapshot.Busy, "Operation settles without blocking the caller");
}
try
{
    int downloads = 0, queries = 0;
    var clock = DateTimeOffset.UtcNow;
    var available = Release(1);
    Task<UpdateRelease> Query(CancellationToken token) { queries++; return Task.FromResult(available); }
    Task Install(UpdateRelease release, CancellationToken token) { downloads++; return Task.CompletedTask; }
    string config = Path.Combine(root, "updates.json");
    using (var service = new UpdateCoordinator(new Version(0, 38, 0), config, Query, Install, () => new Version(0, 38, 0), () => clock))
    {
        await Settle(service);
        Check(queries == 1 && downloads == 0, "Startup checks once and never implicitly installs");
        Check(service.Snapshot.CanInstall && service.Snapshot.Candidate.Version == available.Version, "New stable version is offered");
        Check(service.Snapshot.Notice > 0, "Automatic availability produces a queued local notice");
        Check(service.Install(), "Explicit install accepted");
        Check(!service.Install() && !service.Check(), "Duplicate operations cannot race");
        Check(!service.SetAutomatic(false), "Preference writes cannot race an install");
        await Settle(service);
        Check(downloads == 1 && queries == 2, "Install freshly verifies the selected release before downloading once");
        Check(service.Snapshot.Phase == UpdatePhase.RestartRequired && !service.Snapshot.CanInstall, "Committed install requires restart");
        Check(!service.Install(), "Pending version cannot install repeatedly");
    }
    using (var service = new UpdateCoordinator(new Version(0, 38, 0), config, Query, Install, () => new Version(0, 38, 0), () => clock))
    {
        await Settle(service);
        Check(queries == 2 && service.Snapshot.CanInstall, "Cached release survives restart without another automatic request");
        available = Release(2);
        Check(service.Install(), "Install starts from displayed cached release");
        await Settle(service);
        Check(downloads == 1 && service.Snapshot.Phase == UpdatePhase.Failed, "Changed release is not silently installed instead of selected version");
        Check(service.SetAutomatic(false), "Automatic preference can change while idle");
        await Settle(service);
    }
    clock += TimeSpan.FromDays(1);
    int before = queries;
    using (var service = new UpdateCoordinator(new Version(0, 38, 0), config, Query, Install, () => new Version(0, 38, 0), () => clock))
    {
        await Settle(service);
        Check(!service.Snapshot.Automatic && queries == before, "Automatic-off is persistent");
        Check(service.Check(), "Manual check works when automatic checking is disabled");
        await Settle(service);
        Check(service.Snapshot.Candidate.Version == available.Version && downloads == 1, "Manual check only refreshes availability");
        Check(!service.Check(), "Manual requests are briefly throttled");
    }
    var deferred = new TaskCompletionSource<UpdateRelease>(TaskCreationOptions.RunContinuationsAsynchronously);
    using (var service = new UpdateCoordinator(new Version(0, 38, 0), Path.Combine(root, "cancel.json"), _ => deferred.Task, Install, () => new Version(0, 38, 0), () => clock))
    {
        for (int i = 0; i < 1000 && service.Snapshot.Phase == UpdatePhase.Initializing; i++) { await Task.Delay(2); service.Tick(); }
        Check(service.Snapshot.Phase == UpdatePhase.Checking, "Deferred check in progress");
        long revision = service.Snapshot.Revision;
        service.Dispose(); deferred.SetResult(available); await Task.Delay(10); service.Tick();
        Check(service.Snapshot.Revision == revision && downloads == 1, "Disposed controller cannot publish stale completion or install");
    }
    using (var service = new UpdateCoordinator(new Version(0, 38, 2), Path.Combine(root, "newer.json"), Query, Install, () => new Version(0, 38, 2), () => clock))
    {
        available = Release(1); await Settle(service);
        Check(!service.Snapshot.CanInstall && service.Snapshot.Phase == UpdatePhase.Current, "Older latest release never downgrades running code");
    }
    using (var service = new UpdateCoordinator(new Version(0, 38, 0), Path.Combine(root, "commands.json"), Query, Install, () => new Version(0, 38, 0), () => clock))
    {
        await Settle(service); UpdateFeature.Bind(service);
        Check(UpdateCommand.Execute(new[] { "/one", "update", "status" }, out bool ok, out string message) && ok,
            "Update status recognized without gameplay dependencies");
        foreach (string suffix in new[] { "install extra", "auto", "auto perhaps", "check extra", "unknown" })
            Check(UpdateCommand.Execute(("/one update " + suffix).Split(' '), out ok, out message) && !ok, "Invalid update command cannot mutate: " + suffix);
        Check(!UpdateCommand.Execute(new[] { "/one", "other" }, out ok, out message), "Unrelated commands are not consumed");
        Check(UpdateCommand.Execute(new[] { "/one", "update", "help" }, out ok, out message) && ok && message.Contains("/one update"),
            "Updater help describes explicit installation without starting work");
        Check(UpdateCommand.Execute(new[] { "/ONE", "UPDATE", "AUTO", "OFF" }, out ok, out message) && ok,
            "Case-insensitive command uses the same coordinator");
        await Settle(service); Check(!service.Snapshot.Automatic, "Command persists automatic-off");
        UpdateFeature.Stop();
    }
    before = queries;
    string damaged = Path.Combine(root, "damaged.json");
    File.WriteAllText(damaged, "{\"Automatic\":false,\"Automatic\":true}");
    using (var service = new UpdateCoordinator(new Version(0, 38, 0), damaged, Query, Install, () => new Version(0, 38, 0), () => clock))
    {
        await Settle(service);
        Check(queries == before && service.Snapshot.Phase == UpdatePhase.Failed, "Corrupt duplicate preference cannot enable automatic requests");
        Check(File.ReadAllText(damaged).Contains("false"), "Malformed preference retained for user inspection");
    }
    string limited = Path.Combine(root, "limited.json");
    foreach (string malformed in new[] { "{\"Automatic\":false,\"automatic\":\"true\"}", "{\"automatic\":true}", "{\"Automatic\":\"false\"}" })
    {
        File.WriteAllText(damaged, malformed);
        using var service = new UpdateCoordinator(new Version(0, 38, 0), damaged, Query, Install, () => new Version(0, 38, 0), () => clock);
        await Settle(service);
        Check(queries == before && service.Snapshot.Phase == UpdatePhase.Failed, "Malformed/case-conflicting preference cannot enable requests: " + malformed);
    }
    int limitedQueries = 0;
    Task<UpdateRelease> Limited(CancellationToken _) { limitedQueries++; throw new UpdateRateLimitException(clock.AddHours(1)); }
    using (var service = new UpdateCoordinator(new Version(0, 38, 0), limited, Limited, Install, () => new Version(0, 38, 0), () => clock))
    {
        await Settle(service);
        Check(limitedQueries == 1 && !service.Check(), "Server cooldown blocks repeated checks");
    }
    using (var service = new UpdateCoordinator(new Version(0, 38, 0), limited, Limited, Install, () => new Version(0, 38, 0), () => clock))
    { await Settle(service); Check(limitedQueries == 1 && !service.Check(), "Server cooldown survives a game restart"); }
    Console.WriteLine($"Passed {checks} update coordinator/command checks.");
}
finally { Directory.Delete(root, true); }
