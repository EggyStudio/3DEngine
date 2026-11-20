// Writes the distances from each cell of one cascade of the scene's distance field to the triangles
// of the meshes it is built from, a workgroup a triangle. The triangle is put in the world by its
// instance's matrix, and every cell within the band of cells around it, a box the triangle's bounds
// grown by the band, takes its distance to the triangle, the least of those any triangle gives kept
// by an atomic minimum of a word that holds the distance in 1024ths of a cell above a bit set where
// a double-sided triangle's nearest point to the cell is off its plane's inside, on its rim, a bit
// set where the triangle is double-sided, a bit set where the cell lies in front of the triangle's
// face, the way the face looks in eight bits, and seven bits of its instance last. Of two triangles
// as near to that 1024th, as where a crate stands on the ground, one behind whose face the cell
// lies wins, the cell being inside some mesh, and of two meshes' faces alike in all but the
// instance, the instance's bits say which, so no thread's order does. A double-sided triangle has
// no inside, and its front bit says which side of its plane the cell lies on, so the resolve keeps
// the plane in the cell where the nearest point lies on the face's inside, and a ray traced through
// the field meets a sheet thinner than a cell, as a wall or a floor of a mesh imported
// double-sided, at its plane from either side wherever it lies between the cells. A cell is behind
// a triangle only where the way from the triangle's nearest point to it lies within 60 degrees of
// straight back from the face, so where an edge or a corner is nearest, as above a pillar's rim or
// level with a wall's side above the wall, a face the way runs along does not put the cell inside,
// and a cell whose nearest point lies on an edge no other triangle shares, as a ground plane's rim,
// is in front, so an open mesh puts no wedge below its rim inside. The nearest face a cell lies
// straight behind is kept apart, for a wall thinner than a cell, which may have no cell's middle
// inside it. A cell no triangle comes within the band of keeps the word it was cleared to, the
// band's distance in front. A second dispatch over the same triangles paints each cell the color
// and the emission of a triangle whose word is the one the cell kept, its nearest surface, which
// global illumination is read from, its instance's color times its vertices' colors blended at the
// cell's nearest point, as the model pass blends them across a face. A fourth paints again the
// cells a mesh whose colors were written is nearest, its instances alone, against the words its
// cascade's build kept, into the images the passes read, so colors written in place reach global
// illumination without the distances found again.

import color;
import scenefield;
// The cells' codes, the triangle's nearest point and the paint's corners and alpha, the
// RadianceCascades module's.
import rc_field_splat;

struct FieldInstance
{
    // The rows of the instance's world matrix, a 3x4.
    float4 row0;
    float4 row1;
    float4 row2;
    // x: its mesh's first triangle in the pool. y: how many it has. z: 1 in its lowest bit when the
    // mesh has no inside, as a double-sided one, whose cells are all in front, the seven bits after
    // it the instance's own that end its words, and in its second byte the share of light that
    // passes a unit of its depth in 255ths (lightThroughPaint). w: the
    // first of its triangles counted over every instance before it. z's third byte holds its
    // roughness and its fourth its metallic share, in 255ths.
    uint4 triangles;
    // rgb: the surface's color, linear, its material's times its texture's average. w: 2 where
    // global illumination carries its emission as a glow light, 1 elsewhere.
    float4 color;
    // rgb: the surface's emission, linear. w: where it is emissive, its thinnest
    // extent in world units, its mesh's bounds along each of its own axes scaled into the world.
    float4 emission;
};

// Each triangle of every mesh the field is built from, three corners in the mesh's own space, each
// its place's bits and in w its vertex's color, three sRGB bytes, white where the mesh has none,
// with the bit above them set where the edge from it to the triangle's next corner is open.
[[vk::binding(0, 0)]]
StructuredBuffer<uint4> corners;

[[vk::binding(1, 0)]]
StructuredBuffer<FieldInstance> instances;

// A word a cell of the cascade, as the distance's code.
[[vk::binding(2, 0)]]
RWStructuredBuffer<uint> cells;

// The color of each cell's nearest surface, eight bits a channel, linear, and the light it gives
// off, half floats, which the second dispatch writes.
[[vk::binding(3, 0)]]
RWStructuredBuffer<uint> albedo;

[[vk::binding(4, 0)]]
RWStructuredBuffer<uint2> glow;

// The nearest face each cell lies behind, in 1024ths of a cell, which field_resolve.slang reads for
// a wall thinner than a cell.
[[vk::binding(5, 0)]]
RWStructuredBuffer<uint> behind;

// What the emitters thinner than a cell lend each cell they lie near, six words a cell, the most
// light any of them emits, red, green and blue in 256ths, and the area of their faces near the
// cell seen along x, y and z, in 1024ths of a cell's face, which field_resolve.slang reads.
[[vk::binding(6, 0)]]
RWStructuredBuffer<uint> emitted;

