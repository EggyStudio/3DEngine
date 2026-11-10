using System.Reflection;
using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// The engine's assemblies, the flat API's and each area's, loaded whole where an app is made or a
/// registry is read, so what each registers from its module initializer, its console commands and
/// its scene components, is there before anything looks it up.
/// </summary>
/// <remarks>
/// A project's generated registrations run when its assembly is first touched, so without this a
/// scene file holding a body, read before anything touched the physics, would find no codec for it,
/// and the console would not know the renderer's commands in a program that has not drawn yet. The
/// names are kept here as text, below every project, since Core references none of them, and the
/// norm test of the projects' layers holds the list to the project files.
/// </remarks>
internal static class EngineAssemblies
{
    /// <summary>Every assembly of the engine, by name, the lowest first.</summary>
    internal static readonly string[] Names =
    [
        "3DEngine.Core", "3DEngine.Diagnostics", "3DEngine.Graphics", "3DEngine.Platform", "3DEngine.Assets", "3DEngine.CliServer",
        "3DEngine.Audio", "3DEngine.Rendering", "3DEngine.Physics", "3DEngine.Scenes", "3DEngine.Modeling", "3DEngine.Gui", "3DEngine.GlobalIllumination", "3DEngine",
    ];

    private static readonly object Gate = new();
    private static volatile bool _loaded;

    /// <summary>Loads each assembly of the engine the program has and runs its module initializer, once.</summary>
    /// <remarks>
    /// A module initializer that reads a registry on the thread loading it finds the loading done and
    /// goes on, and another thread waits until every assembly is loaded.
    /// </remarks>
    internal static void Load()
    {
        if (_loaded) return;
        lock (Gate)
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var name in Names)
            {
                try
                {
                    var assembly = Assembly.Load(new AssemblyName(name));
                    RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
                }
                catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException)
                {
                    // A program built on some of the areas' packages and not the rest has fewer.
                }
            }
        }
    }

    /// <summary>Whether <paramref name="assembly"/> is one of the engine's own.</summary>
    internal static bool Holds(Assembly assembly) => Names.Contains(assembly.GetName().Name);

    /// <summary>The engine's assemblies the program has, which a script compiled while it runs references.</summary>
    internal static IEnumerable<Assembly> Loaded()
    {
        Load();
        return AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && Holds(assembly));
    }
}
