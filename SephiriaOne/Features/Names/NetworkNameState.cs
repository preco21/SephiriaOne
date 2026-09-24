#nullable enable

namespace SephiriaOne
{
    internal sealed class NetworkNameState
    {
        private string? pendingName;
        private string? gradientSource;
        private int attempts;
        private double retryAt;
        public bool Exhausted { get; private set; }
        public int RetryToken(double now) => pendingName != null && now >= retryAt && !Exhausted ? attempts + 1 : attempts;
        public string? GradientName { get; private set; }

        public string? Next(string observedName, string plainName, bool multiplayer)
            => Next(observedName, plainName, multiplayer, 0);

        public string? Next(string observedName, string plainName, bool multiplayer, double now)
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
                attempts = 0;
                Exhausted = false;
                return null;
            }

            if (pendingName == desiredName)
            {
                if (now < retryAt || Exhausted) return null;
                if (attempts >= 3) { Exhausted = true; return null; }
            }
            else { attempts = 0; Exhausted = false; }

            // Also counter an in-flight colored request when returning to solo play.
            // Desired names come from the profile, so stale acknowledgments cannot
            // replace a newer name. The game's command uses reliable delivery.
            pendingName = desiredName;
            attempts++;
            retryAt = now + 2;
            return desiredName;
        }

        public static string Gradient(string name) => NameGradient.Format(name);

        public static string Plain(string name) => NameGradient.Plain(name);

        public void Reset()
        {
            pendingName = null;
            gradientSource = null;
            GradientName = null;
            attempts = 0;
            retryAt = 0;
            Exhausted = false;
        }
    }
}
