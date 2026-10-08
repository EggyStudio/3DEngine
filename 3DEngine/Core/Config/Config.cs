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
/// // Customize with named parameters
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
    internal WindowData WindowData { get; init; } = new("3D Engine", 600, 400);

    /// <summary>Window action applied on startup.</summary>
    internal WindowCommand WindowCommand { get; init; } = WindowCommand.Show;

    /// <summary>Desired graphics backend for the application window.</summary>
    public GraphicsBackend Graphics { get; init; } = GraphicsBackend.Vulkan;

    /// <summary>
    /// Whether the app answers the <c>e3d</c> command line on a local socket. Also set by
    /// <c>--serve</c> or <c>E3D_SERVE=1</c> (see <see cref="RunMode"/>).
    /// </summary>
    public bool Serve { get; init; }

    /// <summary>
    /// Whether the app runs with no window and no renderer, so the schedule, the ECS and the flat
    /// API's logic run and nothing is drawn. Also set by <c>--headless</c> or <c>E3D_HEADLESS=1</c>.
    /// </summary>
    public bool Headless { get; init; }

    /// <summary>
    /// Whether a run with no window still renders, into images of the device's own, so frames can
    /// be captured with no display at all, as on a CI machine. Implies <see cref="Headless"/>.
    /// Needs a Vulkan device, which may be a software one such as lavapipe. Also set by
    /// <c>--offscreen</c> or <c>E3D_OFFSCREEN=1</c>.
    /// </summary>
    public bool Offscreen { get; init; }

    /// <summary>
    /// Whether the window is created hidden. Frames are still rendered, so screenshots work, but
    /// nothing appears on the desktop. Also set by <c>--hidden</c> or <c>E3D_HIDDEN=1</c>.
    /// </summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// How many frames to run before closing, or 0 to run until asked to close. Also set by
    /// <c>--frames N</c> or <c>E3D_FRAMES=N</c>.
    /// </summary>
    public ulong Frames { get; init; }

    /// <summary>
    /// How many samples a pixel of the window and of a render target is drawn with, to smooth the
    /// stair steps along triangle edges: 1 for none, or 2, 4 or 8, rounded down to what the device
    /// can do. Defaults to 4, which every desktop device supports.
    /// </summary>
    public int Samples { get; init; } = 4;

    /// <summary>Whether the window can be resized by dragging its edges. Defaults to true.</summary>
    public bool Resizable { get; init; } = true;

    /// <summary>Whether the window has no title bar or border.</summary>
    public bool Undecorated { get; init; }

    /// <summary>Whether the window stays above other windows.</summary>
    public bool Topmost { get; init; }

    /// <summary>Whether the window fills its display when shown.</summary>
    public bool Fullscreen { get; init; }

    /// <summary>
    /// Whether the window is drawn at the monitor's pixels where the monitor scales them, as on
    /// Wayland and macOS, rather than at its size and scaled up by the desktop.
    /// </summary>
    public bool HighPixelDensity { get; init; }

    /// <summary>Whether the desktop shows through where the window's frame is clear, where the desktop composites windows.</summary>
    public bool Transparent { get; init; }

    /// <summary>Whether the window leaves the keyboard focus where it is when shown.</summary>
    public bool Unfocused { get; init; }

    /// <summary>
    /// Whether frames are presented in step with the display's refresh, which caps the frame rate
    /// at it and removes tearing. Off, frames are presented as soon as they are ready.
    /// </summary>
    public bool Vsync { get; init; }

    /// <summary>Frames per second a headless app runs at, so it does not spin a core. Defaults to 60.</summary>
    public double HeadlessFps { get; init; } = 60;

    /// <summary>
    /// Seconds each frame advances time by, when above 0, in place of the clock, so a run steps the
    /// same on every machine, frame for frame, as a test or a capture of something moving needs.
    /// Also set by <c>--frame-time SECONDS</c> or <c>E3D_FRAME_TIME</c>. <see cref="Time.FrameSeconds"/>
    /// changes it while the app runs.
    /// </summary>
    public double FrameSeconds { get; init; }

    /// <summary>
    /// The scene's distance field the app starts with, which ambient occlusion, the sun's contact
    /// shadows and particles read, none unless its cascades are set.
    /// </summary>
    public SceneFieldConfig SceneField { get; init; } = new();

    /// <summary>
    /// How much light bounces between surfaces when the app starts, none by default, which turns
    /// the scene's distance field on at four cascades where <see cref="SceneField"/> has none.
    /// </summary>
    public GlobalIllumination GlobalIllumination { get; init; }

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
    internal Config WithWindow(WindowData windowData) => 
        this with { WindowData = windowData };

    /// <summary>Returns a copy with a different startup window command.</summary>
    /// <param name="command">The <see cref="WindowCommand"/> to apply on startup.</param>
    /// <returns>A new <see cref="Config"/> with the updated command.</returns>
    internal Config WithCommand(WindowCommand command) => 
        this with { WindowCommand = command };

    /// <summary>Returns a copy with a different graphics backend.</summary>
    /// <param name="backend">The <see cref="GraphicsBackend"/> to use.</param>
    /// <returns>A new <see cref="Config"/> with the updated backend.</returns>
    public Config WithGraphics(GraphicsBackend backend) => 
        this with { Graphics = backend };

    /// <summary>Human-readable summary for diagnostics and logging.</summary>
    public override string ToString() => 
        $"Config {{ Window=\"{WindowData.Title}\" {WindowData.Width}x{WindowData.Height}, Graphics={Graphics}, Command={WindowCommand}, Mode={RunMode.Describe(this)}, Serve={Serve}, Frames={Frames} }}";
}
