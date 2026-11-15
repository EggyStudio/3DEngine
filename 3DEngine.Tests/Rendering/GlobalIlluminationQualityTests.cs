using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A quality of global illumination the enum names none of, refused with an error naming it and the
/// quality kept as it was, so the frames after it still draw.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class GlobalIlluminationQualityTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [NeedsVulkanFact]
    [ExpectsError("Engine.GlobalIllumination", "names none of Off, Low, Medium and High, so it stays Low")]
    public void A_Quality_That_Names_No_Value_Is_Refused_And_The_Frames_After_It_Draw()
    {
        // A quality of 7 and one of -1, refused with an error naming each, the quality staying Low,
        // and the frames after them drawn, each cleared to its own color.
        var config = Config.Default.WithWindow("quality test", 64, 48) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetGlobalIllumination(GlobalIllumination.Low);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 1.5f, 3), Vector3.Zero, Vector3.UnitY, 50);
        Frames(cube, camera, 2, Color.Black);
        SetGlobalIllumination((GlobalIllumination)7);
        SetGlobalIllumination((GlobalIllumination)(-1));
        GetApp().World.Resource<GlobalIlluminationSettings>().Quality.Should().Be(GlobalIllumination.Low);
        Frames(cube, camera, 4, Color.DarkBlue);
        var screen = LoadImageFromScreen();
        var corner = GetImageColor(screen, 1, 1);
        UnloadImage(screen);
        corner.B.Should().BeGreaterThan(100, "the frames after the refused values are drawn, cleared to their own color");
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    [ExpectsError("Engine.GlobalIllumination", "Global illumination's quality 9 names none of")]
    public void A_Configured_Quality_That_Names_No_Value_Leaves_It_Off()
    {
        var config = Config.Default.WithWindow("quality test", 64, 48) with
        {
            Headless = true, Offscreen = true, Samples = 1, GlobalIllumination = (GlobalIllumination)9,
        };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        GetApp().World.Resource<GlobalIlluminationSettings>().Quality.Should().Be(GlobalIllumination.Off);
    }

    private static void Frames(Model cube, Camera3D camera, int count, Color clear)
    {
        for (int frame = 0; frame < count; frame++)
        {
            BeginDrawing();
            ClearBackground(clear);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
    }
}
