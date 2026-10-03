// Draws the lines and triangles the flat API's Draw calls record: each vertex's color times the
// batch's texture, which is a white pixel for untextured shapes.

struct PushConstants
{
    float4x4 transform;
};

[[vk::push_constant]]
ConstantBuffer<PushConstants> push;

[[vk::binding(1, 0)]]
Sampler2D boundTexture;

struct VertexOutput
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
    float4 color : COLOR0;
};

[shader("vertex")]
VertexOutput vertexMain(float3 position : POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0)
{
    VertexOutput output;
    output.position = mul(push.transform, float4(position, 1.0));
    output.uv = uv;
    output.color = color;
    return output;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // A texel with no coverage is discarded rather than blended, so the empty corners of a sprite
    // or billboard write no depth and do not hide what is drawn behind them later.
    float4 color = input.color * boundTexture.Sample(input.uv);
    if (color.a <= 0.0)
        discard;
    return color;
}
