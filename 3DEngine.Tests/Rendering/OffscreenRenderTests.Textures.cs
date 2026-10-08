using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Render_Texture_Of_Several_Images_Takes_Each_Output_Of_A_Shader_Into_Its_Own()
    {
        Open(32, 32);
        var target = LoadRenderTexture(8, 4, PixelFormat.UncompressedR8G8B8A8, PixelFormat.UncompressedR8G8B8A8, PixelFormat.UncompressedR8G8B8A8);
        var shader = LoadShaderFromMemory("""
            import engine;

            struct TwoOutputs
            {
                float4 first : SV_Target0;
                float4 second : SV_Target1;
            };

            [shader("fragment")]
            TwoOutputs fragmentMain(VertexOutput input)
            {
                TwoOutputs output;
                output.first = float4(1.0, 0.0, 0.0, 1.0);
                output.second = float4(0.0, 1.0, 0.0, 0.6);
                return output;
            }
            """, "two.slang");
        Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(Color.Blue);
            rlDisableColorBlend();
            BeginShaderMode(shader);
            DrawRectangle(0, 0, 4, 4, Color.White);
            EndShaderMode();
            rlEnableColorBlend();
            EndTextureMode();
            ClearBackground(Color.Black);
        }, "several");

        target.Textures.Should().HaveCount(3);
        var (first, second, third) = (LoadImageFromTexture(target.Textures[0]), LoadImageFromTexture(target.Textures[1]), LoadImageFromTexture(target.Textures[2]));
        GetImageColor(first, 1, 1).Should().Be(new Color(255, 0, 0, 255), "the first output goes into the first image");
        GetImageColor(second, 1, 1).Should().Be(new Color(0, 255, 0, 153), "the second into the second, its alpha kept with blending off");
        GetImageColor(third, 1, 1).Should().Be(Color.Blue, "and an image past the shader's outputs keeps its clear");
        GetImageColor(second, 6, 2).Should().Be(Color.Blue, "every image is cleared to the one color");
        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadShader(shader);
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void A_Model_Shader_Writes_Half_Floats_Into_A_G_Buffer_Read_Back_From_0_To_1()
    {
        Open(32, 32);
        var target = LoadRenderTexture(16, 16, PixelFormat.UncompressedR16G16B16, PixelFormat.UncompressedR8G8B8A8);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            struct GBuffer
            {
                float4 position : SV_Target0;
                float4 albedo : SV_Target1;
            };

            [shader("fragment")]
            GBuffer fragmentMain(ModelVertexOutput input)
            {
                GBuffer output;
                output.position = float4(0.25, 0.5, 2.0, 1.0);
                output.albedo = float4(1.0, 1.0, 0.0, 1.0);
                return output;
            }
            """, "gbuffer.slang");
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0].Shader = shader;
        Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(Color.Blank);
            BeginMode3D(new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45));
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
            EndTextureMode();
            ClearBackground(Color.Black);
        }, "gbuffer");

        GetImageColor(LoadImageFromTexture(target.Textures[0]), 8, 8).Should().Be(new Color(64, 128, 255, 255),
            "a half float reads back from 0 to 1 as a byte, 2 as 1");
        GetImageColor(LoadImageFromTexture(target.Textures[1]), 8, 8).Should().Be(new Color(255, 255, 0, 255));
        GetImageColor(LoadImageFromTexture(target.Textures[0]), 0, 0).Should().Be(Color.Blank, "where the cube is not, the clear");
        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadModel(cube);
        UnloadShader(shader);
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void A_Render_Texture_And_A_Texture_Read_Back_As_Images()
    {
        Open(32, 32);
        var target = LoadRenderTexture(8, 4);
        var source = GenImageColor(4, 2, Color.Green);
        ImageDrawPixel(ref source, 3, 1, Color.Yellow);
        var texture = LoadTextureFromImage(source);
        Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(Color.Blue);
            DrawRectangle(0, 0, 4, 4, Color.Red);
            EndTextureMode();
            ClearBackground(Color.Black);
        }, "drawn");

        var drawn = LoadImageFromTexture(target.Texture);
        (drawn.Width, drawn.Height).Should().Be((8, 4));
        GetImageColor(drawn, 1, 1).Should().Be(Color.Red, "the left half was drawn red");
        GetImageColor(drawn, 6, 2).Should().Be(Color.Blue, "and the rest cleared blue");

        var read = LoadImageFromTexture(texture);
        read.Data.Should().Equal(source.Data, "a texture reads back as the image it was made from");
        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadTexture(texture);
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void A_Rectangle_Of_A_Texture_Is_Replaced_And_The_Rest_Kept()
    {
        Open(32, 32);
        var texture = LoadTextureFromImage(GenImageColor(4, 2, Color.Green));
        GenTextureMipmaps(ref texture);
        Capture(() => ClearBackground(Color.Black), "uploaded");

        var red = new byte[2 * 2 * 4];
        for (int i = 0; i < red.Length; i += 4) (red[i], red[i + 3]) = (255, 255);
        UpdateTextureRec(texture, new Rectangle(2, 0, 2, 2), red).Should().BeTrue();
        UpdateTextureRec(texture, new Rectangle(3, 0, 2, 2), red).Should().BeFalse("a rectangle past the edge is refused");
        Capture(() => ClearBackground(Color.Black), "updated");

        var read = LoadImageFromTexture(texture);
        GetImageColor(read, 0, 1).Should().Be(Color.Green, "the left half is kept");
        GetImageColor(read, 3, 1).Should().Be(new Color(255, 0, 0, 255), "the right half is the new rectangle");
        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void A_Render_Texture_Nothing_Clears_Keeps_What_It_Held_From_Frame_To_Frame()
    {
        // raylib's trails and paintings draw into a render texture a little each frame and clear it
        // never, which a target cleared in every frame drew as the last frame's stroke alone.
        Open(32, 32);
        var target = LoadRenderTexture(8, 4);
        void Frame(Action intoTarget)
        {
            BeginDrawing();
            BeginTextureMode(target);
            intoTarget();
            EndTextureMode();
            ClearBackground(Color.Black);
            EndDrawing();
        }

        Frame(() => DrawRectangle(0, 0, 4, 4, Color.Red));
        var first = LoadImageFromTexture(target.Texture);
        GetImageColor(first, 1, 1).Should().Be(Color.Red);
        GetImageColor(first, 6, 2).Should().Be(Color.Blank, "a new render texture starts transparent black");

        Frame(() => DrawRectangle(4, 0, 4, 4, Color.Blue));
        var second = LoadImageFromTexture(target.Texture);
        GetImageColor(second, 1, 1).Should().Be(Color.Red, "nothing cleared it, so the frame before stays");
        GetImageColor(second, 6, 2).Should().Be(Color.Blue);

        Frame(() => ClearBackground(Color.White));
        var cleared = LoadImageFromTexture(target.Texture);
        GetImageColor(cleared, 1, 1).Should().Be(Color.White, "ClearBackground inside its texture mode clears it");
        GetImageColor(cleared, 6, 2).Should().Be(Color.White);
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty();
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void A_Compute_Shader_Writes_A_Render_Texture_That_Is_Then_Drawn()
    {
        Open(32, 32);
        var target = LoadRenderTexture(4, 4);
        SetTextureFilter(target.Texture, TextureFilter.Point);
        Capture(() => ClearBackground(Color.Black), "made");

        var paint = LoadComputeShaderFromMemory("""
            RWTexture2D<float4> image;

            [shader("compute")]
            [numthreads(4, 4, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                image[id.xy] = float4(id.x / 3.0, 0, id.y / 3.0, 1);
            }
            """, "target.slang");
        SetShaderValueTexture(paint, GetShaderLocation(paint, "image"), target.Texture);
        ComputeShaderDispatch(paint, 1, 1, 1);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawTextureEx(target.Texture, Vector2.Zero, 0, 8, Color.White);
        }, "painted");
        GetImageColor(image, 28, 4).Should().Be(new Color(255, 0, 0, 255), "red grows across, written through the target's own format");
        GetImageColor(image, 4, 28).Should().Be(new Color(0, 0, 255, 255), "and blue down");
        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadShader(paint);
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void A_Compute_Shader_Writes_A_Render_Texture_Through_A_Stand_In_As_It_Writes_One_Directly()
    {
        // The same render texture painted twice, written directly and then through the stand-in a
        // device that cannot store to its format writes, the shader reading the red the frame drew
        // into it and writing green and blue by the place.
        Image Paint(bool standIn)
        {
            Open(32, 32);
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            if (standIn) ((GraphicsDevice)renderer.Context.Graphics!).WriteTargetsThroughStandIns();
            var target = LoadRenderTexture(4, 4);
            SetTextureFilter(target.Texture, TextureFilter.Point);
            Capture(() =>
            {
                BeginTextureMode(target);
                ClearBackground(new Color(200, 0, 0, 255));
                EndTextureMode();
                ClearBackground(Color.Black);
            }, "made");
            var stored = renderer.RenderWorld.TryGet<GpuTextures>()!.TargetFor(target.Texture.Id)!.ColorView.Image.Description.Usage.HasFlag(ImageUsage.Storage);
            if (standIn) stored.Should().BeFalse("the target is written through a stand-in, as on a device that cannot store to its format");

            var paint = LoadComputeShaderFromMemory("""
                RWTexture2D<float4> image;

                [shader("compute")]
                [numthreads(4, 4, 1)]
                void computeMain(uint3 id : SV_DispatchThreadID)
                {
                    image[id.xy] = float4(image[id.xy].r, id.x / 3.0, id.y / 3.0, 1);
                }
                """, "stand-in.slang");
            SetShaderValueTexture(paint, GetShaderLocation(paint, "image"), target.Texture);
            ComputeShaderDispatch(paint, 1, 1, 1);
            var image = Capture(() =>
            {
                ClearBackground(Color.Black);
                DrawTextureEx(target.Texture, Vector2.Zero, 0, 8, Color.White);
            }, standIn ? "through" : "direct");
            GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
            UnloadShader(paint);
            UnloadRenderTexture(target);
            CloseWindow();
            UseApp(null);
            return image;
        }

        var direct = Paint(false);
        var through = Paint(true);
        GetImageColor(through, 28, 28).Should().Be(new Color(200, 255, 255, 255), "the red the target held, read through the stand-in, kept beside the green and blue written");
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                GetImageColor(through, x, y).Should().Be(GetImageColor(direct, x, y), $"the stand-in's picture is the direct one's at {x}, {y}");
    }

    [NeedsVulkanFact]
    public void A_Compute_Shader_Writes_A_Texture_That_Is_Then_Drawn_And_Samples_One()
    {
        Open(32, 32);
        var texture = LoadTextureFromImage(GenImageColor(4, 4, Color.Black));
        SetTextureFilter(texture, TextureFilter.Point);
        Capture(() => ClearBackground(Color.Black), "upload");

        var paint = LoadComputeShaderFromMemory("""
            RWTexture2D<float4> image;

            [shader("compute")]
            [numthreads(4, 4, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                image[id.xy] = float4(id.x / 3.0, id.y / 3.0, 0, 1);
            }
            """, "paint.slang");
        SetShaderValueTexture(paint, GetShaderLocation(paint, "image"), texture);
        ComputeShaderDispatch(paint, 1, 1, 1);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawTextureEx(texture, Vector2.Zero, 0, 8, Color.White);
        }, "painted");
        GetImageColor(image, 4, 4).Should().Be(new Color(0, 0, 0, 255), "the corner the dispatch's first thread wrote");
        GetImageColor(image, 28, 4).Should().Be(new Color(255, 0, 0, 255));
        GetImageColor(image, 4, 28).Should().Be(new Color(0, 255, 0, 255));
        GetImageColor(image, 28, 28).Should().Be(new Color(255, 255, 0, 255));

        var read = LoadComputeShaderFromMemory("""
            Sampler2D source;
            RWStructuredBuffer<float4> result;

            [shader("compute")]
            [numthreads(1, 1, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                result[0] = source.SampleLevel(float2(0.9, 0.1), 0);
            }
            """, "read.slang");
        var result = LoadShaderBuffer(16);
        SetShaderValueTexture(read, GetShaderLocation(read, "source"), texture);
        SetShaderValueBuffer(read, GetShaderLocation(read, "result"), result);
        ComputeShaderDispatch(read, 1, 1, 1);
        var color = new Vector4[1];
        ReadShaderBuffer<Vector4>(result, color);
        color[0].X.Should().BeApproximately(1, 1e-3f, "it sampled the right edge the first dispatch wrote");
        color[0].Y.Should().BeApproximately(0, 1e-3f);

        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadShaderBuffer(result);
        UnloadShader(read);
        UnloadShader(paint);
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void A_Compute_Shader_Writing_A_Mipmapped_Texture_Makes_Its_Smaller_Levels_Again()
    {
        Open(32, 32);
        var texture = LoadTextureFromImage(GenImageColor(16, 16, Color.Black));
        GenTextureMipmaps(ref texture);
        SetTextureFilter(texture, TextureFilter.Bilinear);
        Capture(() => ClearBackground(Color.Black), "upload");

        var paint = LoadComputeShaderFromMemory("""
            RWTexture2D<float4> image;

            [shader("compute")]
            [numthreads(8, 8, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                image[id.xy] = float4(1, 0, 0, 1);
            }
            """, "mips.slang");
        SetShaderValueTexture(paint, GetShaderLocation(paint, "image"), texture);
        ComputeShaderDispatch(paint, 2, 2, 1);

        // Drawn an eighth of its size, so the 2 by 2 level is sampled.
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawTextureEx(texture, Vector2.Zero, 0, 1, Color.White);
            DrawTextureEx(texture, new Vector2(20, 20), 0, 0.125f, Color.White);
        }, "painted");
        GetImageColor(image, 8, 8).Should().Be(new Color(255, 0, 0, 255), "the first level, which the shader wrote");
        GetImageColor(image, 21, 21).Should().Be(new Color(255, 0, 0, 255), "a small level, made again from the first");
        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadShader(paint);
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void A_Model_Shader_Mixes_Its_Own_Texture_With_The_Base_Color()
    {
        Open(32, 32);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            Sampler2D detail;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return float4(toDisplay(baseColor(input).rgb) * detail.Sample(input.uv).rgb, 1.0);
            }
            """, "modeldetail.slang");
        var cyan = LoadTextureFromImage(GenImageColor(2, 2, new Color(0, 255, 255)));
        SetShaderValueTexture(shader, GetShaderLocation(shader, "detail"), cyan);
        var cube = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        cube.Materials[0] = new ModelMaterial(new Color(255, 255, 0)) { Shader = shader };

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45));
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }, "model detail");

        // Green, a little under full where the pass's tonemapping bends the brightest light.
        var middle = GetImageColor(image, 16, 16);
        (middle.R < 10 && middle.G > 230 && middle.B < 10).Should().BeTrue($"the yellow base color times cyan is green, not {middle}");
        UnloadModel(cube);
        UnloadShader(shader);
        UnloadTexture(cyan);
    }

    [NeedsVulkanFact]
    public void ImGui_Draws_Over_The_Frame_Where_It_Is_Told()
    {
        Open(64, 32);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawRectangle(0, 0, 64, 32, Color.Blue);
            // ImGui colors are packed as ABGR, so this is opaque green.
            ImGuiNET.ImGui.GetForegroundDrawList().AddRectFilled(new Vector2(8, 8), new Vector2(24, 24), 0xFF00FF00);
        });

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0), "ImGui draws after the immediate shapes, over them");
        GetImageColor(image, 48, 16).Should().Be(Color.Blue, "the rest of the frame is the shapes beneath");
    }
}
