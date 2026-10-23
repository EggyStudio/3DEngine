using System.Numerics;

namespace Engine;

/// <summary>A quad or a line the day sky records, as <see cref="DrawList.Primitive"/> takes it.</summary>
internal delegate void SkyQuad(ReadOnlySpan<ImmediateVertex> corners, int texture);

// The sun, the moon and the stars, fixed on the sky and turned with it about the eye, the voxel
// game's numbers.
internal sealed partial class DaySky
{
    /// <summary>How far out the sun and the moon are drawn, inside the camera's far plane of 4000.</summary>
    public const float Distance = 3000;

    /// <summary>How far out the stars are drawn, behind the sun and the moon.</summary>
    public const float StarDistance = 3200;

    /// <summary>How many stars the sky holds.</summary>
    public const int StarCount = 3000;

    /// <summary>How many degrees each star's square is across.</summary>
    public const float StarDegrees = 0.28f;

    // The sun's and the moon's bodies are as wide as Minecraft's against their distance, its sun's
    // body 15 units across at 100, about 8.6 degrees, and its moon's 10, about 5.7.
    private const float SunAcross = 0.15f, MoonAcross = 0.10f;

    /// <summary>
    /// Each star's way on the sky as it stands at 6, the half of its square's right side and of its
    /// up there, and its color in sRGB levels from 0 to 1.
    /// </summary>
    public static readonly (Vector3 Way, Vector3 Right, Vector3 Up, Vector3 Color)[] Stars = PlaceStars();

    /// <summary>
    /// The phase of the moon, from 0 for the full moon to 4 for the new one and on to 7, of the
    /// night an hour of a day falls in, the full moon on the night the first day ends with.
    /// </summary>
    /// <remarks>
    /// A night belongs to the day whose evening it begins on, so the hours before noon show the
    /// phase of the day before, and the moon changes while it is down rather than while it is seen.
    /// </remarks>
    public static int MoonPhase(int day, float hour) => ((hour < 12 ? day - 1 : day) % SkyArt.Phases + SkyArt.Phases) % SkyArt.Phases;

    /// <summary>
    /// The right side and the up of a quad fixed on the sky with its middle on a way, each of length
    /// one, the right side along the way the sky turns there and the up across it, so a quad seen
    /// from the middle of the sky shows its front, and a quad keeps them as the sky turns about its
    /// axis from north to south. At the axis itself the right side is east.
    /// </summary>
    public static (Vector3 Right, Vector3 Up) Across(Vector3 way)
    {
        var right = Vector3.Cross(way, Vector3.UnitZ);
        right = right.LengthSquared() > 1e-6f ? Vector3.Normalize(right) : Vector3.UnitX;
        return (right, Vector3.Cross(right, way));
    }

    /// <summary>The sky's turn at an hour since 6, about its axis from north to south, which carries the sun, the moon and the stars as one.</summary>
    public static Matrix4x4 Turn(float hour) => Matrix4x4.CreateRotationZ(float.DegreesToRadians((hour - 6) * 15));

    /// <summary>
    /// The share of what lies behind the drawn sky's horizon band that shows through it along a way
    /// about the eye, none below the horizon and all of it above the band, as the sky's shader blends
    /// the horizon's color.
    /// </summary>
    public static float Clear(Vector3 way, float blendDegrees)
    {
        var above = float.RadiansToDegrees(MathF.Asin(Math.Clamp(way.Y / way.Length(), -1, 1)));
        return blendDegrees > 0 ? SmoothStep(0, blendDegrees, above) : above >= 0 ? 1 : 0;
    }

