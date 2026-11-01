using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The probes of a cascade that see none of the probes of the cascade above, whose rays are carried
// on to the field's edge, and what those rays bring where they meet nothing there.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Floor_Under_A_Solid_Slab_In_Four_Cascades_Takes_The_Sky_Past_Its_Sides_And_Its_Underside_Reflects_Little(GlobalIllumination quality)
    {
        // A gray floor of roughness 1 lit by an ambient light alone, under a dark green slab a unit
        // thick and twenty across two and a half units over it, in a field of four cascades whose
        // last's probes stand 9.6 apart, above the slab and under the ground. The third cascade's
        // probes under the slab see none of them and carry their rays on, and those out past the
        // slab's open sides met nothing and brought nothing, so the floor read 0.0001 in linear
        // light at High where a path-traced reference takes 0.0069. Brought the sky, the slab's
        // underside took the floor's light as more than a face turned down takes in the open and
        // reflected the ambient light whole, 0.048 where the reference reads 0.0019. Read through
        // the frame's display, the floor takes 0.0008 at High and 0.0014 at Low where it took
        // 0.0000 at High, and the underside 0.0037 and 0.0033 where 0.051 with the sky brought and
        // the reflection held whole, and at Low 0.048 before either, on the development machine's
        // GPU.
        Open();
        SetSceneField(4, 0.15f, 2);
        SetGlobalIllumination(quality);
        SetAmbientLight(new Color(200, 220, 255), 1);
        var ground = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        ground.Materials[0].Roughness = 1;
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 1.2f, 7), new Vector3(0, 0.2f, 0), Vector3.UnitY, 50);
        // Drawn until every cascade is built and the light has merged through all four.
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 260 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(ground, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(30, 1, 30), new Color(128, 128, 128));
            DrawModelEx(slab, new Vector3(0, 2.5f, 0), Vector3.UnitY, 0, new Vector3(20, 1, 20), new Color(60, 90, 50));
            EndMode3D();
            if (frame == 240) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        var image = LoadImage(path);
        float Read(Vector3 point)
        {
            var at = GetWorldToScreenEx(point, camera, image.Width, image.Height);
            var c = Mean(image, (int)at.X - 4, (int)at.Y - 2, 8, 4) / 255;
            return 0.2126f * MathF.Pow(c.X, 2.2f) + 0.7152f * MathF.Pow(c.Y, 2.2f) + 0.0722f * MathF.Pow(c.Z, 2.2f);
        }

        var floor = Read(new Vector3(0, 0, 3.5f));
        var underside = Read(new Vector3(0, 2, 3));
        floor.Should().BeGreaterThan(0.0003f, $"the floor takes the sky past the slab's sides, {floor:0.0000} at {quality}");
        underside.Should().BeLessThan(0.012f, $"the underside reflects little of the ambient light, {underside:0.0000} over a floor of {floor:0.0000} at {quality}");
        foreach (var model in new[] { ground, slab }) UnloadModel(model);
    }
}
