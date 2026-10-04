// The HDR frame onto the window: the scene's light with the bloom chain's added, tonemapped and
// encoded as the model pass does it with bloom off, so only the bloom tells the two frames apart.
import color;
import fullscreen;

struct CompositePush
{
    // xy: one texel of the bloom's first level in uv. z: how much of it is added.
    float4 bloom;
};

[[vk::push_constant]]
ConstantBuffer<CompositePush> push;

[[vk::binding(0, 0)]]
Sampler2D scene;

[[vk::binding(1, 0)]]
Sampler2D bloom;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    float3 light = scene.SampleLevel(input.uv, 0).rgb;
    float3 spread = tent(bloom, input.uv, push.bloom.xy);
    return float4(linearToSrgb(tonemap(light + spread * push.bloom.z)), 1.0);
}
