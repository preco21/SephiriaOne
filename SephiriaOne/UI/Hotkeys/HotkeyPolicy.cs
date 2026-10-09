using System;
namespace SephiriaOne
{
    internal delegate bool SaveHotkey(string key, out string error);
    internal sealed class HotkeyPolicy
    {
        private readonly SaveHotkey save;
        private bool held, releaseRequired;
        internal string Binding { get; private set; }
        internal bool Listening { get; private set; }
        internal HotkeyPolicy(string binding, SaveHotkey save) { Binding = binding; this.save = save; }
        internal void BeginCapture() { Listening = true; releaseRequired = true; }
        internal void CancelCapture() { Listening = false; releaseRequired = true; }
        internal bool Capture(string candidate, Func<string, string> validate, out string error)
        {
            error = null;
            if (!Listening) return false;
            error = validate(candidate);
            if (error != null) return false;
            Listening = false;
            releaseRequired = true;
            if (!save(candidate, out error)) return false;
            Binding = candidate; held = false;
            return true;
        }
        internal bool Clear(out string error)
        {
            CancelCapture();
            if (!save(null, out error)) return false;
            Binding = null; held = false;
            return true;
        }
        internal bool Poll(bool pressed, bool blocked)
        {
            bool edge = pressed && !held;
            held = pressed;
            if (!pressed) releaseRequired = false;
            return Binding != null && edge && !releaseRequired && !Listening && !blocked;
        }
    }
}
