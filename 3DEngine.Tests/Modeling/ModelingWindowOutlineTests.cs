using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The modeling window's outlines read from a frame drawn on the device, the selected face's in
/// orange and the face under the pointer's in white, over a box built from its edit mesh, the
/// pointer moved by the console's synthetic input as <c>./e3d</c> moves it.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ModelingWindowOutlineTests : IDisposable
{
    private static readonly Camera3D Camera = new(new Vector3(1, 4, 8), new Vector3(1, 1, 1), Vector3.UnitY, 45);
    private static readonly Color Selected = new(255, 160, 40), Hovered = new(255, 255, 255);
    private readonly TestFolder _folder = new("engine-modeling-outline-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        // Wide enough that the window, at the right edge, leaves the box in the middle to the pointer.
        var config = Config.Default.WithWindow("modeling outline test", 1280, 720) with { Headless = true, Offscreen = true, Samples = 1 };
        var app = new App(config).AddPlugin(new DefaultPlugins());
        app.AddSystem(Stage.First, new SystemDescriptor(world =>
        {
            if (world.TryGetResource<SyntheticInput>(out var synthetic)) synthetic.Update(world.Resource<Input>(), world.Resource<Time>().FrameCount);
        }, "ModelingWindowOutlineTests.SyntheticInput").MainThreadOnly());
        UseApp(app);
    }

    private static void Run(string line)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp())) ConsoleCommands.Run(line);
    }

    // A few frames of the box and the window, the last captured.
    private Image Frame(Model model, EditMesh box)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 14 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(Camera);
            DrawModel(model, Vector3.Zero, 1, Color.DarkGray);
            EndMode3D();
            DrawModelingWindow(box);
            if (frame == 4) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    // Whether a pixel within two of where a point of the world is drawn is near the color, the
    // outline being a line a pixel wide.
    private static bool Outlined(Image image, Vector3 at, Color color)
    {
        var pixel = GetWorldToScreen(at, Camera);
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
            {
                var seen = GetImageColor(image, (int)pixel.X + x, (int)pixel.Y + y);
                if (Math.Abs(seen.R - color.R) < 30 && Math.Abs(seen.G - color.G) < 30 && Math.Abs(seen.B - color.B) < 30) return true;
            }
        return false;
    }

    [NeedsVulkanFact]
    public void The_Selected_Face_Is_Outlined_In_Orange_And_The_Face_Under_The_Pointer_In_White()
    {
        Open();
        var box = LoadEditMesh();
        EditMeshBox(box, new Vector3(2));
        var model = LoadModelFromEditMesh(box);
        // The top's back edge and the front's bottom edge, which no other outline shares.
        var topBack = new Vector3(1, 2, 0);
        var frontBottom = new Vector3(1, 0, 2);

        // Nothing selected and the pointer off the box, so nothing is outlined.
        EditMeshSelectNone(box);
        Run("input.move 20 700");
        var bare = Frame(model, box);
        Outlined(bare, topBack, Selected).Should().BeFalse("no face is selected");
        Outlined(bare, frontBottom, Hovered).Should().BeFalse("the pointer is off the box");

        EditMeshSelectFacing(box, Vector3.UnitY);
        var front = GetWorldToScreen(new Vector3(1, 1, 2), Camera);
        Run($"input.move {(int)front.X} {(int)front.Y}");
        var outlined = Frame(model, box);
        Outlined(outlined, topBack, Selected).Should().BeTrue("the top is selected, and its outline is drawn over the model");
        Outlined(outlined, frontBottom, Hovered).Should().BeTrue("the pointer rests on the front");
        Outlined(outlined, topBack, Hovered).Should().BeFalse("the top is not under the pointer");
        UnloadModel(model);
        UnloadEditMesh(box);
    }
}
