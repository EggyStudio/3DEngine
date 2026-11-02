// The window's scene decoded to linear light, texel for texel, with its depth beside it, for every
// pass that reads the scene's light and for the particles drawn over it. The scene is drawn
// sRGB-encoded and carried on past 1, so its blending and multisampling are done on encoded light
// as raylib's are, and it is decoded here before any filter reads it, since a filter across an edge
// before decoding would dim what is bright beside what is dark. Where the fog is on, its light and
// the share of the scene's light through it are laid over each pixel from its depth, so every pass
// after reads the scene in fog.
import color;
import fullscreen;
import fog;

struct DecodePush
{
    // The window's clip space to the world, through the camera the scene was drawn through.
    float4x4 inverseViewProjection;
    // xyz: the eye. w: 1 where the fog is on.
    float4 eye;
    // x: the distance the fog's slices grow from. y: how far its last slice ends. zw: what a
    // pixel's place in the window is multiplied by for its place in the fog's volume, whose tiles
    // reach past the window's edges.
    float4 slices;
};

[[vk::push_constant]]
ConstantBuffer<DecodePush> push;

[[vk::binding(0, 0)]]
Sampler2D scene;

// The scene's depth, written as the target's own so the particles drawn after are hidden by it.
[[vk::binding(1, 0)]]
Sampler2D sceneDepth;

// The fog's light and the share of light through it from the eye to each slice's far edge
// (fog_integrate.slang), or a cell standing in where the fog is off.
[[vk::binding(0, 1)]]
Sampler3D fogVolume;

// The scene's light at uv, at depth from the eye, seen through the fog. A pixel nearer than the
// first slice's far edge takes that slice's in proportion, and the sky the whole volume.
float3 fogged(float3 light, float2 uv, float depth)
{
    float start = push.slices.x, far = push.slices.y;
    float distance = far;
    if (depth < 1.0)
    {
        float4 world = mul(push.inverseViewProjection, float4(uv * 2.0 - 1.0, depth, 1.0));
        distance = min(length(world.xyz / world.w - push.eye.xyz), far);
    }
    float at = sliceAt(distance, start, far);
    float4 seen = fogVolume.SampleLevel(float3(uv * push.slices.zw, saturate((at - 0.5) / float(FogSlices))), 0);
    if (at < 1.0)
        seen = lerp(float4(0.0, 0.0, 0.0, 1.0), seen, saturate(distance / sliceEdge(1.0, start, far)));
    return light * seen.a + seen.rgb;
}

struct Decoded
{
    float4 color : SV_Target;
    float depth : SV_Depth;
};

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

[shader("fragment")]
Decoded fragmentMain(FullscreenOutput input)
{
    Decoded output;
    float3 light = srgbToLinear(scene.SampleLevel(input.uv, 0).rgb);
    output.depth = sceneDepth.SampleLevel(input.uv, 0).r;
    if (push.eye.w > 0.5)
        light = fogged(light, input.uv, output.depth);
    output.color = float4(light, 1.0);
    return output;
}
