using HarmonyLib;
using Mirror;
using SephiriaOne;
using UnityEngine;

int checks = 0;
void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
var harmony = new Harmony("test.dark-cloud-observers");
var adapter = typeof(Program).Assembly.GetType("SephiriaOne.FriendlyFireDarkCloudVisuals");
var strike = AccessTools.Method(typeof(ComboEffect_DarkCloud), "FireLightning");
var sender = AccessTools.Method(typeof(ComboEffect_DarkCloud), "TargetCreateFx");
void BindSender() => AccessTools.Field(adapter, "send").SetValue(null,
    AccessTools.MethodDelegate<Action<ComboEffect_DarkCloud, NetworkConnectionToClient, Vector3, bool, bool>>(sender));
if (adapter != null)
{
    BindSender();
    harmony.Patch(strike, prefix: new HarmonyMethod(adapter, "BeforeStrike"), finalizer: new HarmonyMethod(adapter, "AfterStrike"));
    harmony.Patch(sender, postfix: new HarmonyMethod(adapter, "AfterNativeSend"));
}
PlayerAvatar Player(int id)
{
    var p = new PlayerAvatar();
    var identity = new NetworkIdentity { netId = (uint)id + 1 };
    var conn = new NetworkConnectionToClient { connectionId = id, identity = identity };
    p.spawner = new() { PlayerAvatar = p, netIdentity = identity, connectionToClient = conn };
    identity.Player = p.spawner;
    NetworkServer.connections[id] = conn; NetworkServer.spawned[identity.netId] = identity;
    return p;
}
PlayerAvatar[] players = null;
ComboEffect_DarkCloud cloud = null;
NetworkConnectionToClient C(int i) => players[i].spawner.connectionToClient;
void Fresh(int owner = 0, int count = 4)
{
    NetworkServer.active = true; NetworkServer.connections.Clear(); NetworkServer.spawned.Clear();
    FriendlyFireParticipants.Scope++; FriendlyFireRuntime.Enabled = FriendlyFireRuntime.HasDamage = true;
    FriendlyFireRuntime.Pair = (_, _) => true;
    players = Enumerable.Range(0, count).Select(Player).ToArray();
    var identity = new NetworkIdentity { netId = 100 };
    cloud = new() { Networkavatar = players[owner], connectionToClient = C(owner), netIdentity = identity };
    NetworkServer.spawned[100] = identity;
    for (int i = 0; i < players.Length; i++) identity.observers[i] = C(i);
}
Fresh(); cloud.FireLightning(players[1]);
Check(C(0).Effects == 1 && C(1).Effects == 1 && C(2).Effects == 1 && C(3).Effects == 1,
    "Host-owned Dark Cloud sends original FX once to owner, victim and other observers");
