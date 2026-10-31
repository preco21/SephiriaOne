using System;
using System.Reflection;

namespace SephiriaOne
{
    internal static class NativePlayerInitialization
    {
        internal static MethodInfo ResolveInitialize(Type spawner)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                BindingFlags.DeclaredOnly | BindingFlags.ExactBinding;
            // 1.1.0 moves the actual initialization into a placement-aware method.
            // Resolve it once during hook installation; all consumers must retire
            // and reapply state at the same boundary, including direct-stage starts.
            Type placement = spawner.Assembly.GetType("ERestartPlacement");
            MethodInfo method = null;
            if (placement != null && placement.IsEnum)
                method = spawner.GetMethod("Initialize", flags, null,
                    new[] { typeof(int), typeof(string), typeof(string), typeof(int), placement }, null);
            if (method == null)
                method = spawner.GetMethod("Initialize", flags, null,
                    new[] { typeof(int), typeof(string), typeof(string), typeof(int) }, null);
            if (method == null || method.ReturnType != typeof(bool) || method.ContainsGenericParameters)
                throw new MissingMethodException("Native player initialization signature changed.");
            return method;
        }
    }
}
