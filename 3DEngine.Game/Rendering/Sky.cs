using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>
/// The time of day, the sun or the moon as the light that casts the shadows, and a painted sky
/// that lights the world from all around and is drawn behind it.
/// </summary>
/// <remarks>
/// The sky is an equirectangular image painted here, a gradient from the horizon to the zenith
/// with the sun's disc and its glow, given to <c>SetEnvironmentMap</c>, which filters it on the GPU.
/// The light that bounces brings the sky's light back where a ray of its last cascade meets nothing,
/// so a cave lit by no lamp is dark. The image is painted again only when the time has moved by
/// <see cref="RepaintHours"/>, since each painting is a new map to filter.
/// </remarks>
public sealed class Sky
{
    private const int Width = 256;
    private const int Height = 128;
    private const float RepaintHours = 0.05f;

    private static readonly Vector3 NightZenith = new(6, 9, 22), DayZenith = new(78, 134, 222);
    private static readonly Vector3 NightHorizon = new(18, 24, 44), DayHorizon = new(178, 208, 240), DuskHorizon = new(238, 150, 96);
    private static readonly Vector3 Noon = new(255, 244, 228), LowSun = new(255, 160, 90), Moonlight = new(150, 170, 230);

    private readonly LightHandle _light;
    private readonly byte[] _pixels = new byte[Width * Height * 4];
    private float _litHour = float.NaN, _paintedHour = float.NaN;

    public Sky(float hour)
    {
        Hour = hour;
        _light = CreateDirectionalLight(-Vector3.UnitY, Color.White, 1, castsShadows: true);
        Apply();
    }

    /// <summary>The hour of the day, from 0 to 24, the sun rising at 6 in the east and setting at 18 in the west.</summary>
    public float Hour { get; set; }

    /// <summary>Whether the time moves on by itself.</summary>
    public bool Cycle { get; set; }

    /// <summary>How many minutes a whole day takes while <see cref="Cycle"/> is set.</summary>
    public float DayMinutes { get; set; } = 20;

    /// <summary>The color of the sky at the horizon, which the frame is cleared to behind the sky.</summary>
    public Color Horizon { get; private set; }

    public void Update(float seconds)
    {
        if (Cycle) Hour = (Hour + seconds * 24 / (DayMinutes * 60)) % 24;
        Apply();
    }

    /// <summary>The way toward the sun at an hour, of length one.</summary>
    public static Vector3 SunToward(float hour)
    {
        var angle = (hour - 6) / 12 * MathF.PI;
        // Tilted toward the south a little, so the shadows of the blocks do not run along their edges at noon.
        return Vector3.Normalize(new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0.3f));
    }

    private void Apply()
    {
        if (Hour == _litHour) return;
        _litHour = Hour;
        var toSun = SunToward(Hour);

        // The sun by day and the moon opposite it by night, each fading out where the light turns
        // from one to the other, so the shadows do not jump with it.
        if (toSun.Y > -0.05f)
        {
            var low = 1 - SmoothStep(0, 0.35f, toSun.Y);
            SetLightDirection(_light, -toSun);
            SetLightColor(_light, ColorOf(Vector3.Lerp(Noon, LowSun, low)), 2.2f * SmoothStep(-0.05f, 0.1f, toSun.Y));
        }
        else
        {
            SetLightDirection(_light, toSun);
            SetLightColor(_light, ColorOf(Moonlight), 0.25f * SmoothStep(0.05f, 0.2f, -toSun.Y));
        }

        if (!float.IsNaN(_paintedHour) && HoursApart(Hour, _paintedHour) < RepaintHours) return;
        _paintedHour = Hour;
        Paint(toSun);
        SetEnvironmentMap(new Image(_pixels, Width, Height));
    }

    private void Paint(Vector3 toSun)
    {
        var day = SmoothStep(-0.15f, 0.2f, toSun.Y);
        var dusk = SmoothStep(-0.2f, 0.0f, toSun.Y) * (1 - SmoothStep(0.0f, 0.3f, toSun.Y));
        var zenith = Vector3.Lerp(NightZenith, DayZenith, day);
        var horizon = Vector3.Lerp(Vector3.Lerp(NightHorizon, DayHorizon, day), DuskHorizon, dusk * 0.8f);
        var ground = horizon * 0.35f;
        Horizon = ColorOf(horizon);

        for (int py = 0; py < Height; py++)
        {
            // Down from the top of the image, as the engine reads an equirectangular map.
            var theta = (py + 0.5f) / Height * MathF.PI;
            for (int px = 0; px < Width; px++)
            {
                var longitude = ((px + 0.5f) / Width - 0.5f) * 2 * MathF.PI;
                var d = new Vector3(MathF.Sin(longitude) * MathF.Sin(theta), MathF.Cos(theta), -MathF.Cos(longitude) * MathF.Sin(theta));

                var color = d.Y >= 0
                    ? Vector3.Lerp(zenith, horizon, MathF.Pow(1 - d.Y, 4))
                    : Vector3.Lerp(horizon, ground, SmoothStep(0, 0.12f, -d.Y));
                var facing = Vector3.Dot(d, toSun);
                if (facing > 0) color += Vector3.Lerp(Noon, DuskHorizon, dusk) * (MathF.Pow(facing, 12) * (0.25f + 0.5f * dusk) * SmoothStep(-0.2f, 0, toSun.Y));
                if (facing > 0.9993f && toSun.Y > -0.05f) color = new Vector3(255, 250, 235);
                if (-facing > 0.9995f && toSun.Y < 0.05f) color = new Vector3(220, 225, 240);
                // A few fixed stars, which show as the day fades.
                if (d.Y > 0.05f && PlaceHash.Of(px, py, 7) % 900 == 0) color = Vector3.Lerp(color, new Vector3(230), 1 - day);

                var at = (py * Width + px) * 4;
                _pixels[at] = (byte)Math.Clamp(color.X, 0, 255);
                _pixels[at + 1] = (byte)Math.Clamp(color.Y, 0, 255);
                _pixels[at + 2] = (byte)Math.Clamp(color.Z, 0, 255);
                _pixels[at + 3] = 255;
            }
        }
    }

    private static float HoursApart(float a, float b)
    {
        var apart = MathF.Abs(a - b) % 24;
        return MathF.Min(apart, 24 - apart);
    }

    private static float SmoothStep(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static Color ColorOf(Vector3 rgb) =>
        new((byte)Math.Clamp(rgb.X, 0, 255), (byte)Math.Clamp(rgb.Y, 0, 255), (byte)Math.Clamp(rgb.Z, 0, 255));
}
