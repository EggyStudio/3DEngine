using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The shadow of a masked surface drawn through a program's own shader, cut where the program's
/// stages cut it, read as <c>A_Masked_Surface_Casts_The_Shadow_Of_Its_Cut_Out</c> reads the engine's
/// material's.
/// </summary>
public sealed partial class OffscreenRenderTests
{
    // Two texels read nearest, the left cut out below the cutoff of a half and the right solid.
    private static Texture2D HalfCut()
    {
        var texture = LoadTextureFromImage(new Image([255, 255, 255, 40, 255, 255, 255, 230], 2, 1));
        SetTextureFilter(texture, TextureFilter.Point);
        return texture;
    }

    // A unit square a unit in front of a wall, from x 0.25 to 1.25, which the sun along -X and -Z
    // throws a unit to the left, answering the wall's red half a unit left of the middle, behind
    // the shadow of the square's left half, and at the middle, behind its right half's.
    private (byte Left, byte Right) SquareShadow(Model square, string name)
    {
        var camera = new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45);
        var wall = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, 0, -1)), Color.White, 1, castsShadows: true);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(wall, Vector3.Zero, Vector3.UnitX, 90, Vector3.One, Color.White);
            DrawModelEx(square, new Vector3(0.75f, 0, 1), Vector3.UnitX, 90, Vector3.One, Color.White);
            EndMode3D();
        }, name);
        UnloadModel(wall);
        return (GetImageColor(image, 22, 32).R, GetImageColor(image, 32, 32).R);
    }

    [NeedsVulkanFact]
    public void A_Masked_Surface_Through_A_Programs_Vertex_Stage_Casts_The_Shadow_Cut_At_The_Coordinate_The_Stage_Gives()
    {
        // A square of positions alone, each corner's texture coordinate packed in its stream as the
        // voxel game packs its plants' tiles. The shadow pass's own stage reads a coordinate of zero
        // on such a mesh, the cut-out texel, and the program's gives each corner its own.
        Open(64, 64);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("vertex")]
            ModelVertexOutput vertexMain(float3 position : POSITION, uint corner : CUSTOM0, ModelInstance instance)
            {
                return transformModelVertex(position, float3(0, 1, 0), float2(corner & 1, corner >> 1), instance);
            }

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                float4 color = alphaTested(baseColor(input), input);
                return float4(lit(color.rgb, input), color.a);
            }
            """, "corners.slang");
        var plane = GenMeshPlane(1, 1, 1, 1);
        GetApp().World.Resource<MeshStore>().TryGetData(plane.Id, out var vertices, out var indices).Should().BeTrue();
        var corners = vertices.Select(v => (uint)v.Uv.X | (uint)v.Uv.Y << 1).ToArray();
        var square = LoadModelFromMesh(UploadMesh([.. vertices.Select(v => v.Position)], indices, MeshStream.From(MeshStreamFormat.UInt, corners.AsSpan()), null));
        var texture = HalfCut();
        square.Materials[0] = new ModelMaterial(Color.White, texture) { AlphaMode = MaterialAlphaMode.Mask, Shader = shader };

        var (left, right) = SquareShadow(square, "corners");
        left.Should().BeGreaterThan(60, "light passes the half the texture cuts out at the coordinate the stage gives");
        right.Should().BeLessThan((byte)(left / 3), "the solid half shadows the wall");
        UnloadModel(square);
        UnloadMesh(plane);
        UnloadTexture(texture);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Programs_Shadow_Stage_Cuts_Its_Masked_Surface_Out_Of_The_Shadow_By_A_Texture_Of_Its_Own()
    {
        // The material's texture is white, which the shadow pass's own cut keeps whole, and the
        // program cuts the square by an atlas of its own, as the voxel game cuts its plants, in its
        // fragment stage and in its shadowMain.
        Open(64, 64);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            Sampler2D atlas;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                if (atlas.Sample(input.uv).a < 0.5)
                    discard;
                return float4(lit(baseColor(input).rgb, input), 1);
            }

            [shader("fragment")]
            void shadowMain(ModelVertexOutput input)
            {
                if (atlas.Sample(input.uv).a < 0.5)
                    discard;
            }
            """, "atlas.slang");
        var atlas = HalfCut();
        SetShaderValueTexture(shader, GetShaderLocation(shader, "atlas"), atlas);
        var square = LoadModelFromMesh(GenMeshPlane(1, 1, 1, 1));
        square.Materials[0] = new ModelMaterial(Color.White) { AlphaMode = MaterialAlphaMode.Mask, Shader = shader };

        var (left, right) = SquareShadow(square, "atlas");
        left.Should().BeGreaterThan(60, "light passes the half the program's shadow stage cuts out");
        right.Should().BeLessThan((byte)(left / 3), "the solid half shadows the wall");
        UnloadModel(square);
        UnloadTexture(atlas);
        UnloadShader(shader);
    }
}
