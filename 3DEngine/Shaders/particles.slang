// Draws one emitter's particles as squares that face the camera, six vertices to an instance and
// an instance to a particle, from the buffer particle_step.slang writes. A particle grows or
// shrinks and changes color from birth to death by the share of its life gone, and is round, with
// an edge that fades out. A lit one is lit as a rough surface facing the camera by the model
// pass's own lighting, and an unlit one gives off its color times the emitter's brightness, so it
// glows through bloom where that is past 1. A dead particle's square is drawn at a point, which
// covers no pixel.
import modelpass;

// The header the step writes, then each particle in two float4 values, as particle_step.slang
// lays them out.
[[vk::binding(0, 2)]]
StructuredBuffer<float4> slots;

static const uint Header = 4;

static const float2 corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

[shader("vertex")]
ModelVertexOutput vertexMain(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    ModelVertexOutput output = (ModelVertexOutput)0;
    uint at = Header + instance * 2;
    float4 positionAge = slots[at];
    float4 velocityLife = slots[at + 1];
    if (velocityLife.w <= 0.0)
    {
        output.position = float4(0, 0, 0, 1);
        return output;
    }

    float t = saturate(positionAge.w / velocityLife.w);
    float4 look = slots[2];
    float size = lerp(look.x, look.y, t);
    float4x4 viewProjection = modelPush.viewProjection;
    float4 eye = eyeInWorld(viewProjection);
    float3 center = positionAge.xyz;
    float3 facing = normalize(eye.w == 0.0 ? eye.xyz : eye.xyz - center);
    float3 right = cross(float3(0, 1, 0), facing);
    right = dot(right, right) > 1e-6 ? normalize(right) : float3(1, 0, 0);
    float3 up = cross(facing, right);

    float2 corner = corners[vertex % 6];
    output.world = center + (right * corner.x + up * corner.y) * (size * 0.5);
    output.position = mul(viewProjection, float4(output.world, 1.0));
    output.normal = facing;
    output.uv = corner * 0.5 + 0.5;
    output.toEye = eye.w == 0.0 ? eye.xyz : eye.xyz - output.world;
    output.color = lerp(slots[0], slots[1], t);
    // Not metallic, wholly rough, with no normal map or occlusion.
    output.factors = float4(0, 1, 0, 0);
    return output;
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float2 p = input.uv * 2.0 - 1.0;
    float near = 1.0 - dot(p, p);
    if (near <= 0.0)
        discard;
    float4 look = slots[2];
    float3 shown = look.w > 0.5 ? lit(input.color.rgb, input) : toDisplay(input.color.rgb * look.z);
    return float4(shown, input.color.a * near * near);
}
