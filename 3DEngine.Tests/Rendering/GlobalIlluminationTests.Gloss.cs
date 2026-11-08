using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A probe facing a sunlit wall, the gloss of what its rays meet.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryTheory]
    [Trait("Category", "Render")]
    [InlineData(0f, 0.3f, 0.120f)]
    [InlineData(0f, 1f, 0.164f)]
    [InlineData(1f, 0.6f, 0.168f)]
    [InlineData(1f, 0.3f, 0.213f)]
    public void A_Probe_Facing_A_Sunlit_Wall_Reads_Its_Gloss_Within_Its_Measured_Difference_From_The_Reference(float metallic, float roughness, float bound)
    {
        // A white wall five units high under the sun at 45 degrees, a dark floor before it, and the
        // first cascade's probe a unit from the wall, its face toward the wall read against a
        // reference of one bounce whose paths reflect by every face's gloss. A probe's ray lights
        // what it meets by its gloss's share at the ray's angle of the light from all around and
        // its color past its metal of what the gloss leaves (reflectedShare in probelight.slang),
        // as though the face reflected it evenly, so a metal's reflection of the sun, which the
        // reference's GGX lobe throws along its mirror way into the probe, is read spread over
        // every way. On the development machine's GPU the face reads 1.104 and 1.142 of the
        // reference for dielectrics of a roughness of 0.3 and 1, and the metals' shortfall shows,
        // 0.854 and 0.815 at 0.6 and 0.3. Taken as a white diffuse face, as the probes took every
        // face before, the metal of 0.6 read 1.27 and that of 0.3 0.98. Each bound is 15% over its
        // reading's distance from the reference.
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

    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Closed_Room_Of_Smooth_White_Walls_Under_A_Lamp_Holds_Its_Light_Steady()
    {
        // A closed room whose walls are a white of 250 and nearly smooth, a lamp inside, its back
        // wall read in linear light once the light has settled and again 300 frames later. A probe's
        // ray's hit sends on its gloss's share and its diffuse share of what the gloss leaves
        // (reflectedShare in probelight.slang), no more than reaches it, and the wall reads 11.80
        // both times on the development machine's GPU. With the whole diffuse share beside the
        // gloss's, a wall met at a grazing way sent on nearly twice what reached it, and the light
        // past the first bounce, which comes back to the probes each frame, grew without end, the
        // wall 13,247 and then 20,935. The bound on the wall's light is 15% over its reading.
        Open();
        SetSceneField(3, 0.25f, 2);
        SetSceneFieldCenter(new Vector3(0, 1.5f, 0));
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        CreatePointLight(new Vector3(0, 2.4f, 0), Color.White, 3, range: 12);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(250, 250, 250)) { Roughness = 0.05f };
        var camera = new Camera3D(new Vector3(0, 1.5f, 1.6f), new Vector3(0, 1.4f, -2), Vector3.UnitY, 70);
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), Color.White);
        float Wall(int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                Box(-2.3f, 2.3f, -0.3f, 0, -2.3f, 2.3f);
                Box(-2.3f, 2.3f, 3, 3.3f, -2.3f, 2.3f);
                Box(-2.3f, -2, 0, 3, -2.3f, 2.3f);
                Box(2, 2.3f, 0, 3, -2.3f, 2.3f);
                Box(-2, 2, 0, 3, -2.3f, -2);
                Box(-2, 2, 0, 3, 2, 2.3f);
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            var (width, sum, count) = ((int)GetScreenWidth(), 0.0, 0);
            for (int y = 40; y < 80; y++)
                for (int x = width / 2 - 30; x < width / 2 + 30; x++)
                {
                    var i = (y * width + x) * 4;
                    sum += 0.2126 * light[i] + 0.7152 * light[i + 1] + 0.0722 * light[i + 2];
                    count++;
                }
            return (float)(sum / count);
        }
        var settled = Wall(SceneFieldPlan.SettleFrames + 300);
        var later = Wall(300);
        float.IsFinite(later).Should().BeTrue("the wall's light stays a number");
        (later / settled).Should().BeInRange(0.99f, 1.01f, $"the wall reads {settled:0.0000} and 300 frames later {later:0.0000}");
        later.Should().BeLessThan(13.6f, "the walls send on no more of the lamp's light than reaches them");
        UnloadModel(slab);
    }
}
