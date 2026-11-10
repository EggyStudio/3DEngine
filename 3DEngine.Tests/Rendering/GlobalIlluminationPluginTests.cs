using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Global illumination as a plugin of its own: a program built from the default plugins without it
/// asks for it and draws as one with it off, holding none of its node, renderer or settings.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class GlobalIlluminationPluginTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-illumination-plugin-");
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // The default plugins without global illumination's.
    private sealed class WithoutIllumination : IPluginGroup
    {
        public IEnumerable<IPlugin> GetPlugins() => new DefaultPlugins().GetPlugins().Where(plugin => plugin is not GlobalIlluminationPlugin);
    }

    // A lit block on a floor, models the model pass lights with global illumination, drawn until the field has settled and the light has bounced, global
    // illumination asked for at a quality, and the last frame read back.
    private Color[] Drawn(IPluginGroup plugins, GlobalIllumination quality, out Engine.Renderer renderer)
    {
        var config = Config.Default.WithWindow("illumination plugin", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        var app = new App(config);
        app.AddPlugins(plugins);
        UseApp(app);
        SetGlobalIllumination(quality);
        SetAmbientLight(Color.Black, 0);
        var light = CreatePointLight(new Vector3(0, 3, 2), Color.White, 2);
        var camera = new Camera3D(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, 50);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 60 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(block, Vector3.Zero, 1, new Color(200, 80, 60));
            DrawModelEx(block, new Vector3(0, -0.6f, 0), Vector3.UnitY, 0, new Vector3(6, 0.2f, 6), Color.Gray);
            EndMode3D();
            if (frame == SceneFieldPlan.SettleFrames + 40) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the frame is written once it has finished on the GPU");
        var shot = LoadImage(path);
        renderer = GetApp().World.Resource<Engine.Renderer>();
        var pixels = Enumerable.Range(0, 96 * 64).Select(i => GetImageColor(shot, i % 96, i / 96)).ToArray();
        UnloadLight(light);
        UnloadModel(block);
        return pixels;
    }

    [NeedsVulkanFact]
    public void A_Program_Built_Without_The_Plugin_Draws_As_With_Global_Illumination_Off_And_Holds_None_Of_It()
    {
        var without = Drawn(new WithoutIllumination(), GlobalIllumination.High, out var renderer);
        renderer.Graph.ContainsNode("global_illumination").Should().BeFalse("no part added the probes' node");
        renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>().Should().BeNull("no part set the illumination's renderer");
        renderer.RenderWorld.TryGet<GlobalIlluminationSettings>().Should().BeNull("no extract carries its settings to the render world");
        CloseWindow();
        UseApp(null);

        var off = Drawn(new DefaultPlugins(), GlobalIllumination.Off, out _);
        without.Should().Equal(off, "the frame is the one drawn with global illumination off");
        CloseWindow();
        UseApp(null);

        // With the plugin, as an offscreen run's renderer starts before it adds its part, the part
        // starts at once and the light bounces.
        var with = Drawn(new DefaultPlugins(), GlobalIllumination.High, out renderer);
        renderer.Graph.ContainsNode("global_illumination").Should().BeTrue("the plugin's part added the probes' node");
        renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes.Should().NotBeNull("the probes are traced");
        with.Should().NotEqual(off, "the light bounces where the plugin is built");
    }
}
