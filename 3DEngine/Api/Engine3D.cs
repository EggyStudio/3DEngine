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

    private static DrawList DrawList => World.Resource<DrawList>();
}
