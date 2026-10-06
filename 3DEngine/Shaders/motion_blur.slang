// Motion blur from the camera's movement over the HDR frame. Each pixel's point in the world, from
// its depth, is put through the camera as it was the frame before, and the way it moved on the
// picture since, scaled by the share of the frame the shutter is open and held to a largest
// length, is the line its color is averaged along, the fastest of the movements around it taken
// so a near thing's edge smears over what is behind it. Where velocity.slang wrote a mesh entity's
// own movement, which it does with per-object blur on, that movement stands in place of the
// camera's.
import fullscreen;

struct BlurPush
{
    // This frame's clip space to the frame before's, as the model pass takes a view-projection.
    float4x4 reprojection;
    // x: the share of the frame's movement blurred. y: the longest blur, as a share of the picture.
    // z: 1 where the velocity image was drawn this frame.
    float4 amount;
};

[[vk::push_constant]]
ConstantBuffer<BlurPush> push;

[[vk::binding(0, 0)]]
Sampler2D scene;

[[vk::binding(1, 0)]]
Sampler2D depth;

// Each moving mesh entity's movement since the frame before, with alpha 1 where one is written.
[[vk::binding(2, 0)]]
Sampler2D velocity;

static const int Taps = 12;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

// How far the point at uv moved on the picture since the frame before, scaled and held to the longest.
float2 movedAt(float2 uv)
{
    float2 moved;
    float4 own = push.amount.z > 0.5 ? velocity.SampleLevel(uv, 0) : float4(0.0);
    if (own.a > 0.5)
        moved = own.xy * push.amount.x;
    else
    {
        float d = depth.SampleLevel(uv, 0).r;
        float4 before = mul(push.reprojection, float4(uv * 2.0 - 1.0, d, 1.0));
        float2 then = before.w > 1e-5 ? before.xy / before.w * 0.5 + 0.5 : uv;
        moved = (uv - then) * push.amount.x;
    }
    float length2 = dot(moved, moved);
    float longest = push.amount.y;
    return length2 > longest * longest ? moved * (longest / sqrt(length2)) : moved;
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    // The fastest movement near this pixel stands for its own, so a moving thing's edge smears out
    // over the still background beside it as well as into itself.
    float2 moved = movedAt(input.uv);
    float reach = push.amount.y * 0.5;
    for (int i = 0; i < 8; i++)
    {
        float angle = float(i) * 0.78539816;
        float2 near = movedAt(input.uv + float2(cos(angle), sin(angle)) * reach);
        if (dot(near, near) > dot(moved, moved))
            moved = near;
    }

    float3 sum = float3(0.0);
    for (int i = 0; i < Taps; i++)
        sum += scene.SampleLevel(input.uv - moved * ((float(i) + 0.5) / float(Taps) - 0.5), 0).rgb;
    return float4(sum / float(Taps), 1.0);
}
