namespace SephiriaOne
{
    // Audited emitter identities, not element/category/prefix matching. Frost
    // Relic includes armaments AND Guillotine; the latter is an explicit exception.
    internal static class AutonomousArtifactCatalog
    {
        internal const int Count = 7;
        internal static int Index(DamageInstance damage)
        {
            if (damage == null || damage.fromType != EDamageFromType.None || damage.damageType != EDamageType.Projectile) return -1;
            switch (damage.id)
            {
                case "Charm_Guillotine": return 0; // Both ice and converted fire.
                case "Charm_ElectricEarring": return 1;
                case "Charm_Meteor": return 2; // Red Snake Eyes explosion.
                case "Charm_FireFeather": return 3;
                case "Charm_Chakram": return 4;
                case "Charm_EchoOfTheGlacier": return 5;
                case "Charm_IceBat": return 6;
                default: return -1;
            }
        }
    }

    internal static partial class FriendlyFireRuntime
    {
        private static int CurrentAutonomousArtifact() => current.Reflection || current.InDebuffDamage || current.InBurnExplosion ||
            !ReferenceEquals(current.Source, current.Attacker) || !(current.Victim is PlayerAvatar) ? -1 :
            AutonomousArtifactCatalog.Index(current.Damage);

        // Read-only early gate: prevent rejected repeats from spending guard MP
        // or entering ordinary impact callbacks. Native veto/evade/zero-damage
        // attempts do not acquire a clock. Reservation is at positive Scale.
        private static bool AutonomousArtifactReady(PvpArtifactRateSettings settings)
        {
            if (!settings.Enabled) return true;
            int source = CurrentAutonomousArtifact();
            if (source < 0) return true;
            var window = CurrentBalanceWindow();
            return window == null || window.ArtifactReady(source, UnityEngine.Time.time, settings.Interval);
        }

        private static float DarkCloudInterval(FriendlyFireSettings settings) => settings.ArtifactRate.Enabled ?
            System.Math.Max(settings.ArtifactRate.Interval, settings.DotIntervals.Interval(PvpDotEffect.DarkCloud)) : settings.DotIntervals.Interval(PvpDotEffect.DarkCloud);
    }
}
