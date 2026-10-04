namespace Engine;

/// <summary>
/// The flat API: every operation a program needs as one static method, in the manner of raylib.
/// Import it with <c>using static Engine.Engine3D;</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="InitWindow"/> builds an <see cref="Engine.App"/> with <see cref="DefaultPlugins"/>,
/// and the program's own loop drives it one frame at a time through <see cref="BeginDrawing"/>
/// and <see cref="EndDrawing"/>. Anything registered on <see cref="GetApp"/> (plugins, systems,
/// behaviors) runs inside those frames, so the flat API and the ECS share one world and one frame.
/// </para>
/// <para>
/// The class is split into a file per area, mirroring raylib's modules. Every public function
/// has its line in <c>.github/CHEATSHEET.md</c>.
/// </para>
/// </remarks>
public static partial class Engine3D
{
    private static App? _app;

    /// <summary>The app <see cref="InitWindow"/> built, for adding plugins, systems and resources.</summary>
    /// <remarks>
    /// A method rather than a property named <c>App</c>, because a program with <c>using Engine;</c>
    /// would have that name resolve to the <see cref="Engine.App"/> type instead.
    /// </remarks>
    /// <exception cref="InvalidOperationException"><see cref="InitWindow"/> has not been called.</exception>
    public static App GetApp() => _app ?? throw new InvalidOperationException("Call InitWindow before using the engine.");

    private static World World => GetApp().World;

    /// <summary>
    /// Runs the flat API against an app the caller built, or against none, so tests can exercise
    /// the functions that need no window.
    /// </summary>
    internal static void UseApp(App? app) => _app = app;

    private static DrawList DrawList => Res<DrawList>();

    // A resource as the flat API reads it, kept from the last lookup until the world or its
    // resources change. A function like GetScreenWidth or DrawTexture is called thousands of times
    // a frame in ordinary code, where a lookup each call cost a measurable part of the frame
    // (RENDERING.md section 6). The flat API is called from the program's thread, as raylib's is.
    private static class Cached<T> where T : notnull
    {
        public static World? World;
        public static int Version;
        public static bool Found;
        public static T Value = default!;
    }

    private static T Res<T>() where T : notnull =>
        TryRes<T>(out var value) ? value : throw new InvalidOperationException($"Resource of type {typeof(T).Name} not found.");

    private static bool TryRes<T>(out T value) where T : notnull
    {
        var world = World;
        var version = world.ResourceVersion;
        if (!ReferenceEquals(Cached<T>.World, world) || Cached<T>.Version != version)
        {
            Cached<T>.Found = world.TryGetResource(out Cached<T>.Value);
            Cached<T>.World = world;
            Cached<T>.Version = version;
        }
        value = Cached<T>.Value;
        return Cached<T>.Found;
    }
}
