using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    // A cube drawn into a target, and that target's depth drawn over the window, at the given samples.
    private Image TargetDepth(int samples)
    {
        Open(64, 64, samples);
        var target = LoadRenderTexture(32, 32);
        // A unit from the cube's front face, where the depth is 0.95 with the near plane at 0.05,
        // and wide enough to see past its edges.
        var camera = new Camera3D(new Vector3(0, 0, 1.5f), Vector3.Zero, Vector3.UnitY, 90);

        var image = Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(Vector3.Zero, 1, 1, 1, Color.Red);
            EndMode3D();
            EndTextureMode();

            ClearBackground(Color.Blue);
            DrawTexture(target.Depth, 0, 0, Color.White);
        }, $"depth {samples}");
        UnloadRenderTexture(target);
        return image;
    }

    [NeedsVulkanFact]
    public void A_Render_Target_Has_Its_Depth_To_Sample()
    {
        var image = TargetDepth(4);

        var cube = GetImageColor(image, 16, 16);
        cube.R.Should().BeInRange(236, 248, "the cube's face is a unit from the camera, at a depth of 0.95");
        (cube.G, cube.B).Should().Be(((byte)0, (byte)0), "a depth is sampled into red alone");
        GetImageColor(image, 1, 1).R.Should().Be(255, "where nothing was drawn the depth is cleared to the far plane");
        GetImageColor(image, 48, 48).Should().Be(Color.Blue, "the depth texture is the target's size");
    }

    [NeedsVulkanFact]
    public void A_Render_Target_Drawn_With_One_Sample_Has_Its_Depth_To_Sample()
    {
        var image = TargetDepth(1);

        GetImageColor(image, 16, 16).R.Should().BeInRange(236, 248);
        GetImageColor(image, 1, 1).R.Should().Be(255);
    }

    [NeedsVulkanFact]
    public void A_Render_Target_Holds_What_Was_Drawn_Into_It_And_Draws_As_A_Texture()
    {
        Open(64, 32);
        var target = LoadRenderTexture(16, 16);
        var green = new Color(0, 255, 0);

        var image = Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(green);
            DrawRectangle(0, 0, 8, 16, Color.Blue);
            EndTextureMode();

            ClearBackground(Color.Black);
            DrawTexture(target.Texture, 32, 8, Color.White);
        });

        GetImageColor(image, 36, 16).Should().Be(Color.Blue, "the target's left half was drawn blue");
        GetImageColor(image, 44, 16).Should().Be(green, "its right half kept the clear color");
        GetImageColor(image, 16, 16).Should().Be(Color.Black, "the window around it is the window's own clear");
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void Pixels_Written_Into_A_Render_Target_Come_After_Its_Clear_And_Replace_What_Is_There()
    {
        Open(64, 32);
        var target = LoadRenderTexture(16, 16);
        var green = new Color(0, 255, 0);
        // A red square with a clear pixel in its corner, which replaces the green rather than
        // being laid over it.
        var pixels = new byte[4 * 4 * 4];
        for (int i = 0; i < 16; i++) (pixels[i * 4], pixels[i * 4 + 3]) = (255, 255);
        pixels[3] = 0;

        var image = Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(green);
            EndTextureMode();
            UpdateTextureRec(target.Texture, new Rectangle(4, 4, 4, 4), pixels).Should().BeTrue();
            BeginTextureMode(target);
            DrawRectangle(6, 6, 1, 1, Color.Blue);
            EndTextureMode();

            ClearBackground(Color.Black);
            DrawTexture(target.Texture, 32, 8, Color.White);
        });

        GetImageColor(image, 32 + 5, 8 + 5).Should().Be(new Color(255, 0, 0), "the pixels were written after the clear");
        GetImageColor(image, 32 + 4, 8 + 4).Should().Be(Color.Black, "a clear pixel written into the target leaves it clear, showing the window");
        GetImageColor(image, 32 + 6, 8 + 6).Should().Be(Color.Blue, "a shape drawn after the pixels lies over them");
        GetImageColor(image, 32 + 10, 8 + 10).Should().Be(green, "the rest of the target kept its clear");
        UpdateTextureRec(target.Texture, new Rectangle(14, 14, 4, 4), pixels).Should().BeFalse("the rectangle reaches past the target");
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_Reads_Its_Slot_And_Applies_Only_Inside_Its_Mode()
    {
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return param(0);
            }
            """, "slot.slang");
        SetShaderValue(shader, 0, new Vector4(1, 0, 1, 1));

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            DrawRectangle(0, 0, 32, 32, Color.White);
            EndShaderMode();
            DrawRectangle(32, 0, 32, 32, Color.White);
        });

        GetImageColor(image, 16, 16).Should().Be(new Color(255, 0, 255), "the shader returns slot 0");
        GetImageColor(image, 48, 16).Should().Be(Color.White, "after EndShaderMode the engine's own shader draws");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void An_Array_Uniform_Takes_Its_Values_Each_At_Sixteen_Bytes()
    {
        Open(32, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            uniform float channels[3];

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return float4(channels[0], channels[1], channels[2], 1.0);
            }
            """, "array.slang");
        SetShaderValueV(shader, GetShaderLocation(shader, "channels"), [1f, 0f, 1f]);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            DrawRectangle(0, 0, 32, 32, Color.White);
            EndShaderMode();
        });

        GetImageColor(image, 16, 16).Should().Be(new Color(255, 0, 255), "each float of the array reached its element");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Shaders_Values_Belong_To_Its_App_So_The_Next_Apps_Shader_Of_The_Same_Id_Starts_Afresh()
    {
        // An app that leaves a shader of one float loaded when it closes, and the next app, whose
        // first shader takes the same id and an array of three, whose values once went into the
        // first shader's block of sixteen bytes and did not fit, as the order the tests ran in on
        // macOS showed.
        Open(32, 32);
        var single = LoadShaderFromMemory("""
            import engine;

            uniform float level;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return float4(level, 0.0, 0.0, 1.0);
            }
            """, "single.slang");
        SetShaderValue(single, GetShaderLocation(single, "level"), 0.5f);
        CloseWindow();
        UseApp(null);

        Open(32, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            uniform float channels[3];

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return float4(channels[0], channels[1], channels[2], 1.0);
            }
            """, "array.slang");
        shader.Id.Should().Be(single.Id, "the new app's store gives ids from 1 again");
        SetShaderValueV(shader, GetShaderLocation(shader, "channels"), [1f, 0f, 1f]);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            DrawRectangle(0, 0, 32, 32, Color.White);
            EndShaderMode();
        });

        GetImageColor(image, 16, 16).Should().Be(new Color(255, 0, 255), "the array's values went into a block of its own shader's size");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Render_Texture_Drawn_Into_Before_BeginDrawing_Is_Drawn_In_That_Frame()
    {
        // raylib's examples draw into a render texture between frames, before BeginDrawing, flat
        // and in 3D, which BeginDrawing once forgot.
        Open(32, 32);
        var flat = LoadRenderTexture(32, 32);
        var deep = LoadRenderTexture(32, 32);
        var camera = new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45);

        var image = Capture(() => { });
        for (int frame = 0; frame < 10; frame++)
        {
            BeginTextureMode(flat);
            ClearBackground(Color.Black);
            DrawRectangle(0, 0, 32, 32, Color.Red);
            EndTextureMode();
            BeginTextureMode(deep);
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(Vector3.Zero, 2, 2, 2, Color.Green);
            EndMode3D();
            EndTextureMode();
            image = Capture(() =>
            {
                ClearBackground(Color.Blue);
                DrawTextureRec(flat.Texture, new Rectangle(0, 0, 16, -32), Vector2.Zero, Color.White);
                DrawTextureRec(deep.Texture, new Rectangle(16, 0, 16, -32), new Vector2(16, 0), Color.White);
            }, $"between-{frame}");
            if (GetImageColor(image, 8, 16) == Color.Red) break;
        }

        GetImageColor(image, 8, 16).Should().Be(Color.Red, "the target was drawn red before the frame began");
        GetImageColor(image, 24, 16).G.Should().BeGreaterThan(100, "the cube was drawn into the other with its camera before the frame began");
        UnloadRenderTexture(flat);
        UnloadRenderTexture(deep);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_Reads_Named_Uniforms_As_They_Were_When_Each_Shape_Was_Drawn()
    {
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            uniform float4 tint;
            uniform float strength;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return float4(tint.rgb * strength, 1.0);
            }
            """, "named.slang");
        var tint = GetShaderLocation(shader, "tint");
        var strength = GetShaderLocation(shader, "strength");
        tint.Should().BeGreaterThanOrEqualTo(0);
        SetShaderValue(shader, strength, 1f);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            SetShaderValue(shader, tint, new Vector4(1, 0, 0, 1));
            DrawRectangle(0, 0, 32, 32, Color.White);
            SetShaderValue(shader, tint, new Vector4(0, 0, 1, 1));
            DrawRectangle(32, 0, 32, 32, Color.White);
            EndShaderMode();
        });

        GetImageColor(image, 16, 16).Should().Be(new Color(255, 0, 0), "the left square was drawn while the tint was red");
        GetImageColor(image, 48, 16).Should().Be(new Color(0, 0, 255), "and the right one after it was set to blue");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Render_Texture_Is_Drawn_At_The_Windows_Samples_Unless_It_Asks_For_One()
    {
        Open(64, 32, samples: 4);
        var smooth = LoadRenderTexture(32, 32);
        var hard = LoadRenderTextureEx(32, 32, PixelFormat.UncompressedR8G8B8A8, samples: 1);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // Read texel for texel, so the frame shows each target's own texels.
        SetTextureFilter(smooth.Texture, TextureFilter.Point);
        SetTextureFilter(hard.Texture, TextureFilter.Point);

        var image = Capture(() =>
        {
            foreach (var target in new[] { smooth, hard })
            {
                BeginTextureMode(target);
                ClearBackground(Color.Black);
                DrawCircle(16, 16, 11.3f, Color.White);
                // A model too, drawn by the model pass's pipelines for the target's samples, unlit
                // and so white, turned so its edges slant.
                BeginMode3D(new Camera3D(new Vector3(0, 0, 9), Vector3.Zero, Vector3.UnitY, 45));
                DrawModelEx(cube, new Vector3(2.2f, 2.2f, 0), Vector3.UnitZ, 30, Vector3.One, Color.White);
                EndMode3D();
                EndTextureMode();
            }
            ClearBackground(Color.Black);
            DrawTextureRec(smooth.Texture, new Rectangle(0, 0, 32, -32), Vector2.Zero, Color.White);
            DrawTextureRec(hard.Texture, new Rectangle(0, 0, 32, -32), new Vector2(32, 0), Color.White);
        }, "target samples");

        // The pixels of a half that are neither the circle nor the ground, the edge's blend.
        int Blended(int left) => Enumerable.Range(0, 32 * 32).Count(i => GetImageColor(image, left + i % 32, i / 32).R is > 20 and < 235);
        Blended(0).Should().BeGreaterThan(20, "a render texture drawn at the window's four samples smooths the circle's edge");
        Blended(32).Should().Be(0, "and one asked for at one sample leaves it hard, as raylib's");
        (GetImageColor(image, 25, 25).R, GetImageColor(image, 57, 25).R).Should().Be(((byte)255, (byte)255), "the cube is drawn into both, past the circle at their lower right");
        UnloadModel(cube);
        UnloadRenderTexture(smooth);
        UnloadRenderTexture(hard);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_Mixes_Its_Own_Texture_With_The_One_Drawn()
    {
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            Sampler2D detail;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return boundTexture.Sample(input.uv) * detail.Sample(input.uv);
            }
            """, "detail.slang");
        var yellow = LoadTextureFromImage(GenImageColor(2, 2, new Color(255, 255, 0)));
        var cyan = LoadTextureFromImage(GenImageColor(2, 2, new Color(0, 255, 255)));
        var detail = GetShaderLocation(shader, "detail");
        detail.Should().BeGreaterThanOrEqualTo(0);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            SetShaderValueTexture(shader, detail, cyan);
            DrawTexturePro(yellow, new Rectangle(0, 0, 2, 2), new Rectangle(0, 0, 32, 32), Vector2.Zero, 0, Color.White);
            EndShaderMode();
        }, "immediate detail");

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0), "yellow times cyan is green");
        UnloadShader(shader);
        UnloadTexture(yellow);
        UnloadTexture(cyan);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_Reads_A_Texture_Through_A_Sampler_Declared_Apart_From_It()
    {
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            Texture2D detail;
            SamplerState detailSampler;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return boundTexture.Sample(input.uv) * detail.Sample(detailSampler, input.uv);
            }
            """, "apart.slang");
        var yellow = LoadTextureFromImage(GenImageColor(2, 2, new Color(255, 255, 0)));
        var cyan = LoadTextureFromImage(GenImageColor(2, 2, new Color(0, 255, 255)));
        (GetShaderLocation(shader, "detail"), GetShaderLocation(shader, "detailSampler")).Should().NotBe((-1, -1));

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            SetShaderValueTexture(shader, GetShaderLocation(shader, "detail"), cyan);
            SetShaderValueTexture(shader, GetShaderLocation(shader, "detailSampler"), cyan);
            DrawTexturePro(yellow, new Rectangle(0, 0, 2, 2), new Rectangle(0, 0, 32, 32), Vector2.Zero, 0, Color.White);
            EndShaderMode();
        }, "immediate apart");

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0), "yellow times cyan is green, the texture read through its own sampler");
        UnloadShader(shader);
        UnloadTexture(yellow);
        UnloadTexture(cyan);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_With_Uniforms_And_A_Texture_Of_Its_Own_Reads_Both()
    {
        Open(32, 32);
        // With a uniform, Slang puts the uniform buffer at binding 0 and the texture after the pass's.
        var shader = LoadShaderFromMemory("""
            import engine;

            uniform float4 tint;
            Sampler2D detail;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return detail.Sample(input.uv) * tint;
            }
            """, "tinteddetail.slang");
        var cyan = LoadTextureFromImage(GenImageColor(2, 2, new Color(0, 255, 255)));
        SetShaderValueTexture(shader, GetShaderLocation(shader, "detail"), cyan);
        SetShaderValue(shader, GetShaderLocation(shader, "tint"), new Vector4(0, 1, 0, 1));

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            DrawRectangle(0, 0, 32, 32, Color.White);
            EndShaderMode();
        }, "tinted detail");

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0), "cyan times green is green");
        UnloadShader(shader);
        UnloadTexture(cyan);
    }

    [NeedsVulkanFact]
    public void A_Frame_Draws_More_Models_With_Their_Own_Uniforms_Than_One_Descriptor_Pool_Holds()
    {
        Open(32, 32);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            uniform float4 tint;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return tint;
            }
            """, "tinted.slang");
        var tint = GetShaderLocation(shader, "tint");
        var cube = LoadModelFromMesh(GenMeshCube(0.01f, 0.01f, 0.01f));
        cube.Materials[0] = new ModelMaterial(Color.White) { Shader = shader };
        var big = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        big.Materials[0] = new ModelMaterial(Color.White) { Shader = shader };

        // Each draw with uniforms of its own takes a set, and a pool holds 4096.
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45));
            for (int i = 0; i < 5000; i++)
            {
                SetShaderValue(shader, tint, new Vector4(i / 5000f, 0, 0, 1));
                DrawModel(cube, new Vector3(10, 0, -i), 1, Color.White);
            }
            SetShaderValue(shader, tint, new Vector4(0, 1, 0, 1));
            DrawModel(big, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }, "many sets");

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0, 255), "the last draw, past the first pool's sets, has its own uniforms");
        UnloadModel(cube);
        UnloadModel(big);
    }

    [NeedsVulkanFact]
    public void Instanced_Copies_Of_A_Mesh_With_Its_Own_Shader_Are_Told_Apart_By_Their_Instance_Counted_From_Zero()
    {
        Open(64, 16);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("vertex")]
            ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0,
                ModelInstance instance, uint id : SV_InstanceID)
            {
                ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
                output.color = float4(id / 3.0, 0, 0, 1);
                return output;
            }

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return input.color;
            }
            """, "instances.slang");
        var cube = LoadModelFromMesh(GenMeshCube(0.8f, 0.8f, 0.8f));
        var plain = cube.Materials[0];
        Matrix4x4[] places = [.. Enumerable.Range(0, 4).Select(i => Matrix4x4.CreateTranslation(i * 2 - 3, 0, 0))];

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY, 2, CameraProjection.Orthographic));
            // Drawn first, so the copies' instances start past the first in the frame's ring.
            DrawMesh(cube.Meshes[0], plain, Matrix4x4.CreateTranslation(0, 100, 0));
            DrawMeshInstanced(cube.Meshes[0], new ModelMaterial(Color.White) { Shader = shader }, places);
            EndMode3D();
        }, "instanced");

        Enumerable.Range(0, 4).Select(i => (int)GetImageColor(image, 8 + i * 16, 8).R)
            .Should().Equal([0, 85, 170, 255], "each copy is colored by its instance, the first 0");
        GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>().DrawCalls
            .Should().Be(2, "the plain cube is one call and the four copies with the shader another");
        UnloadModel(cube);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void Shaders_That_Draw_Read_The_Storage_Buffer_A_Dispatch_Wrote()
    {
        Open(64, 16);
        var fill = LoadComputeShaderFromMemory("""
            RWStructuredBuffer<float4> colors;

            [shader("compute")]
            [numthreads(4, 1, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                colors[id.x] = float4(id.x / 3.0, 1 - id.x / 3.0, 0, 1);
            }
            """, "fill.slang");
        var flat = LoadShaderFromMemory("""
            import engine;

            StructuredBuffer<float4> colors;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return colors[3];
            }
            """, "flat.slang");
        var instances = LoadShaderFromMemory("""
            import modelpass;

            StructuredBuffer<float4> colors;

            [shader("vertex")]
            ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0,
                ModelInstance instance, uint id : SV_InstanceID)
            {
                ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
                output.color = colors[id];
                return output;
            }

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return input.color;
            }
            """, "instanced.slang");
        var colors = LoadShaderBuffer(4 * 16);
        SetShaderValueBuffer(fill, GetShaderLocation(fill, "colors"), colors);
        SetShaderValueBuffer(flat, GetShaderLocation(flat, "colors"), colors);
        SetShaderValueBuffer(instances, GetShaderLocation(instances, "colors"), colors);
        ComputeShaderDispatch(fill, 1, 1, 1);

        var cube = LoadModelFromMesh(GenMeshCube(0.8f, 0.8f, 0.8f));
        Matrix4x4[] places = [.. Enumerable.Range(0, 4).Select(i => Matrix4x4.CreateTranslation(i * 2 - 3, 0, 0))];
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(flat);
            DrawRectangle(0, 0, 64, 2, Color.White);
            EndShaderMode();
            BeginMode3D(new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY, 2, CameraProjection.Orthographic));
            DrawMeshInstanced(cube.Meshes[0], new ModelMaterial(Color.White) { Shader = instances }, places);
            EndMode3D();
        }, "buffers");

        GetImageColor(image, 32, 0).Should().Be(new Color(255, 0, 0, 255), "the rectangle takes the last color the dispatch wrote");
        Enumerable.Range(0, 4).Select(i => (int)GetImageColor(image, 8 + i * 16, 8).R)
            .Should().Equal([0, 85, 170, 255], "each copy takes its own color from the buffer");
        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadModel(cube);
        UnloadShaderBuffer(colors);
        UnloadShader(fill);
        UnloadShader(flat);
        UnloadShader(instances);
    }
}
