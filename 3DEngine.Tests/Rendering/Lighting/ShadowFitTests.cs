using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Rendering.Lighting;

public sealed class ShadowFitTests
{
    private static Matrix4x4 Camera(Vector3 eye, Vector3 target)
    {
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(60), 16f / 9f, 0.1f, 1000f);
        projection.M22 = -projection.M22;
        return Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * projection;
    }

    private static Vector3 Project(Vector3 point, Matrix4x4 viewProjection)
    {
        var clip = Vector4.Transform(new Vector4(point, 1), viewProjection);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

    [Fact]
    public void A_Shorter_Distance_Splits_In_Proportion_And_Spends_The_Map_On_Less_Ground()
    {
        ShadowFit.SplitsFor(60).Should().Equal(4.8f, 18f, 60f);
        ShadowFit.SplitsFor(ShadowFit.Distance).Should().Equal(ShadowFit.Splits.ToArray());

        var camera = Camera(new Vector3(3, 5, 12), Vector3.Zero);
        var sun = Vector3.Normalize(new Vector3(0.3f, -1, -0.5f));
        var near = ShadowFit.FitCascades(camera, sun, 60);
        var far = ShadowFit.FitCascades(camera, sun);
        near.Should().HaveCount(3);
        near[^1].Texel.Should().BeLessThan(far[^1].Texel * 0.6f, "the last cascade covers less than half the ground, in finer texels");
    }

    [Fact]
    public void What_The_Camera_Sees_Within_The_Distance_Lands_Inside_The_Map()
    {
        var camera = Camera(new Vector3(3, 5, 12), Vector3.Zero);
        var sun = Vector3.Normalize(new Vector3(0.3f, -1, -0.5f));
        ShadowFit.TryFit(camera, sun, 0, ShadowFit.Distance, out var shadow, out var texel).Should().BeTrue();

        // The view's middle and the far corners of the part of it the map covers.
        Matrix4x4.Invert(camera, out var inverse);
        var eye = new Vector3(3, 5, 12);
        foreach (var (x, y) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f), (0f, 0f) })
        {
            var far = Vector4.Transform(new Vector4(x, y, 1, 1), inverse);
            var way = Vector3.Normalize(new Vector3(far.X, far.Y, far.Z) / far.W - eye);
            var point = Project(eye + way * (ShadowFit.Distance * 0.95f), shadow);
            point.X.Should().BeInRange(-1, 1);
            point.Y.Should().BeInRange(-1, 1);
            point.Z.Should().BeInRange(0, 1);
        }
        texel.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_Caster_Above_The_View_Toward_The_Light_Is_Still_Inside_The_Map()
    {
        var camera = Camera(new Vector3(0, 2, 6), Vector3.Zero);
        ShadowFit.TryFit(camera, -Vector3.UnitY, 0, ShadowFit.Splits[0], out var shadow, out _).Should().BeTrue();

        Project(new Vector3(0, 30, 0), shadow).Z.Should().BeInRange(0, 1, "a roof 30 units up still shadows the floor");
        Project(new Vector3(0, 30, 0), shadow).Z.Should().BeLessThan(Project(Vector3.Zero, shadow).Z, "what is nearer the light is nearer in the map");
    }

    [Fact]
    public void The_Map_Moves_With_The_Camera_In_Whole_Texels()
    {
        var sun = Vector3.Normalize(new Vector3(0.3f, -1, -0.5f));
        ShadowFit.TryFit(Camera(new Vector3(0, 4, 10), Vector3.Zero), sun, 0, ShadowFit.Splits[0], out var before, out _).Should().BeTrue();
        var start = Project(Vector3.Zero, before);

        // Steps of a few hundredths of a unit across the light, which a map following the camera
        // exactly would show as fractions of a texel.
        var across = Vector3.Normalize(Vector3.Cross(sun, Vector3.UnitY)) * 0.037f;
        for (int i = 1; i <= 10; i++)
        {
            var offset = across * i;
            ShadowFit.TryFit(Camera(new Vector3(0, 4, 10) + offset, offset), sun, 0, ShadowFit.Splits[0], out var after, out _).Should().BeTrue();
            var moved = (Project(Vector3.Zero, after) - start) * (ShadowFit.TileSize / 2f);
            moved.X.Should().BeApproximately(MathF.Round(moved.X), 0.05f, "the same world point lands on the same place in a texel");
            moved.Y.Should().BeApproximately(MathF.Round(moved.Y), 0.05f, "the same world point lands on the same place in a texel");
        }
    }

    [Theory]
    [InlineData(4096f)]
    [InlineData(1140736f)]
    public void Far_From_The_Worlds_Origin_The_Map_Moves_With_The_Camera_In_Whole_Texels_Across_The_Origins_Steps(float far)
    {
        // A camera far out stepping across the light by three eighths of a unit, a float's step
        // there being an eighth, its view pushed from its eye against the render origin the renderer
        // stands at it, which steps by 16 units as the eye passes 8 on the way, and the cascades
        // fitted against that origin. Each cascade's texel is the one the same camera has at the
        // world's origin, and a world point by the camera lands on the same place in a texel of the
        // first at every step, as at the world's origin.
        var sun = Vector3.Normalize(new Vector3(0.3f, -1, -0.5f));
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(60), 16f / 9f, 0.1f, 1000f);
        projection.M22 = -projection.M22;
        var at = new Vector3(far, 0, far / 4);
        float? start = null, startY = null;
        var origins = new HashSet<RenderOrigin>();
        for (int i = 0; i <= 12; i++)
        {
            var eye = at + new Vector3(6 + 0.375f * i, 4, 10);
            var target = at + new Vector3(0.375f * i, 0, 0);
            var origin = RenderOrigin.Near(eye);
            origins.Add(origin);
            var seen = origin.Seen(Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * projection,
                new ViewEye(eye, Matrix4x4.CreateLookAt(Vector3.Zero, target - eye, Vector3.UnitY) * projection));
            for (int c = 0; c < ShadowFit.Splits.Length; c++)
            {
                var (from, to) = (c == 0 ? 0 : ShadowFit.Splits[c - 1], ShadowFit.Splits[c]);
                var home = Matrix4x4.CreateLookAt(eye - at, target - at, Vector3.UnitY) * projection;
                ShadowFit.TryFit([home], sun, from, to, out _, out var homeTexel).Should().BeTrue();
                ShadowFit.TryFit([seen], sun, from, to, out _, out var texel, origin: origin).Should().BeTrue();
                texel.Should().Be(homeTexel, $"cascade {c}'s texel at step {i} is the one the camera has at the world's origin");
            }
            ShadowFit.TryFit([seen], sun, 0, ShadowFit.Splits[0], out var cascade, out _, origin: origin).Should().BeTrue();
            var landed = Project(origin.Against(at), cascade) * (ShadowFit.TileSize / 2f);
            (start, startY) = (start ?? landed.X, startY ?? landed.Y);
            (landed.X - start.Value).Should().BeApproximately(MathF.Round(landed.X - start.Value), 0.05f, $"the point lands on the same place in a texel at step {i}");
            (landed.Y - startY.Value).Should().BeApproximately(MathF.Round(landed.Y - startY.Value), 0.05f, $"the point lands on the same place in a texel at step {i}");
        }
        origins.Should().HaveCount(2, "the camera's path crosses one of the origin's steps");
    }

    [Fact]
    public void A_Camera_That_Cannot_Be_Inverted_Fits_No_Map()
    {
        ShadowFit.TryFit(default(Matrix4x4), -Vector3.UnitY, 0, 10, out _, out _).Should().BeFalse();
        ShadowFit.FitCascades(default(Matrix4x4), -Vector3.UnitY).Should().BeEmpty();
    }

    [Fact]
    public void Each_Cascade_Covers_Its_Slice_Of_The_View_With_Coarser_Texels_Further_Out()
    {
        var eye = new Vector3(0, 3, 0);
        var camera = Camera(eye, new Vector3(0, 3, -1));
        var cascades = ShadowFit.FitCascades(camera, Vector3.Normalize(new Vector3(0.3f, -1, -0.5f)));

        cascades.Should().HaveCount(ShadowFit.Splits.Length);
        float from = 0;
        for (int i = 0; i < cascades.Length; i++)
        {
            // A point straight ahead in the middle of the slice lands inside its cascade.
            var middle = eye + new Vector3(0, 0, -(from + ShadowFit.Splits[i]) / 2);
            var p = Project(middle, cascades[i].ViewProjection);
            p.X.Should().BeInRange(-1, 1);
            p.Y.Should().BeInRange(-1, 1);
            p.Z.Should().BeInRange(0, 1);
            if (i > 0) cascades[i].Texel.Should().BeGreaterThan(cascades[i - 1].Texel);
            from = ShadowFit.Splits[i];
        }

        // Past forty units, where the single map stopped, the last cascade still covers the view.
        Project(eye + new Vector3(0, 0, -100), cascades[^1].ViewProjection).X.Should().BeInRange(-1, 1);
    }

    [Fact]
    public void The_Fill_Covers_The_View_From_The_Last_Cascade_To_The_Far_Plane_In_Coarser_Texels()
    {
        var eye = new Vector3(0, 3, 0);
        var camera = Camera(eye, new Vector3(0, 3, -1));
        var sun = Vector3.Normalize(new Vector3(0.3f, -1, -0.5f));
        var cascades = ShadowFit.FitCascades(camera, sun);
        ShadowFit.TryFitFill([camera], sun, ShadowFit.Distance, out var fill, out var texel).Should().BeTrue();

        // Straight ahead and at the far plane's corners, out to the far plane at a thousand.
        Matrix4x4.Invert(camera, out var inverse);
        foreach (var (x, y) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f), (0f, 0f) })
        {
            var far = Vector4.Transform(new Vector4(x, y, 1, 1), inverse);
            var way = Vector3.Normalize(new Vector3(far.X, far.Y, far.Z) / far.W - eye);
            foreach (var distance in new[] { 160f, 500f, 990f })
            {
                var p = Project(eye + way * distance, fill);
                p.X.Should().BeInRange(-1, 1, $"the fill holds the view {distance} units out");
                p.Y.Should().BeInRange(-1, 1, $"the fill holds the view {distance} units out");
                p.Z.Should().BeInRange(0, 1, $"the fill holds the view {distance} units out");
            }
        }
        texel.Should().BeGreaterThan(cascades[^1].Texel, "the fill spreads one tile over the rest of the view");
    }

    [Fact]
    public void A_Far_Plane_Before_The_Shadow_Distance_Or_None_At_All_Fits_No_Fill()
    {
        var sun = Vector3.Normalize(new Vector3(0.3f, -1, -0.5f));
        var view = Matrix4x4.CreateLookAt(new Vector3(0, 3, 0), new Vector3(0, 3, -1), Vector3.UnitY);
        // Its far corners lie 124 units off, short of the 150 the cascades reach.
        var near = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(60), 16f / 9f, 0.1f, 80f);
        ShadowFit.TryFitFill([view * near], sun, ShadowFit.Distance, out _, out _).Should().BeFalse("the view ends before the cascades do");
        var endless = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(60), 16f / 9f, 0.1f, float.PositiveInfinity);
        ShadowFit.TryFitFill([view * endless], sun, ShadowFit.Distance, out _, out _).Should().BeFalse("a view with no far plane has no end to fit to");
        ShadowFit.TryFitFill([default(Matrix4x4)], sun, ShadowFit.Distance, out _, out _).Should().BeFalse();
    }
}
