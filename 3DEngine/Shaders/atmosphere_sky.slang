// The air's sky in an equirectangular image of linear light, longitude across from the back and
// latitude down from the top as EnvironmentMap reads one, each texel the luminance along its way
// from the ground as the Sky module's SkyLight gives it from its tables (atmosphere.slang), times
// the frame's light and what a luminance is multiplied by for the frame. The sun's own disc is
// left out, which the drawn sky adds (sky.slang), since the sun's light on a surface is
// the light's.
import atmosphere;

[[vk::binding(0, 0)]] StructuredBuffer<float4> tables;
[[vk::binding(1, 0)]] [format("rgba16f")] RWTexture2D<float4> sky;
[[vk::push_constant]] ConstantBuffer<AirPush> push;

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.size.x || id.y >= push.size.y)
        return;
    float2 uv = (float2(id.xy) + 0.5) / float2(push.size.xy);
    float theta = uv.y * Pi, phi = (uv.x - 0.5) * 2.0 * Pi;
    float3 view = float3(sin(theta) * sin(phi), cos(theta), -sin(theta) * cos(phi));
    float3 light = airLuminance(tables, view, normalize(push.sun.xyz), push.sun.w);
    // Half floats stop at 65504. Under the light the air scatters lies a moonless sky's own glow,
    // the stars' and the air's, alike over the sky.
    sky[id.xy] = float4(min(light * push.scale.rgb + push.night.rgb, 65000.0), 1.0);
}
