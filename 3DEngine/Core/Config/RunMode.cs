namespace Engine;

/// <summary>
/// Reads how a run was asked to go from the command line and the environment, so any program
/// built on the engine can be served, run headless or hidden, or stopped after a number of frames,
/// with no code of its own.
/// </summary>
/// <remarks>
/// <para>
/// The flags are <c>--serve</c>, <c>--headless</c>, <c>--offscreen</c>, <c>--hidden</c>,
/// <c>--frames N</c>, <c>--frame-time SECONDS</c>, <c>--samples N</c> and <c>--seed N</c>, and the variables
/// <c>E3D_SERVE</c>, <c>E3D_HEADLESS</c>, <c>E3D_OFFSCREEN</c>, <c>E3D_HIDDEN</c> (any of <c>1</c>,
/// <c>true</c>, <c>yes</c>, <c>on</c>), <c>E3D_FRAMES</c>, <c>E3D_FRAME_TIME</c>, <c>E3D_SAMPLES</c> and <c>E3D_SEED</c>. A flag or variable only turns a mode on, so a
/// program that sets <see cref="Config.Serve"/> itself stays served.
/// </para>
/// <para>
/// <c>e3d open</c> passes the flags it needs, which is how the command line starts an app it can
/// reach.
/// </para>
/// </remarks>
internal static class RunMode
{
    /// <summary>The arguments read, which tests replace.</summary>
    internal static Func<string[]> Arguments { get; set; } = Environment.GetCommandLineArgs;

    /// <summary>The environment read, which tests replace.</summary>
    internal static Func<string, string?> Variable { get; set; } = Environment.GetEnvironmentVariable;

    /// <summary>Returns <paramref name="config"/> with the modes the command line and environment ask for turned on.</summary>
    public static Config Apply(Config config)
    {
        var arguments = Arguments();
        var frames = config.Frames;
        var index = Array.IndexOf(arguments, "--frames");
        if (index >= 0 && index + 1 < arguments.Length && ulong.TryParse(arguments[index + 1], out var given)) frames = given;
        else if (ulong.TryParse(Variable("E3D_FRAMES"), out var variable)) frames = variable;

        var frameSeconds = config.FrameSeconds;
        var at = Array.IndexOf(arguments, "--frame-time");
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        if (at >= 0 && at + 1 < arguments.Length && double.TryParse(arguments[at + 1], invariant, out var seconds)) frameSeconds = seconds;
        else if (double.TryParse(Variable("E3D_FRAME_TIME"), invariant, out var variableSeconds)) frameSeconds = variableSeconds;

        var offscreen = config.Offscreen || Asked(arguments, "--offscreen", "E3D_OFFSCREEN");
        return config with
        {
            Serve = config.Serve || Asked(arguments, "--serve", "E3D_SERVE"),
            Headless = config.Headless || offscreen || Asked(arguments, "--headless", "E3D_HEADLESS"),
            Offscreen = offscreen,
            Hidden = config.Hidden || Asked(arguments, "--hidden", "E3D_HIDDEN"),
            Frames = frames,
            FrameSeconds = Math.Max(0, frameSeconds),
        };
    }

    /// <summary>
    /// The samples a pixel a window is drawn with when its program asks for none, from
    /// <c>--samples N</c> or <c>E3D_SAMPLES</c>, or <paramref name="fallback"/>.
    /// </summary>
    /// <remarks>
    /// One draws as raylib does with no flag asked for, which build/raylib-bench/compare.py runs the
    /// examples at, and a program's <c>FLAG_MSAA_4X_HINT</c> or <c>SetConfigSamples</c> still decides.
    /// </remarks>
    public static int Samples(int fallback)
    {
        var arguments = Arguments();
        var at = Array.IndexOf(arguments, "--samples");
        if (at >= 0 && at + 1 < arguments.Length && int.TryParse(arguments[at + 1], out var given)) return Math.Clamp(given, 1, 8);
        return int.TryParse(Variable("E3D_SAMPLES"), out var variable) ? Math.Clamp(variable, 1, 8) : fallback;
    }

    /// <summary>
    /// The seed <see cref="Engine3D.InitWindow"/> gives the random generator, from <c>--seed N</c>
    /// or <c>E3D_SEED</c>, or <c>null</c> for the clock's, as raylib's takes it.
    /// </summary>
    /// <remarks>
    /// build/raylib-bench/compare.py gives a pair's two programs the same, so what they place at
    /// random is placed alike.
    /// </remarks>
    public static uint? Seed()
    {
        var arguments = Arguments();
        var at = Array.IndexOf(arguments, "--seed");
        if (at >= 0 && at + 1 < arguments.Length && uint.TryParse(arguments[at + 1], out var given)) return given;
        return uint.TryParse(Variable("E3D_SEED"), out var variable) ? variable : null;
    }

    /// <summary><c>offscreen</c>, <c>headless</c>, <c>hidden</c> or <c>window</c>.</summary>
    public static string Describe(Config config) =>
        config.Offscreen ? "offscreen" : config.Headless ? "headless" : config.Hidden ? "hidden" : "window";

    /// <summary>Whether the world has a renderer that draws.</summary>
    public static bool HasRenderer(World world) =>
        world.TryGetResource<Renderer>(out var renderer) && renderer.Context.IsInitialized;

    private static bool Asked(string[] arguments, string flag, string variable) =>
        arguments.Contains(flag) || Variable(variable)?.ToLowerInvariant() is "1" or "true" or "yes" or "on";
}
