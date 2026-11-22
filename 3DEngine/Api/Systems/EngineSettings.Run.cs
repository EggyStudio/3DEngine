using Diagnostics;

namespace Engine;

internal static partial class EngineSettings
{
    // The config of the app whose binding was made last, which the run's settings read.
    private static Config? _started;

    // How a tool started the run, from the command line or the environment, read as the app is
    // made. Each only reads, since no start reads a value set while the program runs, so the file
    // never keeps one and no variable overrides one. Those the app's config keeps read the running
    // app's once its binding is made (Started), and the rest what their variable gives the process.
    private static IEnumerable<Setting> Run()
    {
        yield return new Setting<ulong>("run.frames", "The frames the run stops after, or 0 to run until the window closes, as --frames or E3D_FRAMES gives it.", 0,
            () => _started?.Frames ?? 0)
        { ReadOnly = true, Saved = false };
        yield return new Setting<bool>("run.serve", "Whether the run serves the e3d client, as --serve or E3D_SERVE asks.", false,
            () => _started?.Serve ?? false)
        { ReadOnly = true, Saved = false };
        yield return new Setting<bool>("run.headless", "Whether the run opens no window, as --headless or E3D_HEADLESS asks.", false,
            () => _started?.Headless ?? false)
        { ReadOnly = true, Saved = false };
        yield return new Setting<bool>("run.offscreen", "Whether the run draws into images of its own with no window, as --offscreen or E3D_OFFSCREEN asks.", false,
            () => _started?.Offscreen ?? false)
        { ReadOnly = true, Saved = false };
        yield return new Setting<bool>("run.hidden", "Whether the run's window is never shown, as --hidden or E3D_HIDDEN asks.", false,
            () => _started?.Hidden ?? false)
        { ReadOnly = true, Saved = false };
        yield return new Setting<uint>("run.seed", "The seed InitWindow gave the random generator, as --seed or E3D_SEED gives it or the clock's seconds where neither does, so a run seeded by the clock is run again with --seed.", 0,
            () => E3D.WindowSeed)
        { ReadOnly = true, Saved = false };
        yield return new Setting<int>("window.samples", "The samples a pixel the window is drawn with where the program asks none, as --samples or E3D_SAMPLES gives it.", 4,
            () => _started is { } config ? Math.Clamp(config.Samples, 1, 8) : 4)
        { ReadOnly = true, Saved = false, Low = 1, High = 8 };
        yield return new Setting<string>("run.shuffle", $"The seed each stage's systems are shuffled by as the app starts, so a run finds a system leaning on an order nothing asks for, or empty for none, as {Schedule.ShuffleSeedVariable} gives it.", "",
            () => Environment.GetEnvironmentVariable(Schedule.ShuffleSeedVariable) ?? "")
        { ReadOnly = true, Saved = false };
        yield return new Setting<string>("run.sessions", "The folder the session files of served runs are written to, which the e3d client reads, the user's local application data where empty, as E3D_SESSIONS gives it.", "",
            () => Environment.GetEnvironmentVariable("E3D_SESSIONS") ?? "")
        { ReadOnly = true, Saved = false };
        yield return new Setting<bool>("run.validation", "Whether a release build checks the device's calls against Vulkan's validation layers, which a debug build always does, as ENGINE_VULKAN_VALIDATION=1 asks.", false,
            () => Environment.GetEnvironmentVariable("ENGINE_VULKAN_VALIDATION") == "1")
        { ReadOnly = true, Saved = false };
        yield return new Setting<string>("run.slangc", "The shader compiler the run compiles with, the first slangc on the PATH where empty, as ENGINE_SLANGC gives it.", "",
            () => Environment.GetEnvironmentVariable("ENGINE_SLANGC") ?? "")
        { ReadOnly = true, Saved = false };
    }

    /// <summary>
    /// Gives the settings of how the run was started the config the app was made with, its frames,
    /// its modes and the samples, as its binding is made, so each reads the running app's.
    /// </summary>
    internal static void Started(Config config) => _started = config;
}
