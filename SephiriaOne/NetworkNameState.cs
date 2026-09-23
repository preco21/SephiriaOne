#nullable enable

using System;

namespace SephiriaOne
{
    internal sealed class NetworkNameState
    {
        private const string Prefix = "<color=#0000FF>";
        private const string Suffix = "</color>";
        private string? pendingName;

        public string? Next(string observedName, string plainName, bool multiplayer)
        {
            if (string.IsNullOrEmpty(observedName) || string.IsNullOrEmpty(plainName))
            {
                return null;
            }

            string desiredName = multiplayer ? Blue(plainName) : Plain(plainName);
            if (observedName == desiredName && (pendingName == null || pendingName == desiredName))
            {
                pendingName = null;
                return null;
            }

            if (pendingName == desiredName)
            {
                return null;
            }

            // Also counter an in-flight colored request when returning to solo play.
            // Desired names come from the profile, so stale acknowledgments cannot
            // replace a newer name. The game's command uses reliable delivery.
            pendingName = desiredName;
            return desiredName;
        }

        public static string Blue(string name) => Prefix + Plain(name) + Suffix;

        public static string Plain(string name)
        {
            while (name.Length >= Prefix.Length + Suffix.Length &&
                name.StartsWith(Prefix, StringComparison.Ordinal) &&
                name.EndsWith(Suffix, StringComparison.Ordinal))
            {
                name = name.Substring(Prefix.Length, name.Length - Prefix.Length - Suffix.Length);
            }

            return name;
        }

        public void Reset()
        {
            pendingName = null;
        }
    }
}
