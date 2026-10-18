using System.Numerics;
using FluentAssertions;
using ImGuiNET;
using static Engine.E3D;

namespace Engine.Tests.Gui;

/// <summary>A program's textures drawn through ImGui, read from the pixels of an offscreen frame.</summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ImGuiTextureTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-imgui-texture-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void An_Image_Of_A_Programs_Texture_Draws_That_Texture_Beside_Text_From_The_Font_Atlas()
    {
        var config = Config.Default.WithWindow("imgui texture test", 160, 120) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var red = LoadTextureFromImage(GenImageColor(8, 8, new Color(230, 30, 30)));
        var halves = GenImageColor(8, 8, new Color(30, 200, 40));
        ImageDrawRectangle(ref halves, 4, 0, 4, 8, new Color(40, 60, 230));
        var split = LoadTextureFromImage(halves);
        UnloadImage(halves);

        // Drawn every frame, as ImGui is, the frame after ImGui's first captured.
        var path = Path.Combine(_folder.Path, "frame.png");
        for (int frame = 0; frame < 13 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            var list = ImGui.GetForegroundDrawList();
            list.AddImage(GetImGuiTextureId(red), new Vector2(10, 10), new Vector2(50, 50));
            // The second texture's right half alone, by the coordinates given.
            list.AddImage(GetImGuiTextureId(split), new Vector2(70, 10), new Vector2(110, 50), new Vector2(0.5f, 0), Vector2.One);
            list.AddText(new Vector2(10, 70), 0xFFFFFFFF, "Atlas");
            if (frame == 2) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        var image = LoadImage(path);

        var left = GetImageColor(image, 30, 30);
        ((int)left.R).Should().BeInRange(215, 245, $"the first image is the program's red texture, not a part of the font atlas, {left}");
        ((int)left.G).Should().BeLessThan(50, $"the first image is the program's red texture, not a part of the font atlas, {left}");
        var right = GetImageColor(image, 90, 30);
        ((int)right.B).Should().BeGreaterThan(200, $"the second is the blue right half its coordinates choose, {right}");
        ((int)right.G).Should().BeLessThan(90, $"the second is the blue right half its coordinates choose, {right}");
        var lit = 0;
        for (int y = 70; y < 86; y++)
            for (int x = 10; x < 50; x++)
                if (GetImageColor(image, x, y).R > 150) lit++;
        lit.Should().BeGreaterThan(20, "text drawn after the images still takes its letters from the font atlas");

        UnloadImage(image);
        UnloadTexture(red);
        UnloadTexture(split);
    }
}
