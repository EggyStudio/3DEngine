namespace Engine;

/// <summary>
/// Immutable application configuration: window properties, startup command, and graphics backend.
/// Use <c>with</c> expressions or the fluent <c>With*</c> methods to derive variants.
/// </summary>
/// <example>
/// <code>
/// // Use the canonical defaults
/// var app = new App(Config.Default);
/// </code>
/// <code>
/// // Customise with named parameters
/// var cfg = Config.GetDefault(title: "My Game", width: 1920, height: 1080);
/// </code>
/// <code>
/// // Derive a variant with fluent methods
/// var headless = Config.Default
///     .WithWindow("Headless", 1, 1)
///     .WithCommand(WindowCommand.Hide)
///     .WithGraphics(GraphicsBackend.Sdl);
/// </code>
/// </example>
public sealed record Config
{
    /// <summary>
    /// Canonical defaults: "3D Engine" 600×400 window, <see cref="GraphicsBackend.Vulkan"/> backend,
    /// <see cref="WindowCommand.Show"/> on startup.
    /// </summary>
    public static Config Default { get; } = new();

    /// <summary>Initial window properties (title, size).</summary>
    public WindowData WindowData { get; init; } = new("3D Engine", 600, 400);

    /// <summary>Window action applied on startup.</summary>
    public WindowCommand WindowCommand { get; init; } = WindowCommand.Show;

    /// <summary>Desired graphics backend for the application window.</summary>
    public GraphicsBackend Graphics { get; init; } = GraphicsBackend.Vulkan;

    /// <summary>Returns a copy with the provided window properties.</summary>
    /// <param name="title">Window title bar text.</param>
    /// <param name="width">Window width in pixels.</param>
    /// <param name="height">Window height in pixels.</param>
    /// <returns>A new <see cref="Config"/> with updated window data.</returns>
    public Config WithWindow(string title, int width, int height) => 
        this with { WindowData = new(title, width, height) };

    /// <summary>Returns a copy with the provided window data.</summary>
    /// <param name="windowData">The window properties to apply.</param>
    /// <returns>A new <see cref="Config"/> with the updated window data.</returns>
    public Config WithWindow(WindowData windowData) => 
        this with { WindowData = windowData };

    /// <summary>Returns a copy with a different startup window command.</summary>
    /// <param name="command">The <see cref="WindowCommand"/> to apply on startup.</param>
    /// <returns>A new <see cref="Config"/> with the updated command.</returns>
    public Config WithCommand(WindowCommand command) => 
        this with { WindowCommand = command };

    /// <summary>Returns a copy with a different graphics backend.</summary>
    /// <param name="backend">The <see cref="GraphicsBackend"/> to use.</param>
    /// <returns>A new <see cref="Config"/> with the updated backend.</returns>
    public Config WithGraphics(GraphicsBackend backend) => 
        this with { Graphics = backend };

    /// <summary>Human-readable summary for diagnostics and logging.</summary>
    public override string ToString() => 
        $"Config {{ Window=\"{WindowData.Title}\" {WindowData.Width}x{WindowData.Height}, Graphics={Graphics}, Command={WindowCommand} }}";
}
