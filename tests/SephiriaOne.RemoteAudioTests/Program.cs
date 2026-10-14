using Mirror;
using SephiriaOne;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
PlayerAvatar Add(int id, bool local = false)
{
    NetworkConnectionToClient c = local ? new LocalConnectionToClient() : new NetworkConnectionToClient();
    c.connectionId = id;
    var p = new PlayerAvatar { netId = (uint)id, connectionToClient = c };
    p.spawner = new PlayerSpawner { PlayerAvatar = p, connectionToClient = c, netIdentity = p.netIdentity };
    p.netIdentity.Owner = p.spawner; c.identity = p.netIdentity;
    NetworkServer.connections.Add(id, c); DungeonManager.Instance.netIdentity.observers[id] = c;
    return p;
}
void Reset()
{
    NetworkServer.connections.Clear(); NetworkServer.active = true;
    DungeonManager.Instance = new(); Bullet.Pool = new();
    UnityEngine.Debug.Messages.Clear();
    UnityEngine.Debug.Fail = HostStateAdapter.Fail = false;
    FriendlyFireDeathAudio.Reset(); FriendlyFireDeathVisuals.Compatible = true;
    // Real native IL is validated by GameRemoteAudioTests. Here inject only the
    // validated sender to execute production audience/payload/failure handling.
    HarmonyLib.AccessTools.Field(typeof(FriendlyFireDeathAudio), "initialized").SetValue(null, true);
    HarmonyLib.AccessTools.Field(typeof(FriendlyFireDeathAudio), "targetSend").SetValue(null,
        HarmonyLib.AccessTools.Method(typeof(NetworkBehaviour), "SendTargetRPCInternal"));
}
foreach (bool localVictim in new[] { false, true })
{
    Reset(); var victim = Add(1, localVictim); var host = Add(2, !localVictim); var other = Add(3);
    foreach (var e in NetworkServer.connections) victim.netIdentity.observers[e.Key] = e.Value;
    FriendlyFireDeathAudio.Show(victim);
    Check(victim.connectionToClient.Sent.Count == 0, "Victim retains native death sound without a second cue");
    foreach (var listener in new[] { host, other })
    {
        Check(listener.connectionToClient.Sent.Count == 1, "Host/remote observer receives one targeted cue");
        Check(listener.connectionToClient.Played.SequenceEqual(new[] { victim.transform.position }), "Receiver reaches sound playback at the victim position exactly once");
        Check(listener.connectionToClient.Sent[0].SequenceEqual(new object[] { 1u, false, "FlameBall", victim.transform.position, 0f, 0f, 1f }), "Exact native sound-only payload at victim position");
    }
    Check(NetworkWriterPool.Outstanding == 0, "All writers returned");
}
foreach (string condition in new[] { "victim observer absent", "inventory not ready" })
{
    Reset(); var victim = Add(1); var listener = Add(2, true);
    if (condition != "victim observer absent") victim.netIdentity.observers[2] = listener.connectionToClient;
    listener.spawner.InventoryReady = condition != "inventory not ready";
    FriendlyFireDeathAudio.Show(victim);
    Check(listener.connectionToClient.Played.Count == 1, "Cosmetic dungeon RPC does not require " + condition);
}
foreach (string condition in new[] { "unready", "replaced", "dungeon hidden", "other floor", "missing floor", "wrong owner", "no identity", "duplicate" })
{
    Reset(); var victim = Add(1); var listener = Add(2, true); var c = listener.connectionToClient;
    victim.netIdentity.observers[2] = c;
    switch (condition)
    {
        case "unready": c.isReady = false; break;
        case "replaced": NetworkServer.connections[2] = new() { connectionId = 2 }; break;
        case "dungeon hidden": DungeonManager.Instance.netIdentity.observers.Clear(); break;
        case "other floor": listener.currentFloorGuid = "dungeon"; break;
        case "missing floor": listener.currentFloorGuid = victim.currentFloorGuid = ""; break;
        case "wrong owner": listener.spawner.connectionToClient = new(); break;
        case "no identity": c.identity = null; break;
        case "duplicate": DungeonManager.Instance.netIdentity.observers[99] = c; break;
    }
    FriendlyFireDeathAudio.Show(victim);
    Check(c.Sent.Count == (condition == "duplicate" ? 1 : 0), "Audience validity: " + condition);
}
Reset();
var dead = Add(1); var failing = Add(2); var later = Add(3, true);
foreach (var e in NetworkServer.connections) dead.netIdentity.observers[e.Key] = e.Value;
failing.connectionToClient.Fail = true;
FriendlyFireDeathAudio.Show(dead);
Check(failing.connectionToClient.Sent.Count == 1 && later.connectionToClient.Sent.Count == 1 && NetworkWriterPool.Outstanding == 0, "Ambiguous send is not retried and cannot skip later host recipient");
foreach (string transition in new[] { "disconnect", "dungeon", "revive" })
{
    Reset(); dead = Add(1); var first = Add(2); later = Add(3, true);
    first.connectionToClient.OnSend = () =>
    {
        if (transition == "disconnect") NetworkServer.connections.Remove(3);
        if (transition == "dungeon") DungeonManager.Instance = new();
        if (transition == "revive") dead.IsDead = false;
    };
    FriendlyFireDeathAudio.Show(dead);
    Check(first.connectionToClient.Played.Count == 1 && later.connectionToClient.Played.Count == 0 && NetworkWriterPool.Outstanding == 0,
        "No stale playback after a send callback changes " + transition);
}
Reset(); dead = Add(1); later = Add(2, true);
Bullet.Pool = null; FriendlyFireDeathAudio.Show(dead);
Check(later.connectionToClient.Sent.Count == 0 && UnityEngine.Debug.Messages.Any(m => m.Contains("prefab/pool unavailable")), "Missing pool is observable without fake delivery");
Bullet.Pool = new(); FriendlyFireDeathAudio.Show(dead);
Check(later.connectionToClient.Played.Count == 1, "A later death can use a restored pool; skipped sound is never replayed");
Reset(); dead = Add(1); later = Add(2, true); later.spawner.InventoryReady = false;
for (int i = 0; i < 20; i++) FriendlyFireDeathAudio.Show(dead);
Check(UnityEngine.Debug.Messages.Count == 6 && UnityEngine.Debug.Messages.All(m =>
    m.Contains("queued=1 (host=1)") && m.Contains("oldInventoryGate=1") && m.Contains("oldVictimObserverGate=1") && m.Contains("not a client playback acknowledgment")),
    "Bounded traces distinguish queued packets and removed gates without claiming guest playback");
