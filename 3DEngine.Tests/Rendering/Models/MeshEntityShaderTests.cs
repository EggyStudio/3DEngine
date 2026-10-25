using System.Numerics;
using System.Runtime.InteropServices;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering.Models;

/// <summary>Mesh entities drawn through a program's shader with the values the program set on it, without a GPU.</summary>
/// <remarks>
/// The values are set through the flat API, which runs against one app for the process, so these
/// run in the collection of the other tests that use it, never beside them.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class MeshEntityShaderTests : IDisposable
{
    private readonly App _app = new();

    public MeshEntityShaderTests()
    {
        _app.World.InsertResource(new EcsWorld());
        _app.World.InitResource<ModelDrawList>();
        _app.World.InitResource<MeshStore>();
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<ShaderStore>();
        UseApp(_app);
    }

    public void Dispose() => UseApp(null);

    [Fact]
    public void An_Entity_Whose_Material_Names_A_Shader_Is_Drawn_Alone_Through_It_With_The_Values_Last_Set()
    {
        var world = _app.World;
        var ecs = world.Resource<EcsWorld>();
        var draws = world.Resource<ModelDrawList>();
        var id = world.Resource<ShaderStore>().Add(new ShaderProgram("terrain.slang",
            new Dictionary<ShaderStage, byte[]> { [ShaderStage.Fragment] = [] }, [new ShaderUniform("fog", 0, 16)]));
        var shader = new Shader(id);
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(60f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 5)));
        Vector3[] section = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];
        var entities = new int[2];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = ecs.Spawn();
            ecs.Add(entities[i], new Mesh(section));
            ecs.Add(entities[i], new Material(Color.White) { AlphaMode = MaterialAlphaMode.Opaque, Shader = shader });
            ecs.Add(entities[i], new Transform(new Vector3(i, 0, 0)));
        }
        void Frame()
        {
            draws.Clear();
            MeshEntityDraws.Run(world);
            ecs.BeginFrame();
        }
        Vector4 Fog(in ModelDraw draw) => MemoryMarshal.Cast<byte, Vector4>(draw.Uniforms)[0];

        SetShaderValue(shader, GetShaderLocation(shader, "fog"), new Vector4(1, 2, 3, 4));
        Frame();
        draws.Groups.Where(g => g.Count > 0).Should().BeEmpty("a draw with a program's shader is no group's instance");
        draws.Draws.Should().HaveCount(2).And.OnlyContain(d => d.Shader == id);
        Fog(draws.Draws[0]).Should().Be(new Vector4(1, 2, 3, 4));
        draws.Draws[1].Uniforms.Should().BeSameAs(draws.Draws[0].Uniforms, "one copy a frame serves every entity, which the pass batches them by");
        draws.Draws.Select(d => d.World.Translation.X).Should().BeEquivalentTo([0f, 1f]);

        // Kept chunks take the values of each frame.
        SetShaderValue(shader, GetShaderLocation(shader, "fog"), new Vector4(5, 6, 7, 8));
        Frame();
        Frame();
        world.Resource<MeshEntityDraws>().ChunksKept.Should().Be(1, "nothing the entities are drawn by changed");
        Fog(draws.Draws[0]).Should().Be(new Vector4(5, 6, 7, 8), "the values the program set last");

        // A material without it is the engine's own again, an instance of a group.
        ecs.Update(entities[0], new Material(Color.White) { AlphaMode = MaterialAlphaMode.Opaque });
        Frame();
        draws.Draws.Should().ContainSingle().Which.World.Translation.X.Should().Be(1);
        draws.Groups.Where(g => g.Count > 0).Should().ContainSingle().Which.Template.Shader.Should().Be(0);
    }
}
