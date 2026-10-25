using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// Global illumination reading a mesh a compute shader wrote, its triangles taken into the scene's
// distance field from the GPU once its count has come back.
public sealed partial class GlobalIlluminationTests
{
    // A box from min to max of six faces facing out, two triangles a face, and the count.
    private const string CountedBox = """
        uniform uint count;
        uniform float3 low;
        uniform float3 size;
        RWStructuredBuffer<float> positions;
        RWStructuredBuffer<uint> counter;

        static const float3 Faces[24] = {
            float3(1, 0, 1), float3(1, 0, 0), float3(1, 1, 0), float3(1, 1, 1),
            float3(0, 0, 0), float3(0, 0, 1), float3(0, 1, 1), float3(0, 1, 0),
            float3(0, 1, 1), float3(1, 1, 1), float3(1, 1, 0), float3(0, 1, 0),
            float3(0, 0, 0), float3(1, 0, 0), float3(1, 0, 1), float3(0, 0, 1),
            float3(0, 0, 1), float3(1, 0, 1), float3(1, 1, 1), float3(0, 1, 1),
            float3(1, 0, 0), float3(0, 0, 0), float3(0, 1, 0), float3(1, 1, 0),
        };
        static const uint Corners[6] = { 0, 1, 2, 0, 2, 3 };

        [shader("compute")]
        [numthreads(1, 1, 1)]
        void computeMain(uint3 id : SV_DispatchThreadID)
        {
            for (uint v = 0; v < 36; v++)
            {
                float3 p = low + Faces[(v / 6) * 4 + Corners[v % 6]] * size;
                positions[v * 3] = p.x;
                positions[v * 3 + 1] = p.y;
                positions[v * 3 + 2] = p.z;
            }
            counter[0] = count;
        }
        """;

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Red_Wall_A_Compute_Shader_Wrote_Tints_The_Side_Of_A_White_Block_Facing_It()
    {
        // The room of the red wall and the white block, the wall a box a compute shader wrote, read
        // with its count and with a count of 0, which draws nothing and leaves the field without it.
        // The screen's probes are left out, so the wall's light reaches the block through the
        // cascades alone, which trace the field.
        Open();
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        SetGlobalIllumination(GlobalIllumination.Low);
        SetGlobalIlluminationScreenProbes(false);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var shader = LoadComputeShaderFromMemory(CountedBox, "counted_box.slang");
        shader.IsValid.Should().BeTrue();
        var (low, size) = (new Vector3(-1.75f, 0, -3), new Vector3(0.3f, 3, 6));

        Vector3 Side(uint count)
        {
            var positions = LoadShaderBuffer(36 * 12);
            var counter = LoadShaderBuffer(4);
            SetShaderValue(shader, GetShaderLocation(shader, "count"), (int)count);
            SetShaderValue(shader, GetShaderLocation(shader, "low"), low);
            SetShaderValue(shader, GetShaderLocation(shader, "size"), size);
            SetShaderValueBuffer(shader, GetShaderLocation(shader, "positions"), positions);
            SetShaderValueBuffer(shader, GetShaderLocation(shader, "counter"), counter);
            ComputeShaderDispatch(shader, 1, 1, 1);
            var wall = LoadMeshFromShaderBuffers(positions, default, MeshStreamFormat.UInt, counter, 36);
            SetMeshBoundingBox(wall, new BoundingBox(low, low + size));
            var red = new ModelMaterial(new Color(220, 20, 20));
            var image = Capture(() =>
            {
                DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
                DrawMesh(wall, red, Matrix4x4.Identity);
                DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
            });
            var field = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<SceneFieldRenderer>()!;
            field.Drawn.Any(drawn => drawn.Instance.Mesh == wall.Id).Should().BeTrue("the wall stands in the field's plan once its count came back");
            UnloadMesh(wall);
            UnloadShaderBuffer(positions);
            UnloadShaderBuffer(counter);
            // The block's left side, left of its middle and half way up, as the red wall's test reads it.
            return Mean(image, 58, 48, 6, 10);
        }

        var lit = Side(36);
        var dark = Side(0);
        (lit.X - dark.X).Should().BeGreaterThan(20, $"light the compute-written wall sends on reaches the side, {lit} against {dark} with no wall");
        lit.X.Should().BeGreaterThan(lit.Y + 10, $"and it is red, {lit}");
        UnloadShader(shader);
        UnloadModel(slab);
    }

