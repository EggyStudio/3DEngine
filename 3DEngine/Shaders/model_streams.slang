// model.slang for a mesh with a color or a second texture coordinate at each vertex, which reads
// them from buffers of their own beside the vertices and multiplies the color in. A mesh without
// them is drawn with model.slang, which reads its vertices alone.
import modelpass;

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
    return float4(lit(color.rgb, input.base), color.a);
}
