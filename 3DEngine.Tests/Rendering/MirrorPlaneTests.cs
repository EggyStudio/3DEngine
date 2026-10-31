using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws a polished floor under a lit cube offscreen and reads the floor where the cube's image
/// mirrored in it falls, with the plane a program sets (<see cref="E3D.SetReflectionPlane"/>) and
/// without it.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class MirrorPlaneTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-mirror-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void A_Polished_Floor_In_The_Plane_Mirrors_The_Lit_Cube_Over_It()
    {
        // A red cube a unit and a half across standing on a near black floor polished to 0.05, lit
        // by a sun, under a gray sky. Through the plane the floor shows the cube's red where its
        // image mirrored in the floor falls; without it the floor reflects the sky there, and to
        // the cube's side it shows the sky either way.
        var config = Config.Default.WithWindow("mirror test", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.5f, -0.7f, -0.4f)), Color.White, 2.5f, castsShadows: true);
        SetAmbientLight(new Color(150, 160, 170), 0.5f);
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        floor.Materials[0] = new ModelMaterial(new Color(20, 20, 22)) { Roughness = 0.05f };
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 2.2f, 7.5f), new Vector3(0, 0.4f, 0), Vector3.UnitY, 50);
        void Frame(string? shot = null)
        {
            BeginDrawing();
            ClearBackground(new Color(120, 120, 120));
            BeginMode3D(camera);
            DrawModelEx(floor, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(14, 0.2f, 14), Color.White);
            DrawModelEx(cube, new Vector3(0, 0.75f, 0), Vector3.UnitY, 0, new Vector3(1.5f, 1.5f, 1.5f), new Color(220, 40, 30));
            EndMode3D();
            if (shot is not null) TakeScreenshot(shot);
            EndDrawing();
        }
        Image Capture(string name)
        {
            var path = Path.Combine(_folder.Path, name + ".png");
            for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
                Frame(frame == 2 ? path : null);
            File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
            GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
            return LoadImage(path);
        }
        // How much redder than green the floor is where the cube's front face's middle, mirrored
        // in the floor, falls, and well to its side, where the floor mirrors the sky alone.
        (int Image, int Front) Red(Image image)
        {
            var mirrored = GetWorldToScreenEx(new Vector3(0, -0.6f, 0.75f), camera, image.Width, image.Height);
            var front = GetWorldToScreenEx(new Vector3(3, 0, 1), camera, image.Width, image.Height);
            var (a, b) = (GetImageColor(image, (int)mirrored.X, (int)mirrored.Y), GetImageColor(image, (int)front.X, (int)front.Y));
            return (a.R - a.G, b.R - b.G);
        }

        var plain = Red(Capture("plain"));
        SetReflectionPlane(0, Vector3.UnitY);
        // The window reads the mirrored view once a frame draws it whole, its pipelines made.
        var render = GetApp().World.Resource<Engine.Renderer>().RenderWorld;
        for (int frame = 0; frame < 600 && render.TryGet<MirrorRenderer>() is not { Whole: true }; frame++)
            Frame();
        var mirroring = Red(Capture("mirrored"));

        mirroring.Image.Should().BeGreaterThan(plain.Image + 15, $"the floor shows the cube's red where its mirrored image falls, {mirroring.Image} where {plain.Image} without the plane");
        Math.Abs(mirroring.Front - plain.Front).Should().BeLessThan(6, $"beside the cube the floor reflects the sky either way, {mirroring.Front} and {plain.Front}");
        UnloadModel(floor);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void The_First_Frame_With_A_Plane_Waits_For_None_Of_The_Mirrored_Views_Pipelines()
    {
        // A window of four samples, whose scene's pipelines are made for its own pass, where the
        // mirrored view draws at one sample through pipelines of its own, which worker threads make
        // while the frames go on. The voxel game's first frame with a plane took 24 s of the wall
        // clock with them made on the frame's own thread.
        var config = Config.Default.WithWindow("mirror test", 160, 90) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.5f, -0.7f, -0.4f)), Color.White, 2.5f);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 2.2f, 7.5f), new Vector3(0, 0.4f, 0), Vector3.UnitY, 50);
        void Frame()
        {
            BeginDrawing();
            ClearBackground(new Color(120, 120, 120));
            BeginMode3D(camera);
            DrawModelEx(cube, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(14, 0.2f, 14), new Color(20, 20, 22));
            DrawModelEx(cube, new Vector3(0, 0.75f, 0), Vector3.UnitY, 0, new Vector3(1.5f, 1.5f, 1.5f), new Color(220, 40, 30));
            EndMode3D();
            EndDrawing();
        }
        Frame();
        var render = GetApp().World.Resource<Engine.Renderer>().RenderWorld;

        SetReflectionPlane(0, Vector3.UnitY);
        Frame();
        render.TryGet<MirrorRenderer>()!.Whole.Should().BeFalse("the first frame with the plane draws the mirrored view without the pipelines still being made");
        render.TryGet<PipelineCache>()!.Making.Should().BeGreaterThan(0, "worker threads make them");
        var frames = 1;
        for (; frames < 600 && !render.TryGet<MirrorRenderer>()!.Whole; frames++)
            Frame();
        render.TryGet<MirrorRenderer>()!.Whole.Should().BeTrue($"a later frame draws it whole, here the {frames}th");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        UnloadModel(cube);
    }
}
