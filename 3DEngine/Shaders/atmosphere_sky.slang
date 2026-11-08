// The air's sky in an equirectangular image of linear light, longitude across from the back and
// latitude down from the top as EnvironmentMap reads one, each texel the luminance along its way
// from the ground, the sun's light scattered toward the eye once and more than once and the
// ground's where the way meets it, as the Sky module's SkyLight gives it, times the sun's light
// outside the air and what a luminance is multiplied by for the frame. The sun's own disc is left
// out, which the drawn sky adds (sky.slang), since the sun's light on a surface is the light's.
import atmosphere;

[[vk::binding(0, 0)]] Sampler2D transmittanceTable;
[[vk::binding(1, 0)]] Sampler2D scatterTable;
[[vk::binding(2, 0)]] [format("rgba16f")] RWTexture2D<float4> sky;
[[vk::push_constant]] ConstantBuffer<AirPush> push;

float3 transmittanceAt(float height, float mu)
{
    return transmittanceTable.SampleLevel(transmittanceUv(push, height, mu), 0).rgb;
}

float3 scatteredAt(float height, float mu)
{
    return scatterTable.SampleLevel(scatterUv(push, height, mu), 0).rgb;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.size.x || id.y >= push.size.y)
        return;
    float2 uv = (float2(id.xy) + 0.5) / float2(push.size.xy);
    float theta = uv.y * Pi, phi = (uv.x - 0.5) * 2.0 * Pi;
    float3 view = float3(sin(theta) * sin(phi), cos(theta), -sin(theta) * cos(phi));
    float3 toSun = normalize(push.sun.xyz);

    float2 d = distances(push, 0.0, view.y);
    float distance = d.x >= 0.0 ? d.x : d.y;
    float cosAngle = dot(view, toSun);
    float rayleighShare = rayleighPhase(cosAngle), aerosolShare = aerosolPhase(push.planet.w, cosAngle);
    float3 light = 0.0, through = 1.0;
    const uint Steps = 48;
    for (uint i = 0; i < Steps; i++)
    {
        // The steps lengthen as the square of their count, from the eye out.
        float from = float(i) / Steps, to = float(i + 1) / Steps;
        float near = from * from * distance, far = to * to * distance;
        float4 at = along(push, 0.0, view, (near + far) * 0.5);
        float sunZenith = dot(at.xyz, toSun);
        float3 rayleigh = rayleighAt(push, at.w), aerosol = aerosolScatteringAt(push, at.w);
        float3 source = (rayleigh * rayleighShare + aerosol * aerosolShare) * transmittanceAt(at.w, sunZenith)
            + (rayleigh + aerosol) * scatteredAt(at.w, sunZenith);
        float3 extinction = extinctionAt(push, at.w);
        light += through * source * span(extinction, far - near);
        through *= exp(-extinction * (far - near));
    }
    if (d.x >= 0.0)
    {
        float4 at = along(push, 0.0, view, d.x);
        float sunZenith = dot(at.xyz, toSun);
        light += through * transmittanceAt(0.0, sunZenith) * max(sunZenith, 0.0) * push.ground.rgb / Pi;
    }
    // Half floats stop at 65504.
    // Under the light the air scatters lies a moonless sky's own glow, the stars' and the air's,
    // its luminance in w, alike over the sky.
    sky[id.xy] = float4(min(light * push.light.rgb + push.light.w, 65000.0), 1.0);
}
