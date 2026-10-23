using System.Globalization;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The modeling window's picking, a ray tested against an edit mesh's faces one by one, through a
/// camera onto a known face, and the window taking clicks and a drag in headless frames, the pointer
/// moved by the console's synthetic input as <c>./e3d</c> moves it.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class ModelingWindowTests : IDisposable
{
    private const float Close = 1e-4f;
    private static readonly Camera3D Camera = new(new Vector3(1, 4, 8), new Vector3(1, 1, 1), Vector3.UnitY, 45);
    private readonly EditMesh _box;

    public ModelingWindowTests()
    {
        // Wide enough that the window, at the right edge, leaves the box in the middle to the pointer.
        var config = Config.Default.WithWindow("Modeling", 1280, 720) with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 };
        var app = new App(config).AddPlugin(new DefaultPlugins());

        // The console's input lets go of a button a frame on, as the command line's server steps it
        // in a program that serves, which a test's app does not.
        app.AddSystem(Stage.First, new SystemDescriptor(world =>
        {
            if (world.TryGetResource<SyntheticInput>(out var synthetic)) synthetic.Update(world.Resource<Input>(), world.Resource<Time>().FrameCount);
        }, "ModelingWindowTests.SyntheticInput").MainThreadOnly());
        UseApp(app);
        _box = LoadEditMesh();
        EditMeshBox(_box, new Vector3(2));
    }

    public void Dispose()
    {
        UnloadEditMesh(_box);
        CloseWindow();
        UseApp(null);
    }

    private int Facing(Vector3 way) =>
        Enumerable.Range(0, GetEditMeshInfo(_box).FaceCount).Single(face => GetEditMeshFaceNormal(_box, face) == way);

    private static void Run(string line)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp())) ConsoleCommands.Run(line);
    }

    // The frames the window is drawn in, and how many of them it said the mesh changed in.
    private int Frames(int count)
    {
        var changed = 0;
        for (int i = 0; i < count; i++)
        {
            BeginDrawing();
            BeginMode3D(Camera);
            EndMode3D();
            if (DrawModelingWindow(_box)) changed++;
            EndDrawing();
        }
        return changed;
    }

    private string LastLine => GetEditMeshRecipe(_box).TrimEnd('\n').Split('\n')[^1];

    [Fact]
    public void A_Ray_Meets_The_Face_It_Points_At_Where_The_Model_Is_Drawn_And_None_Past_It()
    {
        var at = new Vector3(10, 0, 0);
        var hit = GetRayCollisionEditMesh(new Ray(new Vector3(11, 5, 1), -Vector3.UnitY), _box, at, out var face);

        face.Should().Be(Facing(Vector3.UnitY));
        (hit.Hit, hit.Normal).Should().Be((true, Vector3.UnitY));
        hit.Distance.Should().BeApproximately(3, Close);
        Vector3.Distance(hit.Point, new Vector3(11, 2, 1)).Should().BeLessThan(Close);

        GetRayCollisionEditMesh(new Ray(new Vector3(1, 5, 1), -Vector3.UnitY), _box, at, out face).Hit.Should().BeFalse("the model is drawn ten meters along X");
        face.Should().Be(-1);
    }

    [Fact]
    public void The_Pointers_Ray_Through_A_Camera_Meets_The_Face_Under_It()
    {
        var front = new Camera3D(new Vector3(1, 1, 8), new Vector3(1, 1, 1), Vector3.UnitY, 45);
        var ray = GetScreenToWorldRayEx(new Vector2(400, 225), front, 800, 450);

        GetRayCollisionEditMesh(ray, _box, Vector3.Zero, out var face).Hit.Should().BeTrue();
        face.Should().Be(Facing(Vector3.UnitZ), "the middle of the view looks at the front face's middle");
    }

    [Fact]
    public void A_Drag_Counts_Whole_Steps_Of_The_Grid_Along_The_Normal_To_The_Point_Nearest_The_Pointer()
    {
        // A ray across the normal's line 1.4 meters above the face, with steps of half a meter.
        ModelingSteps(new Vector3(0, 2, 0), Vector3.UnitY, new Ray(new Vector3(5, 3.4f, 0), -Vector3.UnitX), 0.5f).Should().Be(3);
        ModelingSteps(new Vector3(0, 2, 0), Vector3.UnitY, new Ray(new Vector3(5, 1.4f, -3), new Vector3(-1, 0, 1)), 0.5f).Should().Be(-1);
        ModelingSteps(new Vector3(0, 2, 0), Vector3.UnitY, new Ray(new Vector3(0, 9, 0), -Vector3.UnitY), 0.5f).Should().Be(0, "a ray down the normal gives no point along it");
    }

    [Fact]
    public void A_Click_Selects_The_Face_Under_The_Pointer_And_Shift_Adds_One()
    {
        Frames(1);
        var top = GetWorldToScreen(new Vector3(1, 2, 1), Camera);
        var front = GetWorldToScreen(new Vector3(1, 1, 2), Camera);

        // The box is selected whole as it is made, so a click on its top selects the top alone
        // once the button is let go.
        Run($"input.click {(int)top.X} {(int)top.Y}");
        Frames(5).Should().Be(0, "a selection leaves the shape as it was, so the model is not built again");
        LastLine.Should().Be($"select face {Facing(Vector3.UnitY)}");
        GetEditMeshInfo(_box).SelectedFaceCount.Should().Be(1);

        Run("input.key LeftShift 6");
        Run($"input.click {(int)front.X} {(int)front.Y}");
        Frames(6);
        LastLine.Should().Be($"select add face {Facing(Vector3.UnitZ)}");
        GetEditMeshInfo(_box).SelectedFaceCount.Should().Be(2);
    }

    [Fact]
    public void A_Drag_Up_From_The_Top_Extrudes_It_By_Whole_Steps_Of_The_Grid()
    {
        EditMeshGrid(_box, 0.25f);
        EditMeshSelectFacing(_box, Vector3.UnitY);
        Frames(1);
        var top = GetWorldToScreen(new Vector3(1, 2, 1), Camera);

        Run($"input.move {(int)top.X} {(int)top.Y}");
        Frames(1);
        Run("input.drag Left 0 -60 6 1");
        Frames(10).Should().Be(1, "the frame the button was let go in extruded the top");

        // The step the pointer ended at, along the top's normal, nearest its ray.
        var ray = GetScreenToWorldRay(new Vector2((int)top.X, (int)top.Y - 60), Camera);
        var steps = ModelingSteps(new Vector3(1, 2, 1), Vector3.UnitY, ray, 0.25f);
        steps.Should().BePositive();
        LastLine.Should().Be($"extrude {(steps * 0.25f).ToString(CultureInfo.InvariantCulture)}");
        GetEditMeshInfo(_box).Bounds.Max.Y.Should().BeApproximately(2 + steps * 0.25f, Close);
    }
}
