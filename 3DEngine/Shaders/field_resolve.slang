// Turns the codes field_splat.slang left in one cascade's cells into distances in world units,
// below zero behind a face and within half a cell of a wall thinner than a cell, with the plane of
// a double-sided face whose inside is a cell's nearest point beside the distance, and writes them
// into the cascade's place in the field the meshes alone make and in the field the passes read,
// with the color and the emission it painted, a thread a cell, every cell of the cascade or, for a
// build of a region, the region's cells alone, and keeps each cell's code for a mesh painted again.

// The cells' codes read as distances and planes, and the pinch, the RadianceCascades module's.
import rc_field_code;


[[vk::binding(0, 0)]]
StructuredBuffer<uint> cells;

// The distance in x, and in yzw the plane a cell keeps (sheetOf).
[[vk::binding(1, 0)]] [format("rgba16f")]
RWTexture3D<float4> stillField;

[[vk::binding(2, 0)]] [format("rgba16f")]
RWTexture3D<float4> field;

// The color and emission of each cell's nearest surface, as field_splat.slang painted them,
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

// What emitters thinner than a cell lend each cell, as field_splat.slang left it, the most light
// any emits, red, green and blue in 256ths, and the area of their faces near the cell seen
// along x, y and z, in 1024ths of a cell's face.
[[vk::binding(9, 0)]]
StructuredBuffer<uint> emitted;

// The way each cell's nearest face looks, the eight bits field_splat.slang kept in its uint, into
// the image the passes read and the one the still meshes' faces are kept in for field_stamp.slang,
// so a ray's hit takes its color from cells its own face painted (gi.slang's surfaceAlbedoFacing).
[[vk::binding(10, 0)]] [format("r32ui")]
RWTexture3D<uint> faceField;
[[vk::binding(11, 0)]] [format("r32ui")]
RWTexture3D<uint> stillFaces;

// The word of a second double-sided face as near a cell, as field_splat.slang's painting pass kept
// it, and the second plane it gives the cell, in xyz as the field's own plane is kept (sheetOf).
[[vk::binding(12, 0)]]
StructuredBuffer<uint> second;

[[vk::binding(13, 0)]] [format("rgba16f")]
RWTexture3D<float4> secondField;

// Each cascade's codes as its builds left them, cascade after cascade, which field_splat.slang's
// pass that paints again reads.
[[vk::binding(14, 0)]]
RWStructuredBuffer<uint> kept;

// The emission the still meshes give, kept apart for field_stamp.slang to start its bricks from.
[[vk::binding(15, 0)]] [format("rgba16f")]
RWTexture3D<float4> stillGlow;

// The gloss each cell's nearest surface painted (glossBits in scenefield.slang), which goes beside
// its face's way into the faces images.
[[vk::binding(16, 0)]]
RWStructuredBuffer<uint> gloss;

struct ResolvePush
{
    // x: the cascade. y: the cells along each side. z: the width of a cell. w: the band in cells.
    float4 cascade;
    // x: 1 where an emitter thinner than a cell lent the cells its light, so emitted is read. y:
    // how far either side of its middle a wall thinner than a cell is held, half a cell but in the
    // far cascade, where half the last counted cascade's cell.
    float4 lent;
    // xyz: the first and the last cell along each axis the build turns into distances.
    float4 from;
    float4 to;
};

[[vk::push_constant]]
ConstantBuffer<ResolvePush> push;

// The two halves in a word as floats, by GLSL's own instruction, which needs no 16-bit types of
// the device, where HLSL's f16tof32 declares them.
float2 unpackHalves(uint v)
{
    return spirv_asm { result:$$float2 = OpExtInst glsl450 UnpackHalf2x16 $v };
}

// The distance a cell holds, from the code field_splat.slang left it and the face it lies behind,
// a wall thinner than a cell held push.lent.y either side of its middle (resolvedDistance).
float distanceAt(uint3 c, uint size)
{
    uint index = (c.z * size + c.y) * size + c.x;
    return resolvedDistance(cells[index], behind[index], push.cascade.z, push.cascade.w, push.lent.y);
}

// The cascade's cells as the pinch reads them.
struct ResolvedCells : IResolvedCells
{
    uint side;

    __init(uint side) { this.side = side; }

    int size() { return int(side); }
    float resolvedAt(int3 at) { return distanceAt(uint3(at), side); }
}

[shader("compute")]
[numthreads(4, 4, 4)]
void computeMain(uint3 thread : SV_DispatchThreadID)
{
    uint size = uint(push.cascade.y);
    uint3 id = thread + uint3(push.from.xyz);
    if (any(id > uint3(push.to.xyz)) || any(id >= size))
        return;
    uint code = cells[(id.z * size + id.y) * size + id.x];
    kept[uint(push.cascade.x) * size * size * size + (id.z * size + id.y) * size + id.x] = code;
    // Such a cell is held inside at its distance, so a march through the edge between the two
    // blocks meets the field there, where between the four cells about the edge the distance never
    // falls below zero, and a march from the floor on one side of a wall whose halves meet along
    // one edge would see part of a lamp on the other side and light the floor about the edge, which
    // no light reaches (GlobalIlluminationTests).
    float d = distanceAt(id, size);
    if (d > 0.0 && pinched(ResolvedCells(size), int3(id)))
        d = -d;
    uint3 at = uint3(id.xy, id.z + uint(push.cascade.x) * size);
    float4 kept = float4(d, sheetOf(code));
    stillField[at] = kept;
    field[at] = kept;
    uint other = second[(id.z * size + id.y) * size + id.x];
    secondField[at] = float4(other == 0xFFFFFFFFu ? float3(0.0) : sheetOf(other), 0.0);
    uint color = albedo[(id.z * size + id.y) * size + id.x];
    float4 paint = float4(float(color & 255u), float((color >> 8) & 255u), float((color >> 16) & 255u), float(color >> 24)) / 255.0;
    stillPaint[at] = paint;
    albedoField[at] = paint;
    uint face = ((code >> 8u) & 255u) | gloss[(id.z * size + id.y) * size + id.x];
    stillFaces[at] = face;
    faceField[at] = face;
    uint index = (id.z * size + id.y) * size + id.x;
    uint2 light = glow[index];
    // The lent light times the share of the cell's face the emitters cover along the axis they
    // cover most of, a whole face at most, as the faces of a strip along a wall lend a cell
    // beside it what it shows from the front and from above alike, where summed they would lend
    // it twice that.
    float3 lent = float3(0.0);
    if (push.lent.x > 0.5)
    {
        float3 light = float3(float(emitted[index * 6u]), float(emitted[index * 6u + 1u]), float(emitted[index * 6u + 2u])) / 256.0;
        float3 seen = float3(float(emitted[index * 6u + 3u]), float(emitted[index * 6u + 4u]), float(emitted[index * 6u + 5u])) / 1024.0;
        lent = light * min(max(seen.x, max(seen.y, seen.z)), 1.0);
    }
    // The larger of the two, since an emitter that painted the cell its own already gives it its
    // whole light, as an emissive panel under a ceiling does, times the paint's alpha where a
    // surface painted the cell, as its color is (lightThroughPaint), since a reader divides both by
    // it. Alpha is 1 where the light is an emitter's that global illumination carries as a glow
    // light, which a probe's ray leaves out.
    float3 painted = float3(unpackHalves(light.x), unpackHalves(light.y).x);
    float4 given = float4(max(painted, lent) * (paint.a > 0.0 ? paint.a : 1.0), any(lent > painted) ? 0.0 : unpackHalves(light.y).y);
    stillGlow[at] = given;
    glowField[at] = given;
}
