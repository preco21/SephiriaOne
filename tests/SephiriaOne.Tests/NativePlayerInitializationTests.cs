using SephiriaOne;

internal enum ERestartPlacement { Default, MultiZone, FirstStage }

internal static class NativePlayerInitializationTests
{
    internal static int Run()
    {
        int checks = 0;
        void Accept(Type type, int count)
        {
            var method = NativePlayerInitialization.ResolveInitialize(type);
            if (method.DeclaringType != type || method.GetParameters().Length != count ||
                !(bool)method.Invoke(Activator.CreateInstance(type), count == 4
                    ? new object[] { 1, "costume", "skin", 0 }
                    : new object[] { 1, "costume", "skin", 0, ERestartPlacement.FirstStage })!)
                throw new Exception("Incorrect player initialization: " + type.Name);
            checks++;
        }
        void Reject(Type type)
        {
            try { NativePlayerInitialization.ResolveInitialize(type); }
            catch (MissingMethodException) { checks++; return; }
            throw new Exception("Accepted incompatible initialization: " + type.Name);
        }
        Accept(typeof(Legacy), 4);
        Accept(typeof(Beta), 5);
        Accept(typeof(Both), 5);
        Reject(typeof(WrongReturn));
        Reject(typeof(WrongPlacement));
        Reject(typeof(Static));
        Reject(typeof(Inherited));
        return checks;
    }
    private class Legacy { private bool Initialize(int weapon, string costume, string skin, int loading) => true; }
    private sealed class Beta { private bool Initialize(int weapon, string costume, string skin, int loading, ERestartPlacement placement = ERestartPlacement.Default) => placement == ERestartPlacement.FirstStage; }
    private sealed class Both
    {
        private bool Initialize(int weapon, string costume, string skin, int loading) => false;
        private bool Initialize(int weapon, string costume, string skin, int loading, ERestartPlacement placement) => true;
    }
    private sealed class WrongReturn { private void Initialize(int weapon, string costume, string skin, int loading) { } }
    private sealed class WrongPlacement { private bool Initialize(int weapon, string costume, string skin, int loading, int placement) => true; }
    private sealed class Static { private static bool Initialize(int weapon, string costume, string skin, int loading) => true; }
    private sealed class Inherited : Legacy { }
}
