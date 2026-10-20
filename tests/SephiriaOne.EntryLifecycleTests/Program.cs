using System;
using System.Linq;
using SephiriaOne;
using UnityEngine;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Load(Entry entry) => entry.LoadMod(new HorayModBase.Metadata { modVersion = "fixture" });
void Rejected(Action action, string message)
{
    bool rejected = false;
    try { action(); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, message);
}
void Reset()
{
    Check(HorayModAPI.SubscriberCount == 0, "Previous lifetime released its database subscription");
    Check(GameObject.Created.All(o => o.Destroyed), "Previous lifetime released its controllers");
    LifecycleProbe.Reset(); GameObject.Created.Clear();
}

// The installed loader creates a new Entry and forgets the previous one on each
// LoadAll scan. It calls UnloadMod only on the most recently retained wrapper.
var a = new Entry(); var b = new Entry(); var c = new Entry();
Load(a); Load(b); Load(c); Load(c);
Check(LifecycleProbe.Count("init:FriendlyFire") == 1, "Repeated loader scans must install friendly fire exactly once");
Check(LifecycleProbe.Events.Where(e => e.StartsWith("init:")).GroupBy(e => e).All(g => g.Count() == 1), "All features and localization initialize once");
Check(HorayModAPI.SubscriberCount == 1, "Repeated scans retain one database callback");
Check(GameObject.Created.Count == 1 && GameObject.Created[0].Components.Count == 6, "Repeated scans retain one complete controller object");
HorayModAPI.Ready(); HorayModAPI.Ready();
Check(LifecycleProbe.Count("ready:Collin") == 2 && LifecycleProbe.Count("ready:RabbitLevelUp") == 2 && LifecycleProbe.Count("ready:RabbitStartingArtifact") == 2,
    "Each new database initialization still reaches the original owner once");
Check(LifecycleProbe.Count("ready:Requital") == 2, "Slate catalog refreshes once per database event");
Check(LifecycleProbe.Count("ready:LargePotion") == 2, "Supply catalog refreshes once per database event");
a.UnloadMod(); b.UnloadMod();
Check(LifecycleProbe.Count("stop:FriendlyFire") == 0 && !GameObject.Created[0].Destroyed, "Stale wrappers cannot unload the active runtime");
c.UnloadMod(); c.UnloadMod();
Check(LifecycleProbe.Count("stop:FriendlyFire") == 1 && HorayModAPI.SubscriberCount == 0, "Latest wrapper unloads original owner exactly once");
Check(LifecycleProbe.Count("stop:Requital") == 1, "Requital teardown runs exactly once");
Check(LifecycleProbe.Count("init:Experience") == 1 && LifecycleProbe.Count("stop:Experience") == 1,
    "XP patch shares the single addon lifetime");
Check(LifecycleProbe.Count("stop:LargePotion") == 1, "Latest wrapper clears supply catalog and cursors exactly once");
Check(GameObject.Created[0].Destroyed && GameObject.Created[0].Components.All(component => !component.enabled), "Latest wrapper releases original owner's controller fields");
Check(LifecycleProbe.Events.IndexOf("stop:Deathmatch") < LifecycleProbe.Events.IndexOf("stop:FriendlyFire") &&
    LifecycleProbe.Events.IndexOf("stop:BatCostume") < LifecycleProbe.Events.IndexOf("stop:Choice"), "Match recovery and Bat restoration retain teardown order");
Load(a);
Check(LifecycleProbe.Count("init:FriendlyFire") == 2 && GameObject.Created.Count == 2 && HorayModAPI.SubscriberCount == 1, "A completed unload permits a clean lifetime, even on a reused wrapper");
c.UnloadMod();
Check(HorayModAPI.SubscriberCount == 1, "An old wrapper cannot unload a newer lifetime");
a.UnloadMod(); Reset();

var failed = new Entry(); var rejected = new Entry();
LifecycleProbe.Initializing = name => { if (name == "FriendlyFire") throw new InvalidOperationException("initialization fixture"); };
Rejected(() => Load(failed), "Initialization failure is reported");
LifecycleProbe.Initializing = null;
Rejected(() => Load(rejected), "Partial initialization is quarantined instead of installing twice");
Check(LifecycleProbe.Count("init:FriendlyFire") == 1 && HorayModAPI.SubscriberCount == 1, "Failed initialization retains its original subscription and does not replay prior features");
rejected.UnloadMod();
Check(HorayModAPI.SubscriberCount == 1, "Rejected load cannot take cleanup ownership");
failed.UnloadMod();
Load(rejected); rejected.UnloadMod(); Reset();

var partialControllers = new Entry();
LifecycleProbe.CreatingComponent = name => { if (name == "SettingsPanelController") throw new InvalidOperationException("component fixture"); };
Rejected(() => Load(partialControllers), "Partial controller creation reports its initialization failure");
LifecycleProbe.CreatingComponent = null;
Check(GameObject.Created.Count == 1 && GameObject.Created[0].Components.Count == 3, "Controller fixture fails before name controller assignment");
partialControllers.UnloadMod();
Check(GameObject.Created[0].Destroyed && HorayModAPI.SubscriberCount == 0, "Partial controller object is released without a name controller");
Reset();

var original = new Entry(); var latest = new Entry(); var afterFailure = new Entry();
Load(original); Load(latest);
LifecycleProbe.ShuttingDown = name => { if (name == "Deathmatch") throw new InvalidOperationException("recovery pending fixture"); };
Rejected(latest.UnloadMod, "Pending native recovery blocks teardown");
Check(LifecycleProbe.Count("stop:FriendlyFire") == 0 && HorayModAPI.SubscriberCount == 1 && !GameObject.Created[0].Destroyed,
    "Failed cleanup retains hooks, subscription and controllers for recovery");
Rejected(() => Load(afterFailure), "Cleanup failure quarantines reinstallation");
original.UnloadMod(); afterFailure.UnloadMod();
Check(LifecycleProbe.Count("stop:Deathmatch") == 1, "Stale and rejected wrappers cannot steal cleanup retry");
LifecycleProbe.ShuttingDown = null;
latest.UnloadMod();
Check(LifecycleProbe.Count("stop:Deathmatch") == 2 && LifecycleProbe.Count("stop:FriendlyFire") == 1, "Latest wrapper retries the original owner's interrupted cleanup");
Load(afterFailure); afterFailure.UnloadMod(); Reset();

var outer = new Entry(); var recursive = new Entry();
LifecycleProbe.Initializing = name =>
{
    if (name != "Choice") return;
    Rejected(() => Load(recursive), "Reentrant initialization is rejected");
    Rejected(outer.UnloadMod, "Unload cannot interrupt initialization");
};
Load(outer); LifecycleProbe.Initializing = null;
Check(LifecycleProbe.Count("init:Choice") == 1 && HorayModAPI.SubscriberCount == 1, "Rejected reentry leaves the original initialization intact");
LifecycleProbe.ShuttingDown = name =>
{
    if (name != "Deathmatch") return;
    Rejected(() => Load(recursive), "Load cannot interrupt teardown");
    Rejected(outer.UnloadMod, "Reentrant teardown is rejected");
};
outer.UnloadMod(); LifecycleProbe.ShuttingDown = null; Reset();

Console.WriteLine($"Passed {checks} Entry lifecycle checks (portable callbacks and controller fixtures; not live Unity/Harmony).");
