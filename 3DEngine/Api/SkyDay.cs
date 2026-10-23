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
}
