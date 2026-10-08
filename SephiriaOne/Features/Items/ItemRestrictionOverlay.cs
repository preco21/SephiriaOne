#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    // A reversible projection of native metadata, independent of Unity lifetimes.
    internal sealed class ItemRestrictionOverlay
    {
        private readonly IDictionary<string, string> values;
        private readonly Dictionary<string, string> originals = new Dictionary<string, string>();
        private bool writing;
        internal bool Enabled { get; private set; }
        internal bool HasOverrides => originals.Count != 0;
        internal ItemRestrictionOverlay(IDictionary<string, string> values) { this.values = values; }
        internal void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                foreach (var entry in new List<KeyValuePair<string, string>>(values))
                    if (!originals.ContainsKey(entry.Key)) Capture(entry.Key, entry.Value);
                foreach (var entry in originals)
                    if (values.TryGetValue(entry.Key, out string current) && current == entry.Value) Write(entry.Key, "");
            }
            else
            {
                foreach (var entry in new List<KeyValuePair<string, string>>(originals))
                {
                    if (values.TryGetValue(entry.Key, out string current) && current == "") Write(entry.Key, entry.Value);
                    originals.Remove(entry.Key);
                }
            }
            Enabled = enabled;
        }

        internal void Changed(string key, bool removed)
        {
            if (writing) return;
            originals.Remove(key);
            if (Enabled && !removed && values.TryGetValue(key, out string value)) Capture(key, value);
        }
        internal void Cleared() => originals.Clear();
        internal Dictionary<string, string> NativeSnapshot()
        {
            var result = new Dictionary<string, string>(values);
            foreach (var entry in originals)
                if (result.TryGetValue(entry.Key, out string current) && current == "") result[entry.Key] = entry.Value;
            return result;
        }

        internal static bool TryKey(string key, out int id, out bool bound)
        {
            id = -1; bound = false;
            if (key == null) return false;
            int slash = key.IndexOf('/');
            if (slash <= 0 || !int.TryParse(key.Substring(0, slash), NumberStyles.None, CultureInfo.InvariantCulture, out id) ||
                id < 0 || key.Substring(0, slash) != id.ToString(CultureInfo.InvariantCulture)) return false;
            string purpose = key.Substring(slash + 1);
            bound = purpose == "Bound";
            return bound || purpose == "OwnRestriction";
        }

        private void Capture(string key, string value)
        {
            if (!TryKey(key, out _, out bool bound) || (bound ?
                !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int owner) || owner < 0 : value != "1")) return;
            originals[key] = value; // Journal before any write, including a setter that writes then throws.
            Write(key, "");
        }

        private void Write(string key, string value)
        {
            writing = true;
            try
            {
                values[key] = value;
                if (!values.TryGetValue(key, out string actual) || actual != value)
                    throw new InvalidOperationException("Item restriction metadata changed during write: " + key);
            }
            finally { writing = false; }
        }
    }
}
