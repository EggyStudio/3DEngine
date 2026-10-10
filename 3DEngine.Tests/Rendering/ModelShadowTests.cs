using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A model's material deciding whether it casts a shadow, read from the floor under it, and a
/// caster the camera does not see still casting one where the camera's pass leaves it out.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class ModelShadowTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-shadow-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // Draws frames until the first one's capture has been written, as ReferenceFrameTests does.
    private Image Capture(Action draw, string name)
    {
        var path = Path.Combine(_folder.Path, name + ".png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            draw();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    [NeedsVulkanFact]
    public void A_Material_That_Casts_No_Shadow_Leaves_The_Floor_Under_It_Lit()
    {
        var config = Config.Default.WithWindow("shadow test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(new Vector3(0.01f, -1, 0), Color.White, 1, castsShadows: true);
        var floor = LoadModelFromMesh(GenMeshPlane(10, 10, 1, 1));
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 16, 16));
        var camera = new Camera3D(new Vector3(0, 6, 4), Vector3.Zero, Vector3.UnitY, 45);
        Action Scene(bool casts) => () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(floor, Vector3.Zero, 1, Color.White);
            sphere.Materials[0] = new ModelMaterial(Color.White) { CastsShadows = casts };
            DrawModel(sphere, new Vector3(0, 1.5f, 0), 1, Color.White);
            EndMode3D();
        };

        var shadowed = Capture(Scene(casts: true), "casts");
        var lit = Capture(Scene(casts: false), "none");

        var under = GetWorldToScreen(Vector3.Zero, camera);
        var (x, y) = ((int)under.X, (int)under.Y);
        GetImageColor(shadowed, x, y).R.Should().BeLessThan((byte)(GetImageColor(lit, x, y).R - 60), "the sphere's shadow darkens the floor under it");
        GetImageColor(lit, x, y).R.Should().BeGreaterThan(150, "with no shadow the floor is lit by the sun");
        UnloadModel(floor);
        UnloadModel(sphere);
    }

    [NeedsVulkanFact]
    public void A_Caster_Outside_The_View_Still_Shadows_The_Floor_It_Sees_And_Is_Left_Out_Of_The_Cameras_Pass()
    {
        var config = Config.Default.WithWindow("shadow test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        // The sun falls toward +x at 45 degrees, so a cube six units up and six to the left of the
        // origin, far outside the camera's narrow view, throws its shadow onto the origin.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -1, 0)), Color.White, 1, castsShadows: true);
        var floor = GenMeshPlane(10, 10, 1, 1);
        var cube = GenMeshCube(1.5f, 1.5f, 1.5f);
        var material = new ModelMaterial(Color.White);
        var camera = new Camera3D(new Vector3(0, 5, 3), Vector3.Zero, Vector3.UnitY, 30);
        var caster = new Vector3(-6, 6, 0);
        var seen = GetWorldToScreen(caster, camera);
        (seen.X is >= 0 and < 160 && seen.Y is >= 0 and < 120).Should().BeFalse("the cube lies outside the view");
        Action Scene(bool cast) => () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawMesh(floor, material, Matrix4x4.Identity);
            if (cast) DrawMesh(cube, material, Matrix4x4.CreateTranslation(caster));
            EndMode3D();
        };

        var shadowed = Capture(Scene(cast: true), "outside");
        var calls = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>().CallsByPass.ToDictionary();
        var lit = Capture(Scene(cast: false), "nothing");

        var under = GetWorldToScreen(Vector3.Zero, camera);
        var (x, y) = ((int)under.X, (int)under.Y);
        GetImageColor(shadowed, x, y).R.Should().BeLessThan((byte)(GetImageColor(lit, x, y).R - 60), "the cube's shadow reaches the floor the camera sees");
        calls.GetValueOrDefault("camera").Should().Be(1, "the camera's pass draws the floor and leaves the cube out");
        calls.Where(c => c.Key.StartsWith("cascade", StringComparison.Ordinal)).Max(c => c.Value)
            .Should().Be(2, $"the cascade holding the origin draws the cube with the floor, {string.Join(", ", calls.Select(c => $"{c.Key} {c.Value}"))}");
        UnloadMesh(floor);
        UnloadMesh(cube);
    }
}
