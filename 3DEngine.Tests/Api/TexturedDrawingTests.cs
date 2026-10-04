using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Nine-patches, billboards and turned text, read back as the vertices they record into the draw
/// list, in an app whose stores queue without a GPU.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class TexturedDrawingTests : IDisposable
{
    private readonly App _app = new();
    private readonly Texture2D _texture = new(1, 30, 30);

    public TexturedDrawingTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<DrawList>();
        UseApp(_app);
    }

    public void Dispose() => UseApp(null);

    private ReadOnlySpan<ImmediateVertex> Vertices => _app.World.Resource<DrawList>().Vertices;

    [Fact]
    public void A_Nine_Patch_Keeps_Its_Corners_And_Stretches_The_Rest()
    {
        var info = new NPatchInfo(new Rectangle(0, 0, 30, 30), 10, 10, 10, 10);
        DrawTextureNPatch(_texture, info, new Rectangle(100, 50, 200, 80), Vector2.Zero, 0, Color.White);

        var vertices = Vertices.ToArray();
        vertices.Should().HaveCount(9 * 6, "nine patches of two triangles");
        var xs = vertices.Select(v => v.Position.X).Distinct().Order().ToArray();
        var ys = vertices.Select(v => v.Position.Y).Distinct().Order().ToArray();
        xs.Should().Equal(100, 110, 290, 300);
        ys.Should().Equal(50, 60, 120, 130);
        // The top left corner shows the texture's top left ten pixels at their size.
        vertices.Should().Contain(v => v.Position == new Vector3(110, 60, 0) && v.Uv == new Vector2(1 / 3f, 1 / 3f));
    }

    [Fact]
    public void A_Nine_Patch_Narrower_Than_Its_Borders_Leaves_Out_The_Middle()
    {
        var info = new NPatchInfo(new Rectangle(0, 0, 30, 30), 10, 10, 10, 10);
        DrawTextureNPatch(_texture, info, new Rectangle(0, 0, 10, 100), Vector2.Zero, 0, Color.White);
        Vertices.Length.Should().Be(6 * 6, "the middle column goes, its borders shrunk to five pixels each");
        Vertices.ToArray().Max(v => v.Position.X).Should().Be(10);

        _app.World.Resource<DrawList>().Clear();
        DrawTextureNPatch(_texture, info with { Layout = NPatchLayout.ThreePatchHorizontal }, new Rectangle(0, 0, 100, 999), Vector2.Zero, 0, Color.White);
        Vertices.Length.Should().Be(3 * 6);
        Vertices.ToArray().Max(v => v.Position.Y).Should().Be(30, "three patches across keep the source's height");
    }

    [Fact]
    public void A_Billboard_Stands_Upright_Facing_The_Camera_Around_Its_Origin()
    {
        // The camera looks down -z, so its right is +x.
        var camera = new Camera3D(new Vector3(0, 5, 10), new Vector3(0, 5, 0), Vector3.UnitY);
        DrawBillboardRec(camera, _texture, new Rectangle(0, 0, 30, 30), new Vector3(1, 2, 3), new Vector2(2, 4), Color.White);

        var corners = Vertices.ToArray().Select(v => v.Position).Distinct().ToArray();
        corners.Should().HaveCount(4);
        corners.Min(p => p.X).Should().BeApproximately(0, 1e-5f);
        corners.Max(p => p.X).Should().BeApproximately(2, 1e-5f);
        corners.Min(p => p.Y).Should().BeApproximately(0, 1e-5f);
        corners.Max(p => p.Y).Should().BeApproximately(4, 1e-5f);
        corners.Should().OnlyContain(p => MathF.Abs(p.Z - 3) < 1e-5f);
        // The texture's top is up.
        Vertices.ToArray().Should().Contain(v => v.Uv == Vector2.Zero && v.Position.Y > 3.9f);

        _app.World.Resource<DrawList>().Clear();
        DrawBillboardPro(camera, _texture, new Rectangle(0, 0, 30, 30), Vector3.Zero, Vector3.UnitY, new Vector2(2, 2), Vector2.Zero, 90, Color.White);
        var turned = Vertices.ToArray().Select(v => v.Position).Distinct().ToArray();
        turned.Max(p => p.X).Should().BeApproximately(0, 1e-5f, "a quarter turn counterclockwise swings the right side up and over to the left");
        turned.Min(p => p.X).Should().BeApproximately(-2, 1e-5f);
    }

    [Fact]
    public void Text_Turned_A_Quarter_Runs_Down_The_Screen()
    {
        var font = GetFontDefault();
        DrawTextPro(font, "Hi", new Vector2(100, 100), Vector2.Zero, 90, 20, 1, Color.White);
        var flat = Vertices.ToArray();
        _app.World.Resource<DrawList>().Clear();
        DrawTextEx(font, "Hi", new Vector2(100, 100), 20, 1, Color.White);
        var straight = Vertices.ToArray();

        flat.Should().HaveCount(straight.Length);
        var height = straight.Max(v => v.Position.X) - straight.Min(v => v.Position.X);
        (flat.Max(v => v.Position.Y) - flat.Min(v => v.Position.Y)).Should().BeApproximately(height, 1e-3f, "its width runs down");
        flat.Should().OnlyContain(v => v.Position.X <= 100.001f, "a quarter turn clockwise on a screen whose y runs down puts it left of where it starts");
    }
}
