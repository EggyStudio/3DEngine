using System.Numerics;

namespace Engine;

/// <summary>
/// The day sky, a world resource which <see cref="E3D.SetSkyTime"/> and <see cref="E3D.SetSkyDay"/>
/// start, holding the hours since its first midnight, the day's settings and the light it moves,
/// and painting the environment map from the hour.
/// </summary>
/// <remarks>
/// <para>
/// The numbers are the voxel game's. The sun rises in the east at 6, passes south of the zenith at
/// noon by the day's lean and sets in the west at 18, the sky turning about its axis from north to
/// south, and the moon stands opposite it. The light is the sun's until the sun's height, the sine
/// of its angle over the horizon, falls below -0.05, and the moon's after, each faded out where the
/// light turns from one to the other, so the shadows do not jump.
/// </para>
/// <para>
/// The map is a gradient from the horizon to the zenith colored by the hour, a dusk color mixed into
/// the horizon as the sun crosses it and the night's violet tint over all of it, and below the
/// horizon the horizon's color darkening to half of it, which lights the world from below. It is
/// painted again once the hour has moved by <see cref="RepaintHours"/>, and no more often than every
/// <see cref="RepaintSeconds"/> of the program's time, since each painting is a new map for the GPU
/// to filter. The horizon <see cref="E3D.SetSkyHorizon"/> sets is set to the hour's horizon color at
/// each painting, so the drawn sky below the horizon follows the hour.
/// </para>
/// </remarks>
internal sealed partial class DaySky
{
    /// <summary>The painted map's width and height in pixels, a degree and a half a pixel, which a smooth gradient needs no more than.</summary>
    internal const int Width = 256, Height = 128;

    /// <summary>How far the hour moves before the map is painted again, three minutes of the sky's day.</summary>
    internal const float RepaintHours = 0.05f;

    /// <summary>The least time of the program's between two paintings, so a short day filters at most four maps a second.</summary>
    internal const double RepaintSeconds = 0.25;

    /// <summary>The degrees above the horizon over which the drawn sky blends into the horizon's color, the band the voxel game fades its fog over.</summary>
    internal const float HorizonBlendDegrees = 6;

    // The sky's colors in sRGB levels, the zenith's and the horizon's by night and by day and the
    // horizon's at dusk.
    private static readonly Vector3 NightZenith = new(6, 9, 22), DayZenith = new(78, 134, 222);
    private static readonly Vector3 NightHorizon = new(18, 24, 44), DayHorizon = new(178, 208, 240), DuskHorizon = new(238, 150, 96);

    // The night's tint, which multiplies the sky's colors in linear light, a violet blue, by as much
    // as the night is the night.
    private static readonly Vector3 NightTint = new(1.10f, 0.90f, 1.30f);

    // The sunlight in linear light, a warm white at noon and toward the horizon an orange, mixed by
    // the square root of the sun's height.
    private static readonly Vector3 NoonLight = new Vector3(0.7f, 0.55f, 0.4f) * 1.9f, LowLight = new(0.64f, 0.45f, 0.3f);

    // How strong the sun is for each unit of its light, so its noon is a white of 2.2 in luminance.
    private const float SunStrength = 2.2f * 0.915f / 1.085f;

    // The moonlight's hue in linear light, a cool blue, and its strength, a quarter of the sun's
    // luminance at noon.
    private static readonly Vector3 Moonlight = new Vector3(0.15f, 0.14f, 0.20f) / 0.20f;
    private const float MoonStrength = 0.25f * 0.409f / 0.732f;

    /// <summary>The hours since the sky's first midnight, the day's count in their 24s.</summary>
    public double Hours { get; set; }

    /// <summary>How the day runs.</summary>
    public SkyDay Day { get; set; } = new();

    /// <summary>The light the sky moves, the program's or its own.</summary>
    public LightHandle Light { get; set; }

    /// <summary>Whether the sky made <see cref="Light"/> and unloads it when it ends.</summary>
    public bool OwnsLight { get; set; }

    /// <summary>The horizon's color at the last painting.</summary>
    public Color Horizon { get; private set; }