Check(later.connectionToClient.Played.Count == 20, "Exhausting diagnostic budget never suppresses audio");
Reset(); dead = Add(1); later = Add(2); later.connectionToClient.isReady = false;
FriendlyFireDeathAudio.Show(dead);
Check(UnityEngine.Debug.Messages.Any(m => m.Contains("queued=0") && m.Contains("stale=1")), "No eligible listener is observable");
foreach (string diagnostic in new[] { "readiness", "log sink" })
{
    Reset(); dead = Add(1); later = Add(2, true);
    HostStateAdapter.Fail = diagnostic == "readiness"; UnityEngine.Debug.Fail = diagnostic == "log sink";
    FriendlyFireDeathAudio.Show(dead); FriendlyFireDeathAudio.Show(dead);
    Check(later.connectionToClient.Played.Count == 2 && NetworkWriterPool.Outstanding == 0,
        "Diagnostic failure cannot stop this or later audio: " + diagnostic);
}
foreach (string mutation in new[] { "subclass", "visual", "alternates", "fragments", "null sound", "guid" })
{
    var m = new BulletDestroyModule();
    switch (mutation)
    {
        case "subclass": m = new DerivedDestroyModule(); break;
        case "visual": m.destroyFxPrefab = new(); break;
        case "alternates": m.destroyFxPrefabList.Add(new()); break;
        case "fragments": m.breakFxFragments.Add(new()); break;
        case "null sound": m.destroySound.IsNull = true; break;
        case "guid": m.destroySound.Guid.Data1++; break;
    }
    Check(!FriendlyFireDeathAudio.SoundOnly(m), "Reject changed audio asset: " + mutation);
}
Check(FriendlyFireDeathAudio.SoundOnly(new()), "Stock sound-only asset accepted");
Console.WriteLine($"Passed {checks} death-audio routing, playback-boundary fixture, diagnostics and asset guard checks (not live guest audio).");
