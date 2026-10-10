using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering.Models;

/// <summary>
/// The test the model and shadow passes leave a block of instances out by, the four side planes
/// of a view, which keeps what a view shows and leaves out what lies wholly to a side of it, and
/// for a light's pass its near and far planes, past which the GPU clips what is drawn anyway.
/// </summary>
[Trait("Category", "Unit")]
public class ViewCullingTests
{
    private static readonly Vector3 Half = new(0.5f);

    // A camera at the origin looking down -Z, as Camera3D builds its view and projection.
    private static readonly Matrix4x4 Camera =
        Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY) *
        Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(60), 16 / 9f, 0.1f, 100);

    private static bool Seen(Matrix4x4 view, Vector3 at) => ModelRenderer.Seen(view, at - Half, at + Half);

    [Fact]
    public void A_Camera_Sees_A_Box_In_Front_And_Leaves_Out_One_Behind_Or_Wholly_Aside()
    {
        Seen(Camera, new Vector3(0, 0, -10)).Should().BeTrue();
        Seen(Camera, new Vector3(0, 0, 10)).Should().BeFalse("it is behind the camera");
        Seen(Camera, new Vector3(100, 0, -10)).Should().BeFalse("it is far to the right of the view");
        Seen(Camera, new Vector3(0, -100, -10)).Should().BeFalse("it is far below the view");
    }

    [Fact]
    public void A_Box_The_Edge_Of_A_View_Crosses_Is_Seen()
    {
        // At 10 units the view's right edge is about 10 * tan(30) * 16 / 9, a little over 10.2.
        ModelRenderer.Seen(Camera, new Vector3(9, -1, -11), new Vector3(12, 1, -9)).Should().BeTrue();
    }

    [Fact]
    public void A_Box_Past_The_Far_Plane_Is_Still_Drawn_Since_Only_The_Sides_Cull()
    {
        Seen(Camera, new Vector3(0, 0, -500)).Should().BeTrue("near and far are left to the depth test");
    }

    [Fact]
    public void A_Shadow_Box_Keeps_What_Lies_Along_The_Light_And_Leaves_Out_What_Lies_Beside_It()
    {
        // A sun straight down over a box 20 units across, as a cascade is fitted.
        var light = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitY, -Vector3.UnitZ) *
                    Matrix4x4.CreateOrthographicOffCenter(-10, 10, -10, 10, -50, 50);

        Seen(light, new Vector3(0, 0, 0)).Should().BeTrue();
        Seen(light, new Vector3(3, 400, -3)).Should().BeTrue("a caster high above, toward the light, still throws its shadow in");
        Seen(light, new Vector3(30, 0, 0)).Should().BeFalse("it is beside the box");
    }

    [Fact]
    public void A_Lights_Pass_Leaves_Out_A_Box_Past_Its_Far_Plane_Or_Before_Its_Near_One_And_Keeps_One_Across_Either()
    {
        // A sun straight down whose box reaches from 50 units above the origin to 50 below it.
        var light = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitY, -Vector3.UnitZ) *
                    Matrix4x4.CreateOrthographicOffCenter(-10, 10, -10, 10, -50, 50);
        bool ByLight(Vector3 at) => ModelRenderer.Seen(light, at - Half, at + Half, light: true);

        ByLight(new Vector3(0, 0, 0)).Should().BeTrue();
        ByLight(new Vector3(0, 49.8f, 0)).Should().BeTrue("the near plane crosses it");
        ByLight(new Vector3(0, -49.8f, 0)).Should().BeTrue("the far plane crosses it");
        ByLight(new Vector3(0, -60, 0)).Should().BeFalse("it lies past the far plane, where its shadow could fall on nothing the box holds");
        ByLight(new Vector3(0, 60, 0)).Should().BeFalse("it lies before the near plane, which the GPU clips it by");
        Seen(light, new Vector3(0, -60, 0)).Should().BeTrue("a camera's pass leaves near and far to the depth test");
    }
}