    private float _litHour = float.NaN, _litTilt = float.NaN;
    private LightHandle _litLight;
    private double _paintedHours = double.NaN, _paintedAt = double.NegativeInfinity;
    private readonly byte[] _pixels = new byte[Width * Height * 4];

    /// <summary>The hour of the day, from 0 to 24.</summary>
    public float Hour => (float)(Hours - Math.Floor(Hours / 24) * 24);

    /// <summary>How many whole days have passed since the first midnight.</summary>
    public int Days => (int)Math.Floor(Hours / 24);

    /// <summary>Has the next frame light and paint the sky whatever moved, after the program set its time or its day.</summary>
    public void Changed() => (_litHour, _paintedHours, _paintedAt) = (float.NaN, double.NaN, double.NegativeInfinity);

    /// <summary>
    /// The way toward the sun at an hour, of length one, rising at +X at 6, over +Y at noon leaning
    /// <paramref name="tilt"/> degrees toward +Z, and setting at -X at 18.
    /// </summary>
    public static Vector3 SunToward(float hour, float tilt)
    {
        var lean = float.DegreesToRadians(tilt);
        return Vector3.Transform(new Vector3(MathF.Cos(lean), 0, MathF.Sin(lean)), Matrix4x4.CreateRotationZ(float.DegreesToRadians((hour - 6) * 15)));
    }

    /// <summary>The sun's light in linear light at a height, the sine of its angle over the horizon, a warm white high and an orange low.</summary>
    public static Vector3 SunLight(float height)
    {
        var noon = MathF.Sqrt(MathF.Max(height, 0));
        var power = 1.5f + (1 - noon);
        var low = new Vector3(MathF.Pow(LowLight.X, power), MathF.Pow(LowLight.Y, power), MathF.Pow(LowLight.Z, power)) * 5;
        return Vector3.Lerp(low, NoonLight, noon);
    }

    /// <summary>
    /// The light the sky gives at an hour, the way it travels, its color and its strength, the
    /// sun's until its height falls below -0.05 and the moon's opposite it after.
    /// </summary>
    public static (Vector3 Direction, Color Color, float Intensity) LightAt(float hour, float tilt)
    {
        var toSun = SunToward(hour, tilt);
        if (toSun.Y > -0.05f)
        {
            // The light's hue as the color and its largest part as the strength, so the hue never clips.
            var light = SunLight(toSun.Y);
            var most = MathF.Max(light.X, MathF.Max(light.Y, light.Z));
            return (-toSun, ColorOf(Encode(light / most) * 255), SunStrength * most * SmoothStep(-0.05f, 0.1f, toSun.Y));
        }
        return (toSun, ColorOf(Encode(Moonlight) * 255), MoonStrength * SmoothStep(0.05f, 0.2f, -toSun.Y));
    }

    /// <summary>The zenith's and the horizon's colors in sRGB levels at an hour, the night's tint over both.</summary>
    public static (Vector3 Zenith, Vector3 Horizon) ColorsAt(float hour, float tilt)
    {
        var toSun = SunToward(hour, tilt);
        var day = DayShare(toSun);
        var dusk = DuskShare(toSun);
        var zenith = AtNight(Vector3.Lerp(NightZenith, DayZenith, day), day);
        var horizon = AtNight(Vector3.Lerp(Vector3.Lerp(NightHorizon, DayHorizon, day), DuskHorizon, dusk * 0.8f), day);
        return (zenith, horizon);
    }

    /// <summary>
    /// The environment map's pixels at an hour, a row at a time from the top, each row one color,
    /// the gradient from the zenith down to the horizon above it and the horizon darkening to half
    /// below.
    /// </summary>
    public byte[] Paint(float hour, float tilt)
    {
        var (zenith, horizon) = ColorsAt(hour, tilt);
        var ground = horizon * 0.5f;
        Horizon = ColorOf(horizon);
        for (int row = 0; row < Height; row++)
        {
            // Down from the top of the image, as the engine reads an equirectangular map, so a row's
            // height on the sky is the cosine of its angle from the zenith.
            var up = MathF.Cos((row + 0.5f) / Height * MathF.PI);
            var color = up >= 0
                ? Vector3.Lerp(zenith, horizon, MathF.Pow(1 - up, 4))
                // The horizon's color holds a while before darkening, so the drawn sky's horizon
                // meets an image of its own color.
                : Vector3.Lerp(horizon, ground, SmoothStep(0.1f, 0.6f, -up));
            var (r, g, b) = (Level(color.X), Level(color.Y), Level(color.Z));
            for (int column = 0; column < Width; column++)
            {
                var at = (row * Width + column) * 4;
                (_pixels[at], _pixels[at + 1], _pixels[at + 2], _pixels[at + 3]) = (r, g, b, 255);
            }
        }
        return _pixels;
    }

