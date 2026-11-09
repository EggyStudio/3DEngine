using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering.Immediate;

/// <summary>A texture drawn at its own size through a filter that blends, read pixel by pixel.</summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class TextureFilterTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-texture-filter-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void A_Texture_Drawn_At_Its_Own_Size_Through_A_Blending_Filter_Keeps_Its_Pixels()
    {
        // A checker of single pixels, black and white, drawn at its own size where the window's
        // pixels meet its texels, through the bilinear filter LoadTextureFromImage gives it. Taking
        // sixteen samples along a slant, lavapipe blurred it, its pixels up to 43 levels off black
        // and white where a GPU leaves them whole, and textures_cellular_automata read 7.0% of its
        // pixels apart from raylib's program where with one sample 1.0% (build/raylib-bench/compare.py).
        var config = Config.Default.WithWindow("texture filter test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var checker = LoadTextureFromImage(GenImageChecked(64, 64, 1, 1, Color.Black, Color.White));
        var path = Path.Combine(_folder.Path, "checker.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Gray);
            DrawTexture(checker, 16, 16, Color.White);
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        var image = LoadImage(path);

        // How far the worst of the checker's pixels lies from black or white.
        var worst = 0;
        for (int y = 16; y < 80; y++)
            for (int x = 16; x < 80; x++)
            {
                var c = GetImageColor(image, x, y);
                worst = Math.Max(worst, Math.Min(c.R, 255 - c.R));
            }
        worst.Should().BeLessThan(3, "each pixel of a texture drawn at its own size is its texel's");
        UnloadTexture(checker);
    }
}
