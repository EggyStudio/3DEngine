// Draws a mesh from the flat API's DrawModel: the texture times the color, shaded by one fixed
// directional light over an ambient floor, so a model reads as a solid before any lights exist.

struct PushConstants
{
    float4x4 transform;
    // The world matrix's upper 3x3 as three rows, for turning normals into world space. A full
    // second matrix would take the block past the 128 bytes every device guarantees.
    float4 normalX;
    float4 normalY;
    float4 normalZ;
    float4 color;
};

[[vk::push_constant]]
ConstantBuffer<PushConstants> push;

[[vk::binding(1, 0)]]
Sampler2D boundTexture;

struct VertexOutput
{
    float4 position : SV_Position;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

[shader("vertex")]
VertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0)
{
    VertexOutput output;
    output.position = mul(push.transform, float4(position, 1.0));
    output.normal = float3(dot(push.normalX.xyz, normal), dot(push.normalY.xyz, normal), dot(push.normalZ.xyz, normal));
    output.uv = uv;
    return output;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 color = push.color * boundTexture.Sample(input.uv);
    if (color.a <= 0.0)
        discard;

    float3 toLight = normalize(float3(0.4, 1.0, 0.3));
    float shade = 0.35 + 0.65 * max(dot(normalize(input.normal), toLight), 0.0);
    return float4(color.rgb * shade, color.a);
}