// The colors the still meshes paint and those the passes read, cascade after cascade along z, which
// the dispatch that paints again writes.
[[vk::binding(7, 0)]] [format("rgba8")]
RWTexture3D<float4> stillPaint;

[[vk::binding(8, 0)]] [format("rgba8")]
RWTexture3D<float4> albedoField;

// The word of a second double-sided face of another way as near a cell as the one its word kept,
// within a cell, as where two walls cross on a cell's side, which the painting pass keeps by an
// atomic minimum and field_resolve.slang turns into the cell's second plane.
[[vk::binding(9, 0)]]
RWStructuredBuffer<uint> second;

// The gloss of each cell's nearest surface (glossBits), painted with its color, which
// field_resolve.slang puts beside its face.
[[vk::binding(10, 0)]]
RWStructuredBuffer<uint> gloss;

struct SplatPush
{
    // xyz: the cascade's corner. w: the width of a cell.
    float4 originAndCell;
    // x: the cells along each side. y: how many instances. z: how many triangles in all. w: the
    // band in cells.
    uint4 counts;
    // x: how many workgroups the dispatch has along x, which the triangle's number is read across.
    // y: 1 for the dispatch that paints, 0 for the one that finds the distances, 2 for the one
    // that lends an emitter thinner than a cell's light to the cells near it, 3 for the one that
    // paints again. z: where the cascade's words begin in cells, and w: the cascade, for that one.
    uint4 groups;
    // xyz: the first and the last cell along each axis a triangle writes, the whole cascade's or,
    // for a build of a region, its cells and a cell around them, past which the build keeps the
    // cascade as it was. w: unused.
    uint4 firstCell;
    uint4 lastCell;
};

[[vk::push_constant]]
ConstantBuffer<SplatPush> push;

float3 place(FieldInstance instance, float3 p)
{
    float4 h = float4(p, 1.0);
    return float3(dot(instance.row0, h), dot(instance.row1, h), dot(instance.row2, h));
}

groupshared uint triangleInstance;

// The area of the triangle a b c, given in coordinates of its own plane, within the square of half
// side h about the plane's origin, the triangle cut by each of the square's sides in turn
// (Sutherland and Hodgman), seven corners at most.
float areaWithin(float2 a, float2 b, float2 c, float h)
{
    float2 lo = min(a, min(b, c)), hi = max(a, max(b, c));
    if (all(lo >= -h) && all(hi <= h))
        return abs(cross(float3(b - a, 0.0), float3(c - a, 0.0)).z) * 0.5;
    if (any(lo > h) || any(hi < -h))
        return 0.0;
    float2 corners[8];
    float2 cut[8];
    corners[0] = a;
    corners[1] = b;
    corners[2] = c;
    uint count = 3u;
    for (uint side = 0u; side < 4u; side++)
    {
        float sign = (side & 1u) == 0u ? 1.0 : -1.0;
        uint axis = side >> 1;
        uint kept = 0u;
        for (uint i = 0u; i < count; i++)
        {
            float2 p = corners[i], q = corners[(i + 1u) % count];
            float dp = sign * p[axis] - h, dq = sign * q[axis] - h;
            if (dp <= 0.0)
                cut[kept++] = p;
            if ((dp <= 0.0) != (dq <= 0.0) && kept < 8u)
                cut[kept++] = lerp(p, q, dp / (dp - dq));
        }
        count = kept;
        for (uint i = 0u; i < count; i++)
            corners[i] = cut[i];
        if (count < 3u)
            return 0.0;
    }
    float twice = 0.0;
    for (uint i = 0u; i < count; i++)
    {
        float2 p = corners[i], q = corners[(i + 1u) % count];
        twice += p.x * q.y - q.x * p.y;
    }
    return abs(twice) * 0.5;
}

// Two floats as halves in a word, by GLSL's own instruction, which needs no 16-bit types of the
// device, where HLSL's f32tof16 declares them.
uint packHalves(float2 v)
{
    return spirv_asm { result:$$uint = OpExtInst glsl450 PackHalf2x16 $v };
}

// The cascade's cells as the module's splat keeps their codes, the least of each kept by an atomic
// minimum, since threads of other triangles write the same cells.
struct SplatCells : ISplatCells
{
    void keepLeast(uint index, uint code) { InterlockedMin(cells[index], code); }
    void keepBehind(uint index, uint code) { InterlockedMin(behind[index], code); }
}

// The cascade's cells as the module's paint reads their codes and paints them. The dispatch that
// paints writes the colors, gloss and glow the resolve reads, and the one that paints again writes
// the still meshes' colors at the cascade's place along z, from the codes it kept there.
struct PaintCells : IPaintCells
{
    bool still;
    uint codes;
    int layer;
    int size;
    bool carried;

