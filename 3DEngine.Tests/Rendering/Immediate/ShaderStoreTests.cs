using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering.Immediate;

[Trait("Category", "Unit")]
public class ShaderStoreTests
{
    private static ShaderProgram Program(params ShaderStage[] stages) =>
        new("test.slang", stages.ToDictionary(s => s, _ => new byte[] { 3, 2, 35, 7 }));

    [Fact]
    public void A_Shader_Needs_A_Fragment_Stage()
    {
        var store = new ShaderStore();

        store.Add(Program(ShaderStage.Fragment)).Should().Be(1);
        var act = () => store.Add(Program(ShaderStage.Vertex));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_Unloaded_Shader_Is_Handed_To_The_Renderer_Once()
    {
        var store = new ShaderStore();
        var id = store.Add(Program(ShaderStage.Fragment));

        store.Remove(id).Should().BeTrue();

        store.Get(id).Should().BeNull();
        store.TakeRemovals().Should().Equal(id);
        store.TakeRemovals().Should().BeEmpty();
    }

    [Fact]
    public void A_Slot_Is_Set_Without_Touching_The_Others()
    {
        var values = default(ShaderParams).With(2, new Vector4(1, 2, 3, 4));

        values[2].Should().Be(new Vector4(1, 2, 3, 4));
        values[0].Should().Be(Vector4.Zero);
        var act = () => values.With(4, Vector4.One);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_Change_Of_Shader_Or_Its_Values_Opens_A_Batch()
    {
        var list = new DrawList();
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);
        list.SetShader(1, default);
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);
        list.SetShader(1, default(ShaderParams).With(0, Vector4.One));
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);

        list.Batches.Select(b => (b.Shader, b.Params.P0.X)).Should().Equal((0, 0f), (1, 0f), (1, 1f));
    }
}
