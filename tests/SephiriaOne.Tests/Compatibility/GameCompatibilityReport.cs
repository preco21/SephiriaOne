using System.Reflection;
using System.Runtime.Loader;

// Audit all installed-code contracts even when an earlier feature fails. This
// mode inspects shipped IL; it does not launch Unity or install runtime patches.
internal static class GameCompatibilityReport
{
    internal static bool Run(string managed, string addonPath)
    {
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var addon = Assembly.LoadFrom(Path.GetFullPath(addonPath));
        var failures = new List<string>();
        int passed = 0;
        foreach (var type in typeof(GameCompatibilityReport).Assembly.GetTypes().OrderBy(t => t.Name))
        {
            if (!type.Name.StartsWith("Game") || !type.Name.EndsWith("Tests") ||
                type == typeof(GameChoiceCompatibilityTests) || type == typeof(GameHarmonyRuntimeTests)) continue;
            var run = type.GetMethod("Run", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (run == null) continue;
            var parameters = run.GetParameters();
            if (parameters.Any(p => p.ParameterType != typeof(Assembly) || p.Name is not ("game" or "addon")))
                throw new InvalidOperationException("Uncovered compatibility report signature: " + type.Name);
            try
            {
                run.Invoke(null, parameters.Select(p => (object)(p.Name == "game" ? game : addon)).ToArray());
                passed++;
            }
            catch (Exception error)
            {
                while (error is TargetInvocationException { InnerException: not null }) error = error.InnerException;
                failures.Add(type.Name + ": " + error.GetType().Name + ": " + error.Message);
            }
        }
        Console.WriteLine($"Compatibility report: {passed} groups passed; {failures.Count} failed.");
        foreach (string failure in failures) Console.WriteLine("FAIL " + failure);
        return failures.Count == 0;
    }
}
