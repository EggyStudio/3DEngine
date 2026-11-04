using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// How far a probe's rays went against where the reference's path through each texel's middle first
// met a surface, which gi.probe says beside each way.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Probes_Rays_In_A_Closed_Room_Reach_Where_The_References_Paths_First_Meet_Its_Walls()
    {
        // A closed room 4 across and 3 high under a lamp, the first cascade's probe at its middle,
        // every way meeting a wall, the floor or the ceiling within a few units.
        Open();
        SetSceneField(3, 0.25f, 2);
        SetSceneFieldCenter(new Vector3(0, 1.5f, 0));
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), new Color(200, 200, 200));
        CreatePointLight(new Vector3(0, 2.6f, 0), Color.White, 10, range: 10, castsShadows: true);
        var camera = new Camera3D(new Vector3(-1.5f, 1.6f, 1.5f), new Vector3(1, 1, -1), Vector3.UnitY, 60);
        const float Wall = 0.3f;
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            Box(-2 - Wall, 2 + Wall, -Wall, 0, -2 - Wall, 2 + Wall);
            Box(-2 - Wall, 2 + Wall, 3, 3 + Wall, -2 - Wall, 2 + Wall);
            Box(-2 - Wall, -2, 0, 3, -2, 2);
            Box(2, 2 + Wall, 0, 3, -2, 2);
            Box(-2 - Wall, 2 + Wall, 0, 3, -2 - Wall, -2);
            Box(-2 - Wall, 2 + Wall, 0, 3, 2, 2 + Wall);
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(0, 1.5f, 0), 16, -1, 0, out var why);
        probe.Should().NotBeNull(why);
        var hits = probe!.Each.Where(way => way.Met < 2).ToArray();
        hits.Should().NotBeEmpty();
        hits.Should().OnlyContain(way => way.ReferenceMet > 0, "every way of a closed room meets a surface in the reference");
        // The probe's four rays a texel against the texel's middle, a quarter of a texel apart, which
        // along a wall met at a slant may lie half a unit apart.
        hits.Count(way => MathF.Abs(MathF.Abs(way.Reach) - way.ReferenceMet) <= 0.25f * way.ReferenceMet).Should().BeGreaterThan(hits.Length * 3 / 4,
            $"the probe's rays reach the walls the reference's paths meet, {string.Join(", ", hits.Select(way => $"{way.Reach:0.00}/{way.ReferenceMet:0.00}"))}");
        UnloadModel(slab);
    }
}
