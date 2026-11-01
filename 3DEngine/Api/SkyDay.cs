namespace Engine;

/// <summary>
/// How the day sky's day runs, which <see cref="E3D.SetSkyDay"/> takes, the minutes a whole day
/// lasts, the lean of the sun's path toward the south and the directional light the sky moves.
/// </summary>
/// <remarks>
/// <c>new SkyDay()</c> is the day the sky runs unless set, twenty minutes long with the sun's path
/// leaning 16.7 degrees, as the voxel game's does, and a light of the sky's own. The sky's east is
/// +X, its south +Z and its up +Y.
/// </remarks>
public readonly record struct SkyDay
{
    /// <summary>A day of twenty minutes whose sun leans 16.7 degrees toward the south, lighting the world with a light of the sky's own.</summary>
    public SkyDay()
    {
    }

    /// <summary>How many minutes of the program's time a whole day lasts, or 0 for the hour to stand where <see cref="E3D.SetSkyTime"/> sets it.</summary>
    public float Minutes { get; init; } = 20;

    /// <summary>
    /// How many degrees the sun's path leans from overhead toward the south, so at noon it stands so
    /// far from the zenith and the shadows of a grid of boxes do not run along their edges.
    /// </summary>
    public float Tilt { get; init; } = 16.7f;

    /// <summary>
    /// The directional light the sky turns and colors, the sun's by day and the moon's by night, or
    /// none for the sky to make one of its own that casts shadows, so a program with a sun of its
    /// own keeps it and gets no second.
    /// </summary>
    public LightHandle Light { get; init; }

    /// <summary>
    /// The lux the sun gives a surface facing it when it stands high, seen through the camera
    /// <see cref="E3D.SetCameraExposure"/> sets, or 0 for the sky's own plain strength, a white of
    /// 2.2 at noon. A clear noon's sun gives about 100,000.
    /// </summary>
    /// <remarks>
    /// The sun keeps the sky's own curve through the day, its light falling and reddening as it
    /// sinks, and the moon its share of the sun's.
    /// </remarks>
    public float SunIlluminance { get; init; }

    /// <summary>
    /// The lux the sky gives a level floor at noon apart from the sun, seen through the camera as
    /// <see cref="SunIlluminance"/> is, which scales the light of the sky the environment map holds
    /// and the sky <see cref="E3D.DrawSkybox()"/> draws, or 0 for the sky's own plain strength. A
    /// clear sky gives 15,000 to 25,000, an overcast one 1,000 to 10,000.
    /// </summary>
    public float SkyIlluminance { get; init; }

    /// <summary>
    /// The sun's color at every hour in place of the sky's own, a warm white high and an orange
    /// low, or none, its alpha 0, for the sky's own, as <see cref="E3D.ColorFromTemperature"/> gives
    /// one from a temperature.
    /// </summary>
    public Color SunColor { get; init; }

    /// <summary>
    /// The air the sun's and the moon's light reach the world through, as the Sky module models
    /// it, such as <see cref="Sky.Atmosphere.Earth"/>, or none for the sky's own curve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With air, the light is physical. <see cref="SunIlluminance"/> and <see cref="SunColor"/> are
    /// the sun's outside the air, the module's 128,000 lux at 5,772 K where they are unset, and the
    /// light that reaches the world is that one through the air at the sun's height, dimming and
    /// warming as it sinks and gone as its disc sets. By night the moon gives its light by the
    /// same air, a full moon's third of a lux outside it. The sky's light on a level floor is the
    /// air's at the hour, the sun's light scattered toward it, and <see cref="SkyIlluminance"/> is
    /// not read.
    /// </para>
    /// <para>
    /// The lux are seen through the camera <see cref="E3D.SetCameraExposure"/> sets, so a program
    /// with air sets its camera or its automatic exposure for daylight.
    /// </para>
    /// </remarks>
    public Sky.Atmosphere? Atmosphere { get; init; }
}
