// Writes the distances from each cell of one cascade of the scene's distance field to the
// triangles of the meshes it is built from, a workgroup a triangle. The triangle is put in the
// world by its instance's matrix, and every cell within the band of cells around it, a box the
// triangle's bounds grown by the band, takes its distance to the triangle, the least of those any
// triangle gives kept by an atomic minimum of a word that holds the distance in 1024ths of a cell
// above a bit set where the cell lies in front of the triangle's face. Of two triangles as near to
// that 1024th, as where a crate stands on the ground, one behind whose face the cell lies wins, the
// cell being inside some mesh. A cell is behind a triangle only where the way from the triangle's
// nearest point to it lies within 60 degrees of straight back from the face, so where an edge or a
// corner is nearest, as above a pillar's rim or level with a wall's side above the wall, a face
// the way runs along does not put the cell inside, and of an open mesh, as a ground plane, only a
// narrow wedge below its edge is inside. A cell no triangle comes within the band of keeps the
// word it was cleared to, the band's distance in front.

struct FieldInstance
{
    // The rows of the instance's world matrix, a 3x4.
    float4 row0;
    float4 row1;
    float4 row2;
    // x: its mesh's first triangle in the pool. y: how many it has. z: 1 when the mesh has no
    // inside, as a double-sided one, whose cells are all in front. w: the first of its triangles
    // counted over every instance before it.
    uint4 triangles;
};

// Each triangle of every mesh the field is built from, three corners in the mesh's own space.
[[vk::binding(0, 0)]]
StructuredBuffer<float4> corners;

[[vk::binding(1, 0)]]
StructuredBuffer<FieldInstance> instances;

// A word a cell of the cascade, as the distance's code.
[[vk::binding(2, 0)]]
RWStructuredBuffer<uint> cells;

struct SplatPush
{
    // xyz: the cascade's corner. w: the width of a cell.
    float4 originAndCell;
    // x: the cells along each side. y: how many instances. z: how many triangles in all. w: the band in cells.
    uint4 counts;
    // x: how many workgroups the dispatch has along x, which the triangle's number is read across.
    uint4 groups;
};

[[vk::push_constant]]
ConstantBuffer<SplatPush> push;

// The point of the triangle abc nearest p, as Ericson's Real-Time Collision Detection finds it by
// the region of the triangle's plane p falls in.
float3 nearest(float3 p, float3 a, float3 b, float3 c)
{
    float3 ab = b - a, ac = c - a, ap = p - a;
    float d1 = dot(ab, ap), d2 = dot(ac, ap);
    if (d1 <= 0.0 && d2 <= 0.0)
        return a;
    float3 bp = p - b;
    float d3 = dot(ab, bp), d4 = dot(ac, bp);
    if (d3 >= 0.0 && d4 <= d3)
        return b;
    float vc = d1 * d4 - d3 * d2;
    if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
        return a + ab * (d1 / (d1 - d3));
    float3 cp = p - c;
    float d5 = dot(ab, cp), d6 = dot(ac, cp);
    if (d6 >= 0.0 && d5 <= d6)
        return c;
    float vb = d5 * d2 - d1 * d6;
    if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
        return a + ac * (d2 / (d2 - d6));
    float va = d3 * d6 - d5 * d4;
    if (va <= 0.0 && (d4 - d3) >= 0.0 && (d5 - d6) >= 0.0)
        return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
    float denominator = 1.0 / (va + vb + vc);
    return a + ab * (vb * denominator) + ac * (vc * denominator);
}

float3 place(FieldInstance instance, float3 p)
{
    float4 h = float4(p, 1.0);
    return float3(dot(instance.row0, h), dot(instance.row1, h), dot(instance.row2, h));
}

groupshared uint triangleInstance;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 group : SV_GroupID, uint lane : SV_GroupIndex)
{
    uint t = group.y * push.groups.x + group.x;
    if (t >= push.counts.z)
        return;

    // The instance the triangle is of, the last whose first triangle is at or before it.
    if (lane == 0)
    {
        uint lo = 0, hi = push.counts.y - 1;
        while (lo < hi)
        {
            uint mid = (lo + hi + 1) / 2;
            if (instances[mid].triangles.w <= t)
                lo = mid;
            else
                hi = mid - 1;
        }
        triangleInstance = lo;
    }
    GroupMemoryBarrierWithGroupSync();
    FieldInstance instance = instances[triangleInstance];
    uint corner = (instance.triangles.x + t - instance.triangles.w) * 3;
    float3 a = place(instance, corners[corner].xyz);
    float3 b = place(instance, corners[corner + 1].xyz);
    float3 c = place(instance, corners[corner + 2].xyz);
    float3 face = cross(b - a, c - a);
    bool sided = instance.triangles.z == 0u && dot(face, face) > 1e-20;

    float cell = push.originAndCell.w;
    float band = float(push.counts.w);
    float reach = band * cell;
    int size = int(push.counts.x);
    float3 low = (min(a, min(b, c)) - push.originAndCell.xyz) / cell - band;
    float3 high = (max(a, max(b, c)) - push.originAndCell.xyz) / cell + band;
    int3 first = max(int3(floor(low)), int3(0));
    int3 last = min(int3(ceil(high)), int3(size - 1));
    if (any(last < first))
        return;
    int3 span = last - first + 1;
    uint count = uint(span.x * span.y * span.z);

    for (uint k = lane; k < count; k += 64u)
    {
        int3 at = first + int3(int(k % uint(span.x)), int((k / uint(span.x)) % uint(span.y)), int(k / uint(span.x * span.y)));
        float3 p = push.originAndCell.xyz + (float3(at) + 0.5) * cell;
        float3 q = nearest(p, a, b, c);
        float d = length(p - q);
        if (d >= reach)
            continue;
        uint front = sided && dot(p - q, face) < -0.5 * d * length(face) ? 0u : 1u;
        uint code = (uint(d / cell * 1024.0) << 1) | front;
        InterlockedMin(cells[(at.z * size + at.y) * size + at.x], code);
    }
}
