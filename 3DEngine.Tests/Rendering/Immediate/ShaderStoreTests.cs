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
    public async Task Shaders_Added_Replaced_And_Removed_While_Another_Thread_Looks_Them_Up_Are_Each_Found_And_None_Is_Lost()
    {
        // Two threads load shaders as a program does, each kept one given its second program at once
        // and each other one unloaded, while a third looks up every kept one it has been told of, as
        // the render thread gathering a frame's draws does.
        var store = new ShaderStore();
        const int Each = 1000;
        var (first, second) = (Program(ShaderStage.Fragment), Program(ShaderStage.Fragment, ShaderStage.Vertex));
        var kept = new int[2, Each];
        var told = new int[2];
        var missed = 0;
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
                for (int w = 0; w < 2; w++)
                    for (int i = 0, known = Volatile.Read(ref told[w]); i < known; i++)
                        if (store.Lookup(kept[w, i]) is not ({ }, true)) Interlocked.Increment(ref missed);
        });
        var removed = new List<int>[] { [], [] };
        var writers = Enumerable.Range(0, 2).Select(w => Task.Run(() =>
        {
            for (int i = 0; i < Each; i++)
            {
                var id = store.Add(first);
                store.SetKeepsPositions(id, true);
                store.Replace(id, second).Should().BeTrue();
                kept[w, i] = id;
                Volatile.Write(ref told[w], i + 1);
                var gone = store.Add(first);
                store.SetKeepsPositions(gone, true);
                store.Remove(gone).Should().BeTrue();
                removed[w].Add(gone);
            }
        })).ToArray();
        await Task.WhenAll(writers);
        await stop.CancelAsync();
        await reader;

        missed.Should().Be(0, "a shader once added is found by every lookup after, with whether it keeps its mesh's positions");
        for (int w = 0; w < 2; w++)
            for (int i = 0; i < Each; i++)
                store.Lookup(kept[w, i]).Program.Should().BeSameAs(second, "each shader kept holds the program put in its place");
        removed.SelectMany(ids => ids).Should().OnlyContain(id => store.Lookup(id).Program == null && !store.Lookup(id).KeepsPositions, "each shader unloaded is gone");
        store.TakeRemovals().Order().Should().Equal(removed.SelectMany(ids => ids).Order(), "the renderer is handed every shader unloaded, once");
        Enumerable.Range(0, 2).SelectMany(w => Enumerable.Range(0, Each).Select(i => kept[w, i])).Concat(removed.SelectMany(ids => ids))
            .Should().OnlyHaveUniqueItems("no two shaders are given the same id");
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
