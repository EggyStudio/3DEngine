using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// raylib's camera functions, rcamera's, which move and turn a camera by the amounts a program
/// gives and which <see cref="Engine3D.UpdateCamera"/> moves it with from input.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class CameraTests : IDisposable
{
    // Above the ground, looking down at a slant toward -Z.
    private static readonly Camera3D Slanted = new(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, 45);

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [Fact]
    public void A_Camera_Has_Its_Forward_Up_And_Right_And_Its_Two_Matrices()
    {
        var level = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, new Vector3(0, 2, 0), 45);

        GetCameraForward(level).Should().Be(-Vector3.UnitZ);
        GetCameraUp(level).Should().Be(Vector3.UnitY, "of length one, though the camera's is two");
        GetCameraRight(level).Should().Be(Vector3.UnitX);
        GetCameraViewMatrix(level).Should().Be(level.View);
        GetCameraProjectionMatrix(level, 2).Should().Be(level.ProjectionMatrix(2));
    }

    [Fact]
    public void Moving_Forward_And_Right_Keeps_Level_When_Asked_And_Moving_Up_Follows_The_Up()
    {
        var level = Slanted;
        CameraMoveForward(ref level, 1, moveInWorldPlane: true);
        level.Position.Y.Should().BeApproximately(2, 1e-5f, "kept on the ground's plane");
        level.Position.Z.Should().BeApproximately(4, 1e-5f);
        (level.Target - level.Position).Should().Be(Slanted.Target - Slanted.Position, "the target goes with it");

        var slanting = Slanted;
        CameraMoveForward(ref slanting, 1, moveInWorldPlane: false);
        slanting.Position.Y.Should().BeLessThan(2, "along the way it looks, down toward the target");

        var right = Slanted;
        CameraMoveRight(ref right, 2, moveInWorldPlane: true);
        right.Position.Should().Be(new Vector3(2, 2, 5));

        var up = Slanted;
        CameraMoveUp(ref up, 3);
        up.Position.Should().Be(new Vector3(0, 5, 5));
        up.Target.Should().Be(new Vector3(0, 3, 0));
    }

    [Fact]
    public void Moving_To_The_Target_Stops_A_Thousandth_Short_Of_It()
    {
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);

        CameraMoveToTarget(ref camera, -2);
        camera.Position.Should().Be(new Vector3(0, 0, 3));
        CameraMoveToTarget(ref camera, -10);
        camera.Position.Z.Should().BeApproximately(0.001f, 1e-6f);
    }

    [Fact]
    public void Yaw_Turns_Left_About_Itself_Or_Its_Target_And_Roll_Turns_Its_Up()
    {
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);

        var aboutItself = camera;
        CameraYaw(ref aboutItself, MathF.PI / 2, rotateAroundTarget: false);
        aboutItself.Position.Should().Be(camera.Position);
        Vector3.Distance(aboutItself.Target, new Vector3(-5, 0, 5)).Should().BeLessThan(1e-4f, "a quarter turn left looks along -X");

        var aboutTarget = camera;
        CameraYaw(ref aboutTarget, MathF.PI / 2, rotateAroundTarget: true);
        aboutTarget.Target.Should().Be(camera.Target);
        Vector3.Distance(aboutTarget.Position, new Vector3(5, 0, 0)).Should().BeLessThan(1e-4f, "it circles the target");

        var rolled = camera;
        CameraRoll(ref rolled, MathF.PI / 2);
        Vector3.Distance(rolled.Up, Vector3.UnitX).Should().BeLessThan(1e-4f, "rolled a quarter turn about the way it looks");
    }

    [Fact]
    public void Pitch_Locked_Stops_Short_Of_Straight_Up_And_Down_And_Can_Tip_The_Up()
    {
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);

        var locked = camera;
        CameraPitch(ref locked, MathF.PI, lockView: true, rotateAroundTarget: false, rotateUp: false);
        GetCameraForward(locked).Y.Should().BeGreaterThan(0.999f, "it looks all but straight up and turns no further");
        GetCameraForward(locked).Z.Should().BeLessThan(0, "and has not turned over");

        var free = camera;
        CameraPitch(ref free, MathF.PI / 2, lockView: false, rotateAroundTarget: false, rotateUp: true);
        Vector3.Distance(free.Up, Vector3.UnitZ).Should().BeLessThan(1e-4f, "its up tipped with it");
    }

    [Fact]
    public void The_Orbital_Camera_Circles_Its_Target_Half_A_Radian_A_Second()
    {
        UseApp(new App(Config.Default with { Headless = true, FrameSeconds = 0.2 }).AddPlugin(new DefaultPlugins()));
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);

        BeginDrawing();
        UpdateCamera(ref camera, CameraMode.Orbital);
        EndDrawing();

        var turned = MathF.Atan2(camera.Position.X, camera.Position.Z);
        turned.Should().BeApproximately(0.1f, 1e-3f, "a fifth of a second at half a radian a second");
        Vector3.Distance(camera.Position, camera.Target).Should().BeApproximately(5, 1e-4f);

        var custom = camera;
        BeginDrawing();
        UpdateCamera(ref custom, CameraMode.Custom);
        EndDrawing();
        custom.Should().Be(camera, "a custom camera is the program's to move");
    }
}
