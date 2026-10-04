// The bloom chain, each pass a triangle over its whole target. A downsample reads thirteen texels
// of the level above in Jimenez's pattern, which keeps a small bright point from flickering as it
// moves, and the first one keeps only the light past the threshold. An upsample reads the level
// below through a tent and is added onto the level above it, so the first level ends up holding
// the light spread over every size.
import fullscreen;

struct BloomPush
{
    // xy: one texel of the source in uv. z: the threshold. w: 0 for the first downsample, 1 for
    // the others, 2 for an upsample.
    float4 source;
};

[[vk::push_constant]]
ConstantBuffer<BloomPush> push;

[[vk::binding(0, 0)]]
Sampler2D source;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float3 at(float2 uv, float x, float y)
{
    return source.SampleLevel(uv + push.source.xy * float2(x, y), 0).rgb;
}

// Five overlapping boxes of four texels read by bilinear taps, the middle box weighted half and the
// four around it an eighth each, which keeps the result steady as light moves across texels.
float3 downsample(float2 uv)
{
    float3 inner = at(uv, -1, -1) + at(uv, 1, -1) + at(uv, -1, 1) + at(uv, 1, 1);
    float3 cross = at(uv, 0, -2) + at(uv, -2, 0) + at(uv, 2, 0) + at(uv, 0, 2);
    float3 corners = at(uv, -2, -2) + at(uv, 2, -2) + at(uv, -2, 2) + at(uv, 2, 2);
    return at(uv, 0, 0) * 0.125 + inner * 0.125 + cross * 0.0625 + corners * 0.03125;
}

// The light past the threshold, easing in over a tenth of it so the edge of what blooms is soft
// rather than a step. The scene's light is held under 256 first, so a half float that overflowed
// to infinity cannot spread through the chain.
float3 brightPass(float3 c, float threshold)
{
    c = min(c, 256.0);
    float peak = max(c.r, max(c.g, c.b));
    float knee = max(threshold * 0.1, 1e-4);
    float soft = clamp(peak - threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee);
    return c * (max(soft, peak - threshold) / max(peak, 1e-4));
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    if (push.source.w > 1.5)
        return float4(tent(source, input.uv, push.source.xy), 1.0);
    float3 color = downsample(input.uv);
    if (push.source.w < 0.5)
        color = brightPass(color, push.source.z);
    return float4(color, 1.0);
}
