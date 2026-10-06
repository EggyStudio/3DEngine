using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Cube_Texture_Is_Sampled_By_Direction_And_A_Cube_Never_Set_Reads_Black()
    {
        Open(96, 48);
        // A cross of faces four texels wide, +X red and +Y blue, the rest gray.
        var cross = GenImageColor(16, 12, Color.Gray);
        ImageDrawRectangle(ref cross, 8, 4, 4, 4, new Color(255, 0, 0));
        ImageDrawRectangle(ref cross, 4, 0, 4, 4, new Color(0, 0, 255));
        var cubemap = LoadTextureCubemap(cross, CubemapLayout.AutoDetect);

        const string source = """
            import modelpass;

            uniform SamplerCube sky;
            uniform float4 look;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return float4(sky.Sample(look.xyz).rgb, 1.0);
            }
            """;
        var set = LoadShaderFromMemory(source, "sky.slang");
        var unset = LoadShaderFromMemory(source, "sky.slang");
        SetShaderValueTexture(set, GetShaderLocation(set, "sky"), cubemap);
        var look = GetShaderLocation(set, "look");

        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var image = Capture(() =>
        {
            ClearBackground(new Color(30, 30, 30));
            BeginMode3D(camera);
            cube.Materials[0].Shader = set;
            SetShaderValue(set, look, new Vector4(1, 0, 0, 0));
            DrawModel(cube, new Vector3(-1.2f, 0, 0), 1, Color.White);
            SetShaderValue(set, look, new Vector4(0, 1, 0, 0));
            DrawModel(cube, new Vector3(1.2f, 0, 0), 1, Color.White);
            cube.Materials[0].Shader = unset;
            SetShaderValue(unset, GetShaderLocation(unset, "look"), new Vector4(1, 0, 0, 0));
            DrawModel(cube, new Vector3(0, 0, 0.5f), 0.4f, Color.White);
            EndMode3D();
        });

        GetImageColor(image, 24, 24).Should().Be(new Color(255, 0, 0), "looking along +X reads the face the cross has right of the middle");
        GetImageColor(image, 72, 24).Should().Be(new Color(0, 0, 255), "and along +Y the one at its top");
        GetImageColor(image, 48, 24).Should().Be(new Color(0, 0, 0, 255), "a shader whose cube was never set reads black, as an unset cube reads in OpenGL");
        UnloadModel(cube);
        UnloadShader(set);
        UnloadShader(unset);
        UnloadTexture(cubemap);
    }

    [NeedsVulkanFact]
    public void A_Model_Drawn_With_The_Depth_Mask_Off_Leaves_What_Is_Drawn_After_It_In_Front()
    {
        Open(64, 32);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        Color Middle(bool mask) => GetImageColor(Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            // A near cube drawn first, then a far one behind it.
            if (!mask) rlDisableDepthMask();
            DrawModel(cube, new Vector3(0, 0, 1), 1, new Color(255, 0, 0));
            rlEnableDepthMask();
            DrawModel(cube, new Vector3(0, 0, -1), 0.5f, new Color(0, 0, 255));
            EndMode3D();
        }, mask ? "masked" : "unmasked"), 32, 16);

        var hidden = Middle(mask: true);
        var shown = Middle(mask: false);
        ((int)hidden.R).Should().BeGreaterThan(hidden.B + 100, $"the near cube's depth hides the far one, not {hidden}");
        ((int)shown.B).Should().BeGreaterThan(shown.R + 100, $"a near cube that wrote no depth leaves the far one drawn after it in front, as a sky drawn round the camera does, not {shown}");
        UnloadModel(cube);
    }
}
