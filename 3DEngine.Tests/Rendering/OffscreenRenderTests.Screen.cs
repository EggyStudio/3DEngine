using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void The_Screen_Reads_Back_As_The_Last_Frame_Presented_Left_It()
    {
        Open(32, 16, samples: 1);
        BeginDrawing();
        ClearBackground(new Color(0, 0, 255));
        EndDrawing();

        var first = LoadImageFromScreen();
        (first.Width, first.Height).Should().Be((32, 16));
        GetImageColor(first, 4, 8).Should().Be(new Color(0, 0, 255), "the first call has no frame kept, so it reads as the window's clear color");

        BeginDrawing();
        ClearBackground(new Color(0, 0, 255));
        DrawRectangle(0, 0, 16, 16, new Color(255, 0, 0));
        EndDrawing();

        var second = LoadImageFromScreen();
        GetImageColor(second, 4, 8).Should().Be(new Color(255, 0, 0), "the frame after the first call is kept");
        GetImageColor(second, 24, 8).Should().Be(new Color(0, 0, 255));

        BeginDrawing();
        ClearBackground(new Color(0, 255, 0));
        var inside = LoadImageFromScreen();
        EndDrawing();
        GetImageColor(inside, 4, 8).Should().Be(new Color(255, 0, 0), "inside a frame, nothing of it is on the GPU yet, so the call reads the frame before");
        GetImageColor(inside, 24, 8).Should().Be(new Color(0, 0, 255));

        GetImageColor(LoadImageFromScreen(), 4, 8).Should().Be(new Color(0, 255, 0));
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the copies");
    }
}
