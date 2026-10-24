using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The builds' budget of triangles a frame, hundreds of meshes arriving in one frame built over as
// many frames as it takes, nearest the eye first, each frame under the budget on the plan's counts
// and the field's time on the device under a frame that built them all at once, and a build in
// progress taking in a mesh that came and starting again for one that left.
public sealed partial class SceneFieldTests
{
    // The sections a voxel game streams in at once, as the reading that stalled a frame had them,
    // 500 of 1,024 triangles each, flat boxes four meters a side on a grid of 25 by 20 about the eye.
    private const int Sections = 500, SectionTriangles = 1024;

    private static readonly ModelVertex[] Section =
        [.. Enumerable.Range(0, 8).Select(c => new ModelVertex(new Vector3((c & 1) == 0 ? 0 : 4, (c & 2) == 0 ? 0 : 1, (c & 4) == 0 ? 0 : 4), Vector3.UnitY, Vector2.Zero))];

    private static Vector3 SectionAt(int i) => new((i % 25 - 12) * 4, 0, (i / 25 - 10) * 4);

    private static List<(SceneFieldPlan.Instance, bool)> AllSections() =>
        [.. Enumerable.Range(0, Sections).Select(i => (new SceneFieldPlan.Instance(1, Section, Matrix4x4.CreateTranslation(SectionAt(i)), false), false))];