    /// <summary>
    /// The system, for <see cref="Stage.PostUpdate"/>, which moves the hour on by the frame's time,
    /// turns and colors the light where the hour or the light moved, and paints the map and the
    /// horizon where the hour moved far enough, the flat API made to run against the app while it
    /// does where it runs against another or none.
    /// </summary>
    internal static void Run(World world, App app)
    {
        if (!world.TryGetResource<DaySky>(out var sky)) return;
        var current = E3D.CurrentApp;
        var borrowed = !E3D.Holds(world) && ReferenceEquals(app.World, world);
        if (!borrowed && !E3D.Holds(world)) return;
        if (borrowed) E3D.UseApp(app);
        try
        {
            var time = world.TryGetResource<Time>(out var t) ? t : null;
            if (sky.Day.Minutes > 0 && time is not null) sky.Hours += time.DeltaSeconds * 24 / (sky.Day.Minutes * 60);
            sky.Apply(world, time?.ElapsedSeconds ?? 0);
        }
        finally
        {
            if (borrowed) E3D.UseApp(current);
        }
    }

    private void Apply(World world, double now)
    {
        var (hour, tilt) = (Hour, Day.Tilt);
        if (hour != _litHour || tilt != _litTilt || Light != _litLight)
        {
            (_litHour, _litTilt, _litLight) = (hour, tilt, Light);
            var (direction, color, intensity) = LightAt(hour, tilt);
            E3D.SetLightDirection(Light, direction);
            E3D.SetLightColor(Light, color, intensity);
        }

        if (!double.IsNaN(_paintedHours) && (Math.Abs(Hours - _paintedHours) < RepaintHours || now - _paintedAt < RepaintSeconds)) return;
        (_paintedHours, _paintedAt) = (Hours, now);
        world.InsertResource(EnvironmentMap.FromEquirectangular(new Image(Paint(hour, tilt), Width, Height)));
        var horizon = world.GetOrInsertResource(static () => new SkyHorizon());
        (horizon.Color, horizon.BlendDegrees) = (Horizon, HorizonBlendDegrees);
    }

    // How much of the day's light the sky has, 0 at night and 1 once the sun is well up.
    private static float DayShare(Vector3 toSun) => SmoothStep(-0.15f, 0.2f, toSun.Y);

    // How much of dawn's or dusk's color the sky takes, most as the sun crosses the horizon.
    private static float DuskShare(Vector3 toSun) => SmoothStep(-0.2f, 0.0f, toSun.Y) * (1 - SmoothStep(0.0f, 0.3f, toSun.Y));

    // A sky's color in sRGB levels tinted by the night's tint as far as the day's light is gone.
    private static Vector3 AtNight(Vector3 color, float day)
    {
        static float Decode(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        var tint = Vector3.Lerp(NightTint, Vector3.One, day);
        var linear = new Vector3(Decode(color.X / 255), Decode(color.Y / 255), Decode(color.Z / 255)) * tint;
        return Encode(Vector3.Min(linear, Vector3.One)) * 255;
    }

    // Linear light to sRGB levels from 0 to 1.
    private static Vector3 Encode(Vector3 linear)
    {
        static float One(float c) => c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f;
        return new Vector3(One(linear.X), One(linear.Y), One(linear.Z));
    }

    internal static float SmoothStep(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static Color ColorOf(Vector3 rgb) => new(Level(rgb.X), Level(rgb.Y), Level(rgb.Z));

    // A level from 0 to 255 rounded, so a channel worked out as 254.99 is the 255 it stands for.
    private static byte Level(float level) => (byte)Math.Clamp(MathF.Round(level), 0, 255);
}
