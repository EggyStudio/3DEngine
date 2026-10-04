// Draws a mesh from the model pass: the texture times the color, lit by the lights the ECS holds,
// or by one fixed directional light over an ambient floor when it holds none, so a model reads as
// a solid before any lights exist. The pieces are in modelpass.slang, which a program's own model
// shader imports too.
import modelpass;

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance)
{
    return transformModelVertex(position, normal, uv, instance);
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float4 color = alphaTested(baseColor(input), input);
    return float4(lit(color.rgb, input), color.a);
}
