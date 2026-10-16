namespace SephiriaOne
{
    // Weapon attack IDs are shared by unrelated attacks. Exact prefab keys
    // disambiguate the audited native homing/proximity consumers. Read these
    // from the current pooled projectile, never the owner's equipped weapon.
    internal static class FriendlyFireProjectileIdentity
    {
        internal static bool HasWeaponHoming(Bullet bullet)
        {
            if (bullet.FromType == EDamageFromType.Magic)
                return bullet.damageId == "KatanaMagicBlade" && bullet.spawnedPrefabName == "MagicBlade";
            if (bullet.FromType != EDamageFromType.DirectAttack) return false;
            switch (bullet.damageId)
            {
                case "Weapon_BasicAttack":
                    return bullet.spawnedPrefabName == "ThrowingSpear_Homing" || bullet.spawnedPrefabName == "ShurikenWeapon_Comp_Last";
                case "Weapon_DashAttack": return bullet.spawnedPrefabName == "ShurikenWeapon_Comp";
                case "BasicAttack": return bullet.spawnedPrefabName == "MiniBallistaArrow";
                case "Weapon_SpecialAttack":
                    switch (bullet.spawnedPrefabName)
                    {
                        case "FuryMPBullet_Physical": case "FuryMPBullet_Fire":
                        case "FuryMPBullet_Ice": case "FuryMPBullet_Lightning":
                        case "CrossbowArrow_Special_BigHoming":
                        case "CrossbowMine_Drone": case "CrossbowMine_DroneEnhanced":
                        case "MagicalMissile_SweepBullet": case "MagicalMissile_SweepBullet_Fire":
                        case "MagicalMissile_SweepBullet_Ice": case "MagicalMissile_SweepBullet_Lightning":
                        case "MagicalMissile_SweepBullet_Melee": case "MagicalMissile_SweepBullet_Chaos": return true;
                        default: return false;
                    }
                default: return false;
            }
        }

        internal static bool IsCrossbowMine(Bullet bullet)
        {
            if (!bullet || bullet.FromType != EDamageFromType.DirectAttack) return false;
            switch (bullet.spawnedPrefabName)
            {
                case "CrossbowMine": case "CrossbowMine_Fire": case "CrossbowMine_Poison": return bullet.damageId == "Weapon_DashAttack";
                case "CrossbowMine_Drone": case "CrossbowMine_DroneEnhanced": return bullet.damageId == "Weapon_SpecialAttack";
                default: return false;
            }
        }
    }
}
