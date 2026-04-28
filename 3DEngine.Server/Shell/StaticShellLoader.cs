using System.Reflection;

namespace Editor.Shell;

/// <summary>
/// Discovers compile-time-registered editor shells by reflecting across every loaded assembly
/// for static methods marked with <see cref="GeneratedShellRegistrationAttribute"/>, and invokes
/// each with a <see cref="ShellRegistry"/>. Mirrors the
/// <c>BehaviorsPlugin</c> reflection scan used by the ECS behaviors module.
/// </summary>
/// <remarks>
/// Each generated registration method is emitted by the <c>EditorShellGenerator</c> source
/// generator (one method per consuming assembly) and contributes a
/// <see cref="ShellSourceIds.Static"/>-keyed <see cref="ShellSource"/> to the registry.
/// </remarks>
/// <seealso cref="GeneratedShellRegistrationAttribute"/>
/// <seealso cref="ShellRegistry"/>
public static class StaticShellLoader
{
    /// <summary>Reflects across loaded assemblies and invokes every generated registration method.</summary>
    /// <param name="registry">The registry that receives the static contributions.</param>
    /// <returns>The number of registration methods successfully invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public static int LoadInto(ShellRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var attrType = typeof(GeneratedShellRegistrationAttribute);
        var registryType = typeof(ShellRegistry);

        int invoked = 0;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;

            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }
            catch
            {
                continue;
            }

            foreach (var t in types)
            {
                if (t is null) continue;
                MethodInfo[] methods;
                try
                {
                    methods = t.GetMethods(flags);
                }
                catch
                {
                    continue;
                }

                foreach (var m in methods)
                {
                    if (m.GetCustomAttributes(attrType, inherit: false).Length == 0) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType != registryType) continue;
                    try
                    {
                        m.Invoke(null, new object[] { registry });
                        invoked++;
                    }
                    catch
                    {
                        // Swallow; one bad registration must not prevent others from running.
                    }
                }
            }
        }

        return invoked;
    }
}