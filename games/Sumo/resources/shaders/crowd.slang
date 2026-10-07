// The crowd round the ring in one instanced draw, each copy a supporter of one player or the other,
// alternating by its instance, in a lighter or darker shade of that player's color.
import modelpass;

// The two players' colors, as the program's colors are, encoded for the display.
uniform float4 first;
uniform float4 second;

float hash(uint n)
{
    return frac(sin(float(n) * 12.9898) * 43758.5453);
}

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0,
    ModelInstance instance, uint id : SV_InstanceID)
{
    ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
    float3 team = id % 2 == 0 ? first.rgb : second.rgb;
    output.color = float4(pow(team, 2.2) * (0.55 + 0.6 * hash(id)), 1);
    return output;
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    return float4(lit(input.color.rgb, input), 1);
}
