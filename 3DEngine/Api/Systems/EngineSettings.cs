using System.Runtime.CompilerServices;
using Diagnostics;
using ImGuiNET;
using SDL3;

namespace Engine;

/// <summary>
/// The engine's settings in the Diagnostics module's registry of the process, every value a console
/// command or an environment variable set before there were settings, each with its range or its
/// choices, its default, whether it only reads and the variable that overrides it.
/// </summary>
/// <remarks>
/// <para>
/// A setting reads and sets the world the settings stand for, the world lent to the line that runs
/// or between lines the world of the app made last (<see cref="ConsoleHost.SettingsWorld"/>), and
/// refuses a set where that world has none of what it sets, as before an app is made or with
/// global illumination off, with the reason, so the value stays as it reads.
/// </para>
/// <para>
/// The settings file keeps what a player chooses for the machine, the window's page and
/// <c>imgui.viewports</c>, with the module's five keys. The rest are the run's, a view or a part
/// of the light left out to measure what it gives, or how a tool started the run, which only reads
/// and which the file never keeps.
/// </para>
/// </remarks>
internal static partial class EngineSettings
{
    /// <summary>Every setting of the engine's, by name.</summary>
    internal static IReadOnlyList<Setting> All { get; } = [.. Occlusion(), .. Illumination(), .. Window(), .. Picture(), .. Running(), .. Run()];

