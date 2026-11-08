using System.Numerics;

namespace Engine;

// The night of a day sky with air, as the Sky module models it, the moon at its place and phase,
// the bright stars at theirs, the Milky Way's band and a moonless sky's own glow, which the drawn
// sky adds by their luminance (sky.slang) and the light at night takes the moon's from.
internal sealed partial class DaySky
{
    /// <summary>
    /// The cells along each side of each face of the cube the stars are sorted into for the drawn
    /// sky, some three degrees a cell, a star and a half in each on the mean.
    /// </summary>
    internal const int StarCells = 32;

    /// <summary>
    /// The day of the year the sky's first day is, the March equinox's, whose sun the day sky's
    /// path, rising due east and setting due west, stands for.
    /// </summary>
    internal const float FirstDayOfYear = 79;

    /// <summary>
    /// The moon's disc as the drawn sky adds it, the way toward it, its sunlit part's luminance
    /// through the air in the frame's light, the way toward the sun its lit side faces and the
    /// earthshine's luminance on its dark part in the same light, or none where the sky has no
    /// air or the moon is down.
    /// </summary>
    public (Vector3 Toward, Vector3 Lit, Vector3 TowardSun, float Earthshine)? MoonDisc { get; private set; }

    /// <summary>
    /// The sky's clock for an hour of the sky's day <paramref name="days"/>, which gives the moon's
    /// phase and how far the stars have turned (Sky.SkyClock), at the equator, the day sky's sun
    /// passing over it.
    /// </summary>
    internal static Sky.SkyClock ClockAt(float hour, int days) =>
        new() { Hour = hour, DayOfYear = ((FirstDayOfYear + days) % 365 + 365) % 365, Latitude = 0 };

    /// <summary>
    /// The way toward the moon at an hour of a day and its phase angle in degrees, 180 when new
    /// and 0 when full. It stands on the sun's path behind the sun by the clock's elongation, the
    /// whole sky turning about its axis from north to south, so it rises later each day by the
    /// share of the month gone.
    /// </summary>
    internal static (Vector3 Toward, float PhaseAngle) MoonAt(float hour, float tilt, int days)
    {
        var elongation = ClockAt(hour, days).MoonPhase * 360;
        var lean = float.DegreesToRadians(tilt);
        var toward = Vector3.Transform(new Vector3(MathF.Cos(lean), 0, MathF.Sin(lean)),
            Matrix4x4.CreateRotationZ(float.DegreesToRadians((hour - 6) * 15 - elongation)));
        return (toward, 180 - MathF.Min(elongation, 360 - elongation));
    }

    /// <summary>
    /// The sine and cosine of how far the stars have turned at an hour of a day, the local
    /// sidereal time, by which the drawn sky takes a way in the world to the stars' coordinates,
    /// right ascension along the turn and declination along -Z, the sky's axis toward the north.
    /// </summary>
    internal static Vector2 SiderealTurn(float hour, int days)
    {
        var turn = ClockAt(hour, days).SkyTurn;
        return new Vector2(MathF.Sin(turn), MathF.Cos(turn));
    }

    /// <summary>
    /// The way in the world toward a point of the stars' coordinates, its right ascension and
    /// declination in radians, the sky turned by <paramref name="turn"/>'s sine and cosine, as
    /// sky.slang takes a way back to them.
    /// </summary>
    internal static Vector3 TowardStars(float rightAscension, float declination, Vector2 turn)
    {
        var e = new Vector3(MathF.Cos(declination) * MathF.Cos(rightAscension), MathF.Cos(declination) * MathF.Sin(rightAscension), MathF.Sin(declination));
        var (s, c) = (turn.X, turn.Y);
        return new Vector3(-s * e.X + c * e.Y, c * e.X + s * e.Y, -e.Z);
    }

    /// <summary>
    /// The bright stars as the drawn sky reads them, four uints a record, the cube's
    /// <see cref="StarCells"/> squared cells a face over six faces, each the index of its first star,
    /// one more for the end, padded to four, then each star sorted by its cell, its way in the
    /// stars' coordinates and its illuminance in lux, and its color at a luminance of 1, as floats.
    /// </summary>
    internal static uint[] StarGrid()
    {
        var catalog = Sky.Stars.Catalog;
        var cells = 6 * StarCells * StarCells;
        var placed = catalog.Select(star =>
        {
            var e = new Vector3(MathF.Cos(star.Declination) * MathF.Cos(star.RightAscension), MathF.Cos(star.Declination) * MathF.Sin(star.RightAscension), MathF.Sin(star.Declination));
            return (Cell: CellOf(e), Way: e, Lux: Sky.Stars.Illuminance(star.Magnitude), Color: Sky.Stars.Color(star.ColorIndex));
        }).OrderBy(star => star.Cell).ToArray();
        var head = (cells + 1 + 3) / 4 * 4;
        var data = new uint[head + placed.Length * 8];
        var next = 0;
        for (int cell = 0; cell <= cells; cell++)
        {
            while (next < placed.Length && placed[next].Cell < cell) next++;
            data[cell] = (uint)next;
        }
        for (int i = 0; i < placed.Length; i++)
        {
            var (_, way, lux, color) = placed[i];
            ReadOnlySpan<float> record = [way.X, way.Y, way.Z, lux, color.X, color.Y, color.Z, 0];
            for (int k = 0; k < 8; k++) data[head + i * 8 + k] = BitConverter.SingleToUInt32Bits(record[k]);
        }
        return data;
    }

    /// <summary>The cell of the stars' cube a way in their coordinates falls in, as sky.slang finds it.</summary>
    internal static int CellOf(Vector3 e)
    {
        var a = Vector3.Abs(e);
        int face;
        float u, v, major;
        if (a.X >= a.Y && a.X >= a.Z) (face, major, u, v) = (e.X > 0 ? 0 : 1, a.X, e.Y, e.Z);
        else if (a.Y >= a.Z) (face, major, u, v) = (e.Y > 0 ? 2 : 3, a.Y, e.X, e.Z);
        else (face, major, u, v) = (e.Z > 0 ? 4 : 5, a.Z, e.X, e.Y);
        int Index(float t) => Math.Clamp((int)((t / major * 0.5f + 0.5f) * StarCells), 0, StarCells - 1);
        return (face * StarCells + Index(v)) * StarCells + Index(u);
    }

    // The moon's disc through the air at an hour, its sunlit part's luminance and its dark part's
    // earthshine times what a luminance is multiplied by for the frame, both through the air along
    // its way, or none below the horizon.
    private (Vector3 Toward, Vector3 Lit, Vector3 TowardSun, float Earthshine)? MoonDiscAt(Sky.Atmosphere air, float hour, float tilt, float preExposure)
    {
        var (toMoon, phaseAngle) = MoonAt(hour, tilt, Days);
        if (toMoon.Y <= 0) return null;
        var through = air.Transmittance(0, toMoon.Y);
        var hue = Sky.Sun.Outside / Sky.Colorimetry.Luminance(Sky.Sun.Outside);
        var lit = hue * through * Sky.Moon.LitLuminance(phaseAngle) * preExposure;
        var earthshine = Sky.Moon.EarthshineLuminance(phaseAngle) * Sky.Colorimetry.Luminance(through) * preExposure;
        return (toMoon, lit, SunToward(hour, tilt), earthshine);
    }
}
