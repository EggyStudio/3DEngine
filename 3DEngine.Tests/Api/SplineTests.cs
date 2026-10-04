using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Splines, as the points along their curves and as the vertices a thick one records into the draw
/// list.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class SplineTests : IDisposable
{
    private readonly App _app = new();

    public SplineTests()
    {
        _app.World.InitResource<DrawList>();
        UseApp(_app);
    }

    public void Dispose() => UseApp(null);

    private Vector2[] Positions => [.. _app.World.Resource<DrawList>().Vertices.ToArray().Select(v => new Vector2(v.Position.X, v.Position.Y))];

    [Fact]
    public void Each_Curve_Starts_And_Ends_Where_Its_Kind_Says()
    {
        Vector2 p1 = new(0, 0), p2 = new(10, 20), p3 = new(30, 20), p4 = new(40, 0);

        GetSplinePointLinear(p1, p4, 0.25f).Should().Be(new Vector2(10, 0));
        GetSplinePointBezierQuad(p1, p2, p4, 0).Should().Be(p1);
        GetSplinePointBezierQuad(p1, p2, p4, 1).Should().Be(p4);
        GetSplinePointBezierQuad(p1, p2, p4, 0.5f).Should().Be(new Vector2(15, 10), "halfway is a quarter of each end and half the control");
        GetSplinePointBezierCubic(p1, p2, p3, p4, 0).Should().Be(p1);
        GetSplinePointBezierCubic(p1, p2, p3, p4, 1).Should().Be(p4);
        GetSplinePointCatmullRom(p1, p2, p3, p4, 0).Should().Be(p2, "a Catmull-Rom segment runs between its middle points");
        GetSplinePointCatmullRom(p1, p2, p3, p4, 1).Should().Be(p3);
        GetSplinePointBasis(p1, p2, p3, p4, 0).Should().Be((p1 + 4 * p2 + p3) / 6, "a B-spline passes near its points rather than through them");
    }

    [Fact]
    public void A_Thick_Path_Closes_Its_Corner_At_The_Miter()
    {
        DrawSplineLinear([new(0, 0), new(10, 0), new(10, 10)], 2, Color.White);

        var positions = Positions;
        Nearest(positions, new Vector2(11, -1)).Should().BeLessThan(1e-4f, "the outer edges of both sides meet at the corner's outside");
        Nearest(positions, new Vector2(9, 1)).Should().BeLessThan(1e-4f);
        Nearest(positions, new Vector2(10, -1)).Should().BeGreaterThan(0.5f, "a side ending square at the corner would leave a notch");
    }

    private static float Nearest(Vector2[] positions, Vector2 point) => positions.Min(p => Vector2.Distance(p, point));

    [Fact]
    public void A_Path_Turning_Back_Keeps_Its_Corner_Within_A_Width()
    {
        Vector2[] points = [new(0, 0), new(100, 0), new(0, 1)];
        DrawSplineLinear(points, 4, Color.White);

        Positions.Max(p => points.Min(q => Vector2.Distance(p, q))).Should().BeLessThanOrEqualTo(4.001f);
    }

    [Fact]
    public void A_Curve_Short_Of_Its_Points_Draws_Nothing()
    {
        DrawSplineCatmullRom([new(0, 0), new(1, 1), new(2, 0)], 2, Color.White);
        DrawSplineBezierCubic([new(0, 0), new(1, 1), new(2, 0)], 2, Color.White);
        DrawSplineLinear([new(0, 0)], 2, Color.White);

        _app.World.Resource<DrawList>().Vertices.Length.Should().Be(0);
    }

    [Fact]
    public void Joined_Bezier_Curves_Run_Through_Each_End()
    {
        DrawSplineBezierQuadratic([new(0, 0), new(10, 10), new(20, 0), new(30, -10), new(40, 0)], 2, Color.White);

        var positions = Positions;
        foreach (var end in new Vector2[] { new(0, 0), new(20, 0), new(40, 0) })
            Nearest(positions, end).Should().BeApproximately(1, 0.01f, "the path's edges sit half its width from each end");
    }
}
