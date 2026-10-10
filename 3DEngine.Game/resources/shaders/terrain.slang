// The voxel game's sections: the model pass's own light over each vertex's color, as the engine
// draws a mesh with colors at its vertices, then a haze toward the color of the sky at the
// horizon as the distance from the eye grows, as Minecraft's fog hides where the world ends.
import modelpass;

// The haze's color, as the frame shows it, and the distances in blocks it starts at and covers by.
uniform float4 fogColor;
uniform float fogStart;
uniform float fogEnd;

[shader("vertex")]
ModelStreamsOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance,
    [[vk::location(9)]] float4 color : COLOR0, [[vk::location(10)]] float2 uv2 : TEXCOORD1)
{
    return transformModelVertex(position, normal, uv, color, uv2, instance);
}

[shader("fragment")]
float4 fragmentMain(ModelStreamsOutput input) : SV_Target
{
    float4 color = alphaTested(baseColor(input), input.base);
    float3 shown = lit(color.rgb, input.base);
    float haze = smoothstep(fogStart, fogEnd, length(input.base.toEye));
    return float4(lerp(shown, fogColor.rgb, haze), color.a);
}
