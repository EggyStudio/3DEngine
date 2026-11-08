// The bloom chain, each pass a triangle over its whole target. A downsample reads thirteen texels
// of the level above in Jimenez's pattern, which keeps a small bright point from flickering as it
// moves, and the first one keeps only the light past the threshold. An upsample reads the level
// below through a tent and is added onto the level above it, so the first level ends up holding
// the light spread over every size, or with a scatter, as Unity's HDRP spreads it, takes that
// share of the level above's place, so each level wider down the chain holds less of the light.
import fullscreen;

struct BloomPush
{
    // xy: one texel of the source in uv. z: the threshold. w: 0 for the first downsample, 1 for
    // the others, 2 for an upsample.
    float4 source;
    // x: the share of the threshold the light eases in over. y: 1 where the threshold is times the
    // exposure that follows the scene, as the adapted texel holds it. z: an upsample's scatter, the
    // share of the level above's light it takes the place of, or 0 where it is added. w: white
    // after the exposure as the light before it, times the adapted luminance as the threshold is,
    // for a scattered bloom, 0 to hold the light under 256 itself.
    float4 spread;
};

[[vk::push_constant]]
ConstantBuffer<BloomPush> push;

[[vk::binding(0, 0)]]
Sampler2D source;

// The log2 of the scene's mean luminance as the exposure that follows it has adapted to it, in one
// texel, exposure.slang's.
[[vk::binding(1, 0)]]
Sampler2D adapted;

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

// The light past the threshold, easing in over a share of it so the edge of what blooms is soft
// rather than a step. The scene's light is held under `cap` first, 256 times white as the exposure
// leaves it and below half floats' last value, 65,504, so one that overflowed to infinity cannot
// spread through the chain, and a small bright source blooms alike however far the exposure lifts
// the scene.
float3 brightPass(float3 c, float threshold, float share, float cap)
{
    c = min(c, cap);
    float peak = max(c.r, max(c.g, c.b));
    float knee = max(threshold * share, 1e-4);
    float soft = clamp(peak - threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee);
    return c * (max(soft, peak - threshold) / max(peak, 1e-4));
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    if (push.source.w > 1.5)
        return float4(tent(source, input.uv, push.source.xy), push.spread.z > 0.0 ? push.spread.z : 1.0);
    float3 color = downsample(input.uv);
    if (push.source.w < 0.5)
    {
        // The threshold of light after the exposure, as the light before it, and a scattered
        // bloom's light held under 256 times white after the exposure. Held at 256 before the
        // exposure, the moon a metered night lifts fifteen stops bloomed at some 1,900 times white
        // and the sun at 9:00 in Sponza's court looking up whitened the frame, where the sun at
        // noon, the exposure lifting the scene less, bloomed as a disc with a halo about it.
        float threshold = push.source.z;
        float white = push.spread.w;
        if (push.spread.y > 0.0)
        {
            float mean = exp2(adapted.Load(int3(0, 0, 0)).r);
            threshold *= mean;
            white *= mean;
        }
        color = brightPass(color, threshold, push.spread.x, white > 0.0 ? min(256.0 * white, 60000.0) : 256.0);
    }
    return float4(color, 1.0);
}
