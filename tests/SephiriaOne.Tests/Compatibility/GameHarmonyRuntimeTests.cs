using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;

internal static class GameHarmonyRuntimeTests
{
    // Check the compiled API references against a co-installed implementation.
    // HarmonyX 2.9's runtime detour engine cannot initialize on this .NET runner;
    // metadata inspection avoids running it, Unity, hooks or network methods.
    internal static void Run(string managed, string addonPath, string harmonyPath)
    {
        var context = new RuntimeContext(Path.GetFullPath(managed), Path.GetFullPath(harmonyPath));
        try
        {
            var harmony = context.LoadFromAssemblyPath(Path.GetFullPath(harmonyPath));
            var addon = Assembly.LoadFrom(Path.GetFullPath(addonPath));
            var failures = new HashSet<string>();
            var checkedMembers = new HashSet<MemberInfo>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            foreach (var type in addon.GetTypes())
            {
                var methods = type.GetMethods(flags | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                    .Concat(type.GetConstructors(flags | BindingFlags.DeclaredOnly));
                if (type.TypeInitializer != null) methods = methods.Append(type.TypeInitializer);
                foreach (var method in methods.Distinct())
                {
                    if (method.GetMethodBody() == null) continue;
                    // Supply a neutral generator: original methods can be open
                    // generics, which Harmony's default generator cannot clone.
                    var generator = new DynamicMethod("ApiScan", typeof(void), Type.EmptyTypes).GetILGenerator();
                    foreach (var instruction in PatchProcessor.GetOriginalInstructions(method, generator))
                    {
                        if (instruction.operand is not MemberInfo member || member.DeclaringType?.Assembly.GetName().Name != "0Harmony") continue;
                        if (!checkedMembers.Add(member)) continue;
                        var alternate = harmony.GetType(member.DeclaringType.FullName!);
                        bool found = member switch
                        {
                            MethodInfo m => alternate?.GetMethods(flags).Any(candidate => SameMethod(m, candidate)) == true,
                            ConstructorInfo c => alternate?.GetConstructors(flags).Any(candidate => SameMethod(c, candidate)) == true,
                            FieldInfo f => alternate?.GetFields(flags).Any(candidate => candidate.Name == f.Name &&
                                candidate.IsStatic == f.IsStatic && TypeKey(candidate.FieldType) == TypeKey(f.FieldType)) == true,
                            _ => false
                        };
                        if (!found) failures.Add($"{member.DeclaringType.FullName}.{member} (used by {type.FullName}.{method.Name})");
                    }
                }
            }
            if (checkedMembers.Count == 0) throw new Exception("No compiled Harmony references inspected");
            if (failures.Count > 0) throw new Exception("Co-installed Harmony validation failed:\n" + string.Join("\n", failures));
            Console.WriteLine($"Verified {checkedMembers.Count} compiled Harmony API references against {harmony.FullName} (metadata only, not Mono execution).");
        }
        finally { context.Unload(); }
    }

    private static bool SameMethod(MethodBase expected, MethodBase candidate)
    {
        if (expected is MethodInfo e && e.IsGenericMethod) expected = e.GetGenericMethodDefinition();
        if (candidate is MethodInfo c && c.IsGenericMethod) candidate = c.GetGenericMethodDefinition();
        return expected.Name == candidate.Name && expected.IsStatic == candidate.IsStatic &&
            (expected is MethodInfo eg ? eg.GetGenericArguments().Length : 0) ==
                (candidate is MethodInfo cg ? cg.GetGenericArguments().Length : 0) &&
            expected.GetParameters().Select(p => TypeKey(p.ParameterType)).SequenceEqual(candidate.GetParameters().Select(p => TypeKey(p.ParameterType))) &&
            (expected is not MethodInfo em || candidate is MethodInfo cm && TypeKey(em.ReturnType) == TypeKey(cm.ReturnType));
    }

    private static string TypeKey(Type type) => type.HasElementType
        ? TypeKey(type.GetElementType()!) + (type.IsByRef ? "&" : type.IsPointer ? "*" : "[" + new string(',', type.GetArrayRank() - 1) + "]")
        : type.IsGenericParameter ? (type.DeclaringMethod == null ? "!" : "!!") + type.GenericParameterPosition
        : type.IsGenericType ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(TypeKey)) + ">"
        : type.FullName!;

    private sealed class RuntimeContext : AssemblyLoadContext
    {
        private readonly string managed, harmonyPath;
        internal RuntimeContext(string managed, string harmonyPath) : base(isCollectible: true)
        { this.managed = managed; this.harmonyPath = harmonyPath; }

        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name == "0Harmony") return LoadFromAssemblyPath(harmonyPath);
            // Runtime framework assemblies are shared. Game and alternate
            // Harmony dependencies stay isolated from the normal test package.
            if (name.Name is "mscorlib" or "netstandard" || name.Name!.StartsWith("System", StringComparison.Ordinal)) return null;
            foreach (string folder in new[] { Path.GetDirectoryName(harmonyPath)!, managed })
            {
                string candidate = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(candidate)) return LoadFromAssemblyPath(candidate);
            }
            return null;
        }
    }
}
