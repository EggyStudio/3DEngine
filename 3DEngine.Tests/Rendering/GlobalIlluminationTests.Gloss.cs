using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A probe facing a sunlit wall, the gloss of what its rays meet.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryTheory]
    [Trait("Category", "Render")]
    [InlineData(0f, 0.3f, 0.181f)]
    [InlineData(0f, 1f, 0.184f)]
    [InlineData(1f, 0.6f, 0.161f)]
    [InlineData(1f, 0.3f, 0.207f)]
    public void A_Probe_Facing_A_Sunlit_Wall_Reads_Its_Gloss_Within_Its_Measured_Difference_From_The_Reference(float metallic, float roughness, float bound)
    {
        // A white wall five units high under the sun at 45 degrees, a dark floor before it, and the
        // first cascade's probe a unit from the wall, its face toward the wall read against a
        // reference of one bounce whose paths reflect by every face's gloss. A probe's ray lights
        // what it meets by its color past its metal and its gloss's share at the ray's angle of the
        // light from all around (reflectedShare in probelight.slang), as though the face reflected
        // it evenly, so a metal's reflection of the sun, which the reference's GGX lobe throws along
        // its mirror way into the probe, is read spread over every way. On the development machine's
        // GPU the face reads 1.157 and 1.160 of the reference for dielectrics of a roughness of 0.3
        // and 1, and the metals' shortfall shows, 0.860 and 0.820 at 0.6 and 0.3. Taken as a white
        // diffuse face, as the probes took every face before, the metal of 0.6 read 1.27 and that of
        // 0.3 0.98. Each bound is 15% over its reading's distance from the reference.
        Open();
        SetSceneField(3, 0.25f, 2);
        SetSceneFieldCenter(new Vector3(2, 1.5f, 0));
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, -1, 0)), Color.White, 1, castsShadows: true);
        GetApp().World.Resource<GlobalIlluminationSettings>().AgainOff = true;
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        floor.Materials[0] = new ModelMaterial(new Color(40, 40, 40)) { Roughness = 1 };
        var wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        wall.Materials[0] = new ModelMaterial(Color.White) { Metallic = metallic, Roughness = roughness };
        var camera = new Camera3D(new Vector3(6, 3, 5), new Vector3(1, 1.5f, 0), Vector3.UnitY, 60);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(floor, new Vector3(5, -0.1f, 0), Vector3.UnitY, 0, new Vector3(10, 0.2f, 8), Color.White);
            DrawModelEx(wall, new Vector3(-0.15f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5, 8), Color.White);
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(1.6f, 1.2f, 0), 256, 0, 0, out var why);
        probe.Should().NotBeNull(why);
        // The face toward the wall, -x, the second of the six.
        var (face, reference) = (probe!.FacesMerged[1].X, probe.FacesReference[1].X);
        Math.Abs(face / reference - 1).Should().BeLessThan(bound, $"the probe at {probe.Middle} reads its face toward the wall {face:0.00000} against {reference:0.00000}");
        UnloadModel(floor);
        UnloadModel(wall);
    }
}
