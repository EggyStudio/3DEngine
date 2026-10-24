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
}
