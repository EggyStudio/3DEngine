using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A mesh whose vertices and count a compute shader writes, drawn by an indirect draw with nothing
/// read back: its triangles drawn as many as the count says, none for a count of 0, no more than its
/// capacity for a larger one, and its shadow cast as any mesh's.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ShaderBufferMeshTests : IDisposable
{
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly TestFolder _folder = new("engine-counted-");
    private int _captures;

    public void Dispose()
    {
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the indirect draws");
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // Two squares facing +z, the left one's six vertices first and the right one's after, both wound
    // counter-clockwise as seen from +z, or with flat set one square lying at height facing up and the
    // same square facing down after it, and the count.
    private const string Squares = """
        uniform uint count;
        uniform uint flat;
        uniform float height;
        RWStructuredBuffer<float> positions;
        RWStructuredBuffer<uint> counter;

        [shader("compute")]
        [numthreads(1, 1, 1)]
        void computeMain(uint3 id : SV_DispatchThreadID)
        {
            float2 corners[6] = { float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 0), float2(1, 1), float2(0, 1) };
            for (uint i = 0; i < 12; i++)
            {
                float2 c = corners[i % 6];
                float3 p;
                if (flat == 0)
                    p = float3(i < 6 ? -2.0 + 1.5 * c.x : 0.5 + 1.5 * c.x, -0.75 + 1.5 * c.y, 0.0);
                else
                {
                    float2 q = i < 6 ? c : float2(c.y, c.x);
                    p = float3(-1.0 + 2.0 * q.x, height, 1.0 - 2.0 * q.y);
                }
                positions[i * 3] = p.x;
                positions[i * 3 + 1] = p.y;
                positions[i * 3 + 2] = p.z;
            }
            counter[0] = count;
        }
        """;

    private static void Open()
    {
        var config = Config.Default.WithWindow("counted test", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // The squares' mesh over buffers of twelve vertices, written once with the count given.
    private static (ModelMesh Mesh, Shader Shader, ShaderBuffer Positions, ShaderBuffer Count) Written(uint count, int capacity, bool flat = false, float height = 0)
    {
        var shader = LoadComputeShaderFromMemory(Squares, "squares.slang");
        shader.IsValid.Should().BeTrue();
        var positions = LoadShaderBuffer(12 * 12);
        var counter = LoadShaderBuffer(4);
        SetShaderValue(shader, GetShaderLocation(shader, "count"), (int)count);
        SetShaderValue(shader, GetShaderLocation(shader, "flat"), flat ? 1 : 0);
        SetShaderValue(shader, GetShaderLocation(shader, "height"), height);
        SetShaderValueBuffer(shader, GetShaderLocation(shader, "positions"), positions);
        SetShaderValueBuffer(shader, GetShaderLocation(shader, "counter"), counter);
        ComputeShaderDispatch(shader, 1, 1, 1);
        var mesh = LoadMeshFromShaderBuffers(positions, default, MeshStreamFormat.UInt, counter, capacity);
        mesh.IsValid.Should().BeTrue();
        return (mesh, shader, positions, counter);
    }

    // Draws frames of the scene until one is captured, as the frames drawn after the first few.
    private Image Capture(Action draw, Camera3D camera)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 30 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            draw();
            EndMode3D();
            if (frame == 4) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        return LoadImage(path);
    }

    private static readonly Camera3D Facing = new(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 50);
    private static readonly ModelMaterial Glowing = new(Color.White) { Emissive = Color.White, EmissiveIntensity = 1 };

    // How bright the left square's middle and the right one's are.
    private static (int Left, int Right) Middles(Image image) =>
        (GetImageColor(image, 30, 32).R, GetImageColor(image, 66, 32).R);

    [NeedsVulkanTheory]
    [InlineData(12u, 12, true, true)]
    [InlineData(6u, 12, true, false)]
    [InlineData(0u, 12, false, false)]
    [InlineData(30u, 6, true, false)]
    public void A_Mesh_A_Shader_Wrote_Draws_As_Many_Vertices_As_Its_Count_Says_And_No_More_Than_Its_Capacity(uint count, int capacity, bool left, bool right)
    {
        Open();
        var (mesh, shader, positions, counter) = Written(count, capacity);
        var image = Capture(() => DrawMesh(mesh, Glowing, Matrix4x4.Identity), Facing);
        var (l, r) = Middles(image);
        (l > 128).Should().Be(left, $"the left square, vertices 0 to 5, is drawn where the count of {count} at a capacity of {capacity} reaches it, its middle {l}");
        (r > 128).Should().Be(right, $"the right square, vertices 6 to 11, is drawn where the count of {count} at a capacity of {capacity} reaches it, its middle {r}");
        UnloadMesh(mesh);
        UnloadShaderBuffer(positions);
        UnloadShaderBuffer(counter);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Mesh_A_Shader_Wrote_Casts_Its_Shadow_On_The_Floor_Under_It()
    {
        Open();
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.1f, -1, 0.05f)), Color.White, 2, castsShadows: true);
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var below = new Camera3D(new Vector3(0, 1.2f, 0.01f), Vector3.Zero, Vector3.UnitY, 70);
        float Shade(uint count)
        {
            var (mesh, shader, positions, counter) = Written(count, 12, flat: true, height: 2);
            var image = Capture(() =>
            {
                DrawModelEx(floor, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(8, 0.2f, 8), Color.White);
                DrawMesh(mesh, new ModelMaterial(Color.Gray), Matrix4x4.Identity);
            }, below);
            UnloadMesh(mesh);
            UnloadShaderBuffer(positions);
            UnloadShaderBuffer(counter);
            UnloadShader(shader);
            return GetImageColor(image, 48, 32).R;
        }

        var lit = Shade(0);
        var shaded = Shade(12);
        shaded.Should().BeLessThan(lit * 0.6f, $"the square over the floor's middle hides it from the sun, {shaded} against {lit} with no square");
        UnloadModel(floor);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Null_Device_Records_A_Draw_By_A_Count_A_Shader_Wrote()
    {
        var device = new NullGraphicsDevice();
        var commands = device.BeginFrame().CommandBuffer;
        using (var pass = new TrackedRenderPass(device, commands, new RenderPassDescriptor(device.SwapchainRenderPass, device.GetSwapchainFramebuffer(0), new Extent2D(1, 1))))
            pass.DrawCounted(null!, 6, 1, 0);

        device.CountedDraws.Should().Be(1);
    }
}
