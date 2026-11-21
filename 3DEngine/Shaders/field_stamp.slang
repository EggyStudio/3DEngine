// Stamps the shapes of the meshes that moved into the field the passes read, a workgroup a brick of
// 4 by 4 by 4 cells. Each cell takes the least of its distance in the field the still meshes make
// and its distance to each shape that comes within its brick, a box around a mesh or a part of one
// in its own space, so a brick stamped last frame and not this one goes back to what the still
// meshes make. A brick reads the shapes of its own run alone, so a frame's cost grows with the
// shapes of each brick and not with every shape the frame stamps. Each cell is painted the color
// and the emission of the nearer surface, the still meshes' or a box's, as the build paints a cell
// its nearest triangle's, so indirect light from a moving mesh takes its color and a moving emitter
// lights what it passes, and a cell a box painted takes the still meshes' again once the box has
// left.
import scenefield;
// The stamp of a cell, the RadianceCascades module's.
import rc_field_stamp;

// x: the cascade. y: the brick's place in it, in bricks, a byte an axis. z: where its run of shapes
// begins. w: how many shapes are in it. Past the bricks, the runs one after another, four shapes'
// places to an element.
[[vk::binding(0, 0)]]
StructuredBuffer<uint4> bricks;

[[vk::binding(1, 0)]]
StructuredBuffer<StampShape> shapes;

// The distance in x, and in yzw the plane of a double-sided face the still meshes give the cell
// (sheetOf, the RadianceCascades module's), which a box nearer than it takes away.
[[vk::binding(2, 0)]] [format("rgba16f")]
RWTexture3D<float4> stillField;

[[vk::binding(3, 0)]] [format("rgba16f")]
RWTexture3D<float4> field;

[[vk::binding(4, 0)]]
ConstantBuffer<SceneFieldInfo> info;

[[vk::binding(5, 0)]] [format("rgba8")]
RWTexture3D<float4> stillPaint;

[[vk::binding(6, 0)]] [format("rgba8")]
RWTexture3D<float4> albedoField;

// The way each cell's nearest face looks, the still meshes' and the one the passes read, as
// field_resolve.slang wrote them, a box's face's where the box is nearer.
[[vk::binding(7, 0)]] [format("r32ui")]
RWTexture3D<uint> stillFaces;

[[vk::binding(8, 0)]] [format("r32ui")]
RWTexture3D<uint> faceField;

// The emission the still meshes give each cell and the one the passes read, as field_resolve.slang
// wrote them, a box's where the box is nearer.
[[vk::binding(9, 0)]] [format("rgba16f")]
RWTexture3D<float4> stillGlow;

[[vk::binding(10, 0)]] [format("rgba16f")]
RWTexture3D<float4> glowField;

struct StampPush
{
    // x: how many bricks. y: how many shapes. zw: unused.
    uint4 counts;
};

[[vk::push_constant]]
ConstantBuffer<StampPush> push;

// The shapes of a brick's run, as the module's stamp reads them, the run's places following the
// bricks four to an element.
struct BrickShapes : IStampShapes
{
    uint first;
    uint length;

    uint count() { return length; }

    StampShape shape(uint i)
    {
        uint run = first + i;
        return shapes[bricks[run / 4u][run % 4u]];
    }
}

[shader("compute")]
[numthreads(4, 4, 4)]
void computeMain(uint3 group : SV_GroupID, uint3 local : SV_GroupThreadID)
{
    uint index = group.x;
    if (index >= push.counts.x)
        return;
    uint4 brick = bricks[index];
    uint cascade = brick.x;
    uint size = uint(info.counts.y);
    uint3 place = uint3(brick.y & 0xFFu, (brick.y >> 8) & 0xFFu, (brick.y >> 16) & 0xFFu);
    uint3 cell = place * 4u + local;
    if (any(cell >= size))
        return;
    float4 oc = info.cascades[cascade].originAndCell;
    float3 p = oc.xyz + (float3(cell) + 0.5) * oc.w;
    float reach = info.counts.z * oc.w;

    uint3 at = uint3(cell.xy, cell.z + cascade * size);
    FieldCell still;
    float4 kept = stillField[at];
    still.distance = kept.x;
    still.sheet = kept.yzw;
    still.paint = stillPaint[at];
    still.glow = stillGlow[at];
    still.face = stillFaces[at];
    // The engine keeps no share of a surface's diffuse light sent through to its far side.
    still.through = 0.0;
    FieldCell stamped = stampCell(BrickShapes(push.counts.x * 4u + brick.z, brick.w), p, reach, still);
    field[at] = float4(stamped.distance, stamped.sheet);
    albedoField[at] = stamped.paint;
    glowField[at] = stamped.glow;
    faceField[at] = stamped.face;
}
