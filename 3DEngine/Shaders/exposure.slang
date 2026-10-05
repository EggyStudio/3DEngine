// Exposure that follows the scene, in two passes over a triangle each. The first measures the HDR
// frame into a small target, each texel the mean of the log luminance over its part of the frame.
// The second, one texel, weights those toward the middle of the picture, where a player looks,
// and moves the last frame's value toward what it measured by the share of the way the push
// gives, held between the brightest and darkest the exposure is let reach. Both keep the log2 of
// the luminance, since the eye adapts by ratios and a mean of logs is not pulled up by one lamp.
import fullscreen;

struct ExposurePush
{
    // x: 0 to measure, 1 to adapt. y: the share of the way the value moves this frame, 1 for all
    // of it. zw: the lowest and highest log2 luminance it is held between.
    float4 mode;
};

[[vk::push_constant]]
ConstantBuffer<ExposurePush> push;

// The HDR frame while measuring, the measured texels while adapting.
[[vk::binding(0, 0)]]
Sampler2D source;

// The value adapted the frame before.
[[vk::binding(1, 0)]]
Sampler2D previous;

static const int Measured = 64;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float logLuminance(float3 c)
{
    // Held under 256 as the bloom chain holds it, so a half float that overflowed cannot reach
    // the mean, and above a floor so black does not pull it to minus infinity.
    float y = dot(min(c, 256.0), float3(0.2126, 0.7152, 0.0722));
    return log2(max(y, 1.0 / 4096.0));
}

// Sixteen bilinear taps spread over the part of the frame this texel stands for.
float measure(float2 uv)
{
    float2 span = 1.0 / float(Measured);
    float2 corner = uv - span * 0.5;
    float sum = 0.0;
    for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
            sum += logLuminance(source.SampleLevel(corner + span * (float2(x, y) + 0.5) / 4.0, 0).rgb);
    return sum / 16.0;
}

float adapt()
{
    float sum = 0.0, weights = 0.0;
    for (int y = 0; y < Measured; y++)
        for (int x = 0; x < Measured; x++)
        {
            float2 at = (float2(x, y) + 0.5) / float(Measured) - 0.5;
            // The middle counts four times as much as the edges.
            float weight = 1.0 - 0.75 * saturate(length(at) * 1.41421356);
            sum += source.Load(int3(x, y, 0)).r * weight;
            weights += weight;
        }
    float measured = clamp(sum / weights, push.mode.z, push.mode.w);
    if (push.mode.y >= 1.0)
        return measured;
    float last = previous.Load(int3(0, 0, 0)).r;
    return lerp(last, measured, push.mode.y);
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    float value = push.mode.x < 0.5 ? measure(input.uv) : adapt();
    return float4(value, 0.0, 0.0, 1.0);
}
