// Stamps the shapes of the meshes that moved into the field the passes read, a workgroup a brick
// of 4 by 4 by 4 cells. Each cell takes the least of its distance in the field the still meshes
// make and its distance to each shape that comes within its brick, a box around a mesh or a part
// of one in its own space, so a brick stamped last frame and not this one goes back to what the
// still meshes make. A brick reads the shapes of its own run alone, so a frame's cost grows with
// the shapes of each brick and not with every shape the frame stamps.
import scenefield;

struct FieldShape
{
    // The rows of the matrix from the world into the shape's own space, a 3x4.
    float4 row0;
    float4 row1;
    float4 row2;
    // xyz: the box's middle in its own space. w: the least scale from its own space to the
    // world's, which a distance there is turned into a world distance no longer than the true one by.
    float4 centerAndScale;
    // xyz: the box's half extents in its own space.
    float4 extents;
};

// x: the cascade. y: the brick's place in it, in bricks, a byte an axis. z: where its run of shapes
// begins. w: how many shapes are in it. Past the bricks, the runs one after another, four shapes'
// places to an element.
[[vk::binding(0, 0)]]
StructuredBuffer<uint4> bricks;

[[vk::binding(1, 0)]]
StructuredBuffer<FieldShape> shapes;

[[vk::binding(2, 0)]] [format("r16f")]
RWTexture3D<float> stillField;

[[vk::binding(3, 0)]] [format("r16f")]
RWTexture3D<float> field;

[[vk::binding(4, 0)]]
ConstantBuffer<SceneFieldInfo> info;

struct StampPush
{
    // x: how many bricks. y: how many shapes. zw: unused.
    uint4 counts;
};

[[vk::push_constant]]
ConstantBuffer<StampPush> push;

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
    float d = stillField[at];
    for (uint i = 0; i < brick.w; i++)
    {
        uint run = push.counts.x * 4u + brick.z + i;
        FieldShape shape = shapes[bricks[run / 4u][run % 4u]];
        float4 h = float4(p, 1.0);
        float3 own = float3(dot(shape.row0, h), dot(shape.row1, h), dot(shape.row2, h)) - shape.centerAndScale.xyz;
        float3 q = abs(own) - shape.extents.xyz;
        float box = (length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0)) * shape.centerAndScale.w;
        d = min(d, max(box, -reach));
    }
    field[at] = d;
}
