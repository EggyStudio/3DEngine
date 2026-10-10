// Writes the distances from each cell of one cascade of the scene's distance field to the
// triangles of the meshes it is built from, a workgroup a triangle. The triangle is put in the
// world by its instance's matrix, and every cell within the band of cells around it, a box the
// triangle's bounds grown by the band, takes its distance to the triangle, the least of those any
// triangle gives kept by an atomic minimum of a word that holds the distance in 1024ths of a cell
// above a bit set where the triangle is double-sided, a bit set where the cell lies in front of
// the triangle's face, and the way the face looks in eight bits. Of two triangles as near to that
// 1024th, as where a crate stands on the ground, one behind whose face the cell lies wins, the cell
// being inside some mesh. A double-sided
// triangle has no inside, and the resolve takes half a cell off its distance, so a sheet thinner
// than a cell, as a wall or a floor of a mesh imported double-sided, crosses zero between the cells
// on either side wherever it lies between them and stops a ray traced through the field. A cell is behind a triangle only where the way from the triangle's
// nearest point to it lies within 60 degrees of straight back from the face, so where an edge or a
// corner is nearest, as above a pillar's rim or level with a wall's side above the wall, a face
// the way runs along does not put the cell inside, and a cell whose nearest point lies on an edge
// no other triangle shares, as a ground plane's rim, is in front, so an open mesh puts no wedge
// below its rim inside. The nearest face a cell lies straight behind is kept apart, for a wall
// thinner than a cell, which may have no cell's middle inside it. A cell no triangle comes within the band of
// keeps the word it was cleared to, the band's distance in front. A second dispatch over the same triangles
// paints each cell the color and the light given off of a triangle whose word is the one the cell
// kept, its nearest surface, which light that bounces is read from, its instance's color times its
// vertices' colors blended at the cell's nearest point, as the model pass blends them across a face.

import color;

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
    // rgb: the surface's color, linear, its material's times its texture's average.
    float4 color;
    // rgb: the light the surface gives off, linear. w: where it gives off light, its thinnest
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

// What the emitters thinner than a cell lend each cell they lie near, six words a cell: the most
// light any of them gives off, red, green and blue in 256ths, and the area of their faces near the
// cell seen along x, y and z, in 1024ths of a cell's face, which field_resolve.slang reads.
[[vk::binding(6, 0)]]
RWStructuredBuffer<uint> emitted;

struct SplatPush
{
    // xyz: the cascade's corner. w: the width of a cell.
    float4 originAndCell;
    // x: the cells along each side. y: how many instances. z: how many triangles in all. w: the band in cells.
    uint4 counts;
    // x: how many workgroups the dispatch has along x, which the triangle's number is read across.
    // y: 1 for the dispatch that paints, 0 for the one that finds the distances, 2 for the one
    // that lends an emitter thinner than a cell's light to the cells near it.
    uint4 groups;
};

[[vk::push_constant]]
ConstantBuffer<SplatPush> push;

// The point of the triangle abc nearest p, as Ericson's Real-Time Collision Detection finds it by
// the region of the triangle's plane p falls in, and in open whether that point lies on an edge
// the triangle shares with no other, as edges says of ab, bc and ca, or on a corner of one.
float3 nearest(float3 p, float3 a, float3 b, float3 c, bool3 edges, out bool open)
{
    float3 ab = b - a, ac = c - a, ap = p - a;
    float d1 = dot(ab, ap), d2 = dot(ac, ap);
    open = edges.x || edges.z;
    if (d1 <= 0.0 && d2 <= 0.0)
        return a;
    float3 bp = p - b;
    float d3 = dot(ab, bp), d4 = dot(ac, bp);
    open = edges.x || edges.y;
    if (d3 >= 0.0 && d4 <= d3)
        return b;
    float vc = d1 * d4 - d3 * d2;
    open = edges.x;
    if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
        return a + ab * (d1 / (d1 - d3));
    float3 cp = p - c;
    float d5 = dot(ab, cp), d6 = dot(ac, cp);
    open = edges.y || edges.z;
    if (d6 >= 0.0 && d5 <= d6)
        return c;
    float vb = d5 * d2 - d1 * d6;
    open = edges.z;
    if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
        return a + ac * (d2 / (d2 - d6));
    float va = d3 * d6 - d5 * d4;
    open = edges.y;
    if (va <= 0.0 && (d4 - d3) >= 0.0 && (d5 - d6) >= 0.0)
        return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
    open = false;
    float denominator = 1.0 / (va + vb + vc);
    return a + ab * (vb * denominator) + ac * (vc * denominator);
}

float3 place(FieldInstance instance, float3 p)
{
    float4 h = float4(p, 1.0);
    return float3(dot(instance.row0, h), dot(instance.row1, h), dot(instance.row2, h));
}

// A corner's color in linear light, from the sRGB bytes its word holds below its open edge's bit.
float3 cornerColor(uint word)
{
    return srgbToLinear(float3(float(word & 255u), float((word >> 8) & 255u), float((word >> 16) & 255u)) / 255.0);
}

