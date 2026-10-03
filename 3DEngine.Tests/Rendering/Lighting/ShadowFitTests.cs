using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Rendering.Lighting;

public sealed class ShadowFitTests
{
    private static Matrix4x4 Camera(Vector3 eye, Vector3 target)
    {
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(60), 16f / 9f, 0.1f, 1000f);
        projection.M22 = -projection.M22;
        return Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * projection;
    }

    private static Vector3 Project(Vector3 point, Matrix4x4 viewProjection)
    {
        var clip = Vector4.Transform(new Vector4(point, 1), viewProjection);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

    [Fact]
    public void What_The_Camera_Sees_Within_The_Distance_Lands_Inside_The_Map()
    {
        var camera = Camera(new Vector3(3, 5, 12), Vector3.Zero);
        var sun = Vector3.Normalize(new Vector3(0.3f, -1, -0.5f));
        ShadowFit.TryFit(camera, sun, out var shadow, out var texel).Should().BeTrue();

        // The view's middle and the far corners of the part of it the map covers.
        Matrix4x4.Invert(camera, out var inverse);
        var eye = new Vector3(3, 5, 12);
        foreach (var (x, y) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f), (0f, 0f) })
        {
            var far = Vector4.Transform(new Vector4(x, y, 1, 1), inverse);
            var way = Vector3.Normalize(new Vector3(far.X, far.Y, far.Z) / far.W - eye);
            var point = Project(eye + way * (ShadowFit.Distance * 0.95f), shadow);
            point.X.Should().BeInRange(-1, 1);
            point.Y.Should().BeInRange(-1, 1);
            point.Z.Should().BeInRange(0, 1);
        }
        texel.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_Caster_Above_The_View_Toward_The_Light_Is_Still_Inside_The_Map()
    {
        var camera = Camera(new Vector3(0, 2, 6), Vector3.Zero);
        ShadowFit.TryFit(camera, -Vector3.UnitY, out var shadow, out _).Should().BeTrue();

        Project(new Vector3(0, 30, 0), shadow).Z.Should().BeInRange(0, 1, "a roof 30 units up still shadows the floor");
        Project(new Vector3(0, 30, 0), shadow).Z.Should().BeLessThan(Project(Vector3.Zero, shadow).Z, "what is nearer the light is nearer in the map");
    }

    [Fact]
    public void The_Map_Moves_With_The_Camera_In_Whole_Texels()
    {
        var sun = Vector3.Normalize(new Vector3(0.3f, -1, -0.5f));
        ShadowFit.TryFit(Camera(new Vector3(0, 4, 10), Vector3.Zero), sun, out var before, out _).Should().BeTrue();
        var start = Project(Vector3.Zero, before);

        // Steps of a few hundredths of a unit across the light, which a map following the camera
        // exactly would show as fractions of a texel.
        var across = Vector3.Normalize(Vector3.Cross(sun, Vector3.UnitY)) * 0.037f;
        for (int i = 1; i <= 10; i++)
        {
            var offset = across * i;
            ShadowFit.TryFit(Camera(new Vector3(0, 4, 10) + offset, offset), sun, out var after, out _).Should().BeTrue();
            var moved = (Project(Vector3.Zero, after) - start) * (ShadowFit.MapSize / 2f);
            moved.X.Should().BeApproximately(MathF.Round(moved.X), 0.05f, "the same world point lands on the same place in a texel");
            moved.Y.Should().BeApproximately(MathF.Round(moved.Y), 0.05f, "the same world point lands on the same place in a texel");
        }
    }

    [Fact]
    public void A_Camera_That_Cannot_Be_Inverted_Fits_No_Map()
    {
        ShadowFit.TryFit(default, -Vector3.UnitY, out _, out _).Should().BeFalse();
    }
}