    // Adds the settings as the engine's assembly loads, as its commands are added, so a line names
    // them in a program that has not drawn yet.
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255",
        Justification = "The engine's commands are added from its assembly's initializer too, and the settings join them in the same registry.")]
    internal static void Register()
    {
        foreach (var setting in All) Registry.Process.Add(setting);
        AppWindowPlugin.Opening = DiagnosticsBinding.OpenWindow;
    }

    // The ambient occlusion's ways of working out, its resource made where the world has none.
    private static IEnumerable<Setting> Occlusion()
    {
        static AmbientOcclusionSettings? Made(bool make) => ConsoleHost.SettingsWorld is not { } world ? null
            : make ? world.GetOrInsertResource(static () => new AmbientOcclusionSettings())
            : world.TryGetResource<AmbientOcclusionSettings>(out var held) ? held : null;

        Setting<T> Of<T>(string name, string summary, T @default, Func<AmbientOcclusionSettings, T> read, Action<AmbientOcclusionSettings, T> write,
            double? low = null, double? high = null, double step = 0)
            where T : notnull => Held(name, summary, @default, () => Made(false), () => Made(true), read, write, NoApp, low, high, step);

        yield return Of("ao.method", "How the window's ambient occlusion is worked out, by the horizons or by the circular occluders.",
            OcclusionWay.Horizons, s => s.Method == AmbientOcclusionMethod.Horizons ? OcclusionWay.Horizons : OcclusionWay.Circular,
            (s, v) => s.Method = v == OcclusionWay.Horizons ? AmbientOcclusionMethod.Horizons : AmbientOcclusionMethod.CircularOccluders);
        yield return Of("ao.samples", "The points a pixel the circular occluders take, 8 or 16, any other number taken as 16.", 16,
            s => s.Samples, (s, v) => s.Samples = v == 8 ? 8 : 16, low: 8, high: 16, step: 8);
        yield return Of("ao.filter", "Whether the circular occluders' own filter pass blends each pixel with its neighbors before the history takes it.", true,
            s => s.OwnFilter, (s, v) => s.OwnFilter = v);
        yield return Of("ao.slope", "The most a slanted circle of the circular occluders is grown.", 4f,
            s => s.SlopeCompensation, (s, v) => s.SlopeCompensation = v, low: 1, high: AmbientOcclusionSettings.MostSlopeCompensation, step: 1);
        yield return Of("ao.backface", "The cosine past which a circle leaning toward the pixel is a thin surface's back.", 0.5f,
            s => s.BackfaceThreshold, (s, v) => s.BackfaceThreshold = v, low: 0, high: 1, step: 0.05);
        yield return Of("ao.boost", "How much a thin surface's back is grown, a share of the slope's growth.", 0f,
            s => s.BackfaceBoost, (s, v) => s.BackfaceBoost = v, low: 0, high: 1, step: 0.05);
        yield return Of("ao.screen", "The share of the picture's height a circle of the circular occluders may span.", 0.3f,
            s => s.ScreenShare, (s, v) => s.ScreenShare = v, low: 0.01, high: 1, step: 0.05);
        yield return Of("ao.size", "Whether the window's depth the occlusion and the screen's probes read is drawn at half the window's size or at its own.",
            DepthSize.Half, s => s.FullSize ? DepthSize.Full : DepthSize.Half, (s, v) => s.FullSize = v == DepthSize.Full);
    }

    // The parts of global illumination a reading leaves out and the views drawn over the window,
    // and the cascade of the scene's distance field drawn over it.
    private static IEnumerable<Setting> Illumination()
    {
        static GlobalIlluminationSettings? Lit() =>
            ConsoleHost.SettingsWorld is { } world && world.TryGetResource<GlobalIlluminationSettings>(out var held) ? held : null;

        const string unlit = "The program runs no global illumination, which SetGlobalIllumination turns on.";
        Setting<T> Of<T>(string name, string summary, T @default, Func<GlobalIlluminationSettings, T> read, Action<GlobalIlluminationSettings, T> write,
            double? low = null, double? high = null)
            where T : notnull => Held(name, summary, @default, Lit, Lit, read, write, unlit, low, high);

        Setting<bool> Part(string name, string summary, Func<GlobalIlluminationSettings, bool> off, Action<GlobalIlluminationSettings, bool> leaveOut) =>
            Of(name, summary, true, s => !off(s), (s, on) => leaveOut(s, !on));

        yield return Of("gi.rays", "Whether High traces what the scene's distance field misses through the device's own ray tracing, where it has it.", true,
            s => !s.RaysOff, (s, on) => (s.RaysOff, s.RaysLeftOff) = (!on, null));
        yield return Of("gi.passes", "Whether the profile times global illumination's passes apart, as gpu.gi_ lines, the rest staying gpu.global_illumination.", false,
            s => s.PassesTimed, (s, on) => s.PassesTimed = on);
        yield return Part("gi.history", "Whether the screen's probes take the frame before's light.", s => s.HistoryOff, (s, off) => s.HistoryOff = off);
        yield return Part("gi.filter", "Whether each screen probe's light is blended with its neighbors'.", s => s.FilterOff, (s, off) => s.FilterOff = off);
        yield return Part("gi.screen", "Whether the screen's probes are traced and read, the cascades' light read alone without them.",
            s => s.ScreenOff, (s, off) => s.ScreenOff = off);
        yield return Of("gi.gather", "How the cascades' light reaches the picture where the screen's probes stand, by those probes or by a ray from each texel of a grid of the window's pixels.",
            ScreenGather.Probes, s => s.Gather, (s, v) => s.Gather = v);
        yield return Of("gi.gather_size", "The size of the grid gi.gather pixels sends a ray from each texel of, full, half, quarter or eighth.",
            GatherSize.Half, s => s.GatherSize, (s, v) => s.GatherSize = v);
        yield return Of("gi.gather_reach", "How far gi.gather pixels traces each ray through the scene's distance field before reading the cascades' light along its way, in units, or 0 for as far as the field reaches.",
            0f, s => s.GatherReach, (s, v) => s.GatherReach = v, low: 0);
        yield return Of("gi.gather_guide", "Where gi.gather pixels draws its rays' ways from, off by the cosine alone, or cascades by the cosine for half the texels and toward the light the nearest probe of the cascades holds for the rest.",
            GatherGuide.Off, s => s.GatherGuide, (s, v) => s.GatherGuide = v);
        yield return Of("gi.gather_rays", "Whether gi.gather pixels traces each ray through the meshes by the device's own ray tracing at High, where it has it, in place of the window's depth and the scene's distance field.", false,
            s => s.GatherRays, (s, on) => s.GatherRays = on);
        yield return Part("gi.merge", "Whether each cascade takes the light of the cascade above.", s => s.MergeOff, (s, off) => s.MergeOff = off);
        yield return Part("gi.again", "Whether the light past the first bounce comes from the frame before's probes.", s => s.AgainOff, (s, off) => s.AgainOff = off);
        yield return Part("gi.follow", "Whether the indirect light follows a light that goes out within the frames each bounce takes.",
            s => s.FollowOff, (s, off) => s.FollowOff = off);
        yield return Part("gi.glow", "Whether the small emissive surfaces are carried as lights.", s => s.GlowOff, (s, off) => s.GlowOff = off);
        yield return Part("gi.through", "Whether the rays cross the surfaces that let light through.", s => s.ThroughOff, (s, off) => s.ThroughOff = off);
        yield return Part("gi.albedo", "Whether the surfaces keep their colors, every surface drawn white without them.",
            s => s.AlbedoOff, (s, off) => s.AlbedoOff = off);
        yield return Part("gi.specular", "Whether the surfaces reflect the indirect light.", s => s.SpecularOff, (s, off) => s.SpecularOff = off);
        yield return Part("gi.normals", "Whether the surfaces' normal maps turn the faces they light.", s => s.NormalMapsOff, (s, off) => s.NormalMapsOff = off);
        yield return Part("gi.spread", "Whether the coarser cascades trace half their probes a frame while the eye lies still.",
            s => s.SpreadOff, (s, off) => s.SpreadOff = off);
        yield return Part("gi.points", "Whether the faint glow lights at a probe's hit are taken as one point, every light by its own form without it.",
            s => s.PointsOff, (s, off) => s.PointsOff = off);
        yield return Part("gi.footprint", "Whether a still probe's ray reads the sun at its hit over the patch of the face its cone covers, at the hit alone without it.",
            s => s.FootprintOff, (s, off) => s.FootprintOff = off);
        yield return Of("gi.toward", "Whether a pixel reads each of its probes' faces moved toward the surface the face's ray met as a probe's hit reads them, as they stand without it.", false,
            s => s.FacesMovedToward, (s, on) => s.FacesMovedToward = on);
        yield return new Setting<string>("gi.side", $"How many probes a side each cascade of the world's probes holds, their spacing kept, from the first on as 8,8,12, the last count holding for the cascades past it, 8 for every cascade on a device that draws on its CPU unless set, each even from {GlobalIlluminationSettings.LeastSide} to {GlobalIlluminationSettings.MostSide}.",
            GlobalIlluminationSettings.SidesText(GlobalIlluminationSettings.DefaultSides),
            () => GlobalIlluminationSettings.SidesText(Lit()?.ProbesSides ?? GlobalIlluminationSettings.DefaultSides),
            apply: text =>
            {
                if (GlobalIlluminationSettings.ParseSides(text) is not { } sides)
                    return $"The probes a side are even counts from {GlobalIlluminationSettings.LeastSide} to {GlobalIlluminationSettings.MostSide} joined by commas, as 8,8,12, not {text}.";
                if (Lit() is not { } lit) return unlit;
                lit.ProbesSides = sides;
                return null;
            })
        { Saved = false };
        yield return Of("gi.shell_parts", "The turns the probes past each cascade's inner eight a side trace in, a part a frame and whole on a frame the cascade moves, or 0 for every frame.", 0,
            s => s.ShellParts, (s, v) => s.ShellParts = v, low: 0, high: 8);
        yield return Of("gi.meshes", "Whether the probes' rays are traced through the device's rays at High in place of the distance field.", false,
            s => s.MeshesTraced, (s, on) => s.MeshesTraced = on);
        yield return Of("gi.own_field", "Whether each cascade's probes march the field from the cascade they stand in outward, from its finest cascade holding each point without it.", false,
            s => s.OwnFieldMarched, (s, on) => s.OwnFieldMarched = on);
        yield return Part("gi.own_place", "Whether each cascade's probes are placed by the field from the cascade they stand in outward, by its finest cascade holding each spot without it.",
            s => s.OwnPlaceOff, (s, off) => s.OwnPlaceOff = off);
        yield return Of("gi.cascade", "The one cascade whose rays' light alone reaches the pixels, or -1 for every cascade.", -1,
            s => s.Alone, (s, v) => s.Alone = v, low: -1, high: Engine.Illumination.MostCascades - 1);
        yield return Of("gi.show", "What global illumination holds drawn over the window, its screen's probes, a cascade's rays, merge or probes, the frame against a reference gi.difference reads, or nothing.",
            BounceView.None, s => s.Shown, (s, v) => s.Shown = v);
        yield return Of("gi.show_cascade", "The cascade gi.show draws the rays, the merge or the probes of.", 0,
            s => s.ShownCascade, (s, v) => s.ShownCascade = v, low: 0, high: Engine.Illumination.MostCascades - 1);

        static SceneFieldSettings? Field() =>
            ConsoleHost.SettingsWorld is { } world && world.TryGetResource<SceneFieldSettings>(out var held) && held.On ? held : null;
        yield return Held("field.show", "The cascade of the scene's distance field drawn over the window as the field holds the scene, or -1 for none.", -1,
            Field, Field, s => s.Shown, (s, v) => s.Shown = Math.Clamp(v, -1, s.Cascades - 1),
            "The program holds no distance field of the scene, which global illumination builds.", low: -1, high: GpuSceneField.MaxCascades - 1);
    }

    // What runs as the frames go, the frame's time, the files loaded again as they change, the
    // log's lines of each frame and the scene packs' folder.
    private static IEnumerable<Setting> Running()
    {
        yield return new Setting<double>("time.frame", "The seconds each frame moves time by, reading no clock, or 0 to read the clock.", 0,
            () => ConsoleHost.SettingsWorld?.TryGetResource<Time>(out var time) == true ? time.FrameSeconds : 0,
            seconds => { if (ConsoleHost.SettingsWorld?.TryGetResource<Time>(out var time) == true) time.FrameSeconds = seconds; })
        { Low = 0, Step = 1.0 / 60, EnvironmentVariable = "E3D_FRAME_TIME", Saved = false };
        yield return new Setting<bool>("live.reload", "Whether the program loads again the files it loaded by name as they are saved, which the live command lists.", false,
            () => ConsoleHost.SettingsWorld?.TryGetResource<LiveFiles>(out var live) == true && live.On, Live)
        { EnvironmentVariable = "E3D_LIVE", Saved = false };
        yield return new Setting<bool>("log.frames", "Whether the log takes the lines each frame writes, the stages' times and the render's steps.", false,
            () => LogConfig.PerFrameLogging, on => LogConfig.PerFrameLogging = on)
        { EnvironmentVariable = "ENGINE_LOG_FRAMES", Saved = false };
        yield return new Setting<string>("scenes.folder", "The folder the scene packs are read from, the cache's scenes folder where empty.", "",
            () => ScenePacks.Folder, folder => ScenePacks.Folder = folder.Length > 0 ? folder : null)
        { EnvironmentVariable = "E3D_SCENES", Saved = false };

        // The day sky's clock, which reads 0 where the program runs no day sky.
        static DaySky? Sky() => ConsoleHost.SettingsWorld?.TryGetResource<DaySky>(out var sky) == true ? sky : null;
        yield return Held("sky.hours", "The hours the day sky has run from its first midnight, which sky.time answers with where the sun and the moon stand.", 0.0,
            Sky, Sky, sky => sky.Hours, (sky, hours) => { sky.Hours = hours; sky.Changed(); },
            "The program runs no day sky, which SetSkyTime or SetSkyDay starts.", low: 0, high: 24.0 * int.MaxValue, step: 0.5);
    }

    // Why a setting of what the world makes is refused where there is no world.
    private const string NoApp = "No app is running to take it.";

    // A setting of a field of what a world holds, read and set where the world holds it, reading
    // the default and refusing a set with the reason given where it does not, the setter given what
    // to set or what to make for it.
    private static Setting<T> Held<TResource, T>(string name, string summary, T @default, Func<TResource?> read, Func<TResource?> write,
        Func<TResource, T> get, Action<TResource, T> set, string missing, double? low = null, double? high = null, double step = 0, bool saved = false,
        Func<string?>? locked = null)
        where TResource : class
        where T : notnull =>
        new(name, summary, @default,
            () => read() is { } resource ? get(resource) : @default,
            apply: value =>
            {
                if (write() is not { } resource) return missing;
                set(resource, value);
                return null;
            })
        { Low = low, High = high, Step = step, Saved = saved, Lock = locked };

    // Lets ImGui's windows leave the program's where SDL's driver places windows on the desktop,
    // or says why they cannot.
    private static string? Viewports(bool on)
    {
        if (ImGui.GetCurrentContext() == IntPtr.Zero) return "The program draws no ImGui.";
        if (!SdlImGuiViewports.Installed)
            return $"ImGui keeps its windows inside the main one here, since SDL's {SDL.GetCurrentVideoDriver()} driver cannot place a window on the desktop.";
        var io = ImGui.GetIO();
        io.ConfigFlags = on ? io.ConfigFlags | ImGuiConfigFlags.ViewportsEnable : io.ConfigFlags & ~ImGuiConfigFlags.ViewportsEnable;
        return null;
    }

    // Turns the reloading of the files loaded by name on or off, and the asset server's watch.
    private static void Live(bool on)
    {
        if (ConsoleHost.SettingsWorld is not { } world || !world.TryGetResource<LiveFiles>(out var live)) return;
        live.Turn(on);
        if (!world.TryGetResource<AssetServer>(out var server)) return;
        if (on) server.EnableWatching();
        else server.DisableWatching();
    }

    /// <summary>The ways <c>ao.method</c> works the ambient occlusion out by.</summary>
    internal enum OcclusionWay
    {
        /// <summary>By the horizons, the default.</summary>
        Horizons,

        /// <summary>By the circular occluders.</summary>
        Circular,
    }

    /// <summary>The sizes <c>ao.size</c> draws the window's depth at.</summary>
    internal enum DepthSize
    {
        /// <summary>Half the window's size, the default.</summary>
        Half,

        /// <summary>The window's own size.</summary>
        Full,
    }
}
