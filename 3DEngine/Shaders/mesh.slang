// Draws a mesh in its material's base color. The vertex layout is positions only, so there is no
// lighting yet (RENDERING.md §3).

struct Camera
{
    float4x4 view;
    float4x4 projection;
};

struct PushConstants
{
    float4x4 model;
    float4 albedo;
};

[[vk::binding(0, 0)]]
ConstantBuffer<Camera> camera;

[[vk::push_constant]]
ConstantBuffer<PushConstants> push;

struct VertexOutput
{
    float4 position : SV_Position;
    float4 color : COLOR0;
};

[shader("vertex")]
VertexOutput vertexMain(float3 position : POSITION)
{
    VertexOutput output;
    output.position = mul(camera.projection, mul(camera.view, mul(push.model, float4(position, 1.0))));
    output.color = push.albedo;
    return output;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    return input.color;
}