// How much of each of a triangle's corners a point on it is made of.
float3 barycentric(float3 p, float3 a, float3 b, float3 c)
{
    float3 ab = b - a, ac = c - a, ap = p - a;
    float d00 = dot(ab, ab), d01 = dot(ab, ac), d11 = dot(ac, ac), d20 = dot(ap, ab), d21 = dot(ap, ac);
    float denominator = d00 * d11 - d01 * d01;
    if (denominator <= 1e-20)
        return float3(1.0, 0.0, 0.0);
    float v = saturate((d11 * d20 - d01 * d21) / denominator);
    float w = saturate((d00 * d21 - d01 * d20) / denominator);
    float3 weights = float3(max(1.0 - v - w, 0.0), v, w);
    return weights / max(weights.x + weights.y + weights.z, 1e-6);
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

// A face's way in eight bits, four along each side of the octahedron the sphere is folded onto,
// enough to tell faces that look apart from faces that meet square.
uint way8(float3 n)
{
    n /= abs(n.x) + abs(n.y) + abs(n.z);
    float2 o = n.z >= 0.0 ? n.xy : (1.0 - abs(n.yx)) * float2(n.x >= 0.0 ? 1.0 : -1.0, n.y >= 0.0 ? 1.0 : -1.0);
    uint2 q = min(uint2((o * 0.5 + 0.5) * 16.0), uint2(15u));
    return q.x | (q.y << 4);
}

// Two floats as halves in a word, by GLSL's own instruction, which needs no 16-bit types of the
// device, where HLSL's f32tof16 declares them.
uint packHalves(float2 v)
{
    return spirv_asm { result:$$uint = OpExtInst glsl450 PackHalf2x16 $v };
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
    // the cells' middles, as a bright strip on a wall, so its light was in the field nowhere and lit
    // nothing that bounces. It lends the cells within half a cell of it its light, with the area of
    // its faces near each, which the resolve turns into the share of a cell's face it covers, so a
    // strip 0.06 wide lends cells of 0.15 two fifths of its light and a sheet as thin lends its
    // whole light, where a share of its thickness over the cell lent a sheet 0.02 thick a seventh.
    float thickness = instance.emission.w;
    if (push.groups.y == 2u && !(thickness > 0.0 && thickness < push.originAndCell.w && any(instance.emission.rgb > 0.0)))
        return;
    uint corner = (instance.triangles.x + t - instance.triangles.w) * 3;
    uint4 ca = corners[corner], cb = corners[corner + 1], cc = corners[corner + 2];
    float3 a = place(instance, asfloat(ca.xyz));
    float3 b = place(instance, asfloat(cb.xyz));
    float3 c = place(instance, asfloat(cc.xyz));
    bool3 edges = bool3(((ca.w >> 24) & 1u) != 0u, ((cb.w >> 24) & 1u) != 0u, ((cc.w >> 24) & 1u) != 0u);
    float3 face = cross(b - a, c - a);
    // A triangle of next to no area beside its longest side, as a sphere's last row has at a pole
    // whose corners the rounding of sin(pi) leaves a hair apart, has a face turned whichever way
    // that rounding took it, which, at a distance the triangles around it share, said cells
    // outside lay behind it and turned them inside. It holds no surface its neighbors do not, and
    // is passed over.
    float longest = max(dot(b - a, b - a), max(dot(c - b, c - b), dot(a - c, a - c)));
    if (dot(face, face) <= 1e-8 * longest * longest)
        return;
    bool sided = instance.triangles.z == 0u && dot(face, face) > 1e-20;
    uint way = dot(face, face) > 1e-20 ? way8(face) : 0u;
    float faceLength = length(face);

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

    if (push.groups.y == 2u)
    {
        if (faceLength <= 1e-10)
            return;
        // The cells within a cell of the triangle, which lie within its bounds grown by one, every
        // cell a point on it is blended from where the field is read there.
        first = max(int3(floor((min(a, min(b, c)) - push.originAndCell.xyz) / cell - 1.0)), int3(0));
        last = min(int3(ceil((max(a, max(b, c)) - push.originAndCell.xyz) / cell + 1.0)), int3(size - 1));
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
            if (length(p - nearest(p, a, b, c, edges, open)) >= cell)
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

    for (uint k = lane; k < count; k += 64u)
    {
        int3 at = first + int3(int(k % uint(span.x)), int((k / uint(span.x)) % uint(span.y)), int(k / uint(span.x * span.y)));
        float3 p = push.originAndCell.xyz + (float3(at) + 0.5) * cell;
        bool open;
        float3 q = nearest(p, a, b, c, edges, open);
        float d = length(p - q);
        if (d >= reach)
            continue;
        uint front = sided && !open && dot(p - q, face) < -0.5 * d * faceLength ? 0u : 1u;
        uint code = (uint(d / cell * 1024.0) << 10) | (sided ? 0u : 512u) | (front << 8) | way;
        uint index = (at.z * size + at.y) * size + at.x;
        if (push.groups.y == 0u)
        {
            InterlockedMin(cells[index], code);
            // Straight behind, within 25 degrees, as a wall's far face lies behind a cell by its
            // near one, where a cell beside a box's edge lies 45 degrees behind the face round it.
            if (front == 0u && dot(p - q, face) < -0.9 * d * faceLength)
                InterlockedMin(behind[index], (uint(d / cell * 1024.0) << 8) | way);
        }
        else if (cells[index] == code)
        {
            float3 weights = barycentric(q, a, b, c);
            float3 painted = instance.color.rgb * (weights.x * cornerColor(ca.w) + weights.y * cornerColor(cb.w) + weights.z * cornerColor(cc.w));
            uint3 c = uint3(saturate(painted) * 255.0 + 0.5);
            albedo[index] = c.r | (c.g << 8) | (c.b << 16) | (255u << 24);
            float3 e = max(instance.emission.rgb, 0.0);
            glow[index] = uint2(packHalves(e.rg), packHalves(float2(e.b, 0.0)));
        }
    }
}
