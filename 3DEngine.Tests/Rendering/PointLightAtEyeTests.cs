using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A room lit by a point light standing at the camera's own point, as a game's held light is, read
/// for pixels left black, which a light at the eye leaves none of, every face the eye sees facing it.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class PointLightAtEyeTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-eye-light-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void A_Room_Lit_By_A_Point_Light_At_The_Eye_Has_No_Black_Pixel()
    {
        // A closed room six units across of slabs a fifth of a unit thick, with a block in it,
        // glossy and rough, lit by a point light at the camera's point and nothing else. With
        // Fresnel's base taken unclamped, a dot rounding a hair over 1 gave the fifth power of a
        // base under 0, and lavapipe read 12,617 of 57,600 pixels black where the RTX 4070 read none.
        var config = Config.Default.WithWindow("eye light test", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var eye = new Vector3(1.2f, 1.6f, 2.1f);
        CreatePointLight(eye, Color.White, 4, range: 20);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(200, 190, 170)) { Roughness = 0.6f };
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        block.Materials[0] = new ModelMaterial(new Color(180, 60, 50)) { Roughness = 0.3f, Metallic = 0.2f };
        var camera = new Camera3D(eye, new Vector3(-1.5f, 0.6f, -1.5f), Vector3.UnitY, 70);
        var path = Path.Combine(_folder.Path, "room.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(6, 0.2f, 6), Color.White);
            DrawModelEx(slab, new Vector3(0, 3.1f, 0), Vector3.UnitY, 0, new Vector3(6, 0.2f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-3.1f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.2f, 3.2f, 6), Color.White);
            DrawModelEx(slab, new Vector3(3.1f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.2f, 3.2f, 6), Color.White);
            DrawModelEx(slab, new Vector3(0, 1.5f, -3.1f), Vector3.UnitY, 0, new Vector3(6, 3.2f, 0.2f), Color.White);
            DrawModelEx(slab, new Vector3(0, 1.5f, 3.1f), Vector3.UnitY, 0, new Vector3(6, 3.2f, 0.2f), Color.White);
            DrawModelEx(block, new Vector3(-1.2f, 0.5f, -1.0f), Vector3.UnitY, 25, Vector3.One, Color.White);
            EndMode3D();
            if (frame == 2) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        var image = LoadImage(path);
        var black = 0;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                var c = GetImageColor(image, x, y);
                if (Math.Max(c.R, Math.Max(c.G, c.B)) < 3) black++;
            }
        black.Should().Be(0, $"every face the eye sees faces the light at the eye, where {black} of {image.Width * image.Height} pixels are black");
        UnloadModel(slab);
        UnloadModel(block);
    }
}
