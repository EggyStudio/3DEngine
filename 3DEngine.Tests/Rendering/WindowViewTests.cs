using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

/// <summary>The window's camera as the effects read it far from the world's origin, worked on the CPU with no device.</summary>
[Trait("Category", "Unit")]
public sealed class WindowViewTests
{
    // The window's camera at a place, looking down at a point before it, with the eye noted or not.
    private static WindowView View(Vector3 at, bool noted)
    {
        var camera = new Camera3D(at + new Vector3(0, 2.5f, 5.5f), at + new Vector3(0, 0.5f, 0), Vector3.UnitY, 60);
        var projection = camera.ProjectionMatrix(16f / 9);
        return new WindowView(camera.View * projection, camera.Position) { Turned = noted ? camera.TurnedView * projection : null };
    }

    private static float Apart(Matrix4x4 a, Matrix4x4 b)
    {
        var most = 0f;
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
                most = MathF.Max(most, MathF.Abs(a[row, column] - b[row, column]));
        return most;
    }

    [Fact]
    public void A_Camera_Moved_A_Unit_Far_Out_Is_Not_The_Same_Camera_Where_No_Eye_Was_Noted()
    {
        var at = new Vector3(1140736, 0, 285696);
        var still = View(at, noted: false);
        still.Same(View(at, noted: false)).Should().BeTrue("a camera standing still is the same camera");
        // The view-projection's translations are of the world's size, so a move of a unit is a
        // millionth of each, inside a tolerance relative to the entry.
        still.Same(View(at + Vector3.UnitZ, noted: false)).Should().BeFalse("the camera moved a unit, which temporal antialiasing reprojects by");
        View(at, noted: true).Same(View(at + Vector3.UnitZ, noted: true)).Should().BeFalse();
    }

    [Theory]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void The_Probes_View_Far_From_The_Worlds_Origin_Is_Drawn_Through_The_Camera_As_At_It(float x, float z)
    {
        var (home, far) = (View(Vector3.Zero, noted: true), View(new Vector3(x, 0, z), noted: true));
        // Taken against the origin from the eye, where the camera's view-projection taken against
        // it holds the eye's place rounded to the world's step.
        Apart(BounceViewRenderer.ProbesThrough(far, RenderOrigin.Near(far.Eye)), BounceViewRenderer.ProbesThrough(home, RenderOrigin.Near(home.Eye)))
            .Should().BeLessThan(1e-5f, $"the probes {x} units out are drawn through the camera as at the world's origin");
    }

    [Fact]
    public void Two_Cameras_Whose_View_Projections_Are_One_Matrix_Keep_Neither_Eye()
    {
        // Two eyes a float's step apart at 1,140,736, an eighth, can give one view-projection, its
        // translations of the world's size rounded alike, and the eye noted last would serve both
        // cameras' draws.
        var one = View(new Vector3(1140736, 0, 285696), noted: true);
        var eye = new ViewEye(one.Eye, one.Turned!.Value);
        var draws = new ModelDrawList();
        draws.NoteEye(one.ViewProjection, eye);
        draws.NoteEye(one.ViewProjection, eye);
        draws.EyeOf(one.ViewProjection).Should().Be(eye, "a camera noted twice keeps its eye");
        draws.NoteEye(one.ViewProjection, eye with { Eye = one.Eye with { X = MathF.BitIncrement(one.Eye.X) } });
        draws.EyeOf(one.ViewProjection).Should().BeNull("two cameras noted with one matrix keep neither eye");
    }
}
