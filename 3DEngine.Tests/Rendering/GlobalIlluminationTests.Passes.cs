using System.Numerics;
using FluentAssertions;
using RadianceCascades;
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
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        HashSet<string> Named(int frames) => NamedOver(slab, frames);

        Named(20).Should().Contain("global_illumination").And.NotContain(node => node.StartsWith("gi_", StringComparison.Ordinal),
            "the passes are timed whole unless asked");
        settings.PassesTimed = true;
        Named(10).Should().Contain(["gi_glow_faces", "gi_trace0", "gi_trace1", "gi_merge", "gi_gather"], "each pass is timed apart once asked");
        // A frame's times are read back once the frames in flight after it have finished, so those
        // recorded before the change are let pass.
        settings.PassesTimed = false;
        Named(5);
        Named(10).Should().NotContain(node => node.StartsWith("gi_", StringComparison.Ordinal), "and whole again once not");
        UnloadModel(slab);
    }

    [NeedsSplitTraceFact]
    [Trait("Category", "Render")]
    public void Under_The_Split_Layout_Its_Passes_Timed_Apart_Are_The_Split_Cascades_Each()
    {
        // The split cascades' passes each apart in the profile under the split layout once
        // gi.passes asks, a test of its own, since lavapipe before Mesa 26.3 ends the process in
        // the split cascades' kernel over this slab, where the passes of the grid above run.
        Open();
        SetGlobalIllumination(GlobalIllumination.High);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        settings.Layout = ProbeLayout.Split;
        settings.PassesTimed = true;
        NamedOver(slab, 20).Should().Contain(["gi_split_clear", "gi_split_keep", "gi_split_ask", "gi_split_ask_cascades", "gi_split_trace",
            "gi_split_light", "gi_split_merge", "gi_split_gather"], "each of the split cascades' passes is timed apart");
        settings.Layout = ProbeLayout.Dense;
        UnloadModel(slab);
    }

    // The nodes the profile times over the frames given, each drawing a slab 8 wide under a
    // camera 5 off its middle.
    private static HashSet<string> NamedOver(Model slab, int frames)
    {
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var camera = new Camera3D(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, 50);
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
}
