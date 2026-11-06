using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The cycle of sixteen places a carried probe's rays move through, kept where they move.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.Medium)]
    [InlineData(GlobalIllumination.High)]
    public void A_Carried_Probes_Cycle_Is_Kept_Whole_Only_Where_Its_Rays_Move(GlobalIllumination quality)
    {
        // A carried probe's rays move through sixteen places and meet the meshes at High on a device
        // that traces rays, its cycle's four images a cascade made whole there, 14 MB at Sponza's
        // High, and stand-ins of a texel elsewhere, a mean of one place being that place, so Low,
        // Medium and a device that traces no rays, as lavapipe and MoltenVK, keep none of it.
        Open();
        SetGlobalIllumination(quality);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        Capture(() => DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(8, 1, 8), Color.Gray));
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var probes = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!;
        var moving = quality == GlobalIllumination.High && ((GraphicsDevice)renderer.Context.Graphics!).CanQueryRays;
        probes.CycleBytes.Should().Be(moving ? probes.Texels.Sum(n => 4L * (probes.Probes * n) * (probes.Probes * n) * probes.Probes * 8) : 0,
            $"the cycle is kept whole at {quality} only where the rays move, {(moving ? "here" : "not here")}");
        SceneFieldCommands.IlluminationState(GetApp().World).Should().Contain("world probes");
        UnloadModel(slab);
    }
}
