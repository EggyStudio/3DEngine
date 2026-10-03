using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Renderer;

[Trait("Category", "Unit")]
public class ExtractedViewTests
{
    [Fact]
    public void Stores_View_And_Projection_Matrices()
    {
        var view = Matrix4x4.CreateLookAt(Vector3.UnitZ, Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(1.0f, 16f / 9f, 0.1f, 1000f);

        var ev = new ExtractedView
        {
            View = view,
            Projection = proj,
            Width = 1920,
            Height = 1080
        };

        ev.View.Should().Be(view);
        ev.Projection.Should().Be(proj);
        ev.Width.Should().Be(1920);
        ev.Height.Should().Be(1080);
    }

    [Fact]
    public void Default_Has_Zero_Fields()
    {
        var ev = default(ExtractedView);

        ev.View.Should().Be(default(Matrix4x4));
        ev.Projection.Should().Be(default(Matrix4x4));
        ev.Width.Should().Be(0);
        ev.Height.Should().Be(0);
    }
}

// -- CameraUniform struct --

[Trait("Category", "Unit")]
public class CameraUniformTests
{
    [Fact]
    public void CameraUniform_Stores_Matrices()
    {
        var view = Matrix4x4.CreateLookAt(Vector3.UnitZ, Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(1.0f, 1.0f, 0.1f, 100f);

        var uniform = new CameraUniform { View = view, Projection = proj };

        uniform.View.Should().Be(view);
        uniform.Projection.Should().Be(proj);
    }
}

[Trait("Category", "Unit")]
public class TransformTests
{
    [Fact]
    public void Constructor_Sets_Position_With_Identity_Rotation_And_Unit_Scale()
    {
        var pos = new Vector3(1, 2, 3);

        var t = new Transform(pos);

        t.Position.Should().Be(pos);
        t.Rotation.Should().Be(Quaternion.Identity);
        t.Scale.Should().Be(Vector3.One);
    }

    [Fact]
    public void Default_Has_Zero_Position_And_Default_Quaternion()
    {
        var t = default(Transform);

        t.Position.Should().Be(Vector3.Zero);
        t.Scale.Should().Be(Vector3.Zero); // default struct, not constructor
    }
}

[Trait("Category", "Unit")]
public class CameraTests
{
    [Fact]
    public void Parameterized_Constructor_Has_Sensible_Defaults()
    {
        // Must call the explicit constructor to get default parameter values
        var cam = new Camera(fovY: 60f);

        cam.FovY.Should().BeApproximately(60f * (float)(Math.PI / 180.0), 0.001f);
        cam.Near.Should().BeApproximately(0.1f, 0.001f);
        cam.Far.Should().BeApproximately(1000f, 0.1f);
        cam.TargetName.Should().BeNull();
    }

    [Fact]
    public void Default_Struct_Has_Zero_Fields()
    {
        var cam = default(Camera);

        cam.FovY.Should().Be(0f);
        cam.Near.Should().Be(0f);
        cam.Far.Should().Be(0f);
        cam.TargetName.Should().BeNull();
    }

    [Fact]
    public void Constructor_With_Custom_Values()
    {
        var cam = new Camera(fovY: 85f, near: 0.5f, far: 500f, targetName: "offscreen");

        cam.FovY.Should().BeApproximately(85f * (float)(Math.PI / 180.0), 0.001f);
        cam.Near.Should().BeApproximately(0.5f, 0.001f);
        cam.Far.Should().BeApproximately(500f, 0.1f);
        cam.TargetName.Should().Be("offscreen");
    }
}