    /// <summary>
    /// Records the stars about the eye in the sky's space as it stands at 6, for a transform that
    /// turns it by the hour, fading in as the day fades and near the horizon, hidden beside the sun
    /// and the moon and taking the share of the horizon band left clear at their height, and answers
    /// how many quads.
    /// </summary>
    /// <param name="quad">Where each quad goes, the draw list's in a frame.</param>
    /// <param name="blendDegrees">The horizon band's degrees, as the drawn sky blends it.</param>
    internal int DrawStars(SkyQuad quad, float blendDegrees)
    {
        var toSun = SunToward(Hour, Day.Tilt);
        var night = 1 - DayShare(toSun);
        if (night <= 0.01f) return 0;
        var turn = Turn(Hour);
        var quads = 0;
        Span<ImmediateVertex> corners = stackalloc ImmediateVertex[4];
        foreach (var (way, right, up, color) in Stars)
        {
            var turned = Vector3.Transform(way, turn);
            var beside = MathF.Pow(MathF.Min(1, MathF.Abs(Vector3.Dot(turned, toSun)) * 1.002f), 100);
            var alpha = night * Math.Clamp(turned.Y * 3, 0, 1) * (1 - beside) * Clear(turned, blendDegrees);
            if (alpha < 0.01f) continue;
            var tint = new Color(Level(255 * Math.Min(1, color.X)), Level(255 * Math.Min(1, color.Y)), Level(255 * Math.Min(1, color.Z)), Level(255 * alpha));
            var middle = way * StarDistance;
            Corners(corners, middle, right, up, Vector4.Zero, tint);
            quad(corners, 0);
            quads++;
        }
        return quads;
    }

    /// <summary>
    /// Records the moon and the sun about the eye in the sky's space as it stands at 6, for a
    /// transform that turns it by the hour, each fading as it sets and taking the share of the
    /// horizon band left clear at its height, the moon in the day's phase, and answers how many quads.
    /// </summary>
    /// <param name="quad">Where each quad goes, the draw list's in a frame.</param>
    /// <param name="blendDegrees">The horizon band's degrees, as the drawn sky blends it.</param>
    /// <param name="sun">The sun's texture, <see cref="SkyArt.Texels"/> square.</param>
    /// <param name="moon">The sheet of the moon's phases.</param>
    internal int DrawSunAndMoon(SkyQuad quad, float blendDegrees, int sun, int moon)
    {
        var (hour, tilt) = (Hour, Day.Tilt);
        var toSun = SunToward(hour, tilt);
        var turn = Turn(hour);
        var lean = float.DegreesToRadians(tilt);
        var sunWay = new Vector3(MathF.Cos(lean), 0, MathF.Sin(lean));
        var quads = 0;
        Span<ImmediateVertex> corners = stackalloc ImmediateVertex[4];
        var phase = MoonPhase(Days, hour);
        var sheet = new Vector2(SkyArt.Cell * 4, SkyArt.Cell * 2);
        var moonPart = new Vector4((phase % 4 * SkyArt.Cell + 1) / sheet.X, (phase / 4 * SkyArt.Cell + 1) / sheet.Y,
            (phase % 4 * SkyArt.Cell + 1 + SkyArt.Texels) / sheet.X, (phase / 4 * SkyArt.Cell + 1 + SkyArt.Texels) / sheet.Y);
        quads += Body(quad, corners, moon, moonPart, -sunWay, Distance * MoonAcross / SkyArt.Size, Setting(-toSun), turn, blendDegrees);
        quads += Body(quad, corners, sun, new Vector4(0, 0, 1, 1), sunWay, Distance * SunAcross / SkyArt.Size, Setting(toSun), turn, blendDegrees);
        return quads;
    }

    // A picture on the sky at the sun's and the moon's distance, its middle on a way in the sky's
    // space and each of its pixels so wide, one quad where the horizon band leaves all of it clear,
    // and where it covers part of it, a grid of a quad to each pixel with the share left clear at
    // each corner, so a body sinking into the band dims as the band does.
    private static int Body(SkyQuad quad, Span<ImmediateVertex> corners, int texture, Vector4 part, Vector3 way, float pixel, float alpha, Matrix4x4 turn, float blendDegrees)
    {
        if (alpha <= 0) return 0;
        const int side = SkyArt.Across + 1;
        var (right, up) = Across(way);
        Vector3 At(int row, int column) => way * Distance + right * ((column - SkyArt.Across / 2f) * pixel) + up * ((SkyArt.Across / 2f - row) * pixel);

        var shown = new float[side * side];
        var whole = true;
        for (int row = 0; row < side; row++)
            for (int column = 0; column < side; column++)
            {
                shown[row * side + column] = alpha * Clear(Vector3.Transform(At(row, column), turn), blendDegrees);
                whole &= shown[row * side + column] >= alpha;
            }
        if (whole)
        {
            Corners(corners, way * Distance, right * (SkyArt.Across / 2f * pixel), up * (SkyArt.Across / 2f * pixel), part, new Color(255, 255, 255, Level(255 * alpha)));
            quad(corners, texture);
            return 1;
        }

        var (step, rise) = ((part.Z - part.X) / SkyArt.Across, (part.W - part.Y) / SkyArt.Across);
        var quads = 0;
        for (int row = 0; row < SkyArt.Across; row++)
            for (int column = 0; column < SkyArt.Across; column++)
            {
                // From the top left down and round against the clock as the middle of the sky sees
                // it, as Corners lays a quad.
                ReadOnlySpan<(int Down, int Across)> order = [(0, 0), (1, 0), (1, 1), (0, 1)];
                var clear = false;
                for (int i = 0; i < 4; i++)
                {
                    var (r, c) = (row + order[i].Down, column + order[i].Across);
                    clear |= shown[r * side + c] > 0;
                    corners[i] = new ImmediateVertex(At(r, c), new Vector2(part.X + c * step, part.Y + r * rise), new Color(255, 255, 255, Level(255 * shown[r * side + c])));
                }
                if (!clear) continue;
                quad(corners, texture);
                quads++;
            }
        return quads;
    }

