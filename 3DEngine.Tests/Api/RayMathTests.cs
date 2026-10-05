using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// raymath's functions that C# has no counterpart for, held to raymath's results, and those the
/// comparison with raylib calls C#'s own held to raymath's arithmetic, written here from its source.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RayMathTests
{
    private const float Tolerance = 1e-5f;

    // A raymath Matrix by its fields m0 to m15, which are a Matrix4x4's M11 to M44 in order.
    private static Matrix4x4 FromRaylib(float[] m) =>
        new(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);

    private static void Near(Matrix4x4 actual, Matrix4x4 expected, string because)
    {
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                actual[r, c].Should().BeApproximately(expected[r, c], Tolerance, because);
    }

    private static void Near(Vector3 actual, Vector3 expected, string because = "") =>
        Vector3.Distance(actual, expected).Should().BeLessThan(Tolerance, because);

    [Fact]
    public void The_Scalars_Normalize_Remap_And_Wrap_And_Compare_Within_A_Millionth()
    {
        Normalize(5, 0, 10).Should().Be(0.5f);
        Remap(5, 0, 10, 100, 200).Should().Be(150);
        Wrap(370, 0, 360).Should().BeApproximately(10, Tolerance);
        Wrap(-10, 0, 360).Should().BeApproximately(350, Tolerance);
        FloatEquals(1, 1.0000005f).Should().BeTrue();
        FloatEquals(1, 1.00001f).Should().BeFalse();
    }

    [Fact]
    public void Vector2s_Turn_Clockwise_On_The_Screen_Move_Clamp_And_Refract()
    {
        Vector2CrossProduct(Vector2.UnitX, Vector2.UnitY).Should().Be(1);
        Vector2Angle(Vector2.UnitX, Vector2.UnitY).Should().BeApproximately(MathF.PI/2, Tolerance, "down the screen is clockwise from right");
        Vector2LineAngle(Vector2.Zero, Vector2.UnitY).Should().BeApproximately(-MathF.PI/2, Tolerance);
        Vector2.Distance(Vector2Rotate(Vector2.UnitX, MathF.PI/2), Vector2.UnitY).Should().BeLessThan(Tolerance);
        Vector2MoveTowards(Vector2.Zero, new Vector2(10, 0), 3).Should().Be(new Vector2(3, 0));
        Vector2MoveTowards(Vector2.Zero, new Vector2(10, 0), 20).Should().Be(new Vector2(10, 0), "onto the target where it is nearer");
        Vector2.Distance(Vector2ClampValue(new Vector2(3, 4), 1, 2), new Vector2(1.2f, 1.6f)).Should().BeLessThan(Tolerance);
        Vector2Equals(new Vector2(1, 2), new Vector2(1.0000005f, 2)).Should().BeTrue();
        Vector2Refract(new Vector2(0, -1), Vector2.UnitY, 1).Should().Be(new Vector2(0, -1), "straight on at a ratio of one");
        Vector2Refract(new Vector2(0.8f, -0.6f), Vector2.UnitY, 2).Should().Be(Vector2.Zero, "it reflects whole past the critical angle");
    }

    [Fact]
    public void Vector3s_Project_Reject_Rotate_And_Find_Barycentric_Coordinates()
    {
        var perpendicular = Vector3Perpendicular(new Vector3(0, 0, 1));
        Vector3.Dot(perpendicular, new Vector3(0, 0, 1)).Should().Be(0);
        perpendicular.Length().Should().BeApproximately(1, Tolerance);
        Vector3Angle(Vector3.UnitX, Vector3.UnitY).Should().BeApproximately(MathF.PI/2, Tolerance);
        Near(Vector3Project(new Vector3(1, 1, 0), Vector3.UnitX), Vector3.UnitX);
        Near(Vector3Reject(new Vector3(1, 1, 0), Vector3.UnitX), Vector3.UnitY);

        var (v1, v2) = (new Vector3(2, 0, 0), new Vector3(1, 1, 0));
        Vector3OrthoNormalize(ref v1, ref v2);
        Near(v1, Vector3.UnitX);
        Near(v2, Vector3.UnitY);

        Near(Vector3RotateByAxisAngle(Vector3.UnitX, Vector3.UnitZ, MathF.PI/2), Vector3.UnitY, "counterclockwise looking down Z");
        Near(Vector3RotateByAxisAngle(new Vector3(1, 2, 3), new Vector3(0, 2, 0), 0.7f),
            Vector3.Transform(new Vector3(1, 2, 3), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f)), "the axis is made of length one");
        Vector3MoveTowards(Vector3.Zero, new Vector3(0, 0, 10), 4).Should().Be(new Vector3(0, 0, 4));
        Near(Vector3CubicHermite(Vector3.Zero, Vector3.One, Vector3.UnitX, Vector3.One, 0), Vector3.Zero);
        Near(Vector3CubicHermite(Vector3.Zero, Vector3.One, Vector3.UnitX, Vector3.One, 1), Vector3.UnitX);
        Near(Vector3Barycenter(new Vector3(0.25f, 0.25f, 0), Vector3.Zero, Vector3.UnitX, Vector3.UnitY), new Vector3(0.5f, 0.25f, 0.25f));
        Near(Vector3ClampValue(new Vector3(0, 0, 10), 1, 2), new Vector3(0, 0, 2));
        Vector3Equals(Vector3.One, new Vector3(1, 1, 1.0000005f)).Should().BeTrue();
        Near(Vector3Refract(new Vector3(0, -1, 0), Vector3.UnitY, 1), new Vector3(0, -1, 0));

        // A point carried into clip space and back.
        var view = Matrix4x4.CreateLookAt(new Vector3(3, 2, 5), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(1, 1.5f, 0.1f, 100);
        var world = new Vector3(0.5f, -0.25f, 1);
        var clip = Vector4.Transform(new Vector4(world, 1), view*projection);
        // A perspective's inverse magnifies the rounding of its floats.
        Vector3.Distance(Vector3Unproject(new Vector3(clip.X, clip.Y, clip.Z)/clip.W, projection, view), world)
            .Should().BeLessThan(1e-4f, "unprojected back to where it was");
    }

    [Fact]
    public void Vector4s_Move_And_Compare()
    {
        Vector4MoveTowards(Vector4.Zero, new Vector4(0, 0, 0, 10), 4).Should().Be(new Vector4(0, 0, 0, 4));
        Vector4Equals(Vector4.One, new Vector4(1, 1, 1, 1.0000005f)).Should().BeTrue();
    }

    [Fact]
    public void Matrices_Rotate_By_Three_Angles_And_Compose_As_System_Numerics_Products()
    {
        MatrixTrace(Matrix4x4.Identity).Should().Be(4);
        var (x, y, z) = (0.3f, 0.5f, 0.7f);
        Near(MatrixRotateXYZ(new Vector3(x, y, z)), Matrix4x4.CreateRotationZ(z)*Matrix4x4.CreateRotationY(y)*Matrix4x4.CreateRotationX(x),
            "it turns a point about Z, then Y, then X");
        Near(MatrixRotateZYX(new Vector3(x, y, z)), Matrix4x4.CreateRotationX(x)*Matrix4x4.CreateRotationY(y)*Matrix4x4.CreateRotationZ(z),
            "it turns a point about X, then Y, then Z");

        var rotation = Quaternion.Normalize(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f));
        Near(MatrixCompose(new Vector3(1, 2, 3), rotation, new Vector3(2, 3, 4)),
            Matrix4x4.CreateScale(2, 3, 4)*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(1, 2, 3),
            "scaled, turned and then moved");
    }

    [Fact]
    public void Quaternions_Interpolate_Turn_Between_Directions_And_Convert_As_Raymath_Does()
    {
        var a = new Quaternion(0, 0, 0, 1);
        var b = new Quaternion(0, 1, 0, 0);
        QuaternionLerp(a, b, 0.5f).Should().Be(new Quaternion(0, 0.5f, 0, 0.5f), "straight, not made of length one");
        QuaternionCubicHermiteSpline(a, default, b, default, 0).Should().Be(a);

        var turn = QuaternionFromVector3ToVector3(Vector3.UnitX, Vector3.UnitY);
        Near(Vector3.Transform(Vector3.UnitX, turn), Vector3.UnitY, "it turns the one direction onto the other");

        QuaternionToAxisAngle(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1), out var axis, out var angle);
        Near(axis, Vector3.UnitZ);
        angle.Should().BeApproximately(1, Tolerance);

        var (pitch, yaw, roll) = (0.3f, 0.5f, 0.7f);
        var euler = QuaternionFromEuler(pitch, yaw, roll);
        Near(QuaternionToEuler(euler), new Vector3(pitch, yaw, roll), "the angles come back");
        QuaternionEquals(euler, Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll)).Should().BeFalse(
            "System.Numerics composes the three in another order, which is why raymath's is carried");

        QuaternionTransform(a, Matrix4x4.CreateTranslation(1, 2, 3)).Should().Be(new Quaternion(1, 2, 3, 1));
        QuaternionEquals(euler, -euler).Should().BeTrue("a quaternion and its negative turn alike");
    }

    // -- What the comparison calls C#'s own, against raymath's arithmetic

    [Fact]
    public void Raymaths_Rotations_About_An_Axis_Are_System_Numerics_Own()
    {
        foreach (var t in new[] { 0.4f, -1.3f, 2.9f })
        {
            float c = MathF.Cos(t), s = MathF.Sin(t);
            // MatrixRotateX, Y and Z, each the identity with four fields set.
            Near(Matrix4x4.CreateRotationX(t), FromRaylib([1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1]), "MatrixRotateX");
            Near(Matrix4x4.CreateRotationY(t), FromRaylib([c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1]), "MatrixRotateY");
            Near(Matrix4x4.CreateRotationZ(t), FromRaylib([c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]), "MatrixRotateZ");

            // MatrixRotate about an axis of length one, which raymath makes it and System.Numerics expects.
            var axis = Vector3.Normalize(new Vector3(1, 2, 3));
            var (x, y, z, u) = (axis.X, axis.Y, axis.Z, 1 - c);
            Near(Matrix4x4.CreateFromAxisAngle(axis, t), FromRaylib([
                x*x*u + c, y*x*u + z*s, z*x*u - y*s, 0,
                x*y*u - z*s, y*y*u + c, z*y*u + x*s, 0,
                x*z*u + y*s, y*z*u - x*s, z*z*u + c, 0,
                0, 0, 0, 1]), "MatrixRotate");

            // QuaternionFromAxisAngle, of the same axis.
            var q = Quaternion.CreateFromAxisAngle(axis, t);
            var half = t*0.5f;
            (q.X, q.Y, q.Z, q.W).Should().Be((axis.X*MathF.Sin(half), axis.Y*MathF.Sin(half), axis.Z*MathF.Sin(half), MathF.Cos(half)));
        }
    }

    [Fact]
    public void Raymaths_Look_At_Is_System_Numerics_Own()
    {
        var (eye, target, up) = (new Vector3(3, 2, 5), new Vector3(0, 1, 0), Vector3.UnitY);
        // MatrixLookAt as raymath writes it.
        var vz = Vector3.Normalize(eye - target);
        var vx = Vector3.Normalize(Vector3.Cross(up, vz));
        var vy = Vector3.Cross(vz, vx);
        var raylib = FromRaylib([
            vx.X, vy.X, vz.X, 0,
            vx.Y, vy.Y, vz.Y, 0,
            vx.Z, vy.Z, vz.Z, 0,
            -Vector3.Dot(vx, eye), -Vector3.Dot(vy, eye), -Vector3.Dot(vz, eye), 1]);

        Near(Matrix4x4.CreateLookAt(eye, target, up), raylib, "MatrixLookAt");
    }
}
