using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

/// <summary>
/// The views the flat API draws through with a camera's eye noted, a stereo frame's two eyes and a
/// projection set through rlgl, read back as the immediate pass and the model pass push them
/// against the frame's origin, in an app whose stores queue without a GPU.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class NotedEyeTests : IDisposable
{
    private readonly App _app = new();

    // raylib's core_vr_simulator's device, an Oculus Rift CV1.
    private static readonly VrDeviceInfo Rift = new()
    {
        HResolution = 2160, VResolution = 1200, HScreenSize = 0.133793f, VScreenSize = 0.0669f,
        EyeToScreenDistance = 0.041f, LensSeparationDistance = 0.07f, InterpupillaryDistance = 0.07f,
        LensDistortionValues = new Vector4(1.0f, 0.22f, 0.24f, 0.0f), ChromaAbCorrection = new Vector4(0.996f, -0.004f, 1.014f, 0.0f),
    };

    public NotedEyeTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<DrawList>();
        _app.World.InitResource<ModelDrawList>();
        UseApp(_app);
        ResetRlgl();
    }

    public void Dispose()
    {
        ForgetRlgl();
        UseApp(null);
    }

    // The view of each run of a cube drawn through a camera at a place, as the passes push it
    // against the place it was recorded against from the eye noted with it, and the cube's corners
    // as recorded.
    private (Matrix4x4[] Views, Vector3[] Corners) Drawn(Vector3 at, Action<Camera3D, Action> through)
    {
        var (list, draws) = (_app.World.Resource<DrawList>(), _app.World.Resource<ModelDrawList>());
        list.Clear();
        draws.Clear();
        var camera = new Camera3D(at + new Vector3(0, 2.5f, 5.5f), at + new Vector3(0, 0.5f, 0), Vector3.UnitY, 60);
        through(camera, () => DrawCube(at + new Vector3(1.25f, 0.375f, 0.5f), 0.6f, 0.3f, 0.7f, Color.White));
        return ([.. list.Batches.Where(b => b.DepthTest).Select(b => b.Anchor.Seen(b.Transform, draws.EyeOf(b.Transform)))],
            [.. list.Vertices.ToArray().Select(v => v.Position)]);
    }

    private (Matrix4x4[] Views, Vector3[] Corners) Eyes(Vector3 at) => Drawn(at, (camera, cube) =>
    {
        BeginVrStereoMode(LoadVrStereoConfig(Rift));
        BeginMode3D(camera);
        cube();
        EndMode3D();
        EndVrStereoMode();
    });

    // A projection of its own set through rlgl inside the camera's 3D, as a program narrows the view.
    private (Matrix4x4[] Views, Vector3[] Corners) Projected(Vector3 at) => Drawn(at, (camera, cube) =>
    {
        BeginMode3D(camera);
        var narrow = Matrix4x4.CreatePerspectiveFieldOfView(0.5f, 16f / 9, 0.1f, 1000);
        rlSetMatrixProjection(narrow);
        cube();
        EndMode3D();
    });

    private static float[] Entries(Matrix4x4 m) =>
        [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];

    [Theory]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void Each_Eye_Far_From_The_Worlds_Origin_Is_Pushed_As_At_It(float x, float z)
    {
        var home = Eyes(Vector3.Zero).Views;
        var far = Eyes(new Vector3(x, 0, z)).Views;
        home.Should().HaveCount(2, "the cube is drawn once through each eye");
        Entries(home[0]).Should().NotEqual(Entries(home[1]), "the two eyes stand apart");
        // Each eye's offset of 0.035 is taken with the view's turn, where added to a translation
        // of the world's size it rounds to the step there, 0.125 at 1,140,736, and each eye's
        // picture moves by it.
        far.Should().HaveCount(2);
        for (int eye = 0; eye < 2; eye++)
            Entries(far[eye]).Zip(Entries(home[eye]), (f, h) => MathF.Abs(f - h)).Max()
                .Should().BeLessThan(1e-5f, $"eye {eye} {x} units out is pushed as the same eye at the world's origin");
    }

    [Theory]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void Shapes_Through_A_Projection_Set_Through_Rlgl_Far_From_The_Worlds_Origin_Are_Drawn_As_At_It(float x, float z)
    {
        var (homeViews, homeCorners) = Projected(Vector3.Zero);
        var (farViews, farCorners) = Projected(new Vector3(x, 0, z));
        homeViews.Should().ContainSingle();
        // A projection set through rlgl changes the transform the shapes are recorded with, which
        // left the camera's eye and its place behind, the cube recorded in world floats and pushed
        // through the world's view-projection.
        farCorners.Should().Equal(homeCorners, $"the cube {x} units out is recorded against the camera's eye as at the world's origin");
        farViews.Should().ContainSingle();
        Entries(farViews[0]).Zip(Entries(homeViews[0]), (f, h) => MathF.Abs(f - h)).Max()
            .Should().BeLessThan(1e-5f, "the narrowed view is pushed from the camera's eye");
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void The_Window_View_Carries_The_Far_Plane_Its_Camera_Clips_At_However_Far_Out(float x, float z)
    {
        var at = new Vector3(x + 0.375f, 2, z - 0.625f);
        BeginMode3D(new Camera3D(at, at + new Vector3(3, -1, -10), Vector3.UnitY, 70));
        EndMode3D();
        var renderWorld = new RenderWorld();
        new CameraExtract().Run(_app.World, renderWorld);
        // A far plane read back from the float projection holds within a hundredth at best, which
        // turns with the camera across the far cascade's sizes and makes the field again.
        var view = renderWorld.TryGet<WindowView>();
        view.Should().NotBeNull("BeginMode3D drew into the window");
        SceneFieldRenderer.FarPlane(view!).Should().Be(4000, "the camera clips at 4000 units, which the far cascade's cell follows");
    }

    [Fact]
    public void An_Eye_Noted_With_And_Without_Its_Far_Plane_Keeps_The_Eye_And_The_Far_Plane()
    {
        // BeginMode3D notes the window's eye with the far plane it clips at, and the modeling
        // window notes the same eye through the same view-projection without it.
        var at = new Vector3(1140736.375f, 2, 285695.375f);
        var camera = new Camera3D(at, at + new Vector3(3, -1, -10), Vector3.UnitY, 70);
        var projection = camera.ProjectionMatrix(16f / 9);
        var (viewProjection, turned) = (camera.View * projection, camera.TurnedView * projection);
        var draws = new ModelDrawList();
        draws.NoteEye(viewProjection, new ViewEye(at, turned, Camera3D.FarDistance));
        draws.NoteEye(viewProjection, new ViewEye(at, turned));
        draws.EyeOf(viewProjection).Should().Be(new ViewEye(at, turned, Camera3D.FarDistance), "both noted the one eye, one with its far plane");
        draws.NoteEye(viewProjection, new ViewEye(at + Vector3.UnitX, turned));
        draws.EyeOf(viewProjection).Should().BeNull("a second camera through the same matrix leaves neither eye");
    }
}
