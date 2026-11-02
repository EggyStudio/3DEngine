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
            var (direction, light) = AirLightAt(air, Outside(), hour, tilt);
            var lux = Sky.Colorimetry.Luminance(light);
            var most = MathF.Max(light.X, MathF.Max(light.Y, light.Z));
            var color = lux > 0 ? ColorOf(Encode(light / most) * 255) : Color.White;
            var hue = E3D.Linear(color);
            var intensity = lux > 0 ? units.IntensityOf(LightKind.Directional, lux, PhysicalQuantity.Illuminance, 0, Sky.Colorimetry.Luminance(hue)) : 0;
            E3D.SetLightDirection(Light, direction);
            E3D.SetLightColor(Light, color, intensity);
            SunDisc = DiscAt(air, Outside(), hour, tilt, units.PreExposure);
        }

        if (!double.IsNaN(_paintedHours) && (Math.Abs(Hours - _paintedHours) < RepaintHours || now - _paintedAt < RepaintSeconds)) return;
        (_paintedHours, _paintedAt) = (Hours, now);
        // The air's sky is its own below the horizon, the ground lit through the air, so no horizon
        // color is drawn over it.
        world.InsertResource(EnvironmentMap.FromAir(new AirSky(air, SunToward(hour, tilt), Outside() * units.PreExposure), AirWidth, AirHeight));
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

    // The sun's light outside the air in linear light, its luminance in lux, the program's where it
    // set one and the module's otherwise.
    private Vector3 Outside()
    {
        var lux = Day.SunIlluminance > 0 ? Day.SunIlluminance : Sky.Sun.OutsideIlluminance;
        if (Day.SunColor.A == 0) return Sky.Sun.Outside / Sky.Sun.OutsideIlluminance * lux;
        var hue = E3D.Linear(Day.SunColor with { A = 255 });
        return hue / MathF.Max(Sky.Colorimetry.Luminance(hue), 1e-6f) * lux;
    }

    /// <summary>
    /// The light through <paramref name="air"/> at an hour, the way it travels and its color in
    /// linear light, whose luminance is its lux, the sun's of <paramref name="outside"/> outside
    /// the air while its disc is up and the full moon's opposite it once the sun is well down.
    /// </summary>
    internal static (Vector3 Direction, Vector3 Light) AirLightAt(Sky.Atmosphere air, Vector3 outside, float hour, float tilt)
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
        var moon = Sky.Moon.At(air, float.RadiansToDegrees(MathF.Asin(-toSun.Y)), 0);
        return (toSun, moon.Color * moon.Illuminance * SmoothStep(0.05f, 0.2f, -toSun.Y));
    }
}
