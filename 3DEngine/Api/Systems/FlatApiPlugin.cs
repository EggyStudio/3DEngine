namespace Engine;

/// <summary>
/// The systems that run against the flat API, the recorder, the Diagnostics module's keys, console,
/// overlay and settings panel, the day sky, the animated models and the edit meshes the commands
/// show, each named under <c>RenderPlugin</c>, by which a system ordered before or after one of
/// them names it. <see cref="DefaultPlugins"/> adds it after the render plugin, whose meshes and
/// renderer it reads.
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
        // The Diagnostics module's keys in every program, F1 hiding its interface, F2 writing a
        // screenshot, F3 showing the debug overlay, F4 opening the settings panel and F5 the
        // console, taken once the frame's input is in, the console and the panel drawn in ImGui's
        // frame after them, and the keys' settings taken out of the registry as the app ends. The
        // settings file is read at the start, once every plugin has added its settings and ImGui
        // has its context, and written as the app ends.
        app.World.InitResource<ScreenshotKey>();
        DiagnosticsBinding.Of(app.World);
        app.AddSystem(Stage.Startup, new SystemDescriptor(world => DiagnosticsBinding.OpenSettings(world, app), "RenderPlugin.Settings").MainThreadOnly());
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(world => DiagnosticsBinding.TakeKeys(world, app), "RenderPlugin.DiagnosticKeys").MainThreadOnly().Read<Input>());
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(ScreenshotKey.Run, "RenderPlugin.ScreenshotKey").After("RenderPlugin.DiagnosticKeys").MainThreadOnly().Read<Input>());
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(world => DiagnosticsBinding.DrawWindows(world, app), "RenderPlugin.DiagnosticWindows")
            .After("RenderPlugin.DiagnosticKeys").MainThreadOnly());
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(DiagnosticsBinding.Release, "RenderPlugin.DiagnosticsRelease").MainThreadOnly());
        // The day sky moves its hour, its light and its map after the program's frame, so an hour the
        // program set in it is drawn in it.
        app.AddSystem(Stage.PostUpdate, new SystemDescriptor(world => DaySky.Run(world, app), "RenderPlugin.DaySky").MainThreadOnly());
        // After the mesh entities, which the render plugin records first, each once the frame's
        // transforms are carried down the tree.
        app.AddSystem(Stage.Render, new SystemDescriptor(world => AnimatedModelDraws.Run(world, app), "RenderPlugin.AnimatedModelDraws")
            .After("EcsPlugin.TransformPropagation").MainThreadOnly());
        // The edit meshes model.show put in the world, built again as the commands change them.
        app.AddSystem(Stage.Render, new SystemDescriptor(world => EditMeshNames.Draw(world, app), "RenderPlugin.EditMeshDraws")
            .After("EcsPlugin.TransformPropagation").MainThreadOnly());
        // The overlay is drawn over everything else in 2D, after the frame's systems, before the frame
        // is rendered where a window or an offscreen run renders it, and before the draws are let go
        // where nothing does.
        var renders = app.World.ContainsResource<AppWindow>() || RunMode.HasRenderer(app.World);
        app.AddSystem(Stage.Last, new SystemDescriptor(world => DiagnosticsBinding.DrawOverlay(world, app), "RenderPlugin.DebugOverlay")
            .Before(renders ? "RenderPlugin.Render" : "RenderPlugin.ClearDraws").MainThreadOnly());
    }
}
