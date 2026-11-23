// The frame brought onto the window's larger image by the sharp scaling, in one pass at the frame's
// end (GraphicsDevice.Sharp). The triangle covers the part of the window the frame is drawn into,
// and each window pixel reads the frame at its own place. A window pixel inside a frame pixel takes
// it whole, and one across a frame pixel's edge takes the next one's share of its own width, so
// every frame pixel is a block of even color with one blended window pixel at most on an edge, the
// blocks all of one width. It is what the linear filter gives with each sample moved toward its
// frame pixel's middle by the ratio, worked out here and read with four loads, since the filter's
// weights are fixed point and a sample a float's step from a frame pixel's middle would take a
// 256th of the next one, where a whole ratio takes none and so equals the nearest scaling.
import fullscreen;

[[vk::binding(0, 0)]]
Texture2D frame;

struct SharpPush
{
    // Where the frame's corner stands on the window, in window pixels.
    float2 corner;
    // The window pixels a frame pixel spans along each axis.
    float2 ratio;
};

[[vk::push_constant]]
ConstantBuffer<SharpPush> push;

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
    float2 texel = (input.position.xy - push.corner) / push.ratio;
    float2 middle = floor(texel);
    float2 fromMiddle = texel - middle - 0.5;
    // The share of the window pixel past the frame pixel's edge, which a float's error at a whole
    // ratio leaves a little over none, taken as none under half the filter's step.
    float2 share = max(abs(fromMiddle) - (0.5 - 0.5 / push.ratio), float2(0.0)) * push.ratio;
    share = select(share < float2(1.0 / 512.0), float2(0.0), share);
    int2 last = int2(width, height) - 1;
    int2 at = clamp(int2(middle), int2(0), last);
    int2 next = clamp(at + int2(sign(fromMiddle)), int2(0), last);
    float4 top = lerp(frame.Load(int3(at, 0)), frame.Load(int3(next.x, at.y, 0)), share.x);
    float4 bottom = lerp(frame.Load(int3(at.x, next.y, 0)), frame.Load(int3(next, 0)), share.x);
    return lerp(top, bottom, share.y);
}
