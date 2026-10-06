// How far each moving mesh entity's surface moved on the picture since the frame before, which
// motion blur reads in place of the camera's movement where it is written. Each vertex is put
// through the entity's world and the camera of this frame and of the frame before, and a fragment
// writes the way its point moved between the two, as a share of the picture, with alpha 1 to say it
// is written, where it is the nearest surface the scene drew there, which the scene's depth says.
// The entities of one mesh are drawn as instances, each its world now and then.

struct VelocityPush
{
    float4x4 viewProjection;
    float4x4 lastViewProjection;
};

[[vk::push_constant]]
ConstantBuffer<VelocityPush> push;

[[vk::binding(0, 0)]]
Sampler2D depth;

// The mesh's vertex, and the instance's world now and the frame before, three rows each, as the
// model pass reads a world.
struct VelocityInput
{
    [[vk::location(0)]] float3 position : POSITION;
    [[vk::location(1)]] float4 nowX : NOWX;
    [[vk::location(2)]] float4 nowY : NOWY;
    [[vk::location(3)]] float4 nowZ : NOWZ;
    [[vk::location(4)]] float4 thenX : THENX;
    [[vk::location(5)]] float4 thenY : THENY;
    [[vk::location(6)]] float4 thenZ : THENZ;
};

struct VelocityOutput
{
    float4 position : SV_Position;
    float4 now : TEXCOORD0;
    float4 then : TEXCOORD1;
};

[shader("vertex")]
VelocityOutput vertexMain(VelocityInput input)
{
    float4 p = float4(input.position, 1.0);
    float3 now = float3(dot(input.nowX, p), dot(input.nowY, p), dot(input.nowZ, p));
    float3 then = float3(dot(input.thenX, p), dot(input.thenY, p), dot(input.thenZ, p));
    VelocityOutput output;
    output.now = mul(push.viewProjection, float4(now, 1.0));
    output.then = mul(push.lastViewProjection, float4(then, 1.0));
    output.position = output.now;
    return output;
}

[shader("fragment")]
float4 fragmentMain(VelocityOutput input) : SV_Target
{
    // A surface behind what the scene drew at this pixel is hidden by it, and the camera's
    // movement stands there.
    uint width, height;
    depth.GetDimensions(width, height);
    float scene = depth.SampleLevel(input.position.xy / float2(width, height), 0).r;
    if (input.position.z > scene + 1e-4)
        discard;

    float2 now = input.now.xy / input.now.w * 0.5 + 0.5;
    float2 then = input.then.xy / input.then.w * 0.5 + 0.5;
    return float4(now - then, 0.0, 1.0);
}
