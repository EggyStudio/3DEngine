// A reflection probe's six captured faces gathered into the first level of a cube of linear
// light. Each texel reads the face whose view looks most nearly along it, through that face's own
// view-projection, so the faces may be drawn with any orientation.
import probecube;

[[vk::binding(0, 0)]] Sampler2D face0;
[[vk::binding(1, 0)]] Sampler2D face1;
[[vk::binding(2, 0)]] Sampler2D face2;
[[vk::binding(3, 0)]] Sampler2D face3;
[[vk::binding(4, 0)]] Sampler2D face4;
[[vk::binding(5, 0)]] Sampler2D face5;

// Each face's view-projection, as System.Numerics lays it out, the way each looks, and the
// probe's middle in xyz.
struct Views
{
    float4x4 viewProjection[6];
    float4 forward[6];
    float4 eye;
};

[[vk::binding(6, 0)]] ConstantBuffer<Views> views;

[[vk::binding(7, 0)]] [format("rgba16f")] RWTexture2DArray<float4> cube;

struct GatherPush
{
    uint size;
};

[[vk::push_constant]] ConstantBuffer<GatherPush> push;

float3 faceSample(uint face, float2 uv)
{
    switch (face)
    {
        case 0: return face0.SampleLevel(uv, 0.0).rgb;
        case 1: return face1.SampleLevel(uv, 0.0).rgb;
        case 2: return face2.SampleLevel(uv, 0.0).rgb;
        case 3: return face3.SampleLevel(uv, 0.0).rgb;
        case 4: return face4.SampleLevel(uv, 0.0).rgb;
        default: return face5.SampleLevel(uv, 0.0).rgb;
    }
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.size || id.y >= push.size)
        return;
    float3 d = cubeDirection(id.z, faceCoordinates(id.xy, push.size).x, faceCoordinates(id.xy, push.size).y);

    uint best = 0;
    for (uint f = 1; f < 6; f++)
        if (dot(views.forward[f].xyz, d) > dot(views.forward[best].xyz, d))
            best = f;
    float4 clip = mul(views.viewProjection[best], float4(views.eye.xyz + d, 1.0));
    float2 uv = (clip.xy / clip.w + 1.0) * 0.5;

    // A surface lit past what a half float holds comes back as infinity, kept finite so the
    // filter's sums stay numbers.
    float3 light = min(faceSample(best, uv), float3(65504.0));
    cube[id] = float4(light, 1.0);
}
