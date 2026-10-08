// The window's scene decoded to linear light, texel for texel, with its depth beside it, for every
// pass that reads the scene's light and for the particles drawn over it. The scene is drawn
// sRGB-encoded and carried on past 1, so its blending and multisampling are done on encoded light
// as raylib's are, and it is decoded here before any filter reads it, since a filter across an edge
// before decoding would dim what is bright beside what is dark.
import color;
import fullscreen;

[[vk::binding(0, 0)]]
Sampler2D scene;

// The scene's depth, written as the target's own so the particles drawn after are hidden by it.
[[vk::binding(1, 0)]]
Sampler2D sceneDepth;

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
    output.color = float4(srgbToLinear(scene.SampleLevel(input.uv, 0).rgb), 1.0);
    output.depth = sceneDepth.SampleLevel(input.uv, 0).r;
    return output;
}
