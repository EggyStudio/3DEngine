using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A still mesh that comes or goes within a built cascade, whose cells within its band alone are
// built again, from the still meshes near them.
public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void A_Mesh_That_Settles_In_A_Built_Cascade_Builds_The_Cells_Within_Its_Band_From_The_Meshes_Near_Them_Alone()
    {
        // One cascade of quarter-unit cells, so its band is a unit, two cubes two units wide six
        // apart, built, and a third settling beside the first.
        var plan = new SceneFieldPlan(1, 0.25f, 1);
        var eye = new Vector3(0.1f, 0.1f, 0.1f);
        var (near, far, beside) = (At(new Vector3(-3, 0, 0)), At(new Vector3(3, 0, 0)), At(new Vector3(-3, 0, 2.5f)));
        for (int frame = 0; frame <= SceneFieldPlan.SettleFrames + 1; frame++) plan.Update(eye, [near, far]);
        plan.Whole.Should().BeTrue("both cubes are built");

        for (int frame = 1; frame <= SceneFieldPlan.SettleFrames; frame++) plan.Update(eye, [near, far, beside]);
        var build = plan.Builds.Should().ContainSingle("the third settled").Which;
        build.Cells.Should().NotBeNull("the cascade is built where the third came alone");
        build.Instances.Select(i => i.World.Translation).Should().BeEquivalentTo([new Vector3(-3, 0, 0), new Vector3(-3, 0, 2.5f)],
            "the cubes within the band of its cells and a cell around them, and not the one six units off");
        var (origin, cells) = (plan.BuiltOrigin(0)!.Value, build.Cells!.Value);
        (origin.X + cells.X0 * 0.25f).Should().BeInRange(-5.25f, -5, "its cells reach the band past the third's side");
        (origin.Z + (cells.Z1 + 1) * 0.25f).Should().BeInRange(4.5f, 4.75f);
        (origin.X + (cells.X1 + 1) * 0.25f).Should().BeLessThan(1, "the far cube's band, which begins a unit past its side, is not among them");
        build.Changed.Should().Be(new SceneFieldPlan.Box(new Vector3(-4, -1, 1.5f), new Vector3(-2, 1, 3.5f)));
        plan.Whole.Should().BeTrue("the region's build holds the third");
        plan.Update(eye, [near, far, beside]);
        plan.Builds.Should().BeEmpty();
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Cells_Built_Again_Where_A_Mesh_Was_Replaced_In_Place_Hold_What_A_Whole_Build_Of_The_Scene_Holds()
    {
        // A floor, a red cube and a gray one, the gray replaced in place by a taller block of
        // another color, as a voxel game's section is when a block breaks, its cells built again
        // alone and read against the same scene's cascade built whole.
        var config = Config.Default.WithWindow("scene field test", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        var (floor, red, before, after) = (LoadModelFromMesh(GenMeshCube(8, 0.5f, 8)), LoadModelFromMesh(GenMeshCube(1, 1, 1)),
            LoadModelFromMesh(GenMeshCube(1, 1, 1)), LoadModelFromMesh(GenMeshCube(1, 2, 1)));
        var camera = new Camera3D(new Vector3(0, 2, 6), Vector3.Zero, Vector3.UnitY, 45, CameraProjection.Perspective);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>;
        var builds = new List<SceneFieldPlan.Build>();
        void Draw(Model block, Color color, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.RayWhite);
                BeginMode3D(camera);
                DrawModel(floor, new Vector3(0, -0.25f, 0), 1, Color.Gray);
                DrawModel(red, new Vector3(-1.5f, 0.5f, 0), 1, Color.Red);
                DrawModel(block, new Vector3(1.5f, 0.5f, 0), 1, color);
                EndMode3D();
                EndDrawing();
                builds.AddRange(fields()?.Plan?.Builds ?? []);
            }
        }

        Draw(before, Color.Gray, SceneFieldPlan.SettleFrames + 6);
        builds.Clear();
        Draw(after, Color.Blue, SceneFieldPlan.SettleFrames + 6);
        builds.Should().NotBeEmpty().And.OnlyContain(build => build.Cells != null, "the block replaced is built again where it stands alone");
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var field = fields()!.Field!;
        var (distances, (albedo, glow)) = (SceneFieldStages.Of(device).ReadSceneField(field), SceneFieldStages.Of(device).ReadSceneFieldColors(field));

        fields()!.Plan!.Rebuild(1);
        Draw(after, Color.Blue, 6);
        var (whole, (wholeAlbedo, wholeGlow)) = (SceneFieldStages.Of(device).ReadSceneField(field), SceneFieldStages.Of(device).ReadSceneFieldColors(field));
        var differ = Enumerable.Range(0, distances.Length).Count(i => distances[i] != whole[i]);
        differ.Should().Be(0, "every cell the region's build left or built holds the whole build's distance");
        // The color and the emission of each cell, but where two faces lie as near it and the
        // painting pass's threads, in whatever order they run, choose either, as lavapipe's do.
        var painted = Enumerable.Range(0, distances.Length).Count(i => albedo[i * 4 + 3] > 0);
        var (colors, lights) = (Enumerable.Range(0, distances.Length).Count(i => !albedo.AsSpan(i * 4, 4).SequenceEqual(wholeAlbedo.AsSpan(i * 4, 4))),
            Enumerable.Range(0, distances.Length).Count(i => !glow.AsSpan(i * 4, 4).SequenceEqual(wholeGlow.AsSpan(i * 4, 4))));
        colors.Should().BeLessThan(painted / 100, $"every painted cell but those at a tie holds the whole build's color, {colors} of {painted}");
        lights.Should().BeLessThan(painted / 100, $"and its emission, {lights} of {painted}");
        distances.Count(d => MathF.Abs(d) < 0.25f).Should().BeGreaterThan(1000, "the field holds the scene");
        foreach (var model in new[] { floor, red, before, after }) UnloadModel(model);
    }
}
