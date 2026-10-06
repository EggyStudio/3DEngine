using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void An_Emoji_Of_A_Color_Font_Is_Drawn_In_Its_Colors_By_White_Text()
    {
        Open(32, 32, samples: 1);
        // bitmaps.ttf's U+1F600, red over blue, at 8 pixels to the em, drawn at 16.
        var font = LoadFontEx(Path.Combine(AppContext.BaseDirectory, "Api", "bitmaps.ttf"), 8, [0x1F600]);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawTextEx(font, char.ConvertFromUtf32(0x1F600), new Vector2(8, 8), 16, 0, Color.White);
        });

        var top = GetImageColor(image, 16, 11);
        var bottom = GetImageColor(image, 16, 20);
        ((int)top.R).Should().BeGreaterThan(top.B + 150, $"the emoji's top half is red, not {top}");
        ((int)bottom.B).Should().BeGreaterThan(bottom.R + 150, $"and its bottom half blue, not {bottom}");
        UnloadFont(font);
    }
}
