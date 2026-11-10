using System.Numerics;

namespace Engine;

// The day sky lit through the air the program names (SkyDay.Atmosphere), the sun's and the moon's
// light as the Sky module's model gives them at their height, and the map the air's sky, which the
// GPU draws from the same model (AirSky, atmosphere_sky.slang).
internal sealed partial class DaySky
{
    /// <summary>
    /// The sun's disc's angular radius in degrees, half of its half a degree across, over which its
    /// light fades out around its middle's crossing of the horizon rather than at once.
    /// </summary>
    internal const float DiscRadiusDegrees = 0.27f;

    /// <summary>
    /// The width and height of the air's map, a third of a degree a texel, so the drawn sky's cube
    /// takes faces of 256.
    /// </summary>
    internal const int AirWidth = 1024, AirHeight = 512;

    /// <summary>
    /// The sun's disc as the drawn sky adds it over the air's, the way toward it and its luminance
    /// through the air in the frame's light, or none where the sky has no air or the sun is down.
    /// </summary>
    public (Vector3 Toward, Vector3 Light)? SunDisc { get; private set; }

    // Turns and colors the light through the air, and has the GPU draw the map from the air's sky.
    private void ApplyAir(World world, Sky.Atmosphere air, double now)
    {
        var (hour, tilt) = (Hour, Day.Tilt);
        var units = world.GetOrInsertResource(static () => new PhysicalUnits());
        if (hour != _litHour || tilt != _litTilt || Light != _litLight)
        {
            (_litHour, _litTilt, _litLight) = (hour, tilt, Light);
            var (direction, light) = AirLightAt(air, Outside(hour, tilt), hour, tilt, Days);
            var lux = Sky.Colorimetry.Luminance(light);
            var most = MathF.Max(light.X, MathF.Max(light.Y, light.Z));
            var color = lux > 0 ? ColorOf(Encode(light / most) * 255) : Color.White;
            var hue = E3D.Linear(color);
            var intensity = lux > 0 ? units.IntensityOf(LightKind.Directional, lux, PhysicalQuantity.Illuminance, 0, Sky.Colorimetry.Luminance(hue)) : 0;
            E3D.SetLightDirection(Light, direction);
            E3D.SetLightColor(Light, color, intensity);
            SunDisc = DiscAt(air, Outside(hour, tilt), hour, tilt, units.PreExposure);
            MoonDisc = MoonDiscAt(air, hour, tilt, units.PreExposure);
        }

        if (!double.IsNaN(_paintedHours) && (Math.Abs(Hours - _paintedHours) < RepaintHours || now - _paintedAt < RepaintSeconds)) return;
        (_paintedHours, _paintedAt) = (Hours, now);
        // The air's sky is its own below the horizon, the ground lit through the air, so no horizon
        // color is drawn over it. Once the sun is twelve degrees down, the end of the astronomical
        // twilight, past which it lights no part of the air, the air scatters the moon's light in its
        // place, and under both lies a moonless sky's own glow (Sky.Night).
        var (toMoon, phaseAngle) = MoonAt(hour, tilt, Days);
        var byMoon = SunToward(hour, tilt).Y < MathF.Sin(float.DegreesToRadians(-12));
        var (toward, outside) = byMoon
            ? (toMoon, Sky.Sun.Outside / Sky.Sun.OutsideIlluminance * Sky.Moon.OutsideIlluminance(phaseAngle))
            : (SunToward(hour, tilt), Outside(hour, tilt));
        world.InsertResource(EnvironmentMap.FromAir(new AirSky(air, toward, outside * units.PreExposure, Sky.Night.SkyLuminance * units.PreExposure), AirWidth, AirHeight));
        world.RemoveResource<SkyHorizon>();
    }

    /// <summary>
    /// The sun's disc through <paramref name="air"/> at an hour, its luminance the sun's light at
    /// the ground spread over the disc's solid angle, times <paramref name="preExposure"/>, or none
    /// once its middle is below the horizon.
    /// </summary>
    internal static (Vector3 Toward, Vector3 Light)? DiscAt(Sky.Atmosphere air, Vector3 outside, float hour, float tilt, float preExposure)
    {
        var toSun = SunToward(hour, tilt);
        if (toSun.Y <= 0) return null;
        var solidAngle = 2 * MathF.PI * (1 - MathF.Cos(float.DegreesToRadians(DiscRadiusDegrees)));
        return (toSun, outside * air.Transmittance(0, toSun.Y) / solidAngle * preExposure);
    }

    // The share of the sun's strength a low sun keeps at least where it is scaled by its height
    // (SkyDay.SunByHeight), which lights the twilight's air after sunset.
    private const float LeastHeightShare = 1 / 24f;

    // The sun's light outside the air in linear light, its luminance in lux, the program's where it
    // set one and the module's otherwise, by the sun's height at the hour where the day asks.
    private Vector3 Outside(float hour, float tilt)
    {
        var lux = Day.SunIlluminance > 0 ? Day.SunIlluminance : Sky.Sun.OutsideIlluminance;
        if (Day.SunByHeight) lux *= MathF.Max(LeastHeightShare, SunToward(hour, tilt).Y);
        if (Day.SunColor.A == 0) return Sky.Sun.Outside / Sky.Sun.OutsideIlluminance * lux;
        var hue = E3D.Linear(Day.SunColor with { A = 255 });
        return hue / MathF.Max(Sky.Colorimetry.Luminance(hue), 1e-6f) * lux;
    }

    /// <summary>
    /// The light through <paramref name="air"/> at an hour of day <paramref name="days"/>, the way
    /// it travels and its color in linear light, whose luminance is its lux, the sun's of
    /// <paramref name="outside"/> outside the air while its disc is up and the moon's at its place
    /// and phase once the sun is well down and the moon up (<see cref="MoonAt"/>).
    /// </summary>
    internal static (Vector3 Direction, Vector3 Light) AirLightAt(Sky.Atmosphere air, Vector3 outside, float hour, float tilt, int days = 0)
    {
        var toSun = SunToward(hour, tilt);
        if (toSun.Y > -0.05f)
        {
            var elevation = float.RadiansToDegrees(MathF.Asin(toSun.Y));
            var disc = SmoothStep(-DiscRadiusDegrees, DiscRadiusDegrees, elevation);
            // From its middle while it is up, and from the horizon while its upper edge sets.
            var through = air.Transmittance(0, MathF.Max(toSun.Y, 0));
            return (-toSun, outside * through * disc);
        }
        // The moon's light by its phase and its height, fading in as the sun's sky goes and out as
        // its disc sets, a full moon high giving a third of a lux.
        var (toMoon, phaseAngle) = MoonAt(hour, tilt, days);
        var height = float.RadiansToDegrees(MathF.Asin(Math.Clamp(toMoon.Y, -1, 1)));
        var moon = Sky.Moon.At(air, MathF.Max(height, 0), phaseAngle);
        var shown = SmoothStep(-DiscRadiusDegrees, DiscRadiusDegrees, height) * SmoothStep(0.05f, 0.2f, -toSun.Y);
        return (-toMoon, moon.Color * moon.Illuminance * shown);
    }
}
