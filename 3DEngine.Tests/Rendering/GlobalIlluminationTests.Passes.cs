using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// Global illumination's passes timed apart in the profile.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Its_Passes_Timed_Apart_The_Profile_Names_Each_Cascades_Trace_The_Merge_And_The_Gather()
    {
        // The profile names global illumination's time whole, as gpu.global_illumination, unless
        // gi.passes asks for its passes apart, so a reading of where its time goes, for a cut of
        // High's cost, has it by pass, and an earlier reading's figure keeps its meaning.
        Open();
        SetGlobalIllumination(GlobalIllumination.High);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        var camera = new Camera3D(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, 50);
        HashSet<string> Named(int frames)
        {
            var named = new HashSet<string>();
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(8, 1, 8), Color.Gray);
                EndMode3D();
                EndDrawing();
                foreach (var (node, _) in renderer.Timings.NodeGpu) named.Add(node);
            }
            return named;
        }

        Named(20).Should().Contain("global_illumination").And.NotContain(node => node.StartsWith("gi_", StringComparison.Ordinal),
            "the passes are timed whole unless asked");
        settings.PassesTimed = true;
        Named(10).Should().Contain(["gi_trace0", "gi_trace1", "gi_merge", "gi_gather"], "each pass is timed apart once asked");
        // A frame's times are read back once the frames in flight after it have finished, so those
        // recorded before the change are let pass.
        settings.PassesTimed = false;
        Named(5);
        Named(10).Should().NotContain(node => node.StartsWith("gi_", StringComparison.Ordinal), "and whole again once not");
        UnloadModel(slab);
    }
}
