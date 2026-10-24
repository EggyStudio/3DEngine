using System.Numerics;

namespace Engine;

public static partial class E3D
{
    /// <summary>
    /// Builds a distance field of the scene around the camera, in <paramref name="cascades"/>
    /// cascades from 1 to 8, the finest's cells <paramref name="cellSize"/> world units wide, or
    /// turns it off with 0 cascades, which it is by default. Ambient occlusion reads it beside the
    /// window's depth, the sun casts soft contact shadows through it, and particles collide with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The field holds how far each cell of a cascade is from the nearest surface of the meshes
    /// drawn into the window that cast shadows. Each cascade is 64 cells a side, twice as coarse
    /// and as wide as the one before, so the default of 0.25 reaches 16 units across in the first
    /// and 128 in the fourth. A closed wall thinner than a cell is held half a cell thick on either
    /// side of its middle, as a double-sided mesh is, so a ray between the cells on either side of
    /// it still meets it.
    /// </para>
    /// <para>
    /// A mesh drawn in the same place for eight frames is built into the cascades around it from
    /// its triangles on the GPU, and one that moves is stamped each frame as boxes rather than its
    /// triangles, in its color and with none of its emission. A skinned mesh is a box for each
    /// joint around the vertices it holds, posed, so a character's shadows in global illumination
    /// are those of its limbs as boxes,
    /// and one that does not bend a box for each of up to eight parts its triangles are cut into. A
    /// cascade is built again where the camera has gone past it or a mesh came or went,
    /// <paramref name="updateBudget"/> a frame at most, the finest first, so a game sets a larger
    /// budget for a scene that changes and a smaller one for a slow GPU. The frame profile names
    /// its cost as <c>scene_field</c>.
    /// </para>
    /// <para>
    /// A frame's builds splat <paramref name="triangleBudget"/> triangles at most, a mesh at least,
    /// the still meshes within a cascade's band nearest the eye first, and a build of more goes on
    /// over the frames after, its cascade keeping what it was last built with until the build ends
    /// and the meshes not yet splatted stamped as boxes meanwhile. So hundreds of meshes arriving in
    /// one frame, as a world streamed in on a teleport or a first load, are built over several frames
    /// rather than stalling one, and a scene of fewer triangles builds as many cascades a frame as
    /// <paramref name="updateBudget"/> says.
    /// </para>
    /// </remarks>
    public static void SetSceneField(int cascades, float cellSize = SceneFieldConfig.DefaultCellSize, int updateBudget = SceneFieldConfig.DefaultUpdateBudget,
        int triangleBudget = SceneFieldConfig.DefaultTriangleBudget)
    {
        var field = World.GetOrInsertResource(static () => new SceneFieldSettings());
        field.Cascades = Math.Clamp(cascades, 0, 8);
        field.CellSize = Math.Max(1e-3f, cellSize);
        field.UpdateBudget = Math.Max(1, updateBudget);
        field.TriangleBudget = Math.Max(1, triangleBudget);
    }

    /// <summary>
    /// Works out global illumination in the window's scene at <paramref name="quality"/>, or none
    /// with <see cref="GlobalIllumination.Off"/>, which it is by default, the light a
    /// surface sends on lighting the surfaces around it, so a red wall tints the floor beside it and
    /// a room lit by the sun through a window is lit inside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is traced through the scene's distance field, which it turns on at four cascades where
    /// <see cref="SetSceneField"/> has not, as cascades of light probes (Radiance Cascades) the GPU
    /// works out each frame. Nothing is baked, so every light and every mesh may move. The light a
    /// surface sends on is its material's color, its texture's average, times the sun's light where
    /// the field lets it through, the point and spot lights', each that casts shadows where the field
    /// lets it through too, and the indirect light that
    /// reached it the frame before, with its emission, so an emissive mesh lights its room.
    /// </para>
    /// <para>
    /// It stands in for the diffuse share of the light from all around, the environment map's, a
    /// reflection probe's and the ambient lights', whose light a ray that meets nothing brings back,
    /// and leaves their reflections as they are. It lights the window's view alone, and the frame
    /// profile names its cost as <c>global_illumination</c>.
    /// </para>
    /// </remarks>
    public static void SetGlobalIllumination(GlobalIllumination quality)
    {
        World.GetOrInsertResource(static () => new GlobalIlluminationSettings()).Quality = quality;
        if (quality != GlobalIllumination.Off && World.GetOrInsertResource(static () => new SceneFieldSettings()) is { On: false } field)
            field.Cascades = 4;
    }

