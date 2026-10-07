// Turns the codes field_splat.slang left in one cascade's cells into distances in world units,
// below zero behind a face and within half a cell of a double-sided one, and writes them into the cascade's place in the field the meshes alone
// make and in the field the passes read, with the color and the light given off it painted, a
// thread a cell.

[[vk::binding(0, 0)]]
StructuredBuffer<uint> cells;

[[vk::binding(1, 0)]] [format("r16f")]
RWTexture3D<float> stillField;

[[vk::binding(2, 0)]] [format("r16f")]
RWTexture3D<float> field;

// The color and light given off of each cell's nearest surface, as field_splat.slang painted them,
// and the images they go into, the color's alpha 1 where a surface painted the cell and 0 where
// none did, so a read blending cells divides by it.
[[vk::binding(3, 0)]]
StructuredBuffer<uint> albedo;

[[vk::binding(4, 0)]]
StructuredBuffer<uint2> glow;

[[vk::binding(5, 0)]] [format("rgba8")]
RWTexture3D<float4> albedoField;

[[vk::binding(6, 0)]] [format("rgba16f")]
RWTexture3D<float4> glowField;

struct ResolvePush
{
    // x: the cascade. y: the cells along each side. z: the width of a cell. w: the band in cells.
    float4 cascade;
};

[[vk::push_constant]]
ConstantBuffer<ResolvePush> push;

// The two halves in a word as floats, by GLSL's own instruction, which needs no 16-bit types of
// the device, where HLSL's f16tof32 declares them.
float2 unpackHalves(uint v)
{
    return spirv_asm { result:$$float2 = OpExtInst glsl450 UnpackHalf2x16 $v };
}

[shader("compute")]
[numthreads(4, 4, 4)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint size = uint(push.cascade.y);
    if (any(id >= size))
        return;
    uint code = cells[(id.z * size + id.y) * size + id.x];
    float d = min(float(code >> 2) / 1024.0, push.cascade.w) * push.cascade.z;
    if ((code & 2u) != 0u)
        d -= push.cascade.z * 0.5;
    else if ((code & 1u) == 0u)
        d = -d;
    uint3 at = uint3(id.xy, id.z + uint(push.cascade.x) * size);
    stillField[at] = d;
    field[at] = d;
    uint color = albedo[(id.z * size + id.y) * size + id.x];
    albedoField[at] = float4(float(color & 255u), float((color >> 8) & 255u), float((color >> 16) & 255u), float(color >> 24)) / 255.0;
    uint2 light = glow[(id.z * size + id.y) * size + id.x];
    glowField[at] = float4(unpackHalves(light.x), unpackHalves(light.y).x, 0.0);
}