    // The box of CountedBox with each vertex's color, red as a uint red lowest, and a stream of two
    // uints a vertex of the program's own, as a voxel game's terrain shader reads.
    private static readonly string ColoredBox = CountedBox
        .Replace("RWStructuredBuffer<uint> counter;", "RWStructuredBuffer<uint> counter;\nRWStructuredBuffer<uint> colors;\nRWStructuredBuffer<uint> packed;")
        .Replace("positions[v * 3 + 2] = p.z;", "positions[v * 3 + 2] = p.z;\ncolors[v] = 0xFF1414DCu;\npacked[v * 2] = v;\npacked[v * 2 + 1] = 0u;");

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Wall_A_Compute_Shader_Wrote_Bounces_The_Color_Of_Its_Color_Stream_Beside_A_Stream_Of_Its_Own()
    {
        // The red wall's room, the wall drawn white with its red in a stream of colors beside a
        // stream of two uints, which gave the field no color, so the white wall bounced white.
        Open();
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        SetGlobalIllumination(GlobalIllumination.Low);
        SetGlobalIlluminationScreenProbes(false);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var shader = LoadComputeShaderFromMemory(ColoredBox, "colored_box.slang");
        shader.IsValid.Should().BeTrue();
        var (low, size) = (new Vector3(-1.75f, 0, -3), new Vector3(0.3f, 3, 6));

        Vector3 Side(bool colored)
        {
            var positions = LoadShaderBuffer(36 * 12);
            var counter = LoadShaderBuffer(4);
            var colors = LoadShaderBuffer(36 * 4);
            var packed = LoadShaderBuffer(36 * 8);
            SetShaderValue(shader, GetShaderLocation(shader, "count"), 36);
            SetShaderValue(shader, GetShaderLocation(shader, "low"), low);
            SetShaderValue(shader, GetShaderLocation(shader, "size"), size);
            SetShaderValueBuffer(shader, GetShaderLocation(shader, "positions"), positions);
            SetShaderValueBuffer(shader, GetShaderLocation(shader, "counter"), counter);
            SetShaderValueBuffer(shader, GetShaderLocation(shader, "colors"), colors);
            SetShaderValueBuffer(shader, GetShaderLocation(shader, "packed"), packed);
            ComputeShaderDispatch(shader, 1, 1, 1);
            var wall = LoadMeshFromShaderBuffers(positions, packed, MeshStreamFormat.UInt2, colored ? colors : default, counter, 36, 0, 0);
            wall.IsValid.Should().BeTrue();
            SetMeshBoundingBox(wall, new BoundingBox(low, low + size));
            var image = Capture(() =>
            {
                DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
                DrawMesh(wall, new ModelMaterial(Color.White), Matrix4x4.Identity);
                DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
            });
            UnloadMesh(wall);
            foreach (var buffer in new[] { positions, counter, colors, packed }) UnloadShaderBuffer(buffer);
            return Mean(image, 58, 48, 6, 10);
        }

        var tinted = Side(colored: true);
        var white = Side(colored: false);
        (tinted.X - tinted.Y).Should().BeGreaterThan(10, $"the side takes the red of the wall's colors, {tinted}");
        (tinted.X - tinted.Y).Should().BeGreaterThan(white.X - white.Y + 10, $"where with no colors the wall bounces its material's white, {white}");
        UnloadShader(shader);
        UnloadModel(slab);
    }
}