Check(cloud.Attacks == 1 && players[1].Damage == 1, "Forwarding never repeats damage or callbacks");
Fresh(1); cloud.FireLightning(players[0]);
Check(C(0).Effects == 1 && C(1).Effects == 1 && C(2).Effects == 1, "Guest-owned strike includes host observer once");
Fresh(1); cloud.FireLightning(players[2]);
Check(C(0).Effects == 1 && C(1).Effects == 1 && C(2).Effects == 1 && C(3).Effects == 1, "Guest vs guest includes uninvolved host and guest observers");
foreach (bool ex in new[] { false, true }) foreach (bool ice in new[] { false, true })
{
    Fresh(); cloud.Ex = ex; cloud.Ice = ice; cloud.FireLightning(players[1]);
    Check(C(2).Position.x == 10 && C(2).Position.y == 20 && C(2).Position.z == 3 && C(2).Ex == ex && C(2).Ice == ice,
        "Forward exact native world position and lightning/ice/EX flags");
}
Fresh(); cloud.Reject = true; cloud.FireLightning(players[1]);
Check(C(0).Effects == 0 && C(1).Effects == 0, "Suppressed native strike produces no FX");
foreach (Action change in new Action[] { () => FriendlyFireRuntime.Enabled = false, () => FriendlyFireRuntime.HasDamage = false,
    () => FriendlyFireRuntime.Pair = (_, _) => false, () => cloud.isPlayerCombo = false, () => NetworkServer.active = false,
    () => players[1].IsDead = true, () => players[1].currentFloorGuid = "other" })
{
    Fresh(); change(); cloud.FireLightning(players[1]);
    Check(C(0).Effects == 1 && C(1).Effects == 0, "Ineligible strikes retain only their unchanged native send");
}
Fresh(); cloud.FireLightning(new UnitAvatar());
Check(C(0).Effects == 1 && C(1).Effects == 0, "PvE remains owner-only");
Fresh(); cloud.Callback = () => { players[1].IsDead = true; FriendlyFireRuntime.Enabled = false; FriendlyFireRuntime.Pair = (_, _) => false; };
cloud.FireLightning(players[1]);
Check(C(1).Effects == 1 && C(2).Effects == 1, "Lethal strike still emits after synchronous match end");
foreach (Action change in new Action[] { () => players[0].Retired = true, () => players[1].Generation++, () => FriendlyFireParticipants.Scope++,
    () => players[0].currentFloorGuid = "new", () => players[1].currentFloorGuid = "new", () => cloud.Networkavatar = players[2],
    () => NetworkServer.spawned[100] = new() { netId = 100 }, () => cloud.Destroyed = true, () => cloud.connectionToClient = C(2) })
{
    Fresh(); cloud.Callback = change; cloud.FireLightning(players[1]);
    Check(C(1).Effects == 0 && C(3).Effects == 0, "Stale source/victim/cloud/run/floor never forwards an old visual");
}
Fresh(); C(1).isReady = false; players[2].currentFloorGuid = "other"; cloud.netIdentity.observers.Remove(3);
cloud.FireLightning(players[1]);
Check(C(1).Effects == 0 && C(2).Effects == 0 && C(3).Effects == 0, "Readiness, same-floor and cloud-observer gates");
Fresh(); var old = C(2); cloud.Callback = () => Player(2); cloud.FireLightning(players[1]);
Check(old.Effects == 0 && NetworkServer.connections[2].Effects == 0 && C(3).Effects == 1, "Reused connection ID cannot receive stale observer RPC");
Fresh(); C(1).OnSend = () => cloud.netIdentity.observers.Remove(2); cloud.FireLightning(players[1]);
Check(C(1).Effects == 1 && C(2).Effects == 0 && C(3).Effects == 1, "Observer mutation during dispatch is safe and rechecked");
Fresh(); NetworkConnectionToClient joined = null;
C(1).OnSend = () => { joined = Player(4).spawner.connectionToClient; cloud.netIdentity.observers[4] = joined; };
cloud.FireLightning(players[1]);
Check(joined.Effects == 0 && C(3).Effects == 1, "New observer during dispatch cannot mutate the captured recipient list");
Fresh(); cloud.Callback = () => { AccessTools.Method(adapter, "Reset").Invoke(null, null); BindSender(); };
cloud.FireLightning(players[1]);
Check(C(1).Effects == 0 && C(3).Effects == 0, "Unload/reload invalidates the old in-flight strike");
Fresh(); cloud.Callback = () =>
{
    cloud.Callback = () => { cloud.Callback = null; AccessTools.Method(adapter, "Reset").Invoke(null, null); BindSender(); };
    cloud.FireLightning(players[2]);
};
cloud.FireLightning(players[1]);
Check(C(0).Effects == 2 && C(1).Effects == 0 && C(3).Effects == 0,
    "Nested finalizer cannot revive outer admission from before unload/reload");
Fresh(); C(1).Fail = true; cloud.FireLightning(players[1]);
Check(C(1).Effects == 1 && C(2).Effects == 1 && players[1].Damage == 1, "Ambiguous send is never retried and does not stop other observers or damage");
Fresh(); cloud.Callback = () => { cloud.Callback = null; cloud.FireLightning(players[2]); }; cloud.FireLightning(players[1]);
Check(C(0).Effects == 2 && C(1).Effects == 2 && C(3).Effects == 2 && cloud.Attacks == 2, "Nested strikes restore their own visual scope without recursive duplicates");
foreach (int priority in new[] { Priority.First, Priority.Last })
{
    var limiter = new Harmony("test.cloud-limiter");
    limiter.Patch(strike, prefix: new HarmonyMethod(typeof(NativeStrikeLimiter), "BeforeStrike") { priority = priority });
    Fresh(); cloud.Callback = () => { cloud.RejectPrefix = true; cloud.FireLightning(players[2]); cloud.RejectPrefix = false; };
    cloud.FireLightning(players[1]);
    Check(C(1).Effects == 1 && C(2).Effects == 1 && cloud.Attacks == 1,
        "Rejected nested native prefix preserves outer FX scope at priority " + priority);
    limiter.UnpatchAll(limiter.Id);
}
Fresh(); cloud.ThrowBeforeFx = true;
try { cloud.FireLightning(players[1]); } catch (InvalidOperationException) { }
cloud.TargetCreateFx(C(0), cloud.Position, false, false);
Check(C(1).Effects == 0, "Native exception unwinds scope without leaking to unrelated sends");
Fresh(); cloud.Position.x = float.NaN; cloud.FireLightning(players[1]);
Check(C(0).Effects == 1 && C(1).Effects == 0, "Invalid forwarded coordinates leave native owner behavior intact");
Fresh(count: 9); for (int i = 0; i < 100; i++) cloud.FireLightning(players[1]);
long before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 3000; i++) cloud.FireLightning(players[1]);
long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
Check(bytes == 0, "Warmed forwarding allocates zero managed bytes: " + bytes);
Check(players.All(p => p.spawner.connectionToClient.Effects == 3100), "Nine observers each receive exactly one message per admitted strike");
Console.WriteLine($"Dark Cloud forwarding: {bytes} bytes/3000 strikes to nine observers.");
harmony.UnpatchAll(harmony.Id); Fresh(); cloud.FireLightning(players[1]);
Check(C(0).Effects == 1 && C(1).Effects == 0, "Unload restores native owner-only presentation");
Console.WriteLine($"Passed {checks} Dark Cloud observer routing/lifetime checks (fixtures, not live rendering).");
