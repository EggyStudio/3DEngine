using System.Buffers.Binary;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The circular occluders' table, the projection their parameters are taken from, and the flat call
/// that chooses them.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class CircularOccludersTests : IDisposable
{
    public void Dispose() => UseApp(null);

    [Fact]
    public void The_Occluder_Table_Is_The_One_Its_Rays_And_Circles_Give()
    {
        // CORTAO's table for sixteen rays as its README and its generator describe it, worked out
        // here: the rays a golden-angle spiral over the disc lifted onto the hemisphere, and for
        // each of 16 sizes of circle and each of 24 by 24 ways to it, read through a sphere map,
        // the rays whose way lies within the circle's angle, the nearest way to each ray marked
        // for it at every size, so no ray is missed by every way.
        const int Ways = 24, Sizes = 16, Rays = 16;
        var golden = Math.PI * (3 - Math.Sqrt(5));
        var rays = Enumerable.Range(0, Rays).Select(i =>
        {
            var (angle, radius) = (i * golden, Math.Sqrt((i + 0.5) / Rays));
            var (x, y) = (Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            return (X: x, Y: y, Z: Math.Sqrt(1 - x * x - y * y));
        }).ToArray();
        var table = new ushort[Ways * Ways * Sizes];
        for (int z = 0; z < Sizes; z++)
        {
            var size = Math.Sqrt(2) * (z + 0.5) / Sizes;
            var within = 1 - size * size * 0.5;
            var nearest = Enumerable.Repeat((X: -1, Y: -1, Cos: -1.0), Rays).ToArray();
            for (int y = 0; y < Ways; y++)
            for (int x = 0; x < Ways; x++)
            {
                var (ex, ey) = ((x + 0.5) / Ways * 2 - 1, (y + 0.5) / Ways * 2 - 1);
                var lifted = Math.Max(1 - ex * ex - ey * ey, 0);
                var times = 2 * Math.Sqrt(lifted);
                var way = (X: ex * times, Y: ey * times, Z: lifted * 2 - 1);
                ushort bits = 0;
                if (way.Z != -1)
                    for (int r = 0; r < Rays; r++)
                    {
                        var cos = way.X * rays[r].X + way.Y * rays[r].Y + way.Z * rays[r].Z;
                        if (cos > nearest[r].Cos) nearest[r] = (x, y, cos);
                        if (cos > within) bits |= (ushort)(1 << r);
                    }
                table[(z * Ways + y) * Ways + x] = bits;
            }
            for (int r = 0; r < Rays; r++) table[(z * Ways + nearest[r].Y) * Ways + nearest[r].X] |= (ushort)(1 << r);
        }

        var shipped = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source", "shaders", "cortao", "occluders_24x24x16x16.bin"));
        shipped.Length.Should().Be(CircularOccluders.TableBytes);
        var differing = Enumerable.Range(0, table.Length).Count(i => BinaryPrimitives.ReadUInt16LittleEndian(shipped.AsSpan(i * 2)) != table[i]);
        differing.Should().Be(0, "the table carried is CORTAO's for sixteen rays at 24 by 24 ways and 16 sizes");
    }

    [Fact]
    public void A_Cameras_Projection_Is_Taken_Back_Out_Of_Its_Turned_View()
    {
        // A camera's view and projection with its eye at nothing, as the window's view carries
        // it, is its turn times its projection, Vulkan's y flipped, an offset of its middle as
        // temporal antialiasing's jitter gives it.
        foreach (var (yaw, pitch, fov, jitter) in new[] { (0.3f, -0.2f, 60f, 0f), (2.1f, 0.7f, 45f, 0.002f), (-1.2f, 1.3f, 90f, -0.004f) })
        {
            var turn = Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0)));
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(fov), 16 / 9f, 0.1f, 500);
            projection.M22 = -projection.M22;
            var turned = turn * projection * Matrix4x4.CreateTranslation(jitter, -jitter, 0);
            var expected = projection * Matrix4x4.CreateTranslation(jitter, -jitter, 0);

            var taken = CircularOccluders.Decompose(turned);
            taken.Should().NotBeNull();
            foreach (var (got, want) in new[] { (taken!.Value.M11, expected.M11), (taken.Value.M22, expected.M22), (taken.Value.M31, expected.M31),
                (taken.Value.M32, expected.M32), (taken.Value.M33, expected.M33), (taken.Value.M34, expected.M34), (taken.Value.M43, expected.M43) })
                got.Should().BeApproximately(want, 1e-4f);
        }
        CircularOccluders.Decompose(Matrix4x4.CreateOrthographic(10, 10, 0.1f, 100)).Should().BeNull("an orthographic projection is not one taken");
    }

    [Fact]
    public void The_Circles_Settings_Reach_The_Main_Pass_And_Are_Held_To_Their_Ranges()
    {
        UseApp(new App(Config.Default with { Headless = true }));
        var settings = GetApp().World.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        (settings.SlopeCompensation, settings.BackfaceThreshold, settings.BackfaceBoost, settings.ScreenShare).Should().Be((4f, 0.5f, 0f, 0.3f),
            "CORTAO's own defaults, the screen's share the horizons'");

        SetAmbientOcclusion(1, 2.5f);
        SetCircularOccluders(slopeCompensation: 2, backfaceThreshold: 0.3f, backfaceBoost: 0.7f, screenShare: 0.2f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(60), 16 / 9f, 0.1f, 500);
        projection.M22 = -projection.M22;
        var main = CircularOccluders.Parameters(projection, new Extent2D(640, 360), settings, 1, 8).Main;
        main.Radius.Should().Be(2.5f, "the largest radius is the one SetAmbientOcclusion takes");
        main.InverseMaxSlopeCompensation.Should().BeApproximately(0.5f, 1e-6f);
        main.BackfaceThreshold.Should().BeApproximately(0.3f, 1e-6f);
        main.BackfaceBoost.Should().BeApproximately(0.7f, 1e-6f);
        var halfHeight = 1 / -projection.M22;
        var apparent = halfHeight * 0.2f * 2;
        main.MaxApparentCircleRadiusCoefficient.Should().BeApproximately(apparent / MathF.Sqrt(apparent * apparent + 1), 1e-5f);

        SetCircularOccluders(slopeCompensation: 0.5f, backfaceThreshold: 2, backfaceBoost: -1, screenShare: 0);
        (settings.SlopeCompensation, settings.BackfaceThreshold, settings.BackfaceBoost, settings.ScreenShare).Should().Be((1f, 1f, 0f, 0.01f),
            "a value past its range is held to it");
    }

    [Fact]
    public void The_Method_Is_Chosen_With_Its_Samples_And_A_Value_That_Names_None_Is_Refused()
    {
        UseApp(new App(Config.Default with { Headless = true }));
        var settings = GetApp().World.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        settings.Method.Should().Be(AmbientOcclusionMethod.Horizons, "the horizons are the default");
        SetAmbientOcclusionMethod(AmbientOcclusionMethod.CircularOccluders, 8);
        (settings.Method, settings.Samples).Should().Be((AmbientOcclusionMethod.CircularOccluders, 8));
        SetAmbientOcclusionMethod(AmbientOcclusionMethod.CircularOccluders, 12);
        settings.Samples.Should().Be(16, "a count other than 8 is taken as 16");
        SetAmbientOcclusionMethod((AmbientOcclusionMethod)5);
        settings.Method.Should().Be(AmbientOcclusionMethod.CircularOccluders, "a value that names no method leaves the one chosen");
    }
}
