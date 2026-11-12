namespace Engine;

/// <summary>
/// The systems that run against the flat API, the recorder, the debug keys and overlay, the day sky,
/// the animated models and the edit meshes the commands show, each named under <c>RenderPlugin</c>,
/// by which a system ordered before or after one of them names it. <see cref="DefaultPlugins"/>
/// adds it after the render plugin, whose meshes and renderer it reads.
/// </summary>
internal sealed class FlatApiPlugin : IPlugin
{
    public void Build(App app)
    {
        // The files the flat API loads by name, loaded again as they are saved where the program is
        // live, at the frame's start before anything reads what they were loaded into.
        var config = app.World.TryGetResource<Config>(out var given) ? given : Config.Default;
        var live = new LiveFiles(RunMode.IsLive(config), ProgramFiles.OwnProject());
        app.World.InsertResource(live);
        app.AddSystem(Stage.First, new SystemDescriptor(world => live.Run(world.TryGetResource<Time>(out var time) ? time.FrameCount : 0), "FlatApi.LiveFiles").MainThreadOnly());
        // A recording asks for its pictures before the frame's systems draw, so each is of this
        // frame, and writes what it took where the app ends while it records.
        app.AddSystem(Stage.First, new SystemDescriptor(Recorder.Tick, "RenderPlugin.Recorder").MainThreadOnly());
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(_ => Recorder.Shutdown(), "RenderPlugin.RecorderShutdown").Before("RenderPlugin.Cleanup").MainThreadOnly());
        // F2 writes a screenshot in every program, F1 hides its interface and F3 shows the debug
        // overlay, once the frame's input is in.
        app.World.InitResource<DebugKeys>();
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(DebugKeys.Run, "RenderPlugin.ScreenshotKey").MainThreadOnly().Read<Input>());
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(DebugOverlay.Keys, "RenderPlugin.DebugOverlayKeys").MainThreadOnly().Read<Input>());
        // The day sky moves its hour, its light and its map after the program's frame, so an hour the
        // program set in it is drawn in it.
        app.AddSystem(Stage.PostUpdate, new SystemDescriptor(world => DaySky.Run(world, app), "RenderPlugin.DaySky").MainThreadOnly());
        // After the mesh entities, which the render plugin records first.
        app.AddSystem(Stage.Render, new SystemDescriptor(world => AnimatedModelDraws.Run(world, app), "RenderPlugin.AnimatedModelDraws").MainThreadOnly());
        // The edit meshes model.show put in the world, built again as the commands change them.
        app.AddSystem(Stage.Render, new SystemDescriptor(world => EditMeshNames.Draw(world, app), "RenderPlugin.EditMeshDraws").MainThreadOnly());
        // The overlay is drawn over everything else in 2D, after the frame's systems, before the frame
        // is rendered where a window or an offscreen run renders it, and before the draws are let go
        // where nothing does.
        var renders = app.World.ContainsResource<AppWindow>() || RunMode.HasRenderer(app.World);
        app.AddSystem(Stage.Last, new SystemDescriptor(world => DebugOverlay.Draw(world, app), "RenderPlugin.DebugOverlay")
            .Before(renders ? "RenderPlugin.Render" : "RenderPlugin.ClearDraws").MainThreadOnly());
    }
}
