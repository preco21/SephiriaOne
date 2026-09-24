#nullable enable
using System.Collections.Generic;

namespace SephiriaOne
{
    // Rebuilt from the current native roster before presentation reconciliation.
    // A platform nickname is display text, never an identity key.
    internal sealed class NameStyleDirectory
    {
        private readonly Dictionary<ulong, bool> styles = new Dictionary<ulong, bool>();
        public void Clear() => styles.Clear();
        public void Observe(ulong id, string? nativeName, bool own)
        {
            if (id == 0) return;
            if (styles.ContainsKey(id)) { styles[id] = false; return; }
            styles.Add(id, own || IsStyled(nativeName));
        }
        public bool UseGradient(ulong id) => id != 0 && styles.TryGetValue(id, out bool styled) && styled;
        public static bool IsStyled(string? name) => !string.IsNullOrEmpty(name) && NameGradient.Plain(name!) != name;
    }
}
