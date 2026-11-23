// The picture brought onto the window in the window's pass (GraphicsDevice.Picture), before the
// interface is drawn over it. The triangle covers the part of the window the picture is drawn into,
// and each window pixel reads the picture at its own place, by the filter. Linear blends the four
// picture pixels round the place, and is what the picture takes wherever it is brought down.
// Nearest takes the picture pixel the place is in. Sharp takes a window pixel inside a picture
// pixel from it whole, and one across a picture pixel's edge the next one's share of its own
// width, so every picture pixel is a block of even color with one blended window pixel at most on
// an edge, the blocks all of one width. It is what the linear filter gives with each sample moved
// toward its picture pixel's middle by the ratio, worked out here and read with four loads, since
// a sampler's weights are fixed point and a sample a float's step from a pixel's middle would take
// a 256th of the next one, where a whole ratio takes none and so equals the nearest.
import fullscreen;

[[vk::binding(0, 0)]]
Texture2D frame;

struct PicturePush
{
    // Where the picture's corner stands on the window, in window pixels.
    float2 corner;
    // The window pixels a picture pixel spans along each axis.
    float2 ratio;
    // 0 for linear, 1 for nearest and 2 for sharp.
    uint filter;
};

[[vk::push_constant]]
ConstantBuffer<PicturePush> push;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    uint width, height;
    frame.GetDimensions(width, height);
    int2 last = int2(width, height) - 1;
    float2 texel = (input.position.xy - push.corner) / push.ratio;
    if (push.filter == 1u)
        return frame.Load(int3(clamp(int2(floor(texel)), int2(0), last), 0));
    float2 middle;
    float2 share;
    if (push.filter == 2u)
    {
        middle = floor(texel);
        float2 fromMiddle = texel - middle - 0.5;
        // The share of the window pixel past the picture pixel's edge, which a float's error at a
        // whole ratio leaves a little over none, taken as none under half the filter's step.
        share = max(abs(fromMiddle) - (0.5 - 0.5 / push.ratio), float2(0.0)) * push.ratio;
        share = select(share < float2(1.0 / 512.0), float2(0.0), share);
        int2 at = clamp(int2(middle), int2(0), last);
        int2 next = clamp(at + int2(sign(fromMiddle)), int2(0), last);
        float4 top = lerp(frame.Load(int3(at, 0)), frame.Load(int3(next.x, at.y, 0)), share.x);
        float4 bottom = lerp(frame.Load(int3(at.x, next.y, 0)), frame.Load(int3(next, 0)), share.x);
        return lerp(top, bottom, share.y);
    }
    // The four picture pixels whose middles stand round the place, each by its nearness.
    float2 below = floor(texel - 0.5);
    share = texel - 0.5 - below;
    int2 first = clamp(int2(below), int2(0), last);
    int2 second = clamp(int2(below) + 1, int2(0), last);
    float4 upper = lerp(frame.Load(int3(first, 0)), frame.Load(int3(second.x, first.y, 0)), share.x);
    float4 lower = lerp(frame.Load(int3(first.x, second.y, 0)), frame.Load(int3(second, 0)), share.x);
    return lerp(upper, lower, share.y);
}
