using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A narrow passage walled in thick stone off a sunlit court, as Sponza's vestibule opens off its
// atrium, whose probes of the cascades above stand inside the stone.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Probe_Deep_In_A_Narrow_Walled_Passage_Reads_The_Sunlit_Court_Through_Its_Opening_As_The_Reference_Does()
    {
        // A passage a unit wide and a unit and a half high under a roof, its walls and roof a unit
        // and a half thick, runs eight units in from a court the sun lights from high over it, in
        // the cells of Sponza's field, whose cascades' probes stand half a unit, one and two apart.
        // The first cascade's probe five units in, under the roof by a wall, reads its face toward
        // the opening against a reference of one bounce, the sunlit court floor its light. Its rays
        // that way leave its interval with nothing met, and taking the light past it from the
        // probes of the cascades above, which stand in the stone around it, the face reads nothing
        // where the reference reads the court. So such a ray, its probes above seen for under half
        // their weight, is traced on and lit by what it meets, as Sponza's vestibule off its atrium
        // is.
        Open();
        SetSceneField(4, 0.125f, 2);
        SetSceneFieldCenter(new Vector3(3, 1, 0));
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var elevation = 60 * MathF.PI / 180;
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.4f, -MathF.Sin(elevation), -0.3f)), Color.White, 3, castsShadows: true);
        GetApp().World.Resource<GlobalIlluminationSettings>().AgainOff = true;
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0].Roughness = 1;
        var gray = new Color(160, 160, 160);
        var camera = new Camera3D(new Vector3(-6, 4, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 60);
        // The court lies at x below 0, the passage along +x from its opening at x 0.
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), gray);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            Box(-12, 10, -0.5f, 0, -12, 12);
            Box(0, 8, 0, 3, -2, -0.5f);
            Box(0, 8, 0, 3, 0.5f, 2);
            Box(0, 8, 1.5f, 3, -0.5f, 0.5f);
            Box(8, 9.5f, 0, 3, -2, 2);
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(5, 1.2f, 0), 256, 0, 0, out var why);
        probe.Should().NotBeNull(why);
        // The face toward the opening, -x, the second of the six.
        var (opening, reference) = (probe!.FacesMerged[1].X, probe.FacesReference[1].X);
        reference.Should().BeGreaterThan(0, "the sunlit court floor lies within the face's hemisphere through the opening");
        opening.Should().BeInRange(reference * 0.75f, reference * 1.25f,
            $"the probe at {probe.Middle} reads the court through the opening, {opening:0.00000} against the reference's {reference:0.00000}");
        UnloadModel(slab);
    }
}
