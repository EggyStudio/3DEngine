using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The descriptor sets of the draws through a program's own shader, one a look a frame for every
/// pass that draws them.
/// </summary>
public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void Mesh_Entities_Drawn_Through_One_Programs_Shader_With_The_Same_Values_Share_One_Descriptor_Set_A_Frame()
    {
        // The voxel game's sections, every one drawn through its terrain shader with the same
        // values, each took a set made and written whole in each pass that drew it, about 2,045 a
        // frame at 12 columns over the shadows', the depth's, the camera's and the mirror's passes.
        Open(64, 64);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return float4(baseColor(input).rgb, 1);
            }
            """, "shared.slang");
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, -1, -1)), Color.White, 1, castsShadows: true);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 8)));
        // A mesh each, as a section has, so each is a draw of its own rather than an instance.
        for (int i = 0; i < 8; i++)
        {
            var section = ecs.Spawn();
            ecs.Add(section, new Mesh([new(-0.4f, -0.4f, 0), new(0.4f, -0.4f, 0), new(0.4f, 0.4f, 0), new(-0.4f, -0.4f, 0), new(0.4f, 0.4f, 0), new(-0.4f, 0.4f, 0)]));
            ecs.Add(section, new Material(Vector4.One) { AlphaMode = MaterialAlphaMode.Opaque, Shader = shader });
            ecs.Add(section, new Transform(new Vector3(i - 3.5f, 0, 0)));
        }

        var image = Capture(() => ClearBackground(Color.Black), "shared sets");
        var models = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<ModelRenderer>()!;
        models.DrawCalls.Should().BeGreaterThanOrEqualTo(8, "each square is a draw of its own through the program's shader");
        models.DrawSetsWritten.Should().Be(1, "the eight draws hold one look, so every pass takes the one set");
        // The square right of the middle, half a unit across at about ten pixels a unit.
        GetImageColor(image, 37, 32).R.Should().BeGreaterThan(200, "the squares are drawn with the set they share");
        UnloadShader(shader);
    }
}
