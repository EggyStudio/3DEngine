// What the light that bounces holds, drawn over the window to be looked at (gi.show,
// DrawBounceWindow): the screen's probes as tiles over the picture, each a dot of its light; the
// screen's probes' light, as their rays brought it and as filtered, a block a probe; how much of
// each probe's light the frame before's gave, red for none to green for the most; a cascade of the
// world's probes, its rays' light or its merge, each layer of probes a square of their
// octahedrons; and the window's light against a path-traced reference, red where the frame is
// brighter and blue where it is darker, by the share of the reference's light. Light is shown
// through x / (1 + x), so light past white stays told apart.
import color;
import fullscreen;

struct ViewPush
{
    // x: the view, as BounceView numbers it: 1 the tiles, 2 the screen's light, 3 filtered, 4 the
    // frame before's share, 5 a cascade's rays, 6 its merge, 8 the difference. y: a cascade's
    // probes along each side. z: its octahedron's texels along each side. w: unused.
    uint4 mode;
    // x: a screen tile's pixels along each side. yzw: unused.
    float4 grid;
    // xy: the window's pixels. zw: unused.
    float4 window;
};

[[vk::push_constant]]
ConstantBuffer<ViewPush> push;

// The screen's probes' light shown, a texel a probe, filtered with in its alpha one more than the
// frame before's share.
[[vk::binding(0, 0)]]
Sampler2D plane;

// A cascade's rays' light or its merge, its probes' octahedrons side by side, a layer a slice.
[[vk::binding(1, 0)]]
Texture3D<float4> volume;

// The window's light, decoded.
[[vk::binding(2, 0)]]
Sampler2D frame;

// The reference's light.
[[vk::binding(3, 0)]]
Sampler2D reference;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float3 shown(float3 linear)
{
    return linearToSrgb(max(linear, float3(0.0)) / (1.0 + max(linear, float3(0.0))));
}

float luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    float2 pixel = input.uv * push.window.xy;
    uint mode = push.mode.x;
    uint width, height;
    plane.GetDimensions(width, height);
    float tile = push.grid.x;
    uint2 probe = min(uint2(pixel / tile), uint2(width - 1u, height - 1u));
    float4 light = plane.SampleLevel((float2(probe) + 0.5) / float2(width, height), 0);

    if (mode == 1u)
    {
        // A tile's edges and a dot of its probe's light in its middle, the picture between.
        float2 within = pixel - float2(probe) * tile;
        if (within.x < 1.0 || within.y < 1.0)
            return float4(0.0, 0.0, 0.0, 0.6);
        if (length(within - tile * 0.5) > tile * 0.25)
            discard;
        return light.a > 0.5 ? float4(shown(light.rgb), 1.0) : float4(0.3, 0.0, 0.3, 1.0);
    }
    if (mode == 2u || mode == 3u)
        return light.a > 0.5 ? float4(shown(light.rgb), 1.0) : float4(0.0, 0.0, 0.0, 1.0);
    if (mode == 4u)
    {
        // The frame before's share, four fifths at most, over a little of the probe's light.
        float taken = saturate((light.a - 1.0) / 0.8);
        float3 share = lerp(float3(0.85, 0.1, 0.1), float3(0.1, 0.85, 0.1), taken);
        return light.a > 0.5 ? float4(share * 0.75 + shown(light.rgb) * 0.25, 1.0) : float4(0.0, 0.0, 0.0, 1.0);
    }
    if (mode == 5u || mode == 6u)
    {
        // The layers along z in the grid of squares that makes them largest in the window, each
        // its probes along x across and along y up, so a layer reads as a slice of the scene.
        uint probes = max(push.mode.y, 1u);
        uint texels = max(push.mode.z, 1u);
        uint side = probes * texels;
        uint columns = 1u;
        float square = 0.0;
        for (uint tried = 1u; tried <= probes; tried++)
        {
            float fits = min(push.window.x / float(tried), push.window.y / float((probes + tried - 1u) / tried));
            if (fits > square)
            {
                columns = tried;
                square = fits;
            }
        }
        uint2 cell = uint2(pixel / square);
        uint layer = cell.y * columns + cell.x;
        if (cell.x >= columns || layer >= probes)
            return float4(0.0, 0.0, 0.0, 1.0);
        uint2 texel = min(uint2((pixel - float2(cell) * square) / square * float(side)), uint2(side - 1u));
        texel.y = (probes - 1u - texel.y / texels) * texels + texel.y % texels;
        float4 held = volume.Load(int4(int2(texel), int(layer), 0));
        return float4(shown(held.rgb), 1.0);
    }
    // The frame against the reference.
    float3 f = frame.SampleLevel(input.uv, 0).rgb;
    float3 r = reference.SampleLevel(input.uv, 0).rgb;
    float lr = luminance(r);
    float share = (luminance(f) - lr) / (lr + 0.05);
    // As gi.compare draws it, over the reference's light dimmed.
    float backdrop = min(sqrt(max(lr, 0.0)) * 60.0, 60.0) / 255.0;
    return float4(backdrop + saturate(share), backdrop, backdrop + saturate(-share), 1.0);
}
