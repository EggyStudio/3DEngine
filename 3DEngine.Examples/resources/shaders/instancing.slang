// Copies of one mesh drawn by DrawMeshInstanced, each given a hue of its own from the instance it
// is, counted from 0, and lit as the model pass lights its meshes.
import modelpass;

// How many copies there are, which spread their hues around the color wheel.
uniform float count;

float3 hue(float h)
{
    float3 k = abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1;
    return saturate(k);
}

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0,
    ModelInstance instance, uint id : SV_InstanceID)
{
    ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
    output.color = float4(hue(id / max(count, 1.0)) * 0.8 + 0.2, 1);
    return output;
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    return float4(lit(input.color.rgb, input), 1);
}
