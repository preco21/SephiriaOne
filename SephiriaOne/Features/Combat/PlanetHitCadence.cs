namespace SephiriaOne
{
    // GreenBat.CreateBullet's seven serialized damage IDs. Normal, enhanced
    // and ignite-tail bullets retain the same ID, as do multiple item copies.
    // Unknown sources stay incidental; no projectile/equipment identity cache.
    internal static class PlanetHitCadence
    {
        internal const int Count = 7;

        internal static int Source(DamageInstance damage)
        {
            if (damage.fromType != EDamageFromType.None || damage.damageType != EDamageType.Projectile) return -1;
            switch (damage.id)
            {
                case "Charm_Planet_Blue": return 0;
                case "Charm_Planet_Yellow": return 1;
                case "Charm_Planet_Sky": return 2;
                case "Charm_Planet_White": return 3;
                case "Charm_Planet_Gray": return 4;
                case "Charm_Planet_Black": return 5;
                case "Charm_Planet_Red": return 6;
                default: return -1;
            }
        }
    }
}