    // Runs a plan until it has settled the sections and built every cascade with them, and answers
    // each frame's triangles splatted from the frame they settled in, and the parts of each
    // cascade's build in order.
    private static (List<int> Frames, Dictionary<int, List<SceneFieldPlan.Build>> Parts) BuildSections(int triangleBudget)
    {
        var plan = new SceneFieldPlan(4, 0.25f, 4, triangleBudget: triangleBudget, triangles: _ => SectionTriangles);
        var eye = new Vector3(0, 2, 0);
        var drawn = AllSections();
        var (frames, parts) = (new List<int>(), new Dictionary<int, List<SceneFieldPlan.Build>>());
        for (int frame = 1; frame < 200; frame++)
        {
            plan.Update(eye, drawn);
            if (frame < SceneFieldPlan.SettleFrames) continue;
            frames.Add(plan.Splatted);
            foreach (var build in plan.Builds) (parts.TryGetValue(build.Cascade, out var list) ? list : parts[build.Cascade] = []).Add(build);
            if (plan.Builds.Count == 0 && plan.Waiting == 0) break;
        }
        return (frames, parts);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Five_Hundred_Meshes_Arriving_In_One_Frame_Are_Built_Over_Several_Frames_Under_The_Triangle_Budget_The_Nearest_First()
    {
        // All at once, as the cascades' budget alone has it, the frame they settle in splats every
        // cascade's reach, 500 meshes into the coarsest alone.
        var (whole, wholeParts) = BuildSections(int.MaxValue);
        whole[0].Should().BeGreaterThanOrEqualTo(Sections * SectionTriangles, "the frame the sections settled in built them all into four cascades");
        wholeParts.Values.Should().OnlyContain(list => list.Count == 1 && list[0].First && list[0].Last, "each cascade in one build of one part");

        const int budget = 65_536;
        var (frames, parts) = BuildSections(budget);
        frames.Should().OnlyContain(splatted => splatted <= budget, "no frame splats more than the budget");
        frames.Count(splatted => splatted > 0).Should().BeGreaterThanOrEqualTo(whole[0] / budget, "the builds go on over as many frames as the budget takes");

        foreach (var (cascade, list) in parts)
        {
            list[0].First.Should().BeTrue();
            list.Skip(1).Where(part => part.First).Should().BeEmpty($"cascade {cascade}'s build starts once");
            list[^1].Last.Should().BeTrue();
            list.SkipLast(1).Where(part => part.Last || part.Changed != null).Should().BeEmpty("the passes read the cascade as it was until its last part");
            list.SelectMany(part => part.Instances).Select(instance => instance.World.Translation)
                .Should().BeEquivalentTo(wholeParts[cascade][0].Instances.Select(instance => instance.World.Translation),
                    $"cascade {cascade} is built from the meshes one build would have built it from");
        }

        // Each part's meshes are as near the eye as the next part's or nearer.
        var eye = new Vector3(0, 2, 0);
        float Distance(SceneFieldPlan.Instance instance) => Vector3.DistanceSquared(instance.World.Translation + new Vector3(2, 0.5f, 2), eye);
        foreach (var list in parts.Values.Where(list => list.Count > 1))
            for (int part = 0; part + 1 < list.Count; part++)
                list[part].Instances.Max(Distance).Should().BeLessThanOrEqualTo(list[part + 1].Instances.Min(Distance), "nearest the eye first");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Build_In_Progress_Takes_In_A_Mesh_That_Settles_And_Starts_Again_For_A_Splatted_One_That_Leaves()
    {
        // A budget of one mesh's triangles, so a build of ten meshes takes ten frames, the meshes in
        // a row going away from the eye.
        var plan = new SceneFieldPlan(1, 1, 1, triangleBudget: 10, triangles: _ => 10);
        var eye = new Vector3(0, 2, 6);
        var row = Enumerable.Range(0, 10).Select(k => At(new Vector3(0, 0, 4 - 2 * k))).ToList();
        plan.Update(eye, []);
        plan.Builds.Should().ContainSingle("the empty cascade is built at once");
        for (int frame = 1; frame < SceneFieldPlan.SettleFrames; frame++) plan.Update(eye, row);

        // The frame they settle in starts the build with the nearest, and a newcomer drawn from the
        // frame after settles while the build runs, nearer the eye than the meshes left.
        plan.Update(eye, row);
        plan.Builds.Should().ContainSingle().Which.Should().Match<SceneFieldPlan.Build>(b => b.First && !b.Last && b.Instances.Length == 1);
        plan.Builds[0].Instances[0].World.Translation.Should().Be(new Vector3(0, 0, 4), "the nearest the eye first");
        var newcomer = At(new Vector3(0, 0, 2.5f));
        var parts = new List<SceneFieldPlan.Build>();
        for (int frame = 0; frame < 30 && (parts.Count == 0 || !parts[^1].Last); frame++)
        {
            plan.Update(eye, [.. row, newcomer]);
            parts.AddRange(plan.Builds);
        }
        parts.Should().NotBeEmpty();
        parts.Where(part => part.First).Should().BeEmpty("the build goes on whole, taking the newcomer in");
        parts[^1].Last.Should().BeTrue();
        parts.SelectMany(part => part.Instances).Select(i => i.World.Translation).Should().Contain(new Vector3(0, 0, 2.5f));
        plan.Update(eye, [.. row, newcomer]);
        plan.Builds.Should().BeEmpty("the build that took the newcomer in leaves nothing for after");

        // The farthest leaves, and the cascade is built again; its build's first part splats the
        // nearest, which then leaves too, and the build starts again without it.
        var left = row.Take(9).Append(newcomer).ToList();
        plan.Update(eye, left);
        plan.Builds.Should().ContainSingle().Which.Should().Match<SceneFieldPlan.Build>(b => b.First && !b.Last);
        plan.Builds[0].Instances[0].World.Translation.Should().Be(new Vector3(0, 0, 4));
        left.RemoveAt(0);
        plan.Update(eye, left);
        plan.Builds.Should().ContainSingle().Which.First.Should().BeTrue("a mesh the build splatted left, so it starts again");
        var again = plan.Builds.SelectMany(b => b.Instances).Select(i => i.World.Translation).ToList();
        for (int frame = 0; frame < 30 && !plan.Builds[^1].Last; frame++)
        {
            plan.Update(eye, left);
            again.AddRange(plan.Builds.SelectMany(b => b.Instances).Select(i => i.World.Translation));
        }
        again.Should().BeEquivalentTo(left.Select(entry => entry.Item1.World.Translation), "the build holds the meshes still there and none that left");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Finer_Cascade_The_Eye_Moves_Past_Is_Built_Before_A_Coarser_Build_In_Progress_Which_Starts_Again_After()
    {
        // Two cascades and a budget of one mesh a frame, so the coarser's build of the sections it
        // reaches takes many frames.
        var plan = new SceneFieldPlan(2, 0.25f, 2, triangleBudget: SectionTriangles, triangles: _ => SectionTriangles);
        var eye = new Vector3(0, 2, 0);
        var drawn = AllSections();
        // The finer's 36 sections take as many frames from the eighth, and the coarser's begin after.
        for (int frame = 0; frame < 80; frame++) plan.Update(eye, drawn);
        plan.Builds.Should().ContainSingle().Which.Cascade.Should().Be(1, "the coarser cascade's build is under way");
        plan.Builds[0].First.Should().BeFalse();

        // The eye passes the finer cascade's grid, which is built at once, and the coarser's build
        // starts again the frame after it ends.
        eye += new Vector3(2.5f, 0, 0);
        plan.Update(eye, drawn);
        plan.Builds.Should().NotBeEmpty();
        plan.Builds[0].Should().Match<SceneFieldPlan.Build>(b => b.Cascade == 0 && b.First, "the finer cascade comes first");
        var after = new List<SceneFieldPlan.Build>(plan.Builds);
        for (int frame = 0; frame < 60 && !after.Any(b => b.Cascade == 1); frame++)
        {
            plan.Update(eye, drawn);
            after.AddRange(plan.Builds);
        }
        after.First(b => b.Cascade == 1).First.Should().BeTrue("the coarser cascade's build starts again");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void On_The_Device_Five_Hundred_Meshes_Arriving_At_Once_Are_Built_With_No_Frame_Near_The_Field_Time_Of_All_At_Once()
    {
        // The field's time each frame, the profile's gpu.scene_field as the renderer reads it back,
        // the slowest of a run that draws 500 sections of 1,024 triangles from its first frame until
        // the field has built them, and how many frames its builds took.
        (double Slowest, int Frames) Run(int triangleBudget)
        {
            var config = Config.Default.WithWindow("field budget", 64, 48) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(4, 0.25f, 4, triangleBudget);
            var section = LoadModelFromMesh(GenMeshPlane(4, 4, 16, 32));
            section.Meshes[0].TriangleCount.Should().Be(SectionTriangles);
            var camera = new Camera3D(new Vector3(0, 2, 0), new Vector3(0, 0, -10), Vector3.UnitY, 60);
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var (slowest, building) = (0.0, 0);
            for (int frame = 0; frame < 120; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                for (int i = 0; i < Sections; i++) DrawModel(section, SectionAt(i) + new Vector3(2, 0, 2), 1, Color.White);
                EndMode3D();
                EndDrawing();
                foreach (var (node, ms) in renderer.Timings.NodeGpu)
                    if (node == "scene_field") slowest = Math.Max(slowest, ms);
                if (renderer.RenderWorld.TryGet<SceneFieldRenderer>()?.Plan is { } plan && plan.Builds.Count > 0 && plan.StillCount > 0) building++;
            }
            var field = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;
            (field.StillCount, field.Waiting, field.Builds.Count).Should().Be((Sections, 0, 0), "the field has built every section by the run's end");
            UnloadModel(section);
            CloseWindow();
            UseApp(null);
            return (slowest, building);
        }

        var whole = Run(int.MaxValue);
        var budgeted = Run(65_536);
        // Lavapipe read the frame that built all 500 at once at 5,565 ms, and with a budget of 65,536
        // triangles the slowest of the 14 frames the builds took at 812 ms, the default's 32,768
        // 312 ms over 27, the field's time a frame held to about the budget's share of the whole.
        budgeted.Frames.Should().BeGreaterThan(whole.Frames, "the budget spreads the builds over more frames");
        budgeted.Slowest.Should().BeLessThan(whole.Slowest / 2, $"no frame's field time is near the frame that built them all at once, {budgeted.Slowest:0.0} ms against {whole.Slowest:0.0} ms");
    }
}
