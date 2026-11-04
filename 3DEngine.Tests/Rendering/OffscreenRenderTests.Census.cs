using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The device's census of its images, which memory.images answers with.
public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void The_Image_Census_Counts_A_Texture_By_Its_Size_While_It_Is_Loaded_And_Not_After()
    {
        Open(64, 64);
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        int Held() => device.ImageCensus().Where(entry => entry.Description.Extent is { Width: 37, Height: 23 }).Sum(entry => entry.Count);
        Held().Should().Be(0);

        var source = GenImageColor(37, 23, Color.Red);
        var texture = LoadTextureFromImage(source);
        UnloadImage(source);
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                if (texture.Id != 0) DrawTexture(texture, 0, 0, Color.White);
                EndDrawing();
            }
        }
        Frames(2);
        Held().Should().Be(1, "the texture's image is alive while it is loaded");
        device.ImageCensus().Sum(entry => entry.Count).Should().Be(device.Usage.Images, "the census counts every image the device holds");

        UnloadTexture(texture);
        texture = default;
        // Let go the fifth frame after on the development machine's GPU, the frames in flight having
        // finished.
        Frames(8);
        Held().Should().Be(0, "the image goes once the frames that drew with it have finished");
    }
}
