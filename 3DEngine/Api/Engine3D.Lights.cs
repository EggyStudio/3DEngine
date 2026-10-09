using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    private static Entity _ambient;

    // -- Lights, which are Light entities in the ECS, so a program with light entities of its own
    // and one that makes lights here light the same models.

    /// <summary>A light from far away along <paramref name="direction"/>, as the sun's, which may cast the frame's shadow.</summary>
    public static LightHandle CreateDirectionalLight(Vector3 direction, Color color, float intensity = 1, bool castsShadows = false) =>
        Spawn(Light.Directional(Linear(color), intensity) with { CastsShadows = castsShadows }, Vector3.Zero, direction);

    /// <summary>
    /// A light shining every way from <paramref name="position"/>, reaching <paramref name="range"/>
    /// units, or every distance with 0, which may cast shadows all around it, as the twelve such
    /// lights that matter most to the camera's view do.
    /// </summary>
    /// <remarks>
    /// A model drawn around a shadowed light, as a lamp's bulb, shadows everything from it. Draw the
    /// bulb as a shape, with <see cref="DrawSphere"/>, which casts no shadow.
    /// </remarks>
    public static LightHandle CreatePointLight(Vector3 position, Color color, float intensity = 1, float range = 0, bool castsShadows = false) =>
        Spawn(Light.Point(Linear(color), intensity, range) with { CastsShadows = castsShadows }, position, -Vector3.UnitZ);

    /// <summary>
    /// A light from <paramref name="position"/> along <paramref name="direction"/>, full inside
    /// <paramref name="innerAngle"/> and gone past <paramref name="outerAngle"/> degrees, which may
    /// cast a shadow, as the ten such lights that matter most to the camera's view do.
    /// </summary>
    public static LightHandle CreateSpotLight(Vector3 position, Vector3 direction, Color color, float intensity = 1,
        float innerAngle = 25, float outerAngle = 30, float range = 0, bool castsShadows = false) =>
        Spawn(Light.Spot(Linear(color), intensity, innerAngle, outerAngle, range) with { CastsShadows = castsShadows }, position, direction);

    /// <summary>Moves a point or spot light.</summary>
    public static void SetLightPosition(LightHandle light, Vector3 position)
    {
        if (Resolve(light) is { } entity) Ecs.GetRef<Transform>(entity).Position = position;
    }

    /// <summary>Turns a directional or spot light to shine along <paramref name="direction"/>.</summary>
    public static void SetLightDirection(LightHandle light, Vector3 direction)
    {
        if (Resolve(light) is { } entity) Ecs.GetRef<Transform>(entity).Rotation = Facing(direction);
    }

    /// <summary>Sets a light's color and how bright it is.</summary>
    public static void SetLightColor(LightHandle light, Color color, float intensity = 1)
    {
        if (Resolve(light) is not { } entity) return;
        ref var l = ref Ecs.GetRef<Light>(entity);
        l.Color = Linear(color);
        l.Intensity = intensity;
    }

    /// <summary>Sets whether a light casts shadows, which the first directional light that does casts, and of spot and point lights the ten and the twelve that matter most to the camera's view.</summary>
    public static void SetLightCastsShadows(LightHandle light, bool castsShadows)
    {
        if (Resolve(light) is { } entity) Ecs.GetRef<Light>(entity).CastsShadows = castsShadows;
    }

    /// <summary>
    /// Sets how far past the camera, in world units, the sun's shadows reach (150 by default), and
    /// how far a spot or point light with no range casts them. A shorter distance spends the same
    /// shadow map on less ground, so its shadows are sharper.
    /// </summary>
    public static void SetShadowDistance(float distance) =>
        World.GetOrInsertResource(() => new ShadowSettings()).Distance = Math.Max(1, distance);

    /// <summary>
    /// Sets how many texels wide each tile of the shadow map is, the sun's cascades and the spot
    /// lights each having one, rounded to a power of two from 256 to 4096 (2048 by default). A point
    /// light's faces are a quarter of it.
    /// </summary>
    /// <remarks>
    /// A game's shadow quality setting: 4096 sharpens shadows at four times the memory and drawing
    /// of 2048, and 1024 is cheaper and softer. The map is made again at the new size the next frame.
    /// </remarks>
    public static void SetShadowMapSize(int size) =>
        World.GetOrInsertResource(() => new ShadowSettings()).TileSize =
            (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Clamp(size, 256, 4096));

    /// <summary>Removes a light.</summary>
    public static void UnloadLight(LightHandle light)
    {
        if (Resolve(light) is { } entity) Ecs.Despawn(entity);
    }

    /// <summary>
    /// Darkens the light from all around where the surfaces near a point close it off, as where a
    /// floor meets a wall or under a crate, by <paramref name="intensity"/>, looking for those
    /// surfaces within <paramref name="radius"/> world units, or turns it off with an intensity of
    /// 0, which it is by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It darkens the ambient light, the environment map and a reflection probe's light, and not a
    /// light's own, which reaches a corner as well as a wall. 1 suits most scenes, and a radius
    /// about the size of the things that stand close together, a unit for a room's furniture and a
    /// few for buildings.
    /// </para>
    /// <para>
    /// It is worked out for the window's view each frame, from the depth of its models drawn at
    /// half the window's size ahead of its pass, so a frame costs one more pass over the models
    /// that cast a shadow, and a model that casts none darkens nothing around it. A render texture
    /// that draws models through a camera works out its own the same way ahead of its pass, and
    /// probe captures are drawn without it.
    /// </para>
    /// </remarks>
    public static void SetAmbientOcclusion(float intensity, float radius = 1)
    {
        var occlusion = World.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        occlusion.Intensity = Math.Max(0, intensity);
        occlusion.Radius = Math.Max(0, radius);
    }

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
    /// and 128 in the fourth. A closed wall thinner than half a cell is held half a cell thick on
    /// either side of its middle, as a double-sided mesh is, so a ray between the cells on either
    /// side of it still meets it.
    /// </para>
    /// <para>
    /// A mesh drawn in the same place for eight frames is built into the cascades around it from
    /// its triangles on the GPU, and one that moves is stamped each frame as boxes rather than its
    /// triangles, in its color and giving off none of its light: a skinned mesh a box for each
    /// joint around the vertices it holds,
    /// posed, so a character's shadows in the light that bounces are those of its limbs as boxes,
    /// and one that does not bend a box for each of up to eight parts its triangles are cut into. A
    /// cascade is built again where the camera has gone past it or a mesh came or went,
    /// <paramref name="updateBudget"/> a frame at most, the finest first, so a game sets a larger
    /// budget for a scene that changes and a smaller one for a slow GPU. The frame profile names
    /// its cost as <c>scene_field</c>.
    /// </para>
    /// </remarks>
    public static void SetSceneField(int cascades, float cellSize = SceneFieldConfig.DefaultCellSize, int updateBudget = SceneFieldConfig.DefaultUpdateBudget)
    {
        var field = World.GetOrInsertResource(static () => new SceneFieldSettings());
        field.Cascades = Math.Clamp(cascades, 0, 8);
        field.CellSize = Math.Max(1e-3f, cellSize);
        field.UpdateBudget = Math.Max(1, updateBudget);
    }

    /// <summary>
    /// Lets light bounce between the surfaces of the window's scene at <paramref name="quality"/>,
    /// or none with <see cref="GlobalIllumination.Off"/>, which it is by default, the light a
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
    /// lets it through too, and what bounced to it the
    /// frame before, with the light it gives off, so an emissive mesh lights its room.
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
    /// Draws what the light that bounces holds in an ImGui window of its own, which a program shows
    /// while its light is worked on: how much it traces and the memory that takes, a view of its
    /// probes drawn over the window, its parts each to be left out, and a path-traced reference of
    /// the view with the frame's error over each region against it.
    /// </summary>
    /// <remarks>
    /// Called between <c>BeginDrawing</c> and <c>EndDrawing</c>, as any ImGui window is. The views
    /// and parts are those <c>gi.show</c> and <c>gi.toggle</c> set, and the reference is traced as
    /// <c>gi.reference</c> traces it, through the device's rays, which light bouncing at
    /// <see cref="GlobalIllumination.High"/> holds the meshes for.
    /// </remarks>
    public static void DrawBounceWindow()
    {
        if (!TryRes<GlobalIlluminationSettings>(out var settings)) return;
        ImGuiNET.ImGui.SetNextWindowSize(new Vector2(480, 560), ImGuiNET.ImGuiCond.FirstUseEver);
        if (!ImGuiNET.ImGui.Begin("Light that bounces"))
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
        if (ImGuiNET.ImGui.Checkbox("The light that bounces again", ref again)) settings.AgainOff = !again;
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

    // The bounce window's reference, its paths a pixel, what its last button said and its last comparison.
    private static string _bouncePath = "bounce-reference.png";
    private static int _bounceSamples = 256;
    private static string? _bounceSaid;
    private static IReadOnlyList<BounceReference.RegionLight>? _bounceRegions;

    /// <summary>
    /// Sets the light from all around that keeps the side of a model away from every other light
    /// from going black, replacing the one set before, or removes it with an intensity of 0.
    /// </summary>
    public static void SetAmbientLight(Color color, float intensity)
    {
        if (Ecs.TryResolve(_ambient, out var existing))
        {
            if (intensity <= 0)
            {
                Ecs.Despawn(existing);
                _ambient = Entity.None;
                return;
            }
            Ecs.GetRef<Light>(existing) = Light.Ambient(Linear(color), intensity);
            return;
        }
        if (intensity > 0) _ambient = Spawn(Light.Ambient(Linear(color), intensity), Vector3.Zero, -Vector3.UnitZ).Entity;
    }

    private static EcsWorld Ecs => Res<EcsWorld>();

    private static LightHandle Spawn(Light light, Vector3 position, Vector3 direction)
    {
        var entity = Ecs.Spawn();
        Ecs.Add(entity, light);
        Ecs.Add(entity, new Transform(position, Facing(direction), Vector3.One));
        return new LightHandle(Ecs.Handle(entity));
    }

    private static int? Resolve(LightHandle light) =>
        Ecs.TryResolve(light.Entity, out var entity) && Ecs.Has<Light>(entity) ? entity : null;

    /// <summary>The rotation that turns a light's -Z, the way it shines, to <paramref name="direction"/>.</summary>
    internal static Quaternion Facing(Vector3 direction)
    {
        if (direction.LengthSquared() < 1e-12f) return Quaternion.Identity;
        var to = Vector3.Normalize(direction);
        var from = -Vector3.UnitZ;
        var dot = Vector3.Dot(from, to);
        if (dot > 0.99999f) return Quaternion.Identity;
        if (dot < -0.99999f) return Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        return Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(from, to)), MathF.Acos(dot)));
    }
}
