using System;
using System.IO;
using SephiriaOne;

static class Program
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Main(string[] args)
    {
        if (args.Length == 2)
        {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) => { string dll = Path.Combine(args[0], name.Name + ".dll"); return File.Exists(dll) ? System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(dll) : null; };
            GameHotkeyCompatibilityTests.Run(System.Reflection.Assembly.LoadFrom(Path.Combine(args[0], "Assembly-CSharp.dll")), System.Reflection.Assembly.LoadFrom(Path.GetFullPath(args[1])));
        }
        string path = Path.Combine(Path.GetTempPath(), "SephiriaOne.HotkeyTests-" + Guid.NewGuid(), "ui.json");
        try
        {
            Check(!HotkeyInputOwnership.IsBlocked(new[] { "pause", "our panel" }, value => value == "our panel"), "owned top panel can close over pause");
            Check(HotkeyInputOwnership.IsBlocked(new[] { "our panel", "foreign" }, value => value == "our panel"), "foreign top retains input");
            Check(!HotkeyInputOwnership.IsBlocked(Array.Empty<string>(), value => false), "empty native stack allows candidate");
            var store = new HotkeyStore(path);
            var policy = new HotkeyPolicy(store.Load(_ => { }), store.TrySave);
            Check(policy.Binding == null, "default unassigned");
            int validations = 0;
            Check(!policy.Capture("F9", _ => { validations++; return null; }, out _) && validations == 0, "capture ignored outside listening");
            policy.BeginCapture(); policy.CancelCapture();
            Check(policy.Binding == null && !policy.Listening, "cancel preserves default");
            policy.BeginCapture();
            Check(!policy.Capture("Escape", _ => "reserved", out _) && policy.Listening, "reserved rejected");
            Check(!policy.Capture("A", _ => "conflict", out _) && policy.Binding == null, "conflict rejected");
            Check(policy.Capture("F9", _ => null, out _) && policy.Binding == "F9", "capture saves");
            Check(!policy.Poll(true, false), "capture completion press suppressed");
            Check(!policy.Poll(false, false) && policy.Poll(true, false), "release arms edge");
            Check(!policy.Poll(true, false), "held key does not repeat");
            policy.Poll(false, false); Check(!policy.Poll(true, true), "blocked press consumed");
            Check(!policy.Poll(true, false), "closing chat/menu while held does not activate");
            policy.Poll(false, false); Check(policy.Poll(true, false), "new press activates");
            foreach (string owner in new[] { "chat", "text entry", "menu", "foreign binding capture" })
            {
                policy.Poll(false, false); Check(!policy.Poll(true, true), owner + " consumes press");
                Check(!policy.Poll(true, false), owner + " held press stays consumed after release of ownership");
            }
            policy.BeginCapture(); policy.Poll(false, false); Check(!policy.Poll(true, false), "owned capture suppresses assigned shortcut");
            policy.CancelCapture(); Check(!policy.Poll(true, false), "cancel keeps release barrier");
            policy.BeginCapture(); policy.CancelCapture(); Check(policy.Binding == "F9", "cancel preserves binding");
            Check(store.Load(_ => { }) == "F9", "restart persists");
            Check(policy.Clear(out _) && store.Load(_ => { }) == null, "clear persists");
            var failed = new HotkeyPolicy("F9", (string key, out string error) => { error = "disk"; return false; });
            failed.BeginCapture(); Check(!failed.Capture("F10", _ => null, out _) && failed.Binding == "F9" && !failed.Listening, "failed capture preserves old and releases capture");
            Check(!failed.Clear(out _) && failed.Binding == "F9", "failed clear preserves old");
            foreach (string invalid in new[] { "{", "{\"version\":99,\"key\":\"F9\"}", "{\"version\":1,\"key\":123}" })
            {
                File.WriteAllText(path, invalid); int warnings = 0;
                Check(store.Load(_ => warnings++) == null && warnings == 1, "invalid store safe and warns once");
            }
            Check(!new HotkeyStore(Path.GetDirectoryName(path)).TrySave("F9", out _), "real persistence failure reported");
            Console.WriteLine($"PASS: {checks} hotkey policy/store checks");
        }
        finally { Directory.Delete(Path.GetDirectoryName(path), true); }
    }
}
