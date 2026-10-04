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
