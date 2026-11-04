using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A lamp just outside a closed room of walls thinner than a cell, which the march toward it from a
// probe's hit inside must find hidden.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Lamp_Just_Outside_A_Thin_Wall_Is_Hidden_From_Most_Of_The_Surfaces_A_Probe_Inside_Meets()
    {
        // A closed room of walls 0.3 thick in cells of 0.5, lit by nothing inside, a lamp outside
        // half a unit up and 0.7 from the east wall's middle, and the first cascade's probe inside
        // by that wall, whose own rays' hits a reference of one bounce takes none of the lamp's
        // light at. The march toward the lamp from a hit began two cells past a cell and a half off
        // it, beyond the wall for a hit within a unit or so of it, and its hits brought 0.110 a
        // channel on the development machine's GPU, where looking from its first step they bring
        // 0.042, those so near the wall that the two cells it stops short of the lamp take the wall
        // in too still lit.
        Open();
        SetSceneField(3, 0.5f, 2);
        SetSceneFieldCenter(new Vector3(1, 1.5f, 1));
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        GetApp().World.Resource<GlobalIlluminationSettings>().AgainOff = true;
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0].Roughness = 1;
        const float Half = 0.15f, High = 3, Low = -1, Up = 3;
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), new Color(200, 200, 200));
        CreatePointLight(new Vector3(Up + 0.7f, 0.5f, 1), Color.White, 25, range: 10, castsShadows: true);
        var camera = new Camera3D(new Vector3(-0.4f, 1.6f, 2.4f), new Vector3(2.8f, 0.2f, -0.2f), Vector3.UnitY, 60);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            Box(-8, 8, -0.6f, 0, -8, 8);
            Box(Up - Half, Up + Half, 0, High, Low - Half, Up + Half);
            Box(Low - Half, Low + Half, 0, High, Low - Half, Up + Half);
            Box(Low + Half, Up - Half, 0, High, Low - Half, Low + Half);
            Box(Low + Half, Up - Half, 0, High, Up - Half, Up + Half);
            Box(Low - Half, Up + Half, High, High + 0.6f, Low - Half, Up + Half);
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(2.4f, 0.6f, 1), 256, 0, 0, out var why);
        probe.Should().NotBeNull(why);
        probe!.Hits.Should().BeGreaterThan(probe.Ways / 2, "the probe's rays meet the room's surfaces around it");
        probe.HitReference.X.Should().Be(0, "no light reaches the inside of the room");
        foreach (var channel in new[] { probe.HitRays.X, probe.HitRays.Y, probe.HitRays.Z })
            channel.Should().BeLessThan(0.07f, $"the lamp is hidden from most of what the probe at {probe.Middle} meets, its hits bringing {probe.HitRays}");
        UnloadModel(slab);
    }
}
