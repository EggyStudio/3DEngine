// Turns the codes field_splat.slang left in one cascade's cells into distances in world units,
// below zero behind a face and within half a cell of a double-sided one or a wall thinner than
// half a cell, and writes them into the cascade's place in the field the meshes alone
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

// The nearest face each cell lies behind, in 1024ths of a cell, as field_splat.slang kept it.
[[vk::binding(7, 0)]]
StructuredBuffer<uint> behind;

// The colors the still meshes paint, kept apart for field_stamp.slang to start its bricks from.
[[vk::binding(8, 0)]] [format("rgba8")]
RWTexture3D<float4> stillPaint;

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

// A face's way from its eight bits, as field_splat.slang folded it.
float3 way(uint bits)
{
    float2 o = (float2(float(bits & 15u), float((bits >> 4) & 15u)) + 0.5) / 16.0 * 2.0 - 1.0;
    float3 n = float3(o, 1.0 - abs(o.x) - abs(o.y));
    if (n.z < 0.0)
        n.xy = (1.0 - abs(n.yx)) * float2(n.x >= 0.0 ? 1.0 : -1.0, n.y >= 0.0 ? 1.0 : -1.0);
    return normalize(n);
}

[shader("compute")]
[numthreads(4, 4, 4)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint size = uint(push.cascade.y);
    if (any(id >= size))
        return;
    uint code = cells[(id.z * size + id.y) * size + id.x];
    float d = min(float(code >> 10) / 1024.0, push.cascade.w) * push.cascade.z;
    // A cell in front of one face and straight behind another that looks the other way, less than
    // half a cell beyond it, lies against a wall thinner than half a cell, which is held as a
    // double-sided sheet is, half a cell thick on either side of the wall's middle, so a ray
    // between the cells on either side still meets it, where the faces of a box's edge, which meet
    // square, do not make one.
    uint hidden = behind[(id.z * size + id.y) * size + id.x];
    float beyond = float(hidden >> 8) / 1024.0 * push.cascade.z;
    bool thin = (code & 0x300u) == 0x100u && hidden != 0xFFFFFFFFu && beyond - d < push.cascade.z * 0.5
        && dot(way(code), way(hidden)) < -0.7;
    if ((code & 0x200u) != 0u)
        d -= push.cascade.z * 0.5;
    else if (thin)
        d = (d + beyond) * 0.5 - push.cascade.z * 0.5;
    else if ((code & 0x100u) == 0u)
        d = -d;
    uint3 at = uint3(id.xy, id.z + uint(push.cascade.x) * size);
    stillField[at] = d;
    field[at] = d;
    uint color = albedo[(id.z * size + id.y) * size + id.x];
    float4 paint = float4(float(color & 255u), float((color >> 8) & 255u), float((color >> 16) & 255u), float(color >> 24)) / 255.0;
    stillPaint[at] = paint;
    albedoField[at] = paint;
    uint2 light = glow[(id.z * size + id.y) * size + id.x];
    glowField[at] = float4(unpackHalves(light.x), unpackHalves(light.y).x, 0.0);
}
