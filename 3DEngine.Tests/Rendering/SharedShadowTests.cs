using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// One scene drawn into two render textures side by side, as a split screen draws it: views whose
/// cameras stand near share one set of the sun's cascades, drawn once, and each reads its shadows
/// as it does drawn alone, and views far apart keep their own.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class SharedShadowTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-shared-shadow-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static Camera3D Looking(float x) => new(new Vector3(x, 4, 8), new Vector3(x, 0, 0), Vector3.UnitY, 45);

    // The views drawn into textures of 160 by 120 shown side by side, and how many times the
    // shadow map was drawn in the frame captured.
    private (Image Frame, int Maps) Capture(Model slab, params Camera3D[] cameras)
    {
        var textures = cameras.Select(_ => LoadRenderTexture(160, 120)).ToArray();
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        var maps = 0;
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            for (int v = 0; v < cameras.Length; v++)
            {
                BeginTextureMode(textures[v]);
                ClearBackground(Color.Black);
                BeginMode3D(cameras[v]);
                DrawModelEx(slab, new Vector3(20, -0.1f, 0), Vector3.UnitY, 0, new Vector3(80, 0.2f, 20), Color.White);
                foreach (var x in new[] { 0f, 30f })
                    DrawModelEx(slab, new Vector3(x, 1, 0), Vector3.UnitY, 0, new Vector3(1, 2, 1), new Color(200, 120, 60));
                EndMode3D();
                EndTextureMode();
            }
            BeginDrawing();
            ClearBackground(Color.Black);
            for (int v = 0; v < cameras.Length; v++)
                DrawTextureRec(textures[v].Texture, new Rectangle(0, 0, 160, -120), new Vector2(160 * v, 0), Color.White);
            if (frame == 4) TakeScreenshot(path);
            EndDrawing();
            if (frame == 4) maps = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>().ShadowMapsDrawn;
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        foreach (var texture in textures) UnloadRenderTexture(texture);
        return (LoadImage(path), maps);
    }

    // The share of a view's pixels, at a column offset in each frame, apart by more than 24 of 255.
    private static double Apart(Image a, int fromA, Image b, int fromB)
    {
        var apart = 0;
        for (int y = 0; y < 120; y++)
            for (int x = 0; x < 160; x++)
            {
                Color p = GetImageColor(a, fromA + x, y), q = GetImageColor(b, fromB + x, y);
                if (Math.Max(Math.Max(Math.Abs(p.R - q.R), Math.Abs(p.G - q.G)), Math.Abs(p.B - q.B)) > 24) apart++;
            }
        return apart / (160.0 * 120);
    }

    [NeedsVulkanFact]
    public void Two_Near_Views_Draw_The_Suns_Shadow_Once_And_Read_It_As_Each_Does_Alone()
    {
        var config = Config.Default.WithWindow("shared shadow test", 320, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), Color.White, 2, castsShadows: true);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        var (both, shared) = Capture(slab, Looking(0), Looking(0.3f));
        var (left, _) = Capture(slab, Looking(0));
        var (right, _) = Capture(slab, Looking(0.3f));
        var (apart, own) = Capture(slab, Looking(0), Looking(30));
        var (far, _) = Capture(slab, Looking(30));

        shared.Should().Be(1, "two cameras a third of a unit apart share the sun's cascades, drawn once");
        own.Should().Be(2, "two cameras thirty units apart keep their own");
        Apart(both, 0, left, 0).Should().BeLessThan(0.01, "the first view reads its shadows from the shared cascades as from its own");
        Apart(both, 160, right, 0).Should().BeLessThan(0.01, "and so does the second");
        Apart(apart, 160, far, 0).Should().BeLessThan(0.01, "and the far view reads its own as it does alone");
        UnloadModel(slab);
    }
}
