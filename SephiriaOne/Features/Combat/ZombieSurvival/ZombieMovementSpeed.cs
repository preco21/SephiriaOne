namespace SephiriaOne
{
    // Lives in the existing match roster, never a saved stat or global player map.
    // Base speed composes with native equipment, frostbite and weapon modifiers.
    internal struct ZombieMovementSpeed
    {
        private const float Scale = 0.7f;
        private float original, applied;
        private bool owned;

        internal void Update(PlayerAvatar player, bool zombie)
        {
            if (!player) { owned = false; return; }
            float current = player.moveSpeed;
            if (!zombie)
            {
                // Do not undo a newer external replacement. This also restores
                // the exact old avatar on retirement, never a reused network ID.
                if (owned && current == applied) player.NetworkmoveSpeed = original;
                owned = false;
                return;
            }
            if (owned && current == applied) return;
            // A native/mod replacement becomes the new baseline, not another
            // application of our previous multiplier. Invalid values stay native.
            owned = false;
            if (current <= 0 || float.IsNaN(current) || float.IsInfinity(current)) return;
            original = current;
            applied = current * Scale;
            owned = true;
            player.NetworkmoveSpeed = applied;
        }
    }
}
