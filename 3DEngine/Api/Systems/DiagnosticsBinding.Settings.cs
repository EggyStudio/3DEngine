using System.Reflection;
using Diagnostics;

namespace Engine;

internal sealed partial class DiagnosticsBinding
{
    private static readonly ILogger SettingsLogger = Log.Category("Engine.Settings");

    // The settings file read at the start, and the saved settings whose change writes it.
    private readonly Lock _fileGate = new();
    private SettingsFile? _file;
    private readonly List<Setting> _watched = [];

    /// <summary>
    /// The path of a run's settings file, the program's file in the player's folder where its
    /// window is shown, or none where the run has no window or one never shown, as a tool or a test
    /// drives, so such a run starts from the defaults and leaves the player's file as it was.
    /// </summary>
    internal static Func<Config, string?> SettingsPath { get; set; } = ShownProgram;

    // The file of a program of the entry assembly's name, for a run whose window is shown.
    private static string? ShownProgram(Config config) => config.Headless || config.Hidden || config.Offscreen ? null
        : SettingsFile.For(Assembly.GetEntryAssembly()?.GetName().Name ?? "3DEngine").Path;

    /// <summary>
    /// Reads the settings file into the registry's settings once in <see cref="Stage.Startup"/>,
    /// with the world lent so each reaches the app, and writes it again from then on as a saved
    /// setting changes.
    /// </summary>
    internal static void OpenSettings(World world, App app)
    {
        if (!world.TryGetResource<Config>(out var config) || SettingsPath(config) is not { } path) return;
        var binding = Of(world);
        var file = new SettingsFile(path);
        IReadOnlyList<string> refused;
        try
        {
            using (ConsoleHost.Lend(world, app)) refused = file.Read(ConsoleHost.Registry);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be read is never written over, so the values it holds stay.
            SettingsLogger.Warn($"The settings file {path} could not be read, so every setting starts at its default. {error.Message}");
            return;
        }

        foreach (var line in refused) SettingsLogger.Warn($"{line} The setting starts at its default.");
        lock (binding._fileGate)
        {
            binding._file = file;
            foreach (var setting in ConsoleHost.Registry.Settings)
            {
                if (!setting.Saved) continue;
                setting.Changed += binding.WriteSettings;
                binding._watched.Add(setting);
            }
        }
    }

    // Writes the settings file once more and stops writing it on a change, as the app shuts down.
    private void CloseSettings()
    {
        lock (_fileGate)
        {
            foreach (var setting in _watched) setting.Changed -= WriteSettings;
            _watched.Clear();
        }
        WriteSettings(null);
        lock (_fileGate) _file = null;
    }

    // Writes the file where its text would change, the failure to write it a warning in the log.
    private void WriteSettings(Setting? changed)
    {
        lock (_fileGate)
        {
            if (_file is not { } file) return;
            try
            {
                file.Write(ConsoleHost.Registry);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                SettingsLogger.Warn($"The settings file {file.Path} could not be written{(changed is null ? "" : $" for {changed.Name}")}. {error.Message}");
            }
        }
    }
}
