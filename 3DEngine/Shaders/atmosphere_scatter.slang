// The air's light scattered twice or more at a height with the sun at a cosine with the zenith,
// for a sun of 1 outside the air, as Hillaire's approximation of 2020 gathers it over 64 ways
// spread evenly over the sphere, each marched in 20 even steps, the second order's light divided
// by one less the share the air sends on, as the Sky module's SkyLight makes its table.
import atmosphere;

[[vk::binding(0, 0)]] Sampler2D transmittanceTable;
[[vk::binding(1, 0)]] [format("rgba16f")] RWTexture2D<float4> table;
[[vk::push_constant]] ConstantBuffer<AirPush> push;

float3 transmittanceAt(float height, float mu)
{
    return transmittanceTable.SampleLevel(transmittanceUv(push, height, mu), 0).rgb;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= ScatterAngles || id.y >= ScatterHeights)
        return;
    float height = push.planet.y * float(id.y) / (ScatterHeights - 1);
    float mu = -1.0 + 2.0 * float(id.x) / (ScatterAngles - 1);
    float3 toSun = float3(sqrt(max(0.0, 1.0 - mu * mu)), mu, 0.0);
    const float Isotropic = 1.0 / (4.0 * Pi);
    const uint Ways = 8, Steps = 20;
    float3 second = 0.0, sent = 0.0;
    for (uint i = 0; i < Ways; i++)
    {
        for (uint j = 0; j < Ways; j++)
        {
            float c = 1.0 - 2.0 * (float(i) + 0.5) / Ways;
            float s = sqrt(1.0 - c * c);
            float turn = 2.0 * Pi * (float(j) + 0.5) / Ways;
            float3 way = float3(s * cos(turn), c, s * sin(turn));
            float2 d = distances(push, height, c);
            float step = (d.x >= 0.0 ? d.x : d.y) / Steps;
            float3 light = 0.0, onward = 0.0, through = 1.0;
            for (uint k = 0; k < Steps; k++)
            {
                float4 at = along(push, height, way, (float(k) + 0.5) * step);
                float3 scattering = rayleighAt(push, at.w) + aerosolScatteringAt(push, at.w);
                float3 extinction = extinctionAt(push, at.w);
                float3 within = span(extinction, step);
                light += through * scattering * transmittanceAt(at.w, dot(at.xyz, toSun)) * Isotropic * within;
                onward += through * scattering * within;
                through *= exp(-extinction * step);
            }
            if (d.x >= 0.0)
            {
                float4 at = along(push, height, way, d.x);
                float sunZenith = dot(at.xyz, toSun);
                light += through * transmittanceAt(0.0, sunZenith) * max(sunZenith, 0.0) * push.ground.rgb / Pi;
            }
            // Each way's share of the sphere times the phase function alike everywhere, 1 / N.
            second += light / (Ways * Ways);
            sent += onward / (Ways * Ways);
        }
    }
    table[id.xy] = float4(second / (1.0 - sent), 1.0);
}
