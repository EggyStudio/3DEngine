using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

// The field's places far from the world's origin worked on the CPU: the point it lies around ahead
// of the eye and the rows a moving mesh is stamped by.
public sealed partial class SceneFieldTests
{
    // The window's camera at a place, looking a little down and aside, its eye noted.
    private static (WindowView View, Camera3D Camera) Window(Vector3 at)
    {
        var camera = new Camera3D(at, at + new Vector3(3, -1, -10), Vector3.UnitY, 70);
        var projection = camera.ProjectionMatrix(16f / 9);
        return (new WindowView(camera.View * projection, camera.Position) { Turned = camera.TurnedView * projection }, camera);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0f, 0f)]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void The_Field_Lies_Ahead_Of_A_Camera_Along_Its_View_However_Far_Out(float x, float z)
    {
        var (view, camera) = Window(new Vector3(x + 0.375f, 2, z - 0.625f));
        // Half the finest cascade's 64 cells of 0.25, 8 units along the way the camera looks, which
        // the difference of its target and place gives exactly, within the float's step at the eye.
        var way = Vector3.Normalize(camera.Target - camera.Position);
        var ahead = SceneFieldRenderer.AheadOf(view, 0.5f, 0.25f);
        (ahead - (camera.Position + way * 8)).Length().Should().BeLessThan(0.25f, "the field lies 8 units ahead of the eye along its view");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0f, 0f)]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void A_Moving_Mesh_Turned_And_Scaled_Is_Stamped_Where_It_Is_Drawn_However_Far_Out(float x, float z)
    {
        var plan = new SceneFieldPlan(1, 0.25f, 1);
        var at = new Vector3(x + 0.375f, 1, z - 0.625f);
        var eye = at + new Vector3(0, 2, 6);
        var turn = Matrix4x4.CreateScale(0.3f) * Matrix4x4.CreateRotationY(0.7f) * Matrix4x4.CreateRotationX(0.2f);
        (SceneFieldPlan.Instance, bool) Drawn(Vector3 place) => (new SceneFieldPlan.Instance(1, Cube, turn * Matrix4x4.CreateTranslation(place), false), false);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames; frame++) plan.Update(eye, [Drawn(at)]);
        var moved = at + new Vector3(1, 0, 0);
        plan.Update(eye, [Drawn(moved)]);
        var shape = plan.Shapes.Should().ContainSingle("a mesh that moved is stamped as its box").Subject;

        // A corner of the mesh's own box taken against the frame's origin, worked in doubles from
        // the matrix's entries, comes back to that corner through the rows the stamp reads.
        var origin = RenderOrigin.Near(eye);
        var corner = new Vector3(1, -1, 1);
        var w = shape.World;
        double Row(float a, float b, float c, float t, double o) => (double)corner.X * a + (double)corner.Y * b + (double)corner.Z * c + t - o;
        var place = new Vector3((float)Row(w.M11, w.M21, w.M31, w.M41, origin.X), (float)Row(w.M12, w.M22, w.M32, w.M42, origin.Y),
            (float)Row(w.M13, w.M23, w.M33, w.M43, origin.Z));
        var back = Vector3.Transform(place, SceneFieldRenderer.StampRows(shape, origin));
        (back - corner).Length().Should().BeLessThan(1e-3f, "the stamp's box lies where the mesh is drawn, six tenths of a unit across");
    }
}
