using System.Globalization;
using Diagnostics;

namespace Engine;

internal static partial class EngineSettings
{
    // How a tool started the run, from the command line or the environment, read as the app is
    // made, so a set waits for the next start and the file never keeps one, the next start being
    // the tool's to ask. Each holds what its variable gives, and those the app's config keeps are
    // told what the running app was made with as its binding is made (Started).
    private static IEnumerable<Setting> Run()
    {
        yield return new Setting<int>("run.frames", "The frames the run stops after, or 0 to run until the window closes, as --frames gives it.", 0)
        {
            AtNextStart = true, Saved = false, Low = 0, EnvironmentVariable = "E3D_FRAMES",
        };
        yield return new Setting<bool>("run.serve", "Whether the run serves the e3d client, as --serve asks.", false)
        {
            AtNextStart = true, Saved = false, EnvironmentVariable = "E3D_SERVE",
        };
        yield return new Setting<bool>("run.headless", "Whether the run opens no window, as --headless asks.", false)
        {
            AtNextStart = true, Saved = false, EnvironmentVariable = "E3D_HEADLESS",
        };
        yield return new Setting<bool>("run.offscreen", "Whether the run draws into images of its own with no window, as --offscreen asks.", false)
        {
            AtNextStart = true, Saved = false, EnvironmentVariable = "E3D_OFFSCREEN",
        };
        yield return new Setting<bool>("run.hidden", "Whether the run's window is never shown, as --hidden asks.", false)
        {
            AtNextStart = true, Saved = false, EnvironmentVariable = "E3D_HIDDEN",
        };
        yield return new Setting<int>("run.seed", "The seed InitWindow gives the random generator, as --seed gives it, or -1 for the clock's.", -1)
        {
            AtNextStart = true, Saved = false, Low = -1, EnvironmentVariable = "E3D_SEED",
        };
        yield return new Setting<int>("window.samples", "The samples a pixel the window is drawn with where the program asks none, as --samples gives it.", 4)
        {
            AtNextStart = true, Saved = false, Low = 1, High = 8, EnvironmentVariable = "E3D_SAMPLES",
        };
        yield return new Setting<string>("run.shuffle", "The seed each stage's systems are shuffled by as the app starts, so a run finds a system leaning on an order nothing asks for, or empty for none.", "")
        {
            AtNextStart = true, Saved = false, EnvironmentVariable = Schedule.ShuffleSeedVariable,
        };
        yield return new Setting<string>("run.sessions", "The folder the session files of served runs are written to, which the e3d client reads, the user's local application data where empty.", "")
        {
            AtNextStart = true, Saved = false, EnvironmentVariable = "E3D_SESSIONS",
        };
        yield return new Setting<bool>("run.validation", "Whether a release build checks the device's calls against Vulkan's validation layers, which a debug build always does.", false)
        {
            AtNextStart = true, Saved = false, EnvironmentVariable = "ENGINE_VULKAN_VALIDATION",
        };
        yield return new Setting<string>("run.slangc", "The shader compiler the run compiles with, the first slangc on the PATH where empty.", "")
        {
            AtNextStart = true, Saved = false, EnvironmentVariable = "ENGINE_SLANGC",
        };
    }

    /// <summary>
    /// Tells the settings of how the run was started what the app was made with, its frames, its
    /// modes, the seed and the samples, as its binding is made, so each reads the running app's.
    /// </summary>
    internal static void Started(Config config)
    {
        Start("run.frames", config.Frames > int.MaxValue ? int.MaxValue.ToString(CultureInfo.InvariantCulture) : config.Frames.ToString(CultureInfo.InvariantCulture));
        Start("run.serve", config.Serve);
        Start("run.headless", config.Headless);
        Start("run.offscreen", config.Offscreen);
        Start("run.hidden", config.Hidden);
        Start("run.seed", (RunMode.Seed() is { } seed ? (long)Math.Min(seed, int.MaxValue) : -1).ToString(CultureInfo.InvariantCulture));
        Start("window.samples", Math.Clamp(config.Samples, 1, 8).ToString(CultureInfo.InvariantCulture));
    }

    private static void Start(string name, bool on) => Start(name, on ? "on" : "off");

    private static void Start(string name, string text) => Registry.Process.FindSetting(name)?.TryStart(text);
}
