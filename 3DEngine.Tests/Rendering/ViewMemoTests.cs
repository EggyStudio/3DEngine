using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

/// <summary>The views the model passes of a frame draw through, worked against its origin and kept, read on the CPU with no device.</summary>
[Trait("Category", "Unit")]
public sealed class ViewMemoTests
{
    [Fact]
    public void A_Pass_Of_Many_Batches_Through_One_View_Looks_In_The_Kept_Views_Once()
    {
        // The window's camera far out with its eye noted, and a light's view, as a pass of the
        // window's 660 batches asks for each batch's view and its push.
        var camera = new Camera3D(new Vector3(1140736, 2.5f, 285701.5f), new Vector3(1140736, 0.5f, 285696), Vector3.UnitY, 60);
        var projection = camera.ProjectionMatrix(16f / 9);
        var window = camera.View * projection;
        var draws = new ModelDrawList();
        draws.NoteEye(window, new ViewEye(camera.Position, camera.TurnedView * projection));
        var origin = RenderOrigin.Near(camera.Position);
        var back = Matrix4x4.CreateTranslation(0.001f, -0.002f, 0);
        var memo = new ViewMemo();
        memo.Use(origin, draws);
        for (int batch = 0; batch < 660; batch++)
        {
            memo.Placed(window).Should().Be(origin.Seen(window, draws.EyeOf(window)));
            memo.Push(window, back).ViewProjection.Should().Be(origin.Push(window, draws.EyeOf(window), back).ViewProjection);
        }
        // A batch's view found by comparing it with the last one asked for, where a key of a
        // view-projection hashed each time cost every batch of the pass.
        memo.Lookups.Should().Be(2, "the view and its push are each looked for once, the first time");

        // A pass through another view after it, and the window's again, look for each once more.
        var light = Matrix4x4.CreateOrthographic(40, 40, 0, 100);
        for (int batch = 0; batch < 660; batch++) memo.Placed(light);
        for (int batch = 0; batch < 660; batch++) memo.Placed(window);
        memo.Lookups.Should().Be(4);
        memo.Placed(light).Should().Be(origin.Taking(light), "a view no eye was noted with is taken against the origin as it is");

        memo.Use(origin with { X = origin.X + 16 }, draws);
        memo.Lookups.Should().Be(0, "views kept against one origin are forgotten for another");
        memo.Placed(window).Should().Be((origin with { X = origin.X + 16 }).Seen(window, draws.EyeOf(window)));
    }
}