    uint kept(uint index) { return cells[codes + index]; }

    void paint(uint index, float4 color, float3 emission, uint glossed)
    {
        uint4 c = uint4(color * 255.0 + 0.5);
        if (still)
        {
            uint side = uint(size);
            int3 into = int3(int(index % side), int((index / side) % side), int(index / (side * side)) + layer);
            float4 written = float4(c) / 255.0;
            stillPaint[into] = written;
            albedoField[into] = written;
            return;
        }
        albedo[index] = c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
        gloss[index] = glossed;
        glow[index] = uint2(packHalves(emission.rg), packHalves(float2(emission.b, carried && any(emission > 0.0) ? 1.0 : 0.0)));
    }
}

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
    // An emitter thinner than a cell paints no cell its own where a surface behind it is nearer
    // the cells' middles, as a bright strip on a wall, so its light would be in the field nowhere
    // and add nothing to global illumination. It lends the cells within half a cell of it its
    // light, with the area of its faces near each, which the resolve turns into the share of a
    // cell's face it covers, so a strip 0.06 wide lends cells of 0.15 two fifths of its light and a
    // sheet as thin lends its whole light, where a share of its thickness over the cell would lend
    // a sheet 0.02 thick a seventh.
    // An emitter global illumination carries as a glow light lends nothing, its light reaching the
    // cells' surfaces as a light's does.
    float thickness = instance.emission.w;
    bool carried = instance.color.w > 1.5;
    if (push.groups.y == 2u && (carried || !(thickness > 0.0 && thickness < push.originAndCell.w && any(instance.emission.rgb > 0.0))))
        return;
    uint corner = (instance.triangles.x + t - instance.triangles.w) * 3;
    uint4 ca = corners[corner], cb = corners[corner + 1], cc = corners[corner + 2];
    float3 a = place(instance, asfloat(ca.xyz));
    float3 b = place(instance, asfloat(cb.xyz));
    float3 c = place(instance, asfloat(cc.xyz));
    bool3 edges = bool3(((ca.w >> 24) & 1u) != 0u, ((cb.w >> 24) & 1u) != 0u, ((cc.w >> 24) & 1u) != 0u);
    float3 face = cross(b - a, c - a);
    // A triangle of next to no area beside its longest side, as a sphere's last row has at a pole
    // whose corners the rounding of sin(pi) leaves apart, has a face turned whichever way
    // that rounding took it, which, at a distance the triangles around it share, would say cells
    // outside lie behind it and turn them inside. It holds no surface its neighbors do not, and
    // is passed over.
    float longest = max(dot(b - a, b - a), max(dot(c - b, c - b), dot(a - c, a - c)));
    if (dot(face, face) <= 1e-8 * longest * longest)
        return;
    // The triangle and the cascade as the module's splat takes them.
    SplatTriangle given;
    given.a = a;
    given.b = b;
    given.c = c;
    given.openEdges = edges;
    given.doubleSided = (instance.triangles.z & 1u) != 0u;
    given.instance = instance.triangles.z >> 1u;
    SplatCascade into;
    into.corner = push.originAndCell.xyz;
    into.cell = push.originAndCell.w;
    into.size = int(push.counts.x);
    into.band = float(push.counts.w);
    into.first = int3(push.firstCell.xyz);
    into.last = int3(push.lastCell.xyz);
    // The codes, the module's splat of the triangle into the cells its band covers.
    if (push.groups.y == 0u)
    {
        SplatCells codes;
        splatCodes(codes, given, into, lane, 64u);
        return;
    }
    bool sided = (instance.triangles.z & 1u) == 0u && dot(face, face) > 1e-20;
    float faceLength = length(face);

    float cell = push.originAndCell.w;
    float band = float(push.counts.w);
    float reach = band * cell;
    int size = int(push.counts.x);
    float3 low = (min(a, min(b, c)) - push.originAndCell.xyz) / cell - band;
    float3 high = (max(a, max(b, c)) - push.originAndCell.xyz) / cell + band;
    int3 first = max(int3(floor(low)), int3(push.firstCell.xyz));
    int3 last = min(int3(ceil(high)), int3(push.lastCell.xyz));
    if (any(last < first))
        return;
    int3 span = last - first + 1;
    uint count = uint(span.x * span.y * span.z);

    if (push.groups.y == 2u)
    {
        if (faceLength <= 1e-10)
            return;
        // The cells within a cell of the triangle, which lie within its bounds grown by one, every
        // cell a point on it is blended from where the field is read there.
        first = max(int3(floor((min(a, min(b, c)) - push.originAndCell.xyz) / cell - 1.0)), int3(push.firstCell.xyz));
        last = min(int3(ceil((max(a, max(b, c)) - push.originAndCell.xyz) / cell + 1.0)), int3(push.lastCell.xyz));
        if (any(last < first))
            return;
        span = last - first + 1;
        count = uint(span.x * span.y * span.z);
        uint3 light = uint3(max(instance.emission.rgb, 0.0) * 256.0);
        // The face's way and two ways along it, square to the axes where the face is, so a cell's
        // square lies along an upright wall's rows of cells.
        float3 n = face / faceLength;
        float3 across = abs(n.x) < abs(n.y) ? (abs(n.x) < abs(n.z) ? float3(1, 0, 0) : float3(0, 0, 1)) : (abs(n.y) < abs(n.z) ? float3(0, 1, 0) : float3(0, 0, 1));
        float3 u = normalize(cross(n, across));
        float3 v = cross(n, u);
        // The share of a cell's face it covers seen along each axis, 1024 a whole face.
        float3 seen = abs(n) * (1024.0 / (cell * cell));
        for (uint k = lane; k < count; k += 64u)
        {
            int3 at = first + int3(int(k % uint(span.x)), int((k / uint(span.x)) % uint(span.y)), int(k / uint(span.x * span.y)));
            float3 p = push.originAndCell.xyz + (float3(at) + 0.5) * cell;
            bool open;
            if (length(p - nearestOnTriangle(p, a, b, c, edges, open)) >= cell)
                continue;
            // A face of a closed mesh counts where the cell lies before it, as a sheet's top face
            // for a cell above it and its bottom face for one below, so the two faces of a sheet
            // are not counted twice; a mesh with no inside counts every face.
            float off = dot(p - a, n);
            if (sided && off <= 0.0)
                continue;
            // The face's area within a cell's square about the cell's middle laid on its plane.
            float3 q = p - n * off;
            float area = areaWithin(float2(dot(a - q, u), dot(a - q, v)), float2(dot(b - q, u), dot(b - q, v)),
                float2(dot(c - q, u), dot(c - q, v)), cell * 0.5);
            if (area <= 0.0)
                continue;
            uint3 share = uint3(area * seen + 0.5);
            uint index = (at.z * size + at.y) * size + at.x;
            InterlockedMax(emitted[index * 6u], light.r);
            InterlockedMax(emitted[index * 6u + 1u], light.g);
            InterlockedMax(emitted[index * 6u + 2u], light.b);
            InterlockedAdd(emitted[index * 6u + 3u], share.x);
            InterlockedAdd(emitted[index * 6u + 4u], share.y);
            InterlockedAdd(emitted[index * 6u + 5u], share.z);
        }
        return;
    }

    // A second double-sided face of another way passing through or beside the cell, as where two
    // walls cross on a cell's side and the four cells about it lie as near both, each keeping one,
    // so the other would be held there by the cells' blend alone and the world's probes standing
    // among them would read dark round patches at the walls' feet.
    if (push.groups.y == 1u && !sided)
    {
        for (uint k = lane; k < count; k += 64u)
        {
            int3 at = first + int3(int(k % uint(span.x)), int((k / uint(span.x)) % uint(span.y)), int(k / uint(span.x * span.y)));
            float3 p = push.originAndCell.xyz + (float3(at) + 0.5) * cell;
            float d;
            uint behindCode;
            uint code = cellCode(p, a, b, c, edges, given.doubleSided, given.instance, cell, d, behindCode);
            uint index = (at.z * size + at.y) * size + at.x;
            uint kept = cells[index];
            if (d < reach && d < cell * 0.75 && (code & Rim) == 0u && kept != code
                && (kept & (Rim | DoubleSided)) == DoubleSided && ((kept >> WayShift) & 255u) != ((code >> WayShift) & 255u))
                InterlockedMin(second[index], code);
        }
    }

    // The paint, the module's, of each cell whose code the triangle kept, its color blended from
    // its corners' times the paint's alpha, with its emission and gloss.
    SplatSurface surface;
    surface.color = instance.color.rgb;
    surface.corners = float3x3(cornerColor(ca.w), cornerColor(cb.w), cornerColor(cc.w));
    surface.lightThrough = float((instance.triangles.z >> 8u) & 255u) / 255.0;
    surface.emission = max(instance.emission.rgb, 0.0);
    surface.gloss = glossBits(float((instance.triangles.z >> 16u) & 255u) / 255.0, float(instance.triangles.z >> 24u) / 255.0);
    PaintCells painted;
    painted.still = push.groups.y == 3u;
    painted.codes = painted.still ? push.groups.z : 0u;
    painted.layer = int(push.groups.w) * size;
    painted.size = size;
    painted.carried = carried;
    splatPaint(painted, given, into, surface, lane, 64u);
}
