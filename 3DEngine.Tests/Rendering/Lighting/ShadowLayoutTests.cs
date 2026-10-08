using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering.Lighting;

/// <summary>Where the spot and point lights' shadows are drawn, by rank, and which lights the view sees.</summary>
[Trait("Category", "Unit")]
public class ShadowLayoutTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(ShadowFit.MaxSpotLights)]
    public void Every_Spot_Light_Has_A_Square_Of_The_Spot_Tile_To_Itself(int count)
    {
        var tile = ShadowFit.TileSize;
        var (x0, y0) = ShadowFit.TileOrigin(ShadowFit.SpotTile, tile);
        var squares = Enumerable.Range(0, count).Select(s => ShadowFit.SpotTileArea(s, count, tile)).ToList();

        foreach (var (x, y, size) in squares)
        {
            x.Should().BeGreaterThanOrEqualTo(x0);
            y.Should().BeGreaterThanOrEqualTo(y0);
            (x + size).Should().BeLessThanOrEqualTo(x0 + tile);
            (y + size).Should().BeLessThanOrEqualTo(y0 + tile);
        }
        for (int a = 0; a < count; a++)
            for (int b = a + 1; b < count; b++)
                Overlap(squares[a], squares[b]).Should().BeFalse($"slots {a} and {b} are apart");
        if (count > 4) squares[0].Size.Should().BeGreaterThan(squares[^1].Size, "the light that matters most has more texels");
    }

    [Fact]
    public void Every_Point_Light_Face_Has_A_Square_Of_A_Layer_To_Itself()
    {
        var size = ShadowFit.PointFaceSize;
        var faces = new List<(int Layer, int X, int Y, int Size)>();
        for (int slot = 0; slot < ShadowFit.MaxPointLights; slot++)
            for (int face = 0; face < 6; face++)
                faces.Add(ShadowFit.PointFaceArea(slot, face, size));

        faces.Should().OnlyContain(f => f.Layer >= 0 && f.Layer < ShadowFit.PointLayers && f.X + f.Size <= size && f.Y + f.Size <= size);
        faces.Take(ShadowFit.FullPointLights * 6).Should().OnlyContain(f => f.Size == size, "the first lights' faces fill a layer");
        faces.Skip(ShadowFit.FullPointLights * 6).Should().OnlyContain(f => f.Size == size / 2, "the rest share layers four to one");
        for (int a = 0; a < faces.Count; a++)
            for (int b = a + 1; b < faces.Count; b++)
                (faces[a].Layer == faces[b].Layer && Overlap((faces[a].X, faces[a].Y, faces[a].Size), (faces[b].X, faces[b].Y, faces[b].Size)))
                    .Should().BeFalse($"faces {a} and {b} are apart");
    }

    // A spot light with a reach of 2 at a place, of a brightness.
    private static RenderLight Spot(Vector3 at, float brightness) =>
        new() { Kind = LightKind.Spot, Position = at, EmittedColor = new Vector3(brightness), Range = 2, CastsShadows = true };

    [Fact]
    public void Shadowed_Lights_Are_Ranked_By_The_Light_That_Reaches_The_Eye()
    {
        // A candle the eye stands in, a lamp of forty times its light whose reach is 3 units off, and
        // the same lamp twice as far.
        var lights = new List<RenderLight> { Spot(new Vector3(0, 0, -1), 0.5f), Spot(new Vector3(0, 0, -5), 20), Spot(new Vector3(0, 0, -10), 20) };

        LightingUboPrepare.Rank(lights, [0, 1, 2], Vector3.Zero, null, 150).Should().Equal([1, 0, 2],
            "20 over 1 + 3 squared beats the candle's 0.5, which beats 20 over 1 + 8 squared");
        LightingUboPrepare.Rank([Spot(new Vector3(0, 0, -5), 1), Spot(new Vector3(0, 0, -3), 1)], [0, 1], Vector3.Zero, null, 150)
            .Should().Equal([1, 0], "of two alike, the one whose reach comes nearer reaches the eye the brighter");

        var camera = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(1, 1, 0.1f, 100);
        LightingUboPrepare.Rank([Spot(new Vector3(0, 0, 10), 100), Spot(new Vector3(0, 0, -10), 1)], [0, 1], Vector3.Zero, camera, 150)
            .Should().Equal([1, 0], "a light the camera sees comes before a brighter one behind it");
    }

    [Fact]
    public void Shadowed_Lights_Are_Ranked_For_Every_View_The_Frame_Draws_Meshes_Through()
    {
        // The window looks down -Z from the origin and a render target's camera down +X from a
        // hundred units off. A bright light behind the window and a dim one the target sees.
        Matrix4x4 Looking(Vector3 from, Vector3 to) =>
            Matrix4x4.CreateLookAt(from, to, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(1, 1, 0.1f, 100);
        var window = Looking(Vector3.Zero, -Vector3.UnitZ);
        var target = Looking(new Vector3(100, 0, 0), new Vector3(200, 0, 0));
        RenderLight[] lights = [Spot(new Vector3(0, 0, 10), 100), Spot(new Vector3(110, 0, 0), 1)];

        LightingUboPrepare.Rank(lights, [0, 1], Vector3.Zero, window, 150).Should().Equal([0, 1], "the window alone sees neither, and the first is the brighter");
        LightingUboPrepare.Rank(lights, [0, 1], [(Vector3.Zero, window), (new Vector3(100, 0, 0), target)], 150).Should().Equal([1, 0],
            "the light the target's camera sees comes before one no camera sees");
    }

    [Fact]
    public void The_Light_Lighting_Most_Of_The_Picture_Keeps_Its_Shadows_Past_The_Limit()
    {
        // Thirteen shadowed point lights, one past the twelve that cast shadows: twelve bright ones
        // with a reach of 1 in a corner of the view, and a dimmer lamp whose reach of 10 lights a
        // wall across it. The corner's lights reach the eye some five times as bright, and cover a
        // tenth of the picture between them where the lamp covers it whole.
        var camera = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(1, 1, 0.1f, 100);
        RenderLight Point(Vector3 at, float brightness, float range) =>
            new() { Kind = LightKind.Point, Position = at, EmittedColor = new Vector3(brightness), Range = range, CastsShadows = true };
        var lights = Enumerable.Range(0, 12).Select(i => Point(new Vector3(-2.5f + i % 3 * 0.2f, 2 + i / 3 * 0.2f, -6), 50, 1)).ToList();
        lights.Add(Point(new Vector3(0, 0, -20), 30, 10));

        var kept = LightingUboPrepare.Rank(lights, Enumerable.Range(0, 13), Vector3.Zero, camera, 150).Take(ShadowFit.MaxPointLights).ToList();
        kept.Should().Contain(12, "the lamp lighting the wall across the view weighs more than a bright light in its corner");
        kept[0].Should().Be(12, "and it lights most of the picture, so it has the most texels");
    }

    [Fact]
    public void A_Lights_Share_Of_The_Picture_Is_What_Its_Reach_Covers()
    {
        var camera = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(1, 1, 0.1f, 100);
        LightingUboPrepare.Share(camera, new Vector3(0, 0, -20), 10).Should().Be(1, "a reach wider than the view at its distance covers all of it");
        LightingUboPrepare.Share(camera, new Vector3(0, 0, -1), 3).Should().Be(1, "a reach round the eye covers all of it");
        LightingUboPrepare.Share(camera, new Vector3(0, 0, -50), 1).Should().BeApproximately(0.0014f, 0.0005f,
            "a reach of 1 fifty units off covers about a fiftieth of the view's width and height");
        LightingUboPrepare.Share(camera, new Vector3(100, 0, -10), 1).Should().Be(0, "a reach off to the side covers none of it");
    }

    [Fact]
    public void A_Light_Behind_The_Camera_Is_Out_Of_View()
    {
        var camera = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY)
                     * Matrix4x4.CreatePerspectiveFieldOfView(1, 1, 0.1f, 100);

        LightingUboPrepare.InView(camera, new Vector3(0, 0, -10), 1).Should().BeTrue("ahead of the camera");
        LightingUboPrepare.InView(camera, new Vector3(0, 0, 10), 1).Should().BeFalse("behind it");
        LightingUboPrepare.InView(camera, new Vector3(0, 0, 2), 3).Should().BeTrue("behind it, but its reach comes round");
    }

    private static bool Overlap((int X, int Y, int Size) a, (int X, int Y, int Size) b) =>
        a.X < b.X + b.Size && b.X < a.X + a.Size && a.Y < b.Y + b.Size && b.Y < a.Y + a.Size;
}
