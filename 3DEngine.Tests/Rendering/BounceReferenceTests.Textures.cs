using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The reference reading a copy's texture and its normal map at each point it meets.
public sealed partial class BounceReferenceTests
{
    [NeedsRayQueryFact]
    public void A_Slab_Of_Two_Colors_Reads_Each_In_The_Reference_As_In_The_Frame()
    {
        // A slab whose texture is dark on one half and light on the other, under a lamp. Taking the
        // copy's color as its texture's average, the reference read the slab's top one gray
        // everywhere, each pixel 0.925 of its light off the frame's on the development machine's
        // GPU and the whole top 0.188 under it, the lamp nearer the light half; read from the
        // texture at each point, 0.003 and 0.001, the bound leaving the paths' noise room.
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        CreatePointLight(new Vector3(0.5f, 2, 0.5f), new Color(255, 230, 200), 3, range: 10);
        var halves = LoadTextureFromImage(GenImageChecked(64, 64, 32, 64, new Color(30, 30, 30), new Color(230, 230, 230)));
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(Color.White, halves) { Roughness = 1 };
        var floor = Slab(slab, new Camera3D(new Vector3(0, 3, 4), Vector3.Zero, Vector3.UnitY, 45), "halves.png");
        var apart = Luminance(floor.Absolute) / Luminance(floor.Reference);
        apart.Should().BeLessThan(0.03f, $"each pixel of the slab's top reads as the reference's, {apart:0.000} of its light apart");
        UnloadModel(slab);
        UnloadTexture(halves);
    }

    [NeedsRayQueryFact]
    public void A_Slab_Bent_By_Its_Normal_Map_Reads_In_The_Reference_As_In_The_Frame()
    {
        // A slab whose normal map leans every point of it 35 degrees between its texture's two
        // ways, under a sun from a corner of the sky. Lit by the face's own normal, the reference
        // read the slab's top so that the frame took 1.614 times its light on the development
        // machine's GPU; bent by the map in the triangle's tangent frame as the model pass bends
        // it, 1.001, the bound leaving the paths' noise room.
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        CreateDirectionalLight(-Vector3.Normalize(new Vector3(1, 1, 1)), Color.White, 1);
        var leaning = LoadTextureFromImage(GenImageColor(16, 16, new Color(179, 179, 233)));
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(200, 200, 200)) { Roughness = 1, NormalMap = leaning };
        var floor = Slab(slab, new Camera3D(new Vector3(0, 3, 4), Vector3.Zero, Vector3.UnitY, 45), "leaning.png");
        Math.Abs(floor.Share).Should().BeLessThan(0.02f, $"the slab's top reads in the frame {floor.Frame} as in the reference {floor.Reference}");
        UnloadModel(slab);
        UnloadTexture(leaning);
    }

    // The top of a slab four units across drawn under the lights set, through the camera given,
    // against a reference of its direct light.
    private BounceReference.RegionLight Slab(Model slab, Camera3D camera, string name)
    {
        for (int frame = 0; frame < 12; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(4, 0.2f, 4), Color.White);
            EndMode3D();
            EndDrawing();
        }
        var path = Path.Combine(_folder.Path, name);
        BounceReference.Trace(GetApp().World, path, 256, bounces: 0).Should().StartWith("traced");
        var regions = BounceReference.Measure(GetApp().World, path, out var why, out _);
        regions.Should().NotBeNull(why);
        var floor = regions!.Single(r => r.Name.EndsWith("floor", StringComparison.Ordinal));
        floor.Pixels.Should().BeGreaterThan(5000, "the slab's top fills most of the view");
        return floor;
    }

    private static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
}
