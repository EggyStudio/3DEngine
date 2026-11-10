namespace Engine;

/// <summary>
/// Global illumination added to the renderer: its settings from the config the app starts with,
/// their extract into the render world each frame, and a part of the renderer that loads its
/// shaders, sets its renderers and adds its nodes, the probes' between the window's occlusion and
/// the render targets and the view of what it holds after the field's view.
/// </summary>
/// <remarks>
/// Listed by <c>DefaultPlugins</c> after <see cref="RenderPlugin"/>, whose renderer it adds its part
/// to. A program built without it draws as with global illumination off, its settings never reaching
/// the render world and none of its shaders loaded.
/// </remarks>
internal sealed class GlobalIlluminationPlugin : IPlugin
{
    /// <inheritdoc />
    public IReadOnlyCollection<Type> Dependencies { get; } = [typeof(RenderPlugin)];

    /// <inheritdoc />
    public void Build(App app)
    {
        var cfg = app.World.Resource<Config>();
        if (cfg.GlobalIllumination != GlobalIllumination.Off)
            app.World.InsertResource(new GlobalIlluminationSettings { Quality = cfg.GlobalIllumination });
        var renderer = app.World.Resource<Renderer>();
        renderer.AddExtractSystem(new SettingsExtract());
        renderer.AddPart(new Part());
    }

    // The settings copied into the render world each frame, or taken out where the program has none.
    private sealed class SettingsExtract : IExtractSystem
    {
        public void Run(World world, RenderWorld renderWorld)
        {
            if (world.TryGetResource<GlobalIlluminationSettings>(out var settings)) renderWorld.Set(settings);
            else renderWorld.Remove<GlobalIlluminationSettings>();
        }
    }

    private sealed class Part : IRendererPart
    {
        private GlobalIlluminationRenderer? _illumination;
        private BounceViewRenderer? _view;

        public void Initialize(Renderer renderer, GraphicsDevice? device, AssetServer server)
        {
            // The probes' trace and its build for a frame where no surface lets light through,
            // which leaves the crossing of such a surface out.
            if (device is not null)
                GlobalIlluminationStages.Of(device).InitializeGlobalIllumination(server.LoadSync<ShaderProgram>("shaders/gi_trace.slang").Compute,
                    server.LoadSync<ShaderProgram>("shaders/gi_merge.slang").Compute,
                    server.LoadSync<ShaderProgram>("shaders/gi_ambient.slang").Compute,
                    server.LoadSync<ShaderProgram>("shaders/gi_screen.slang").Compute,
                    server.LoadSync<ShaderProgram>("shaders/gi_screen_filter.slang").Compute,
                    server.LoadSync<ShaderProgram>("shaders/gi_screen_glow.slang").Compute,
                    Renderer.Build("gi_trace.slang", ["NO_LIGHT_THROUGH"], "The probes' rays cross what lets light through in every frame")?.Compute,
                    device.CanQueryRays ? Renderer.Build("gi_trace.slang", ["RAY_QUERY", "NO_LIGHT_THROUGH"], "The probes' rays are traced through the field alone")?.Compute : default,
                    device.CanQueryRays ? Renderer.Build("gi_trace.slang", ["RAY_QUERY"], "The probes' rays cross what lets light through in the field alone")?.Compute : default);
            _illumination = new GlobalIlluminationRenderer();
            renderer.RenderWorld.Set(_illumination);
            renderer.RenderWorld.Set<IIlluminationPasses>(_illumination);
            _view = new BounceViewRenderer(server.LoadSync<ShaderProgram>("shaders/gi_view.slang"), server.LoadSync<ShaderProgram>("shaders/gi_probes.slang"));
            renderer.RenderWorld.Set(_view);
            renderer.RenderWorld.Set<IProbesView>(_view);
        }

        public void AddNodes(RenderGraph graph)
        {
            // Traced through the field before every pass that lights a view, the render targets'
            // screen probes with each target after it.
            graph.AddNode("global_illumination", new GlobalIlluminationNode());
            graph.AddNodeEdge("ambient_occlusion", "global_illumination");
            graph.AddNodeEdge("global_illumination", "targets");
            // And what global illumination holds over the window, where a command or its window asks.
            graph.AddNode("bounce_view", new BounceViewNode());
            graph.AddNodeEdge("scene_field_view", "bounce_view");
        }

        public void Dispose()
        {
            _illumination?.Dispose();
            _view?.Dispose();
        }
    }
}
