using System.Numerics;
using FluentAssertions;
using Xunit.Abstractions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Ambient occlusion, read from chosen pixels of frames drawn offscreen, a cube on a floor lit by
/// ambient light and a weak sun, in the corner of two walls, whose floor darkens beside the cube's
/// foot and in the corner and nowhere far from them, in the window and in a render texture alike,
/// and the frame's passes for it, which the profile names one by one.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class AmbientOcclusionTests(ITestOutputHelper output) : IDisposable
{
    private readonly TestFolder _folder = new("engine-ao-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(2, 3, 5), new Vector3(-1, 0.5f, -1), Vector3.UnitY, 45);
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("ambient occlusion test", 320, 240) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames of the cube on its floor, into the window or into a render texture of the
    // window's size shown over it, the last of them captured.
    private Image Capture(Model floor, Model cube, int frames = 4, RenderTexture2D? into = null) => Capture(() => Scene(floor, cube), frames, into);

    // Draws frames of a scene, as above.
    private Image Capture(Action scene, int frames = 4, RenderTexture2D? into = null)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < frames + 10 && !File.Exists(path); frame++)
        {
            if (into is { } texture)
            {
                BeginTextureMode(texture);
                scene();
                EndTextureMode();
                BeginDrawing();
                ClearBackground(Color.Black);
                DrawTexture(texture.Texture, 0, 0, Color.White);
            }
            else
            {
                BeginDrawing();
                scene();
            }
            if (frame == frames - 1) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        return LoadImage(path);
    }

    private void Scene(Model floor, Model cube)
    {
        ClearBackground(Color.Black);
        BeginMode3D(_camera);
        DrawModel(floor, Vector3.Zero, 1, Color.White);
        DrawModel(cube, new Vector3(0, 0.5f, 0), 1, Color.White);
        DrawModelEx(cube, new Vector3(-1.5f, 1, -1.5f), Vector3.UnitY, 0, new Vector3(5, 2, 0.2f), Color.White);
        DrawModelEx(cube, new Vector3(-3.9f, 1, 0.9f), Vector3.UnitY, 0, new Vector3(0.2f, 2, 5), Color.White);
        EndMode3D();
    }

    private static int Sum(Color c) => c.R + c.G + c.B;

    private Color At(Image image, Vector3 world)
    {
        var p = GetWorldToScreen(world, _camera);
        return GetImageColor(image, (int)p.X, (int)p.Y);
    }

    // The floor a tenth of a unit in front of the cube, the floor in the walls' corner, and the
    // floor a unit and a half from anything. Near white with the ambient light and the sun on it,
    // the floor in a place closed off reads more than 12 under its light without the occlusion.
    private static readonly Vector3 Beside = new(0, 0, 0.6f), Corner = new(-3.7f, 0, -1.3f), Far = new(2.2f, 0, 0.3f);

    [NeedsVulkanFact]
    public void The_Floor_Darkens_Beside_A_Cube_And_In_A_Corner_And_Not_Far_From_Them()
    {
        Open();
        SetAmbientLight(Color.White, 0.6f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, -0.3f)), Color.White, 0.6f);
        var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var without = Capture(floor, cube);
        SetAmbientOcclusion(1);
        var with = Capture(floor, cube);

        foreach (var (name, place) in new[] { ("beside the cube", Beside), ("in the corner", Corner), ("far", Far) })
            output.WriteLine($"the floor {name} reads {Sum(At(with, place))} with the occlusion and {Sum(At(without, place))} without");
        Sum(At(with, Beside)).Should().BeLessThan(Sum(At(without, Beside)) - 12, "the cube closes off the floor at its foot");
        Sum(At(with, Corner)).Should().BeLessThan(Sum(At(without, Corner)) - 12, "and the walls the floor in their corner");
        Sum(At(with, Far)).Should().BeInRange(Sum(At(without, Far)) - 6, Sum(At(without, Far)) + 6, "nothing is near the floor out in the open");

        // Drawn through the HDR frame, which the model pass leaves its light linear for, it darkens alike.
        SetBloom(0.3f);
        var hdrWith = Capture(floor, cube);
        SetAmbientOcclusion(0);
        var hdrWithout = Capture(floor, cube);
        Sum(At(hdrWith, Beside)).Should().BeLessThan(Sum(At(hdrWithout, Beside)) - 12, "the HDR frame is darkened beside the cube too");
        UnloadModel(floor);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Still_View_Holds_Its_Darkening_From_Frame_To_Frame_As_Its_Slices_Turn()
    {
        // Each frame reads two slices a pixel at angles of their own, and the history keeps most of
        // what the frames before read, so the darkening of a still view moves little from one
        // frame to the next where each frame's own slices alone would show their angles.
        Open();
        SetAmbientLight(Color.White, 0.6f);
        var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        SetAmbientOcclusion(1);
        var settled = Capture(floor, cube, frames: 30);
        var next = Capture(floor, cube, frames: 1);
        double moved = 0;
        for (int y = 0; y < settled.Height; y++)
            for (int x = 0; x < settled.Width; x++)
                moved += Math.Abs(Sum(GetImageColor(settled, x, y)) - Sum(GetImageColor(next, x, y)));
        moved /= settled.Width * settled.Height;
        output.WriteLine($"a pixel moved {moved:0.000} of 765 between two frames");
        // The bound lies between a still view's movement with its history and without it.
        moved.Should().BeLessThan(0.3, "the history holds what the frames before read");
        UnloadModel(floor);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Render_Texture_Drawn_Through_A_Camera_Darkens_As_The_Window_Does()
    {
        Open();
        SetAmbientLight(Color.White, 0.6f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, -0.3f)), Color.White, 0.6f);
        var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var texture = LoadRenderTexture(GetScreenWidth(), GetScreenHeight());
        var without = Capture(floor, cube, into: texture);
        SetAmbientOcclusion(1);
        var window = Capture(floor, cube);
        var drawn = Capture(floor, cube, into: texture);

        foreach (var (place, name) in new[] { (Beside, "beside the cube"), (Corner, "in the walls' corner") })
        {
            output.WriteLine($"the render texture's floor {name} reads {Sum(At(drawn, place))} with the occlusion, {Sum(At(without, place))} without and {Sum(At(window, place))} in the window");
            Sum(At(drawn, place)).Should().BeLessThan(Sum(At(without, place)) - 12, $"the render texture's floor {name} is closed off");
            Sum(At(drawn, place)).Should().BeInRange(Sum(At(window, place)) - 9, Sum(At(window, place)) + 9,
                $"and darkens {name} as the window's does, {At(drawn, place)} against {At(window, place)}");
        }
        Sum(At(drawn, Far)).Should().BeInRange(Sum(At(without, Far)) - 6, Sum(At(without, Far)) + 6, "and not out in the open");
        UnloadRenderTexture(texture);
        UnloadModel(floor);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Floor_Is_Closed_Off_About_The_Normal_It_Is_Shaded_With_And_Else_About_The_Depth_s()
    {
        // A floor filling the view under the ambient light alone. Drawn with its vertices' normals
        // leaning 60 degrees, it is closed off by its own plane about the normal it is shaded with,
        // where the depth alone gives it the level floor's normal and nothing to close it off. A
        // floor of positions alone, which the pass beside the depth does not draw, takes the
        // depth's normal, and nothing closes it off, its bottom row and its first and last columns
        // no more than its middle.
        Open();
        SetAmbientLight(Color.White, 0.6f);
        var camera = new Camera3D(new Vector3(0, 3, 3), Vector3.Zero, Vector3.UnitY, 45);
        var leaning = Vector3.Normalize(new Vector3(MathF.Sqrt(3), 1, 0));
        Vector3[] corners = [new(-20, 0, -20), new(20, 0, -20), new(20, 0, 20), new(-20, 0, 20)];
        uint[] faces = [0, 2, 1, 0, 3, 2];
        foreach (var name in new[] { "level", "leaning", "of positions" })
        {
            var mesh = name == "of positions" ? UploadMesh(corners, faces, null, null)
                : UploadMesh([.. corners.Select(c => new ModelVertex(c, name == "leaning" ? leaning : Vector3.UnitY, Vector2.Zero))], faces);
            var floor = LoadModelFromMesh(mesh);
            void Floor()
            {
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(floor, Vector3.Zero, 1, Color.White);
                EndMode3D();
            }
            SetAmbientOcclusion(0);
            var without = Capture(Floor);
            SetAmbientOcclusion(1);
            var with = Capture(Floor);
            // The share of its light each part keeps, by the mean of its pixels with the occlusion
            // over that without it.
            double Kept(IEnumerable<(int X, int Y)> pixels) =>
                pixels.Sum(p => Sum(GetImageColor(with, p.X, p.Y))) / (double)pixels.Sum(p => Sum(GetImageColor(without, p.X, p.Y)));
            var (width, height) = (with.Width, with.Height);
            var middle = Kept(Enumerable.Range(0, width).Select(x => (x, height / 2)));
            var bottom = Kept(Enumerable.Range(0, width).Select(x => (x, height - 1)));
            var sides = Kept(Enumerable.Range(0, height).SelectMany(y => new[] { (0, y), (width - 1, y) }));
            output.WriteLine($"the floor {name} keeps {middle:F4} of its light in its middle row, {bottom:F4} in its bottom row and {sides:F4} in its first and last columns, "
                + $"{Sum(GetImageColor(without, width / 2, height / 2))} at the middle without the occlusion");
            if (name == "leaning")
                middle.Should().BeLessThan(0.9, "the floor's own plane closes off the leaning normal's half of the sky");
            else
            {
                middle.Should().BeGreaterThan(0.98, $"nothing closes off the floor {name} about a level normal");
                bottom.Should().BeGreaterThan(middle - 0.02, $"the floor {name} darkens no more in its bottom row");
                sides.Should().BeGreaterThan(middle - 0.02, $"nor in its first and last columns");
            }
            UnloadModel(floor);
        }
    }

    [NeedsVulkanFact]
    public void The_Window_Gathers_Its_Batches_Draws_Its_Depth_And_Works_Out_Its_Occlusion_In_Nodes_Of_Their_Own()
    {
        Open();
        SetAmbientLight(Color.White, 0.6f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, -0.3f)), Color.White, 0.6f);
        var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        SetAmbientOcclusion(1);
        Capture(floor, cube);

        // The profile's cpu and gpu lines are the nodes', so the gather, the depth and the occlusion
        // each read apart, in the order they run.
        var nodes = renderer.Timings.NodeCpu.Select(n => n.Node).ToList();
        nodes.Should().ContainInOrder("model_batches", "window_depth", "ambient_occlusion", "global_illumination");
        renderer.RenderWorld.TryGet<WindowDepth>().Should().NotBeNull("the occlusion reads the depth");
        renderer.RenderWorld.TryGet<AmbientOcclusionImage>().Should().NotBeNull();

        // With nothing to read it, no field for the sun's contact shadows and no global
        // illumination, neither is drawn.
        SetAmbientOcclusion(0);
        Capture(floor, cube);
        renderer.RenderWorld.TryGet<WindowDepth>().Should().BeNull("nothing reads the depth");
        renderer.RenderWorld.TryGet<AmbientOcclusionImage>().Should().BeNull();
        UnloadModel(floor);
        UnloadModel(cube);
    }
}
