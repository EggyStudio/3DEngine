using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A closed room of thin walls with a lit board outside it past the lamp.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Medium)]
    [InlineData(GlobalIllumination.High)]
    public void A_Lit_Board_Outside_A_Closed_Room_Of_Thin_Walls_Lights_Nothing_Inside(GlobalIllumination quality)
    {
        // The wall foot's room, walls 0.3 thick in cells of 0.5 on a floor running on past them, a
        // bright lamp outside low beside the east wall and a board three units past it lit by the
        // lamp, the room's inside read in linear light by its probes alone, the screen's off, which
        // a path-traced reference takes as dark. The cascades of 8 a side laid out their reach as
        // the mean of each texel's four rays, and the whole view read 0.0195 at Medium and 0.0197
        // at High. With each ray's own reach it reads 0.0016 at both.
        const int width = 160, height = 90;
        var config = Config.Default.WithWindow("board", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.5f, 2);
        SetSceneFieldCenter(new Vector3(1, 1.5f, 1));
        SetGlobalIllumination(quality);
        SetAmbientLight(Color.Black, 0);
        GetApp().World.Resource<GlobalIlluminationSettings>().ScreenOff = true;
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(230, 230, 230)) { Roughness = 0.9f };
        const float Half = 0.15f, High = 3, Low = -1, Up = 3;
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), Color.White);
        CreatePointLight(new Vector3(Up + 1.2f, 0.5f, 1), Color.White, 25, range: 10, castsShadows: true);
        var camera = new Camera3D(new Vector3(-0.4f, 1.6f, 2.4f), new Vector3(2.8f, 0.2f, -0.2f), Vector3.UnitY, 60);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 240; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            Box(-8, 8, -0.6f, 0, -8, 8);
            Box(Up - Half, Up + Half, 0, High, Low - Half, Up + Half);
            Box(Low - Half, Low + Half, 0, High, Low - Half, Up + Half);
            Box(Low + Half, Up - Half, 0, High, Low - Half, Low + Half);
            Box(Low + Half, Up - Half, 0, High, Up - Half, Up + Half);
            Box(Low - Half, Up + Half, High, High + 0.6f, Low - Half, Up + Half);
            Box(Up + 3, Up + 3.3f, 0, High, Low - 2, Up + 2);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        double sum = 0;
        for (int i = 0; i < width * height; i++)
            sum += 0.2126 * light[i * 4] + 0.7152 * light[i * 4 + 1] + 0.0722 * light[i * 4 + 2];
        var inside = (float)(sum / (width * height));
        UnloadModel(slab);
        inside.Should().BeLessThan(0.00184f, $"the board's light reaches none of the room's inside, which reads {inside:0.00000}");
    }
}
