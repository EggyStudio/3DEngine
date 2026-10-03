// Draws the lines and triangles the flat API's Draw calls record, in the color each vertex carries.

struct PushConstants
{
    float4x4 transform;
};

[[vk::push_constant]]
ConstantBuffer<PushConstants> push;

struct VertexOutput
{
    float4 position : SV_Position;
    float4 color : COLOR0;
};

[shader("vertex")]
VertexOutput vertexMain(float3 position : POSITION, float4 color : COLOR0)
{
    VertexOutput output;
    output.position = mul(push.transform, float4(position, 1.0));
    output.color = color;
    return output;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    return input.color;
}
