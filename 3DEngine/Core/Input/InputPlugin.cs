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
            Logger.Warn("InputPlugin: No IInputBackend resource found - input events will not be forwarded.");
        }

        app.AddSystem(Stage.Last, new SystemDescriptor(static world =>
            {
                world.Resource<Input>().BeginFrame();
            }, "InputPlugin.BeginFrame")
            .Write<Input>());
        Logger.Info("InputPlugin: Input system registered to Last stage.");
    }
}
