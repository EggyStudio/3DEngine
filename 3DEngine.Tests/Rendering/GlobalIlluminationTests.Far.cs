using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The field's far cascade, whose probes the cascades of probes below it merge, so a ray that leaves
// the counted cascades meets what the far cascade holds before the sky.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Ray_That_Leaves_The_Counted_Cascades_Meets_A_Wall_In_The_Far_Cascade_Before_The_Sky()
    {
        // A sky of 1 and a black wall 200 units wide 40 units ahead of the eye, past the two
        // counted cascades' 19 units across, so a probe of the first cascade sees the wall over most
        // of its face toward it where the far cascade holds the wall, and the sky there without it.
        float Toward(float farCascade)
        {
            Open();
            SetSceneField(2, 0.15f, 2, farCascade: farCascade);
            SetAmbientLight(Color.White, 1);
            SetGlobalIllumination(GlobalIllumination.Low);
            var wall = LoadModelFromMesh(GenMeshCube(200, 200, 1));
            Capture(() => DrawModel(wall, new Vector3(0, 1.2f, -40), 1, Color.Black));
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var probes = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!;
            probes.Cascades.Should().Be(farCascade > 0 ? 3 : 2, "the far cascade has probes of its own past those Low traces");
            var plan = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;
            plan.BuiltOrigin(plan.Cascades - 1).Should().NotBeNull("every cascade is built by the capture");
            var cubes = ((GraphicsDevice)renderer.Context.Graphics).ReadIlluminationCubes(probes);
            // The -z face of the first cascade's middle probe.
            var (p, middle) = (probes.Probes, probes.Probes / 2);
            var face = cubes[((middle * p + middle) * 6 * p + 5 * p + middle) * 4];
            UnloadModel(wall);
            CloseWindow();
            UseApp(null);
            return face;
        }

        var sky = Toward(0);
        var far = Toward(100);
        sky.Should().BeGreaterThan(MathF.PI * 0.8f, "past the counted cascades a ray takes the sky");
        far.Should().BeLessThan(sky * 0.5f, "the far cascade holds the wall, which the rays meet before the sky");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Far_Cascades_Probes_Trace_A_Part_Of_Them_A_Frame_And_All_On_A_Frame_It_Moved()
    {
        // The sky's light changes every frame, so the far cascade's probes whose rays' light changed
        // from the frame before are those that traced, a part of them in layers of probes, and all
        // of them on the frame the far cascade moved with the eye.
        Open();
        SetSceneField(2, 0.15f, 2, farCascade: 100);
        SetGlobalIllumination(GlobalIllumination.Low);
        var wall = LoadModelFromMesh(GenMeshCube(200, 200, 1));
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var lit = 1f;
        void Frame(float x)
        {
            SetAmbientLight(Color.White, lit -= 0.01f);
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(x, 1.5f, 4.5f), new Vector3(x, 1.2f, 0), Vector3.UnitY, 50));
            DrawModel(wall, new Vector3(0, 1.2f, -40), 1, Color.Black);
            EndMode3D();
            EndDrawing();
        }
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 10; frame++)
            Frame(0);
        var probes = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!;
        var (far, p, side) = (probes.Cascades - 1, probes.Probes, probes.Probes * probes.Texels[probes.Cascades - 1]);
        // The layers of probes along z whose rays' light changed since the reading before.
        var before = device.ReadProbeVolume(probes, far, merged: false);
        List<int> Changed()
        {
            var now = device.ReadProbeVolume(probes, far, merged: false);
            var layers = Enumerable.Range(0, p).Where(z => Enumerable.Range(0, side * side).Any(t => now[(z * side * side + t) * 4] != before[(z * side * side + t) * 4])).ToList();
            before = now;
            return layers;
        }
        var each = p / GraphicsDevice.FarParts;
        var seen = new HashSet<int>();
        for (int frame = 0; frame < GraphicsDevice.FarParts; frame++)
        {
            Frame(0);
            var layers = Changed();
            layers.Should().HaveCount(each, "a part of the far cascade's probes traces a frame");
            (layers[^1] - layers[0]).Should().Be(each - 1, "a part is layers of probes side by side");
            seen.UnionWith(layers);
        }
        seen.Should().HaveCount(p, "every part of the far cascade's probes traces in turn");

        var plan = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;
        var from = plan.BuiltOrigin(plan.Cascades - 1);
        var moved = false;
        for (int frame = 0; frame < 40 && !moved; frame++)
        {
            Frame(40);
            var layers = Changed();
            moved = plan.BuiltOrigin(plan.Cascades - 1) != from;
            if (moved)
                layers.Should().HaveCount(p, "every probe of the far cascade traces on the frame it moved");
            else
                layers.Should().HaveCount(each, "a part traces while the far cascade waits to be built where the eye went");
        }
        moved.Should().BeTrue("the far cascade follows the eye 40 units along");
        UnloadModel(wall);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Screen_Probes_Stand_In_The_Counted_Cascades_And_A_Floor_Past_Them_Takes_The_Far_Probes()
    {
        // A floor under a sky reaching far ahead of an eye four units over it, two counted cascades
        // of 0.15 reaching about 16 units ahead and a far cascade of 100 past them, at Medium. The
        // floor 24 to 100 units off lies in the rows 31 to 44 of the window's 96.
        Open();
        SetSceneField(2, 0.15f, 2, farCascade: 100);
        SetAmbientLight(Color.White, 1);
        SetGlobalIllumination(GlobalIllumination.Medium);
        var floor = LoadModelFromMesh(GenMeshCube(400, 1, 400));
        var image = Capture(() => DrawModel(floor, new Vector3(0, -0.5f, -180), 1, Color.Gray),
            new Camera3D(new Vector3(0, 4, 0), new Vector3(0, 0, -20), Vector3.UnitY, 50));
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var screen = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen!;
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var (irradiance, geometry) = (device.ReadFloats(screen.Irradiance), device.ReadFloats(screen.Geometry));
        var probes = Enumerable.Range(0, screen.Across * screen.Down).Where(probe => geometry[probe * 4 + 3] >= 0).ToList();
        bool Lit(int probe) => irradiance[probe * 4] + irradiance[probe * 4 + 1] + irradiance[probe * 4 + 2] > 0;
        var near = probes.Where(probe => geometry[probe * 4 + 3] < 10).ToList();
        var far = probes.Where(probe => geometry[probe * 4 + 3] > 24).ToList();
        near.Should().HaveCountGreaterThan(10, "the floor near the eye holds screen probes");
        far.Should().HaveCountGreaterThan(10, "the floor past the counted cascades holds screen probes");
        near.Count(Lit).Should().BeGreaterThan(near.Count * 9 / 10, "a probe in the counted cascades traces its rays");
        far.Count(Lit).Should().Be(0, "a probe past the counted cascades traces none, the far cascade's world probes lighting its surface");
        var distant = Mean(image, 0, 33, image.Width, 8);
        distant.X.Should().BeGreaterThan(20, "the floor far ahead takes the far cascade's probes' light");
        UnloadModel(floor);
    }
}