    /// <summary>
    /// Sets whether global illumination stands a probe on the window's surfaces every few pixels,
    /// beside the cascades of probes through the scene, which it does until a program leaves them
    /// out.
    /// </summary>
    /// <remarks>
    /// The screen's probes trace the near light through the window's depth, so the light under a
    /// table or where a wall meets the floor is seen at the size the window draws it, and the
    /// model pass reads them before the cascades' probes. Left out, they are neither made nor traced,
    /// for the window or a render texture, and every surface reads the cascades' light alone, which
    /// is smoother and coarser, so a game whose art shows the screen's tiles, as a world of blocks
    /// does, and one that has no time for them, leaves them out.
    /// </remarks>
    public static void SetGlobalIlluminationScreenProbes(bool enabled) =>
        World.GetOrInsertResource(static () => new GlobalIlluminationSettings()).ScreenProbes = enabled;

    /// <summary>
    /// Draws what global illumination holds in an ImGui window of its own, which a program shows
    /// while its light is worked on, with how much it traces and the memory that takes, a view of
    /// its probes drawn over the window, its parts each to be left out, and a path-traced reference
    /// of the view with the frame's error over each region against it.
    /// </summary>
    /// <remarks>
    /// Called between <c>BeginDrawing</c> and <c>EndDrawing</c>, as any ImGui window is. The views
    /// and parts are those <c>gi.show</c> and <c>gi.toggle</c> set, and the reference is traced as
    /// <c>gi.reference</c> traces it, through the device's rays, which global illumination
    /// at <see cref="GlobalIllumination.High"/> holds the meshes for.
    /// </remarks>
    public static void DrawBounceWindow()
    {
        if (!TryRes<GlobalIlluminationSettings>(out var settings)) return;
        ImGuiNET.ImGui.SetNextWindowSize(new Vector2(480, 560), ImGuiNET.ImGuiCond.FirstUseEver);
        if (!ImGuiNET.ImGui.Begin("Global illumination"))
        {
            ImGuiNET.ImGui.End();
            return;
        }
        ImGuiNET.ImGui.PushTextWrapPos(0);
        ImGuiNET.ImGui.TextUnformatted(SceneFieldCommands.IlluminationState(World));
        ImGuiNET.ImGui.Separator();

        var names = Enum.GetNames<BounceView>();
        var view = (int)settings.Shown;
        if (ImGuiNET.ImGui.Combo("View", ref view, names, names.Length)) settings.Shown = (BounceView)view;
        var cascades = TryRes<Renderer>(out var renderer) && renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()?.Probes is { } probes ? probes.Cascades : 0;
        var cascade = Math.Min(settings.ShownCascade, Math.Max(cascades - 1, 0));
        if (cascades > 1 && ImGuiNET.ImGui.SliderInt("Cascade", ref cascade, 0, cascades - 1)) settings.ShownCascade = cascade;
        ImGuiNET.ImGui.TextUnformatted(SceneFieldCommands.Shown(settings));

        // Each part on while it is ticked, and the cascade chosen alone while that is.
        var history = !settings.HistoryOff;
        if (ImGuiNET.ImGui.Checkbox("The frame before's light", ref history)) settings.HistoryOff = !history;
        var filter = !settings.FilterOff;
        if (ImGuiNET.ImGui.Checkbox("The screen's filter", ref filter)) settings.FilterOff = !filter;
        var screen = !settings.ScreenOff;
        if (ImGuiNET.ImGui.Checkbox("The screen's probes", ref screen)) settings.ScreenOff = !screen;
        var merge = !settings.MergeOff;
        if (ImGuiNET.ImGui.Checkbox("The merge of the cascades", ref merge)) settings.MergeOff = !merge;
        var again = !settings.AgainOff;
        if (ImGuiNET.ImGui.Checkbox("The light past the first bounce", ref again)) settings.AgainOff = !again;
        var follow = !settings.FollowOff;
        if (ImGuiNET.ImGui.Checkbox("Indirect light following a light out", ref follow)) settings.FollowOff = !follow;
        var glow = !settings.GlowOff;
        if (ImGuiNET.ImGui.Checkbox("The glow lights", ref glow)) settings.GlowOff = !glow;
        var alone = settings.Alone >= 0;
        ImGuiNET.ImGui.Checkbox("The cascade chosen alone", ref alone);
        settings.Alone = alone ? settings.ShownCascade : -1;
        ImGuiNET.ImGui.Separator();

        ImGuiNET.ImGui.InputText("Reference", ref _bouncePath, 1024);
        ImGuiNET.ImGui.InputInt("Paths a pixel", ref _bounceSamples);
        _bounceSamples = Math.Clamp(_bounceSamples, 1, 65536);
        if (ImGuiNET.ImGui.Button("Trace")) _bounceSaid = BounceReference.Trace(World, _bouncePath, _bounceSamples);
        ImGuiNET.ImGui.SameLine();
        if (ImGuiNET.ImGui.Button("Compare"))
        {
            _bounceRegions = BounceReference.Measure(World, _bouncePath, out var why, out var difference);
            _bounceSaid = why ?? $"the difference drawn in {difference}";
        }
        ImGuiNET.ImGui.SameLine();
        if (ImGuiNET.ImGui.Button("Show the difference"))
        {
            _bounceSaid = SceneFieldCommands.GiveReference(settings, _bouncePath);
            if (_bounceSaid is null) settings.Shown = BounceView.Difference;
        }
        if (_bounceSaid is not null) ImGuiNET.ImGui.TextUnformatted(_bounceSaid);
        ImGuiNET.ImGui.PopTextWrapPos();

        // Each region's light by luminance, the reference's and the frame's, and the frame's error as a share.
        if (_bounceRegions is { Count: > 0 } regions
            && ImGuiNET.ImGui.BeginTable("regions", 5, ImGuiNET.ImGuiTableFlags.Borders | ImGuiNET.ImGuiTableFlags.RowBg))
        {
            foreach (var heading in new[] { "Region", "Pixels", "Reference", "Frame", "Error" }) ImGuiNET.ImGui.TableSetupColumn(heading);
            ImGuiNET.ImGui.TableHeadersRow();
            static float Luminance(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;
            foreach (var region in regions)
            {
                ImGuiNET.ImGui.TableNextRow();
                string[] cells = [region.Name, $"{region.Pixels}", $"{Luminance(region.Reference):0.000}", $"{Luminance(region.Frame):0.000}", $"{region.Share * 100:+0;-0;0}%"];
                foreach (var cell in cells)
                {
                    ImGuiNET.ImGui.TableNextColumn();
                    ImGuiNET.ImGui.TextUnformatted(cell);
                }
            }
            ImGuiNET.ImGui.EndTable();
        }
        ImGuiNET.ImGui.End();
    }

    // The global illumination window's reference, its paths a pixel, what its last button said and its last comparison.
    private static string _bouncePath = "bounce-reference.png";
    private static int _bounceSamples = 256;
    private static string? _bounceSaid;
    private static IReadOnlyList<BounceReference.RegionLight>? _bounceRegions;
}
