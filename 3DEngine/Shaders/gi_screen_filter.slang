// Blends each screen probe's light with the probes around it that stand on a surface like its own,
// a thread a probe, five by five of them, each weighed by how near it is and how alike its normal and
// its distance from the eye are to the probe's, so the few rays each probe sends average over many
// and the tiles the window is cut into do not show.

// The light each probe's rays summed, and each probe's surface, as gi_screen.slang left them.
[[vk::binding(0, 0)]]
Texture2D<float4> irradiance;

[[vk::binding(1, 0)]]
Texture2D<float4> geometry;

[[vk::binding(2, 0)]] [format("rgba16f")]
RWTexture2D<float4> blended;

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint across, down;
    irradiance.GetDimensions(across, down);
    if (id.x >= across || id.y >= down)
        return;
    float4 own = irradiance.Load(int3(id.xy, 0));
    float4 surface = geometry.Load(int3(id.xy, 0));
    if (own.a < 0.5 || surface.w < 0.0)
    {
        blended[id.xy] = own;
        return;
    }
    float4 sum = float4(0.0);
    for (int y = -2; y <= 2; y++)
        for (int x = -2; x <= 2; x++)
        {
            int2 at = clamp(int2(id.xy) + int2(x, y), int2(0), int2(int(across) - 1, int(down) - 1));
            float4 light = irradiance.Load(int3(at, 0));
            float4 other = geometry.Load(int3(at, 0));
            if (light.a < 0.5 || other.w < 0.0)
                continue;
            float weight = exp(-float(x * x + y * y) / 4.0) * pow(saturate(dot(surface.xyz, other.xyz)), 16.0)
                         * exp(-abs(other.w - surface.w) / (0.02 * surface.w + 0.05));
            sum += float4(light.rgb, 1.0) * weight;
        }
    blended[id.xy] = float4(sum.rgb / max(sum.w, 1e-5), 1.0);
}
