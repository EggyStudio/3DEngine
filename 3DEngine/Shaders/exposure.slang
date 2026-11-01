// Exposure that follows the scene, in two passes over a triangle each. The first measures the HDR
// frame into a small target, each texel the mean of the log luminance over its part of the frame.
// The second, one texel, weights those toward the middle of the picture, where a player looks,
// and moves the last frame's value toward what it measured by the share of the way the push
// gives, one where the scene brightened and another where it darkened, held between the brightest
// and darkest the exposure is let reach. Both keep the log2 of the luminance, since the eye adapts
// by ratios and a mean of logs is not pulled up by one lamp. Where the push leaves out the darkest
// and the brightest shares of the weighted picture, as HDRP's histogram metering does, the mean is
// of what lies between, binned by half a stop, so a sky in a corner of the view does not dim it.
import fullscreen;

struct ExposurePush
{
    // x: 0 to measure, 1 to adapt weighing the whole picture and 2 its middle. y: the share of the
    // way the value moves this frame where the scene brightened, 1 for all of it. zw: the lowest and
    // highest log2 luminance it is held between.
    float4 mode;
    // xy: the shares of the weighted picture, from its darkest, it measures between, 0 and 1 for
    // all of it. z: the share of the way the value moves where the scene darkened. w: unused.
    float4 kept;
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

// The log2 luminance the histogram's bins span, from far under a starlit night to past the sun's
// disc, half a stop a bin.
static const int Bins = 64;
static const float BinsFrom = -16.0, BinsTo = 16.0;

float weightAt(int x, int y)
{
    float2 at = (float2(x, y) + 0.5) / float(Measured) - 0.5;
    // The middle counts four times as much as the edges, or, center-weighted, most of all,
    // falling as a bell curve a fifth of the picture wide.
    return push.mode.x > 1.5 ? exp(-dot(at, at) / (2.0 * 0.2 * 0.2)) : 1.0 - 0.75 * saturate(length(at) * 1.41421356);
}

// The weighted mean of the log luminance of the part of the picture between the kept shares,
// each bin standing for its middle.
float keptMean()
{
    float bins[Bins];
    for (int b = 0; b < Bins; b++)
        bins[b] = 0.0;
    float weights = 0.0;
    for (int y = 0; y < Measured; y++)
        for (int x = 0; x < Measured; x++)
        {
            float weight = weightAt(x, y);
            float at = (source.Load(int3(x, y, 0)).r - BinsFrom) / (BinsTo - BinsFrom) * float(Bins);
            bins[clamp(int(at), 0, Bins - 1)] += weight;
            weights += weight;
        }
    float from = weights * push.kept.x, to = weights * push.kept.y;
    float below = 0.0, sum = 0.0, kept = 0.0;
    for (int b = 0; b < Bins; b++)
    {
        float inside = max(min(below + bins[b], to) - max(below, from), 0.0);
        sum += inside * (BinsFrom + (float(b) + 0.5) * (BinsTo - BinsFrom) / float(Bins));
        kept += inside;
        below += bins[b];
    }
    return kept > 0.0 ? sum / kept : 0.0;
}

float adapt()
{
    float measured;
    if (push.kept.x > 0.0 || push.kept.y < 1.0)
        measured = keptMean();
    else
    {
        float sum = 0.0, weights = 0.0;
        for (int y = 0; y < Measured; y++)
            for (int x = 0; x < Measured; x++)
            {
                float weight = weightAt(x, y);
                sum += source.Load(int3(x, y, 0)).r * weight;
                weights += weight;
            }
        measured = sum / weights;
    }
    measured = clamp(measured, push.mode.z, push.mode.w);
    if (push.mode.y >= 1.0)
        return measured;
    float last = previous.Load(int3(0, 0, 0)).r;
    return lerp(last, measured, measured >= last ? push.mode.y : push.kept.z);
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    float value = push.mode.x < 0.5 ? measure(input.uv) : adapt();
    return float4(value, 0.0, 0.0, 1.0);
}
