// Holds the light of the cells of the scene's distance field the merges' marches meet this frame,
// those within a cell of a surface that no finer cascade holds, by the RadianceCascades module's
// holdCellLight, each side of its surface, a sheet's two and a solid's one, lit as the trace lights
// a hit (ShadedField), which the merges read at the side of the cell their marches meet
// (HeldHitLight in gi_merge.slang). Built with LIST_HELD, it lists those of the cells the merges'
// marches alone marked (MarkedCells in gi_merge.slang), a thread a cell of the field
// (listHeldCell); without, it shades the listed cells, a thread a listed cell (holdListedCell), so
// no workgroup waits on a few of its threads. Both run each frame before the merges, so a change
// shows in the next frame's light.
import gi;
import gi_hits;
import rc_probe_passes;

// The light held for each cell of each of the field's cascades, the cascades one after another
// along z, and along x for each side of the cell's surface, a sheet's two and a solid's one, the
// light it gives of its own, its glow and the lights', and after it the light it sends on of what
// reached it past the first bounce, which the merge takes times its probe's share.
[[vk::binding(22, 0)]] [format("rgba16f")]
RWTexture3D<float4> heldLight;

struct HoldPush
{
    // x: the probes along each side. y: the cascades of probes. z: the field's cascades.
    uint4 counts;
    // x: the probes' spacing in cells.
    float4 spacing;
};

[[vk::push_constant]]
ConstantBuffer<HoldPush> push;

// The cells to shade, the workgroups of the dispatch that shades them along x, y and z, their
// count, and their indices as holdCellLight counts them, one after another.
[[vk::binding(23, 0)]]
RWStructuredBuffer<uint> heldList;

// The cells the merge's marches meet this frame, a bit a cell (MarkedCells in gi_merge.slang), the
// only ones listed.
[[vk::binding(24, 0)]]
RWStructuredBuffer<uint> heldMarks;

// The texel of a cell's side's own light, part 0, or its probes' light, part 1.
int3 heldTexel(int cascade, int3 cell, uint side, uint part)
{
    int size = int(fieldInfo.counts.y);
    return int3(cell.x + int(side * 2u + part) * size, cell.y, cell.z + cascade * size);
}

// The held light, each side's own and its probes' apart.
struct HeldCells : IHeldLight
{
    float3 heldAt(int cascade, int3 cell, uint side) { return heldLight[heldTexel(cascade, cell, side, 0u)].rgb; }
    float3 heldProbeLightAt(int cascade, int3 cell, uint side) { return heldLight[heldTexel(cascade, cell, side, 1u)].rgb; }
    float probeShare(float3 place, int cascade, int3 cell) { return 1.0; }

    void hold(int cascade, int3 cell, uint side, float3 own, float3 probes)
    {
        heldLight[heldTexel(cascade, cell, side, 0u)] = float4(own, 1.0);
        heldLight[heldTexel(cascade, cell, side, 1u)] = float4(probes, 1.0);
    }
}

// The last place shaded and its light, its own and its probes' apart, so the two reads of one
// side's shading shade it once.
static bool heldShaded = false;
static float3 heldPlace, heldWay, heldOwn, heldProbes;

// The surface at place toward -way lit as the trace lights a hit, its own light the one that
// shading leaves (hitOwn) and the rest its probes'.
void shadeHeld(ShadedField field, float3 place, float3 way, int cascade, int3 cell)
{
    if (heldShaded && all(place == heldPlace) && all(way == heldWay))
        return;
    float3 light = field.hitLight(place, way, cascade, cell);
    heldOwn = hitOwn;
    heldProbes = light - hitOwn;
    heldPlace = place;
    heldWay = way;
    heldShaded = true;
}

// The trace's shading of a hit, its own light and its probes' apart (IHeldShading).
struct HeldShading : IHeldShading
{
    ShadedField field;

    float3 ownLight(float3 place, float3 way, int cascade, int3 cell)
    {
        shadeHeld(field, place, way, cascade, cell);
        return heldOwn;
    }

    float3 probeLight(float3 place, float3 way, int cascade, int3 cell)
    {
        shadeHeld(field, place, way, cascade, cell);
        return heldProbes;
    }
}

// The list of cells to shade over heldList (IHeldList), one more workgroup to shade them for
// every 64 listed.
struct HeldListBuffer : IHeldList
{
    void list(uint at)
    {
        if ((heldMarks[at >> 5u] & (1u << (at & 31u))) == 0u)
            return;
        uint slot;
        InterlockedAdd(heldList[3], 1u, slot);
        heldList[4u + slot] = at;
        if (slot % 64u == 0u)
            InterlockedAdd(heldList[0], 1u);
    }

    uint count() { return heldList[3]; }
    uint listedAt(uint index) { return heldList[4u + index]; }
}

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    // The whole of the indirect light, and no widening, since a cell's light is the same for every
    // ray that meets it.
    ShadedField field;
    field.share = float3(1.0);
    field.widen = 0.0;
    field.probes = float(push.counts.x);
    field.spacing = push.spacing.x;
    field.cascades = float(push.counts.y);
    HeldListBuffer list;
#if defined(LIST_HELD)
    listHeldCell(field, list, push.counts.z, 1u, 0u, id.x);
#else
    HeldShading shading = { field };
    HeldCells held;
    holdListedCell(field, shading, held, list, id.x);
#endif
}
