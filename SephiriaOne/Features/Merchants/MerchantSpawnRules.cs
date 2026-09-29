namespace SephiriaOne
{
    // Values are captured by the host before evaluation. Definitions can add rules
    // without knowing about Unity objects, persistence or network spawning.
    internal readonly struct MerchantSpawnContext
    {
        public int FloorNumber { get; }
        public int SuccessfulSpawns { get; }
        public int Difficulty { get; }
        public string StageName { get; }

        public MerchantSpawnContext(int floorNumber, int successfulSpawns, int difficulty, string stageName)
        { FloorNumber = floorNumber; SuccessfulSpawns = successfulSpawns; Difficulty = difficulty; StageName = stageName; }
    }

    internal static class MerchantSpawnRules
    {
        internal static bool Allows(MerchantDefinition definition, MerchantSettings settings, MerchantSpawnContext context)
        {
            return settings.Enabled && (settings.FirstFloor <= 1 || context.FloorNumber >= settings.FirstFloor) &&
                (settings.MaxPerRun == 0 || context.SuccessfulSpawns < settings.MaxPerRun) &&
                (definition.Condition == null || definition.Condition(context));
        }
    }
}
