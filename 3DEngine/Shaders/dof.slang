// Depth of field over the HDR frame. Each pixel's distance from the eye is found from its depth
// through the inverse of the camera's view-projection, and how far it is from the focus, over the
// focus range, sets how wide its blur is, up to the largest the lens allows. The blur gathers 32
// taps on a spiral out to that largest radius, and a tap counts where its own blur reaches this
// pixel, so a blurred foreground spreads over a sharp background behind it. A tap farther than the
// pixel counts no wider than the pixel's own blur, so a blurred background never spreads over a
// sharp foreground.
import fullscreen;

struct DofPush
{
    // Clip space to the world, as the model pass takes a view-projection.
    float4x4 inverseViewProjection;
    // xyz: the eye. w: the distance in focus.
    float4 eyeAndFocus;
    // x: the distance either side of the focus over which the blur grows to its widest. y: the
    // widest blur's radius, as a share of the picture's height. z: the picture's width over its
    // height. w: its height in pixels.
    float4 lens;
};

[[vk::push_constant]]
ConstantBuffer<DofPush> push;

[[vk::binding(0, 0)]]
Sampler2D scene;

[[vk::binding(1, 0)]]
Sampler2D depth;

static const int Taps = 32;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

// How far from the eye the surface at uv is, the nearest of the texels around it, so a pixel on
// the edge of a thing, which multisampling gave part of the thing's color and the depth of what is
// behind it, counts as the thing and does not spread its color as the background would. Two
// gathers on the corners of the texel at uv cover the texels on every side of it, where gathers
// at uv itself leaned toward one side and missed a thing below an edge.
float distanceAt(float2 uv)
{
    float2 size = float2(push.lens.z * push.lens.w, push.lens.w);
    float2 corner = floor(uv * size);
    float4 a = depth.Gather(corner / size);
    float4 b = depth.Gather((corner + 1.0) / size);
    float d = min(min(min(a.x, a.y), min(a.z, a.w)), min(min(b.x, b.y), min(b.z, b.w)));
    float4 world = mul(push.inverseViewProjection, float4(uv * 2.0 - 1.0, d, 1.0));
    return length(world.xyz / world.w - push.eyeAndFocus.xyz);
}

// How wide the blur is at a distance, as a radius in uv of the picture's height.
float blurAt(float distance)
{
    return saturate(abs(distance - push.eyeAndFocus.w) / max(push.lens.x, 1e-4)) * push.lens.y;
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    float here = distanceAt(input.uv);
    float own = blurAt(here);
    float widest = push.lens.y;
    float2 aspect = float2(1.0 / push.lens.z, 1.0);

    float3 sum = scene.SampleLevel(input.uv, 0).rgb;
    float weights = 1.0;
    // The golden angle turns each tap from the one before, so the spiral covers the disc evenly.
    for (int i = 0; i < Taps; i++)
    {
        float r = widest * sqrt((float(i) + 0.5) / float(Taps));
        float angle = float(i) * 2.39996323;
        float2 uv = input.uv + float2(cos(angle), sin(angle)) * r * aspect;
        float there = distanceAt(uv);
        float reach = blurAt(there);
        if (there > here)
            reach = min(reach, own);
        // Counted once its blur reaches past the tap's distance, fully a pixel further.
        float weight = saturate((reach - r) * push.lens.w);
        sum += scene.SampleLevel(uv, 0).rgb * weight;
        weights += weight;
    }
    return float4(sum / weights, 1.0);
}
