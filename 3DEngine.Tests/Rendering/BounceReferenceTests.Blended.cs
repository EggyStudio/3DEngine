using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The reference meeting a copy the model pass blends at less than half of it, as a decal, by the
// chance its coverage gives.
public sealed partial class BounceReferenceTests
{
    [NeedsRayQueryFact]
    public void A_Blended_Layer_Is_Met_In_The_Reference_By_Its_Coverage()
    {
        // A light slab with a dark sheet blended at 0.35 lying over its middle, as a decal lies on
        // a wall, under a sun from above. Left out of the reference with the field, the sheet left
        // the slab under it as light as the slab past it. Met by its coverage, the slab under it
        // keeps 0.65 of its light and the sheet, its color 0.012 of the slab's, adds 0.35 of its
        // own, 0.655 of the light past it. The frame blends the sheet on sRGB-encoded light, as
        // raylib does, which under so dark a sheet keeps 0.48 of the light past it, so the
        // reference is held to the coverage and not to the frame. The pixels under the sheet keep
        // the slab's name, which the path naming them reads past the sheet.
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        CreateDirectionalLight(-Vector3.Normalize(new Vector3(0.2f, 1, 0.1f)), Color.White, 1);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(200, 200, 200)) { Roughness = 1 };
        var sheet = LoadModelFromMesh(GenMeshPlane(2, 2, 1, 1));
        sheet.Materials[0] = new ModelMaterial(new Color(20, 20, 20, 89)) { Roughness = 1, AlphaMode = MaterialAlphaMode.Blend, DoubleSided = false };
        var camera = new Camera3D(new Vector3(0, 3, 4), Vector3.Zero, Vector3.UnitY, 45);
        for (int frame = 0; frame < 12; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(4, 0.2f, 4), Color.White);
            DrawModelEx(sheet, new Vector3(0, 0.002f, 0), Vector3.UnitY, 0, Vector3.One, Color.White);
            EndMode3D();
            EndDrawing();
        }
        var path = Path.Combine(_folder.Path, "sheet.png");
        BounceReference.Trace(GetApp().World, path, 256, bounces: 0).Should().StartWith("traced");
        var regions = BounceReference.Measure(GetApp().World, path, out var why, out _);
        regions.Should().NotBeNull(why);
        var floor = regions!.Single(r => r.Name.EndsWith("floor", StringComparison.Ordinal));
        floor.Pixels.Should().BeGreaterThan(5000, "the slab's top fills most of the view, under the sheet as past it");

        // The view's middle shows the sheet and the pixels to its right the slab alone.
        var (width, _, light) = BounceReference.ReadPfm(path + ".pfm");
        float Block(int left, int right)
        {
            var sum = 0f;
            for (int y = 52; y <= 68; y++)
                for (int x = left; x <= right; x++)
                    sum += light[(y * width + x) * 3 + 1];
            return sum / (17 * (right - left + 1));
        }
        var (under, past) = (Block(70, 90), Block(116, 130));
        (under / past).Should().BeApproximately(0.655f, 0.02f, $"the reference meets the sheet by its coverage, {under} under it against {past} past it");
        UnloadModel(sheet);
        UnloadModel(slab);
    }
}
