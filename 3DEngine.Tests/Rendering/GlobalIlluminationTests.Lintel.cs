using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A probe whose ray meets a surface past its interval, where the probes above it see past that
// surface along the same way.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Probe_Whose_Ray_Meets_A_Lintel_Past_Its_Interval_Takes_No_Sky_The_Probes_Above_See_Under_It()
    {
        // A hall under a roof four units up opens toward -x past a lintel hanging from the roof to
        // two units up, six units from the probe read, in the cells of Sponza's field, the sky's
        // light alone lighting it. The first cascade's probe at the hall's middle reads its face
        // toward the opening against a reference of one bounce. Its rays that way leave its
        // interval and meet the lintel past it, where the probes above it, standing lower, see the
        // sky under the lintel along the same ways, and blended so the face read 0.1227 where the
        // reference reads 0.0812, 51% over, as Sponza's vestibule took the sky past its arch.
        // With its ray marched on past its interval and those probes passed over it reads 0.0778.
        Open();
        SetSceneField(4, 0.125f, 2);
        SetSceneFieldCenter(new Vector3(-2, 1.5f, 0));
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(new Color(200, 220, 255), 1);
        GetApp().World.Resource<GlobalIlluminationSettings>().AgainOff = true;
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0].Roughness = 1;
        var gray = new Color(160, 160, 160);
        var camera = new Camera3D(new Vector3(6, 3, 6), new Vector3(0, 1.5f, 0), Vector3.UnitY, 60);
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), gray);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            Box(-24, 10, -0.5f, 0, -12, 12);
            Box(-6.5f, 10.5f, 4, 4.5f, -3.5f, 3.5f);
            Box(-6.5f, -6, 2, 4, -3.5f, 3.5f);
            Box(10, 10.5f, 0, 4, -3.5f, 3.5f);
            Box(-6.5f, 10.5f, 0, 4, -4, -3.5f);
            Box(-6.5f, 10.5f, 0, 4, 3.5f, 4);
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(0, 1.5f, 0), 256, 0, 0, out var why);
        probe.Should().NotBeNull(why);
        // The face toward the opening, -x, the second of the six.
        var (opening, reference) = (probe!.FacesMerged[1], probe.FacesReference[1]);
        float Luminance(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;
        Luminance(opening).Should().BeInRange(Luminance(reference) * 0.8f, Luminance(reference) * 1.1f,
            $"the probe at {probe.Middle} reads the sky under the lintel, {Luminance(opening):0.00000} against the reference's {Luminance(reference):0.00000}");
        UnloadModel(slab);
    }
}
