using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The floor in a wall's long shadow under a low sun, as a probe in a field of coarse cells reads
// it, against a path-traced reference of the same probe.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Probe_Over_A_Walls_Long_Shadow_In_Coarse_Cells_Reads_The_Shaded_Floor_As_The_Reference_Does()
    {
        // A wall two units high under a sun 20 degrees up casts a shadow 5.5 units long on the
        // floor, and the probe of the first cascade nearest a point over its far half, in a field of
        // cells of half a unit, reads its face toward the floor against a reference of one bounce.
        // A hit taken a cell and a half off the floor before the shadow map was read, 0.75 units,
        // stood over the wall from 3.4 units out, so the far 2.1 units of the shadow read sunlit,
        // and the probe, which stands at 6, 2, 2, read its face 0.979 against the reference's 0.629
        // on the development machine's GPU, where moved a cell toward the sun, which reads the map at
        // the same place, it reads 0.633.
        Open();
        SetSceneField(3, 0.5f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var elevation = 20 * MathF.PI / 180;
        CreateDirectionalLight(Vector3.Normalize(new Vector3(MathF.Cos(elevation), -MathF.Sin(elevation), 0)), Color.White, 3, castsShadows: true);
        GetApp().World.Resource<GlobalIlluminationSettings>().AgainOff = true;
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0].Roughness = 1;
        var gray = new Color(160, 160, 160);
        var camera = new Camera3D(new Vector3(4, 3, 8), new Vector3(4, 0, 0), Vector3.UnitY, 60);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.25f, 0), Vector3.UnitY, 0, new Vector3(40, 0.5f, 40), gray);
            DrawModelEx(slab, new Vector3(0, 1, 0), Vector3.UnitY, 0, new Vector3(0.5f, 2, 20), gray);
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(4.5f, 1, 0), 256, 0, 0, out var why);
        probe.Should().NotBeNull(why);
        var (floor, reference) = (probe!.FacesMerged[3].X, probe.FacesReference[3].X);
        reference.Should().BeGreaterThan(0, "the sunlit floor past the shadow lies within the face's hemisphere");
        floor.Should().BeInRange(reference * 0.75f, reference * 1.25f,
            $"the probe at {probe.Middle} reads the floor under it in the shadow, {floor:0.0000} against the reference's {reference:0.0000}");
        UnloadModel(slab);
    }
}
