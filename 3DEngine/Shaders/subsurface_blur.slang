// Subsurface scattering over the window's frame, the light that enters a surface leaving it
// nearby, as Jimenez's separable screen-space scattering spreads it: the diffuse light of the
// pixels subsurface.slang marked is spread across, then down, each of red, green and blue by a
// Gaussian whose width is its share of the material's radius over three, so a channel that travels
// farther blurs farther, and the pass down adds the spread light in place of the light that was
// there to the decoded frame. A tap that is not marked, or that lies farther from the eye than the
// radius from the pixel's distance, is left out, so the spread never reaches an unmarked surface
// nor crosses a depth edge, and an unmarked pixel is never touched. The radius is in world units,
// turned into pixels at each pixel's distance from the eye.
import fullscreen;

struct BlurPush
{
    // Clip space to the world, for each pixel's point from the depth.
    float4x4 inverseViewProjection;
    // xyz: the eye. w: the share of the picture's height a world unit spans a unit from the eye.
    float4 eyeAndHeight;
    // xy: one texel in uv. zw: the way spread, (1, 0) across or (0, 1) down.
    float4 texelAndWay;
    // x: 0 to spread across into an image of its own, 1 to spread down and add the change to the
    // frame. y: the taps along the way, nine or seventeen by the quality. zw: unused.
    float4 mode;
};

[[vk::push_constant]]
ConstantBuffer<BlurPush> push;

// The marked pixels' diffuse light, 1 in alpha where a pixel is marked.
[[vk::binding(0, 0)]]
Sampler2D diffuse;

// Each marked pixel's share of the radius for red, green and blue, and the radius in alpha.
[[vk::binding(1, 0)]]
Sampler2D profile;

// The scene's depth.
[[vk::binding(2, 0)]]
Sampler2D depth;

// What the pass spreads, the diffuse light across and the light spread across down.
[[vk::binding(3, 0)]]
Sampler2D source;

// The light that comes through the marked surfaces from behind, which the pass across spreads with
// the diffuse light, so the frame, which holds the diffuse light alone, gains it spread.
[[vk::binding(4, 0)]]
Sampler2D through;


[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

// How far the scene's point at uv lies from the eye.
float distanceAt(float2 uv)
{
    float4 world = mul(push.inverseViewProjection, float4(uv * 2.0 - 1.0, depth.SampleLevel(uv, 0).r, 1.0));
    return length(world.xyz / world.w - push.eyeAndHeight.xyz);
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    float2 uv = input.uv;
    float4 here = diffuse.SampleLevel(uv, 0);
    if (here.a <= 0.0)
        discard;
    float4 shape = profile.SampleLevel(uv, 0);
    float radius = shape.a;
    // Each channel's width, a third of its share of the radius, so it fades out at the radius.
    float3 widths = max(radius * saturate(shape.rgb) / 3.0, float3(1e-6));
    float reach = 3.0 * max(max(widths.r, widths.g), widths.b);
    float distance = distanceAt(uv);
    // The uv a world unit spans along the way at this distance, from the share of the picture's
    // height a unit spans a unit from the eye, the picture's height in pixels and a texel's uv.
    float pixels = push.eyeAndHeight.w / max(distance, 1e-4) / push.texelAndWay.y;
    float2 perUnit = push.texelAndWay.zw * push.texelAndWay.xy * pixels;

    // Taps along the way, the middle one the pixel's own, out to three widths of the widest channel.
    int taps = int(push.mode.y + 0.5);
    float3 sum = float3(0.0);
    float3 weights = float3(0.0);
    for (int i = 0; i < taps; i++)
    {
        float x = (float(i) / float(taps - 1) * 2.0 - 1.0) * reach;
        float2 at = uv + perUnit * x;
        if (i != taps / 2)
        {
            if (any(at < float2(0.0)) || any(at > float2(1.0)) || diffuse.SampleLevel(at, 0).a <= 0.0)
                continue;
            if (abs(distanceAt(at) - distance) > radius)
                continue;
        }
        float3 weight = exp(-x * x / (2.0 * widths * widths));
        float3 tap = source.SampleLevel(at, 0).rgb;
        if (push.mode.x < 0.5)
            tap += through.SampleLevel(at, 0).rgb;
        sum += weight * tap;
        weights += weight;
    }
    float3 spread = sum / max(weights, float3(1e-6));
    if (push.mode.x < 0.5)
        return float4(spread, 1.0);
    // Added to the frame, which held the pixel's own diffuse light and none of what comes through,
    // so it holds the spread light.
    return float4(spread - here.rgb, 0.0);
}
