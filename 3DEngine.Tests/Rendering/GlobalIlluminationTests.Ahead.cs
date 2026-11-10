using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The field held ahead of the camera along its view by a share of its finest cascade's width.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Field_Held_Ahead_Lies_Around_The_Point_Half_Its_Finest_Cascade_Before_The_Eye()
    {
        // The voxel game's center worked out by hand, the eye plus its way ahead times 64 cells
        // times the cell size over 2, here the test's cells of 0.15.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        SetSceneFieldAhead(0.5f);
        GetSceneFieldAhead().Should().Be(0.5f);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var (eye, target) = (new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0));
        Capture(() => DrawModel(block, Vector3.Zero, 1, Color.Gray), new Camera3D(eye, target, Vector3.UnitY, 50));
        var plan = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;
        var expected = eye + Vector3.Normalize(target - eye) * (64 * 0.15f / 2);
        Vector3.Distance(plan.Eye, expected).Should().BeLessThan(1e-3f, $"the field lies around {expected}, where it lay around {plan.Eye}");

        // A center the program gives comes first.
        SetSceneFieldCenter(new Vector3(3, 0, 0));
        Capture(() => DrawModel(block, Vector3.Zero, 1, Color.Gray), new Camera3D(eye, target, Vector3.UnitY, 50));
        plan.Eye.Should().Be(new Vector3(3, 0, 0));
        UnloadModel(block);
    }
}
