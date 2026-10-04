namespace Engine;

/// <summary>Registers the <see cref="Input"/> resource and wires up the platform input backend.</summary>
/// <remarks>
/// During <see cref="IPlugin.Build"/>, this plugin initialises the <see cref="Input"/> resource,
/// looks up the optional <see cref="IInputBackend"/> resource from the world, and registers a
/// <see cref="Stage.Last"/> system that clears per-frame transient state (pressed/released sets,
/// mouse deltas, wheel, text input) at the end of each frame.
/// </remarks>
/// <seealso cref="Input"/>
/// <seealso cref="IInputBackend"/>
/// <seealso cref="Key"/>
/// <seealso cref="MouseButton"/>
public sealed class InputPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Input");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("InputPlugin: Registering Input resource...");
        app.World.InitResource<Input>();

        var input = app.World.Resource<Input>();

        if (app.World.TryGetResource<IInputBackend>(out var backend))
        {
            Logger.Info($"InputPlugin: Wiring input backend: {backend.GetType().Name}");
            backend.Initialize(app, input);
        }
        else
        {
            // A headless or offscreen run has no window to read, and input reaches it only through
            // the console's queue, so this is how such a run starts rather than a fault.
            Logger.Info("InputPlugin: No IInputBackend resource found - only queued input will arrive.");
        }

        app.AddSystem(Stage.Last, new SystemDescriptor(static world =>
            {
                world.Resource<Input>().BeginFrame();
            }, "InputPlugin.BeginFrame")
            .Write<Input>());

        // Gestures from the frame's fingers, or the left mouse button with none, in fractions of
        // the window, once the frame's events are in.
        app.World.InitResource<Gestures>();
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(static world =>
            {
                if (!world.TryGetResource<Time>(out var time)) return;
                var input = world.Resource<Input>();
                var (width, height) = world.TryGetResource<AppWindow>(out var window)
                    ? (window.Sdl.Width, window.Sdl.Height)
                    : world.TryGetResource<Config>(out var config) ? (config.WindowData.Width, config.WindowData.Height) : (1, 1);
                var size = new System.Numerics.Vector2(Math.Max(1, width), Math.Max(1, height));
                Span<System.Numerics.Vector2> points = stackalloc System.Numerics.Vector2[Math.Min(input.Touches.Count, 10)];
                for (int i = 0; i < points.Length; i++) points[i] = input.Touches[i].Position / size;
                if (input.Touches.Count == 0 && input.MouseDown(MouseButton.Left))
                {
                    Span<System.Numerics.Vector2> mouse = [new System.Numerics.Vector2(input.MouseX, input.MouseY) / size];
                    world.Resource<Gestures>().Update(mouse, time.ElapsedSeconds);
                    return;
                }
                world.Resource<Gestures>().Update(points, time.ElapsedSeconds);
            }, "InputPlugin.Gestures")
            .Read<Input>()
            .Write<Gestures>());
        Logger.Info("InputPlugin: Input system registered to Last stage.");
    }
}
