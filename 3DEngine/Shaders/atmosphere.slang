// The air the day sky is drawn through, as the Sky module models it, shared by the passes that
// make its two tables and its sky (atmosphere_transmittance, atmosphere_scatter, atmosphere_sky).
// Every number and step is the module's own (Atmosphere and SkyLight), so the sky drawn here is the
// one the module's luminance gives, which AirSkyTests reads.

// The air's numbers in meters and per meter at sea level, the sun's way and its light, and the
// size of the image a pass writes.
public struct AirPush
{
    // rgb: the molecules' scattering. w: their scale height.
    public float4 rayleigh;
    // rgb: the aerosols' scattering, their extinction times their albedo. w: their scale height.
    public float4 aerosol;
    // rgb: the ozone's absorption in the middle of its layer. w: the height its layer starts at.
    public float4 ozone;
    // rgb: the ground's albedo. w: the ozone layer's depth.
    public float4 ground;
    // x: the planet's radius. y: the height the air ends at. z: the aerosols' extinction. w: how
    // strongly they scatter forward.
    public float4 planet;
    // xyz: the way toward the sun, +Y up. w: unused.
    public float4 sun;
    // rgb: the sun's light outside the air in lux, times what a luminance is multiplied by for the
    // frame. w: unused.
    public float4 light;
    // xy: the width and height of the image the pass writes. zw: unused.
    public uint4 size;
}

public static const float Pi = 3.14159265;

// The two tables' sizes, the cosine with the zenith across and the height down, as SkyLight's.
public static const uint TransmittanceAngles = 256;
public static const uint TransmittanceHeights = 32;
public static const uint ScatterAngles = 32;
public static const uint ScatterHeights = 32;

public float3 rayleighAt(AirPush air, float height) { return air.rayleigh.rgb * exp(-max(height, 0.0) / air.rayleigh.w); }
public float aerosolAt(AirPush air, float height) { return air.planet.z * exp(-max(height, 0.0) / air.aerosol.w); }
public float3 ozoneAt(AirPush air, float height) { return air.ozone.rgb * max(0.0, 1.0 - abs(2.0 * (height - air.ozone.w) / air.ground.w - 1.0)); }
public float3 extinctionAt(AirPush air, float height) { return rayleighAt(air, height) + aerosolAt(air, height) + ozoneAt(air, height); }
// The aerosols' scattering at a height, their extinction there times their albedo.
public float3 aerosolScatteringAt(AirPush air, float height)
{
    return air.aerosol.rgb * exp(-max(height, 0.0) / air.aerosol.w);
}

// How far a way from a height whose cosine with the zenith is mu runs to the ground, or -1 where
// it misses it, and to the top of the air.
public float2 distances(AirPush air, float height, float mu)
{
    float radius = air.planet.x;
    float r = radius + max(height, 0.0);
    float top = radius + air.planet.y;
    float outward = r * r * (mu * mu - 1.0) + top * top;
    float toTop = outward > 0.0 ? max(0.0, -r * mu + sqrt(outward)) : 0.0;
    float inward = r * r * (mu * mu - 1.0) + radius * radius;
    float toGround = mu < 0.0 && inward >= 0.0 ? max(0.0, -r * mu - sqrt(inward)) : -1.0;
    return float2(toGround, toTop);
}

// The height and the zenith a distance along a way from a height, the ground's curve turning the
// zenith under it.
public float4 along(AirPush air, float height, float3 way, float distance)
{
    float3 point = float3(0.0, air.planet.x + height, 0.0) + way * distance;
    float radius = length(point);
    return float4(point / radius, radius - air.planet.x);
}

// The share of the light that crosses the air from a height toward a way of cosine mu with the
// zenith, out to the top, or none where the way meets the ground, summed over 64 steps that
// lengthen as the square of their count, as Atmosphere.Transmittance sums them.
public float3 transmittance(AirPush air, float height, float mu)
{
    float2 d = distances(air, height, mu);
    if (d.x >= 0.0) return 0.0;
    float3 depth = 0.0;
    const uint Steps = 64;
    for (uint i = 0; i < Steps; i++)
    {
        float from = float(i) / Steps, to = float(i + 1) / Steps;
        float near = from * from * d.y, far = to * to * d.y;
        float4 at = along(air, height, float3(sqrt(max(0.0, 1.0 - mu * mu)), mu, 0.0), (near + far) * 0.5);
        depth += extinctionAt(air, at.w) * (far - near);
    }
    return exp(-depth);
}

// Where a height and a cosine with the zenith fall in the transmittance table, its heights crowded
// near the ground as the square of their row, each texel's middle at its grid point.
public float2 transmittanceUv(AirPush air, float height, float mu)
{
    float row = sqrt(saturate(height / air.planet.y)) * (TransmittanceHeights - 1);
    float column = (clamp(mu, -1.0, 1.0) + 1.0) * 0.5 * (TransmittanceAngles - 1);
    return float2((column + 0.5) / TransmittanceAngles, (row + 0.5) / TransmittanceHeights);
}

// Where a height and the sun's cosine with the zenith fall in the table of light scattered more
// than once, its heights even.
public float2 scatterUv(AirPush air, float height, float mu)
{
    float row = saturate(height / air.planet.y) * (ScatterHeights - 1);
    float column = (clamp(mu, -1.0, 1.0) + 1.0) * 0.5 * (ScatterAngles - 1);
    return float2((column + 0.5) / ScatterAngles, (row + 0.5) / ScatterHeights);
}

// The integral of the transmittance over a step, (1 - e^(-sigma l)) / sigma, or the length where
// the air takes nearly nothing.
public float3 span(float3 extinction, float length)
{
    float3 t = extinction * length;
    return select(t < 1e-5, length, (1.0 - exp(-t)) / max(extinction, 1e-30));
}

// The molecules' phase function, 3 / (16 pi) (1 + cos^2).
public float rayleighPhase(float c) { return 3.0 / (16.0 * Pi) * (1.0 + c * c); }

// Cornette and Shanks's phase function for the aerosols.
public float aerosolPhase(float g, float c)
{
    return 3.0 / (8.0 * Pi) * (1.0 - g * g) * (1.0 + c * c) / ((2.0 + g * g) * pow(max(1.0 + g * g - 2.0 * g * c, 1e-6), 1.5));
}
