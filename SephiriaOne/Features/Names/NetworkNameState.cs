#nullable enable

namespace SephiriaOne
{
    internal sealed class NetworkNameState
    {
        private string? pendingName;
        private string? gradientSource;
        public string? GradientName { get; private set; }

        public string? Next(string observedName, string plainName, bool multiplayer)
        {
            if (string.IsNullOrEmpty(observedName) || string.IsNullOrEmpty(plainName))
            {
                return null;
            }

            plainName = Plain(plainName);
            if (gradientSource != plainName)
            {
                gradientSource = plainName;
                GradientName = Gradient(plainName);
            }
            string desiredName = multiplayer ? GradientName! : plainName;
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

        public static string Gradient(string name) => NameGradient.Format(name);

        public static string Plain(string name) => NameGradient.Plain(name);

        public void Reset()
        {
            pendingName = null;
            gradientSource = null;
            GradientName = null;
        }
    }
}
