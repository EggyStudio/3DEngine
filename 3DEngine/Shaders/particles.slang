// Draws one emitter's particles as squares that face the camera, six vertices to an instance and
// an instance to a particle, from the buffer particle_step.slang writes. A particle grows or
// shrinks and changes color from birth to death by the share of its life gone, and is round, with
// an edge that fades out, or the emitter's texture, the material's base color, tinted, whole or a
// frame of a sheet played through over its life. A lit one is lit as a rough surface facing the camera by the model
// pass's own lighting, and an unlit one gives off its color times the emitter's brightness, so it
// glows through bloom where that is past 1. A dead particle's square is drawn at a point, which
// covers no pixel.
import modelpass;

// The header the step writes, then each particle in two float4 values, as particle_step.slang
// lays them out, then the keys particle_sort.slang sorts an emitter laid over by alpha by, which
// the header's last value marks in y.
[[vk::binding(0, 2)]]
StructuredBuffer<float4> slots;

static const uint Header = 4;

static const float2 corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

// How the emitter is drawn, which Particles.cs packs into the look's last value: bit 0 lit, bit 1
// textured, then the texture's columns and rows of frames, less one, eight bits each, and bit 18
// whether a sheet's frames blend.
bool isLit(float4 look) { return (uint(look.w) & 1u) != 0u; }
bool isTextured(float4 look) { return (uint(look.w) & 2u) != 0u; }
uint2 sheet(float4 look) { return uint2(((uint(look.w) >> 2u) & 255u) + 1u, ((uint(look.w) >> 10u) & 255u) + 1u); }
bool blendsFrames(float4 look) { return (uint(look.w) & (1u << 18u)) != 0u; }

// What every particle's square gives the fragment stage, with the next frame of a sheet and how
// far the particle is from this frame to it, which a sheet whose frames blend mixes.
struct ParticleOutput
{
    ModelVertexOutput base;
    float2 nextUv : TEXCOORD3;
    nointerpolation float blend : TEXCOORD4;
};

// Where a corner of the square falls on one frame of a sheet, left to right and then down.
float2 frameUv(uint frame, uint2 frames, float2 uv) { return (float2(frame % frames.x, frame / frames.x) + uv) / float2(frames); }

[shader("vertex")]
ParticleOutput vertexMain(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    ParticleOutput particleOutput = (ParticleOutput)0;
    ModelVertexOutput output = (ModelVertexOutput)0;
    // Sorted, the instance is the sorted key's particle, drawn far to near.
    uint capacity = uint(slots[3].x);
    uint particle = slots[3].y > 0.5 ? uint(slots[Header + capacity * 2 + instance].y) : instance;
    uint at = Header + particle * 2;
    float4 positionAge = slots[at];
    float4 velocityLife = slots[at + 1];
    if (velocityLife.w <= 0.0)
    {
        output.position = float4(0, 0, 0, 1);
        particleOutput.base = output;
        return particleOutput;
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
    // A texture's first row is its top. On a sheet the frame is the share of the life gone, left
    // to right and then down.
    output.uv = float2(corner.x * 0.5 + 0.5, 0.5 - corner.y * 0.5);
    if (isTextured(look))
    {
        uint2 frames = sheet(look);
        uint count = frames.x * frames.y;
        float played = t * float(count);
        uint frame = min(uint(played), count - 1u);
        float2 corner01 = output.uv;
        output.uv = frameUv(frame, frames, corner01);
        // The last frame has none after it, and is held.
        particleOutput.nextUv = frameUv(min(frame + 1u, count - 1u), frames, corner01);
        particleOutput.blend = blendsFrames(look) ? saturate(played - float(frame)) : 0.0;
    }
    output.toEye = eye.w == 0.0 ? eye.xyz : eye.xyz - output.world;
    output.color = lerp(slots[0], slots[1], t);
    // Not metallic, wholly rough, with no normal map or occlusion.
    output.factors = float4(0, 1, 0, 0);
    particleOutput.base = output;
    return particleOutput;
}

[shader("fragment")]
float4 fragmentMain(ParticleOutput particle) : SV_Target
{
    ModelVertexOutput input = particle.base;
    float4 look = slots[2];
    float3 color = input.color.rgb;
    float alpha = input.color.a;
    if (isTextured(look))
    {
        float4 texel = boundTexture.Sample(input.uv);
        // On the emitter's setting, the same for every particle of the draw, so both samples are
        // taken in control flow every pixel shares.
        if (blendsFrames(look))
            texel = lerp(texel, boundTexture.Sample(particle.nextUv), particle.blend);
        color *= texel.rgb;
        alpha *= texel.a;
    }
    else
    {
        float2 p = input.uv * 2.0 - 1.0;
        float near = 1.0 - dot(p, p);
        if (near <= 0.0)
            discard;
        alpha *= near * near;
    }
    float3 shown = isLit(look) ? lit(color, input) : toDisplay(color * look.z);
    // The window's particles are drawn over its scene decoded to linear light, so they add and lay
    // their light over it in linear light, and the window's encoding is undone.
    if (lighting.output.y > 0.0)
        shown = srgbToLinear(shown);
    return float4(shown, alpha);
}
