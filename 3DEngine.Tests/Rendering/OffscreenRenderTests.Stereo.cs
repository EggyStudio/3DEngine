using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    // raylib's core_vr_simulator's device, an Oculus Rift CV1.
    private static readonly VrDeviceInfo Rift = new()
    {
        HResolution = 2160, VResolution = 1200, HScreenSize = 0.133793f, VScreenSize = 0.0669f,
        EyeToScreenDistance = 0.041f, LensSeparationDistance = 0.07f, InterpupillaryDistance = 0.07f,
        LensDistortionValues = new Vector4(1.0f, 0.22f, 0.24f, 0.0f), ChromaAbCorrection = new Vector4(0.996f, -0.004f, 1.014f, 0.0f),
    };

    [Fact]
    public void A_Stereo_Config_Has_The_Lens_Parameters_And_Eyes_Raylibs_Has()
    {
        var config = LoadVrStereoConfig(Rift);

        // Worked from raylib's LoadVrStereoConfig for the device, its lens shift -0.0116 and the
        // distortion at the lens's edge 1.3985.
        config.LeftLensCenter.X.Should().BeApproximately(0.2384f, 1e-4f);
        config.RightLensCenter.X.Should().BeApproximately(0.7616f, 1e-4f);
        config.LeftScreenCenter.Should().Be(new Vector2(0.25f, 0.5f));
        config.RightScreenCenter.Should().Be(new Vector2(0.75f, 0.5f));
        config.ScaleIn.X.Should().BeApproximately(4f, 1e-4f);
        config.ScaleIn.Y.Should().BeApproximately(2.2222f, 1e-4f);
        config.Scale.X.Should().BeApproximately(0.1788f, 1e-4f);
        config.Scale.Y.Should().BeApproximately(0.3218f, 1e-4f);
        config.ViewOffset[0].Translation.Should().Be(new Vector3(0.035f, 0.075f, 0.045f), "the left eye's world is moved right, as raylib's is");
        config.ViewOffset[1].Translation.Should().Be(new Vector3(-0.035f, 0.075f, 0.045f));

        // A point straight ahead lands moved by four times the lens shift, one way for each eye.
        static float Across(Matrix4x4 projection) { var clip = Vector4.Transform(new Vector4(0, 0, -1, 1), projection); return clip.X / clip.W; }
        Across(config.Projection[0]).Should().BeApproximately(-0.0464f, 1e-4f);
        Across(config.Projection[1]).Should().BeApproximately(0.0464f, 1e-4f);
    }

    [NeedsVulkanFact]
    public void Stereo_Mode_Draws_The_3D_Once_In_Each_Half_Through_Each_Eye()
    {
        Open(160, 80, samples: 1);
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var config = LoadVrStereoConfig(Rift);
        void Cube() => DrawCube(Vector3.Zero, 1, 1, 1, Color.Red);

        var mono = Capture(() =>
        {
            ClearBackground(Color.White);
            BeginMode3D(camera);
            Cube();
            EndMode3D();
        }, "mono");
        var stereo = Capture(() =>
        {
            ClearBackground(Color.White);
            BeginVrStereoMode(config);
            BeginMode3D(camera);
            Cube();
            EndMode3D();
            EndVrStereoMode();
            // Drawn after, in 2D, over the whole frame as before.
            DrawRectangle(0, 76, 160, 4, Color.Blue);
        }, "stereo");

        GetImageColor(mono, 80, 40).Should().Be(Color.Red, "one camera draws the cube in the middle");
        GetImageColor(mono, 40, 40).Should().Be(Color.White);
        GetImageColor(stereo, 40, 40).Should().Be(Color.Red, "the left eye draws it in the middle of the left half");
        GetImageColor(stereo, 120, 40).Should().Be(Color.Red, "and the right eye in the middle of the right");
        GetImageColor(stereo, 80, 40).Should().Be(Color.White, "neither eye draws past its half");
        GetImageColor(stereo, 10, 78).Should().Be(Color.Blue, "2D after the stereo mode draws over both halves");
        GetImageColor(stereo, 150, 78).Should().Be(Color.Blue);
    }

    [NeedsVulkanFact]
    public void A_Model_In_Stereo_Mode_Is_Drawn_Through_Each_Eye_In_Its_Half()
    {
        Open(160, 80, samples: 1);
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var config = LoadVrStereoConfig(Rift);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0] = new ModelMaterial(Color.Green);

        var stereo = Capture(() =>
        {
            ClearBackground(Color.White);
            BeginVrStereoMode(config);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
            EndVrStereoMode();
        }, "stereo model");

        // Unlit, with no light made, its green as it is.
        GetImageColor(stereo, 40, 40).Should().Be(Color.Green, "the left eye draws it in the middle of the left half");
        GetImageColor(stereo, 120, 40).Should().Be(Color.Green, "and the right eye in the middle of the right");
        GetImageColor(stereo, 80, 40).Should().Be(Color.White, "neither eye draws past its half");
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Model_Drawn_In_Scissor_Mode_Is_Kept_To_Its_Rectangle()
    {
        Open(64, 32, samples: 1);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 90);
        var wall = LoadModelFromMesh(GenMeshCube(20, 20, 0.1f));
        wall.Materials[0] = new ModelMaterial(Color.Green);

        var image = Capture(() =>
        {
            ClearBackground(Color.White);
            BeginScissorMode(0, 0, 32, 32);
            BeginMode3D(camera);
            DrawModel(wall, Vector3.Zero, 1, Color.White);
            EndMode3D();
            EndScissorMode();
        }, "scissored model");

        GetImageColor(image, 16, 16).Should().Be(Color.Green, "the wall covers the frame, and the rectangle keeps it here");
        GetImageColor(image, 48, 16).Should().Be(Color.White, "and out of here, as raylib's scissor keeps all it draws");
        UnloadModel(wall);
    }
}
