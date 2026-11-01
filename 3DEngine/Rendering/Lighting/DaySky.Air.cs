using System.Numerics;

namespace Engine;

// The day sky lit through the air the program names (SkyDay.Atmosphere), the sun's and the moon's
// light as the Sky module's model gives them at their height, and the painted map as strong as the
// air's sky lights a level floor at the hour.
internal sealed partial class DaySky
{
    // The sun's disc, half a degree across, sets over the half degree around its middle's
    // crossing of the horizon, so its light fades out as the disc goes rather than at once.
    private const float DiscRadiusDegrees = 0.27f;

    // The sky of the air last lit through, made again when the program names other air.
    private Sky.SkyLight? _skyLight;

    // Turns and colors the light through the air, and paints the map as strong as the air's sky.
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
        }

        if (!double.IsNaN(_paintedHours) && (Math.Abs(Hours - _paintedHours) < RepaintHours || now - _paintedAt < RepaintSeconds)) return;
        (_paintedHours, _paintedAt) = (Hours, now);
        if (_skyLight?.Air != air) _skyLight = new Sky.SkyLight(air);
        // The air's sky for the module's sun, scaled to the program's sun outside the air.
        var level = Sky.Colorimetry.Luminance(_skyLight.Illuminance(SunToward(hour, tilt))) * Sky.Colorimetry.Luminance(Outside()) / Sky.Sun.OutsideIlluminance;
        var strength = level / MathF.PI * units.PreExposure / FloorLight(hour, tilt);
        world.InsertResource(EnvironmentMap.FromEquirectangular(new Image(Paint(hour, tilt), Width, Height), strength));
        var horizon = world.GetOrInsertResource(static () => new SkyHorizon());
        (horizon.Color, horizon.BlendDegrees) = (Horizon, HorizonBlendDegrees);
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
