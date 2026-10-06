using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// The thick outlines of the shape functions, which raylib draws in pieces that meet without
/// overlapping, so a color that is partly clear shows one shade throughout, inside the shape's edge
/// for a width above 0 and outside it for one below. Read back from the draw list of an app whose
/// stores queue without a GPU, and sampled on a grid finer than a pixel.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class OutlineTests : IDisposable
{
    private const float Step = 0.25f;
    private const float Margin = 1e-3f;
    private static readonly Rectangle Area = new(0, 0, 160, 140);
    private static readonly Color Half = new(255, 0, 0, 128);
    private readonly App _app = new();

    public OutlineTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<DrawList>();
        UseApp(_app);
        ResetRlgl();
    }

    public void Dispose()
    {
        ForgetRlgl();
        UseApp(null);
    }

    // How many of the triangles a draw records cover each point of a grid a quarter of a pixel
    // apart over the area. A point is counted where it lies more than the margin inside a
    // triangle's edges, so at a thousandth of a pixel two triangles that share an edge do not both
    // count a point on it, and at less than nothing a shape counts the points of its own edges.
    private int[,] Coverage(Action draw, float margin = Margin)
    {
        var list = _app.World.Resource<DrawList>();
        list.Clear();
        draw();
        var vertices = list.Vertices.ToArray();
        var indices = list.Indices.ToArray();
        int columns = (int)(Area.Width / Step), rows = (int)(Area.Height / Step);
        var count = new int[columns, rows];
        foreach (var batch in list.Batches)
        {
            if (batch.Topology != PrimitiveTopology.TriangleList) continue;
            for (int i = batch.FirstIndex; i < batch.FirstIndex + batch.IndexCount; i += 3)
            {
                Vector2 a = Flat(vertices[indices[i]].Position), b = Flat(vertices[indices[i + 1]].Position), c = Flat(vertices[indices[i + 2]].Position);
                var turn = Cross(b - a, c - a);
                if (MathF.Abs(turn) < 1e-6f) continue;
                var low = Vector2.Min(a, Vector2.Min(b, c));
                var high = Vector2.Max(a, Vector2.Max(b, c));
                for (int x = Math.Max(0, (int)(low.X / Step) - 1); x < Math.Min(columns, (int)(high.X / Step) + 2); x++)
                    for (int y = Math.Max(0, (int)(low.Y / Step) - 1); y < Math.Min(rows, (int)(high.Y / Step) + 2); y++)
                        if (Within(Sample(x, y), a, b, c, MathF.Sign(turn), margin)) count[x, y]++;
            }
        }
        return count;
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Y);

    // A point of the grid, off the whole and half pixels a straight edge is most often drawn on.
    private static Vector2 Sample(int x, int y) => new(Area.X + (x + 0.37f) * Step, Area.Y + (y + 0.37f) * Step);

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static bool Within(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float turn, float margin) =>
        Inside(p, a, b, turn, margin) && Inside(p, b, c, turn, margin) && Inside(p, c, a, turn, margin);

    private static bool Inside(Vector2 p, Vector2 from, Vector2 to, float turn, float margin) =>
        turn * Cross(to - from, p - from) / Vector2.Distance(from, to) > margin;

    private static IEnumerable<(int X, int Y)> Points(int[,] coverage)
    {
        for (int x = 0; x < coverage.GetLength(0); x++)
            for (int y = 0; y < coverage.GetLength(1); y++)
                yield return (x, y);
    }

    // The first few points of a list where they are, so a failure says where to look.
    private static List<Vector2> Named(IEnumerable<(int X, int Y)> points) => points.Take(5).Select(p => Sample(p.X, p.Y)).ToList();

    private static bool Covered(int[,] coverage, Vector2 point) =>
        coverage[(int)((point.X - Area.X) / Step), (int)((point.Y - Area.Y) / Step)] > 0;

    private static Vector2 At(Vector2 center, float radius, float degrees) =>
        center + new Vector2(MathF.Cos(float.DegreesToRadians(degrees)), MathF.Sin(float.DegreesToRadians(degrees))) * radius;

    private static readonly Vector2 Middle = new(80, 70);

    // Each outline, its width above 0 and below, beside the filled shape it outlines.
    public static TheoryData<string> Outlines() =>
    [
        "triangle", "triangle outside", "rectangle", "rectangle outside", "rounded", "rounded outside", "rounded past its corners",
        "polygon", "polygon outside", "circle", "circle outside", "ellipse", "ellipse outside",
        "sector", "sector of a quarter", "sector of a whole turn", "sector narrow", "sector outside", "sector outside past half",
        "ring", "ring with crossing ends", "ring outside", "ring outside with no hole",
    ];

    private static readonly Dictionary<string, (Action Outline, Action Filled)> Shapes = new()
    {
        ["triangle"] = (() => DrawTriangleLinesEx(new(80, 20), new(30, 110), new(130, 110), 6, Half), () => DrawTriangle(new(80, 20), new(30, 110), new(130, 110), Half)),
        ["triangle outside"] = (() => DrawTriangleLinesEx(new(80, 30), new(40, 100), new(120, 100), -6, Half), () => DrawTriangle(new(80, 30), new(40, 100), new(120, 100), Half)),
        ["rectangle"] = (() => DrawRectangleLinesEx(new Rectangle(30, 30, 100, 70), 6, Half), () => DrawRectangleRec(new Rectangle(30, 30, 100, 70), Half)),
        ["rectangle outside"] = (() => DrawRectangleLinesEx(new Rectangle(30, 30, 100, 70), -6, Half), () => DrawRectangleRec(new Rectangle(30, 30, 100, 70), Half)),
        ["rounded"] = (() => DrawRectangleRoundedLinesEx(new Rectangle(30, 30, 100, 70), 0.5f, 6, 5, Half), () => DrawRectangleRounded(new Rectangle(30, 30, 100, 70), 0.5f, 6, Half)),
        ["rounded outside"] = (() => DrawRectangleRoundedLinesEx(new Rectangle(30, 30, 100, 70), 0.5f, 6, -5, Half), () => DrawRectangleRounded(new Rectangle(30, 30, 100, 70), 0.5f, 6, Half)),
        ["rounded past its corners"] = (() => DrawRectangleRoundedLinesEx(new Rectangle(30, 30, 100, 70), 0.2f, 0, 12, Half), () => DrawRectangleRounded(new Rectangle(30, 30, 100, 70), 0.2f, 0, Half)),
        ["polygon"] = (() => DrawPolyLinesEx(Middle, 6, 50, 15, 6, Half), () => DrawPoly(Middle, 6, 50, 15, Half)),
        ["polygon outside"] = (() => DrawPolyLinesEx(Middle, 5, 45, 15, -6, Half), () => DrawPoly(Middle, 5, 45, 15, Half)),
        ["circle"] = (() => DrawCircleLinesEx(Middle, 50, 6, Half), () => DrawCircleV(Middle, 50, Half)),
        ["circle outside"] = (() => DrawCircleLinesEx(Middle, 45, -6, Half), () => DrawCircleV(Middle, 45, Half)),
        ["ellipse"] = (() => DrawEllipseLinesEx(Middle, 60, 40, 6, Half), () => DrawEllipseV(Middle, 60, 40, Half)),
        ["ellipse outside"] = (() => DrawEllipseLinesEx(Middle, 60, 40, -6, Half), () => DrawEllipseV(Middle, 60, 40, Half)),
        ["sector"] = (() => DrawCircleSectorLinesEx(Middle, 50, 20, 270, 36, 4, Half), () => DrawCircleSector(Middle, 50, 20, 270, 36, Half)),
        ["sector of a quarter"] = (() => DrawCircleSectorLinesEx(Middle, 50, 0, 90, 8, 8, Half), () => DrawCircleSector(Middle, 50, 0, 90, 8, Half)),
        ["sector of a whole turn"] = (() => DrawCircleSectorLinesEx(Middle, 50, 30, 390, 36, 5, Half), () => DrawCircleSector(Middle, 50, 30, 390, 36, Half)),
        ["sector narrow"] = (() => DrawCircleSectorLinesEx(Middle, 50, 0, 20, 4, 12, Half), () => DrawCircleSector(Middle, 50, 0, 20, 4, Half)),
        ["sector outside"] = (() => DrawCircleSectorLinesEx(Middle, 40, 30, 120, 12, -5, Half), () => DrawCircleSector(Middle, 40, 30, 120, 12, Half)),
        ["sector outside past half"] = (() => DrawCircleSectorLinesEx(Middle, 40, 0, 300, 30, -5, Half), () => DrawCircleSector(Middle, 40, 0, 300, 30, Half)),
        ["ring"] = (() => DrawRingLinesEx(Middle, 25, 50, 20, 270, 36, 4, Half), () => DrawRing(Middle, 25, 50, 20, 270, 36, Half)),
        ["ring with crossing ends"] = (() => DrawRingLinesEx(Middle, 20, 50, 0, 24, 6, 6, Half), () => DrawRing(Middle, 20, 50, 0, 24, 6, Half)),
        ["ring outside"] = (() => DrawRingLinesEx(Middle, 25, 45, 20, 270, 36, -4, Half), () => DrawRing(Middle, 25, 45, 20, 270, 36, Half)),
        ["ring outside with no hole"] = (() => DrawRingLinesEx(Middle, 0, 40, 30, 200, 20, -4, Half), () => DrawRing(Middle, 0, 40, 30, 200, 20, Half)),
    };

    [Theory]
    [MemberData(nameof(Outlines))]
    public void A_Thick_Outline_Covers_No_Point_Twice_And_Lies_On_Its_Side_Of_The_Edge(string shape)
    {
        var (outline, filled) = Shapes[shape];
        var outside = shape.Contains("outside");
        var drawn = Coverage(outline);
        // The shape with its edges where the outline is to lie within it, and without them where
        // it is to lie beyond, so a point on the edge between them is no fault either way.
        var inside = Coverage(filled, outside ? Margin : -Margin);
        var points = Points(drawn).ToList();

        points.Count(p => drawn[p.X, p.Y] > 0).Should().BeGreaterThan(1000, $"the {shape} outline covers its band");
        Named(points.Where(p => drawn[p.X, p.Y] > 1)).Should().BeEmpty($"no two pieces of the {shape} outline overlap, so a half clear color shows one shade");
        if (outside)
            Named(points.Where(p => drawn[p.X, p.Y] > 0 && inside[p.X, p.Y] > 0)).Should().BeEmpty($"a negative width puts the {shape} outline outside the shape");
        else
            Named(points.Where(p => drawn[p.X, p.Y] > 0 && inside[p.X, p.Y] == 0)).Should().BeEmpty($"the {shape} outline lies inside the shape");
    }

    [Fact]
    public void A_Sector_Outline_Is_Its_Arc_And_Both_Radii_Around_An_Empty_Middle()
    {
        var drawn = Coverage(() => DrawCircleSectorLinesEx(Middle, 50, 20, 270, 36, 4, Half));

        Covered(drawn, At(Middle, 48, 147)).Should().BeTrue("the arc's band is drawn");
        Covered(drawn, At(Middle, 25, 20) + At(Vector2.Zero, 2, 110)).Should().BeTrue("the first radius is drawn, inside the slice");
        Covered(drawn, At(Middle, 25, 270) + At(Vector2.Zero, 2, 180)).Should().BeTrue("the last radius is drawn, inside the slice");
        Covered(drawn, At(Middle, 25, 20) + At(Vector2.Zero, 2, -70)).Should().BeFalse("nothing is drawn outside the slice");
        Covered(drawn, At(Middle, 25, 145)).Should().BeFalse("the middle of the slice is left empty");
    }

    [Fact]
    public void A_Ring_Outline_Is_Both_Arcs_And_Both_Ends_Around_An_Empty_Middle()
    {
        var drawn = Coverage(() => DrawRingLinesEx(Middle, 25, 50, 20, 270, 36, 4, Half));

        Covered(drawn, At(Middle, 48, 147)).Should().BeTrue("the outer arc's band is drawn");
        Covered(drawn, At(Middle, 27, 147)).Should().BeTrue("the inner arc's band is drawn");
        Covered(drawn, At(Middle, 37, 21)).Should().BeTrue("the first end is drawn");
        Covered(drawn, At(Middle, 37, 269)).Should().BeTrue("the last end is drawn");
        Covered(drawn, At(Middle, 37, 145)).Should().BeFalse("the ring between its outlines is left empty");
    }

    [Fact]
    public void Circles_Take_Raylibs_Thirty_Six_Pieces_And_Arcs_Asked_For_Too_Few_Take_As_Many_As_Keep_Them_Smooth()
    {
        var list = _app.World.Resource<DrawList>();
        int Triangles(Action draw)
        {
            list.Clear();
            draw();
            return list.Batches.Where(b => b.Topology == PrimitiveTopology.TriangleList).Sum(b => b.IndexCount) / 3;
        }

        Triangles(() => DrawCircleV(Middle, 200, Color.Red)).Should().Be(36, "raylib draws every circle in one piece each ten degrees, however large");
        Triangles(() => DrawEllipseV(Middle, 200, 10, Color.Red)).Should().Be(36);
        Triangles(() => DrawCircleSector(Middle, 23, 0, 360, 0, Color.Red)).Should().Be(16,
            "an arc asked for fewer pieces than one a quarter turn takes as many as keep it within half a pixel, sixteen at a radius of 23");
        Triangles(() => DrawCircleSector(Middle, 23, 0, 270, 2, Color.Red)).Should().Be(12, "two pieces are fewer than the three quarter turns");
        Triangles(() => DrawCircleSector(Middle, 23, 0, 270, 5, Color.Red)).Should().Be(5, "five pieces are as many as asked");
    }
}