    // A quad's corners about its middle, half its right side and half its up from it, from the top
    // left down and round against the clock as the middle of the sky sees it, which is the face a
    // shape drawn in 3D shows, with the texture's part from its top left to its bottom right.
    private static void Corners(Span<ImmediateVertex> corners, Vector3 middle, Vector3 right, Vector3 up, Vector4 uv, Color color)
    {
        corners[0] = new ImmediateVertex(middle - right + up, new Vector2(uv.X, uv.Y), color);
        corners[1] = new ImmediateVertex(middle - right - up, new Vector2(uv.X, uv.W), color);
        corners[2] = new ImmediateVertex(middle + right - up, new Vector2(uv.Z, uv.W), color);
        corners[3] = new ImmediateVertex(middle + right + up, new Vector2(uv.Z, uv.Y), color);
    }

    // How much of the sun or the moon shows as it sets, gone once its middle is a little below the
    // horizon.
    private static float Setting(Vector3 toward) => SmoothStep(-0.12f, 0.02f, toward.Y);

    // The stars, spread evenly over the sky from a fixed hash, so each run has the same ones, as the
    // voxel game draws its own after Aurora's Shaders, squares of StarDegrees, each as bright as 40
    // times the square of how far the product of three noises passes 0.7, the product drawn until it
    // passes, so most are faint and a few bright, in a cool white, the light taken to sRGB levels.
    private static (Vector3, Vector3, Vector3, Vector3)[] PlaceStars()
    {
        var half = StarDistance * MathF.Tan(float.DegreesToRadians(StarDegrees)) / 2;
        var stars = new (Vector3, Vector3, Vector3, Vector3)[StarCount];
        float Next(int i, int k) => Hash(i, k, 13) / (float)uint.MaxValue;
        for (int i = 0; i < StarCount; i++)
        {
            var z = Next(i, 0) * 2 - 1;
            var ring = MathF.Sqrt(1 - z * z);
            var around = Next(i, 1) * MathF.Tau;
            var way = new Vector3(ring * MathF.Cos(around), ring * MathF.Sin(around), z);
            var product = 0f;
            for (int k = 2; product <= 0.7f; k += 3) product = Next(i, k) * Next(i, k + 1) * Next(i, k + 2);
            var light = 40 * (product - 0.7f) * (product - 0.7f) * new Vector3(0.38f, 0.4f, 0.5f);
            var (right, up) = Across(way);
            stars[i] = (way, right * half, up * half, new Vector3(MathF.Pow(light.X, 1 / 2.2f), MathF.Pow(light.Y, 1 / 2.2f), MathF.Pow(light.Z, 1 / 2.2f)));
        }
        return stars;
    }

    // The voxel game's hash of three integers, its places' and its stars'.
    private static uint Hash(int x, int z, int seed)
    {
        unchecked
        {
            var h = (uint)x * 0x27D4EB2Du ^ (uint)z * 0x165667B1u ^ (uint)seed * 0x9E3779B9u;
            h ^= h >> 15;
            h *= 0x85EBCA77u;
            h ^= h >> 13;
            h *= 0xC2B2AE3Du;
            h ^= h >> 16;
            return h;
        }
    }
}
