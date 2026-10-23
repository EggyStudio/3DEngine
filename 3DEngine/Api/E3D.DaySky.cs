namespace Engine;

public static partial class E3D
{
    /// <summary>
    /// Sets the day sky's time in hours from its first midnight, starting the day sky where it was
    /// not running, so the environment map, the light and the horizon follow the hour, 30 being six
    /// in the morning of the second day.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The day sky paints the environment map from the hour, a gradient from the horizon to the
    /// zenith with a dusk as the sun crosses the horizon and a violet tint by night, which lights
    /// the world from all around and which <see cref="DrawSkybox()"/> draws. It turns a directional
    /// light that casts shadows, the sun's by day, a warm white at noon and an orange low, and the
    /// moon's by night, and sets the horizon <see cref="SetSkyHorizon"/> sets to the hour's horizon
    /// color, which <see cref="GetSkyHorizon"/> reads for a program's own fog.
    /// </para>
    /// <para>
    /// The hour moves on by itself as <see cref="SetSkyDay"/> says, twenty minutes a day unless set.
    /// <see cref="SetEnvironmentMap(Image, float)"/> or <see cref="UnloadEnvironmentMap"/> ends the
    /// day sky, unloading the light it made and the horizon it set.
    /// </para>
    /// </remarks>
    public static void SetSkyTime(float hours)
    {
        if (!float.IsFinite(hours)) return;
        var sky = StartDaySky();
        sky.Hours = hours;
        sky.Changed();
    }

    /// <summary>The day sky's time in hours from its first midnight, or 0 where none runs.</summary>
    public static float GetSkyTime() => TryRes<DaySky>(out var sky) ? (float)sky.Hours : 0;

    /// <summary>
    /// Sets how the day sky's day runs, the minutes it lasts, the lean of the sun's path and the
    /// light it turns, starting the day sky at midnight where it was not running.
    /// </summary>
    /// <remarks>
    /// A day of 0 minutes stands at the hour <see cref="SetSkyTime"/> sets. A light the program
    /// names, its own sun, is turned and colored in place of one of the sky's own, and the one the
    /// sky made is unloaded, so a program with a sun of its own gets no second.
    /// </remarks>
    public static void SetSkyDay(SkyDay day)
    {
        var sky = StartDaySky();
        if (day.Light.IsValid && day.Light != sky.Light)
        {
            if (sky.OwnsLight) UnloadLight(sky.Light);
            (sky.Light, sky.OwnsLight) = (day.Light, false);
        }
        else if (!day.Light.IsValid && !sky.OwnsLight)
            (sky.Light, sky.OwnsLight) = (MakeSkyLight(), true);
        sky.Day = day with { Minutes = float.IsFinite(day.Minutes) ? Math.Max(0, day.Minutes) : 0, Tilt = float.IsFinite(day.Tilt) ? Math.Clamp(day.Tilt, -90, 90) : 0 };
        sky.Changed();
    }

    /// <summary>How the day sky's day runs, as <see cref="SetSkyDay"/> left it, or the day it runs unless set.</summary>
    public static SkyDay GetSkyDay() => TryRes<DaySky>(out var sky) ? sky.Day : new SkyDay();

    // The day sky, started at midnight with a light of its own where it was not running.
    private static DaySky StartDaySky()
    {
        if (TryRes<DaySky>(out var running)) return running;
        var sky = new DaySky { Light = MakeSkyLight(), OwnsLight = true };
        World.InsertResource(sky);
        return sky;
    }

    private static LightHandle MakeSkyLight() => CreateDirectionalLight(-System.Numerics.Vector3.UnitY, Color.White, 1, castsShadows: true);

    // A map the program sets or unloads takes the day sky's place, with the light it made and the
    // horizon it set.
    private static void EndDaySky()
    {
        if (!TryRes<DaySky>(out var sky)) return;
        if (sky.OwnsLight) UnloadLight(sky.Light);
        World.RemoveResource<DaySky>();
        World.RemoveResource<SkyHorizon>();
    }
}
