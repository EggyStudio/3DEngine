// Meshes a hill of voxels on the GPU, a thread a column, as shaders_packed_vertices meshes it on the
// CPU: each voxel's faces against the air, six vertices a face, every vertex its position and four
// bytes packed as packed.slang unpacks them, the face, its corner and the tile. A face takes its six
// places by adding to the count, which the engine draws as many vertices as, so the faces of the
// columns fall where they fall and the count says how many there are. A face past the capacity is
// left out, the count running past it, which the draw holds to the capacity.
//
// A dispatch with clear set empties the count first, a dispatch of one thread before the mesh's.

uniform float time;
uniform uint size;
uniform uint height;
uniform uint capacity;
uniform uint clear;
RWStructuredBuffer<float> positions;
RWStructuredBuffer<uint> packed;
RWStructuredBuffer<uint> counter;

// Each face's way out of the voxel, and its four corners in a unit cube, wound to face outward.
static const int3 Ways[6] = { int3(1, 0, 0), int3(-1, 0, 0), int3(0, 1, 0), int3(0, -1, 0), int3(0, 0, 1), int3(0, 0, -1) };
static const float3 Faces[24] = {
    float3(1, 0, 1), float3(1, 0, 0), float3(1, 1, 0), float3(1, 1, 1),
    float3(0, 0, 0), float3(0, 0, 1), float3(0, 1, 1), float3(0, 1, 0),
    float3(0, 1, 1), float3(1, 1, 1), float3(1, 1, 0), float3(0, 1, 0),
    float3(0, 0, 0), float3(1, 0, 0), float3(1, 0, 1), float3(0, 0, 1),
    float3(0, 0, 1), float3(1, 0, 1), float3(1, 1, 1), float3(0, 1, 1),
    float3(1, 0, 0), float3(0, 0, 0), float3(0, 1, 0), float3(1, 1, 0),
};
// The corners of a face's two triangles.
static const uint Corners[6] = { 0, 1, 2, 0, 2, 3 };

// A column's height, a hill with waves rolling across it.
int heightAt(int x, int z)
{
    float dx = float(x) - float(size) * 0.5;
    float dz = float(z) - float(size) * 0.5;
    float hill = float(height) - sqrt(dx * dx + dz * dz) * 0.7 + 2.0 * sin(float(x) * 0.6 + time) * cos(float(z) * 0.5 + time * 0.7);
    return clamp(int(hill), 1, int(height));
}

bool solid(int x, int y, int z)
{
    return x >= 0 && z >= 0 && x < int(size) && z < int(size) && y >= 0 && y < heightAt(x, z);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (clear != 0)
    {
        if (id.x == 0 && id.y == 0)
            counter[0] = 0;
        return;
    }
    if (id.x >= size || id.y >= size)
        return;
    int x = int(id.x), z = int(id.y);
    int top = heightAt(x, z);
    for (int y = 0; y < top; y++)
        for (uint face = 0; face < 6; face++)
        {
            int3 way = Ways[face];
            if (solid(x + way.x, y + way.y, z + way.z))
                continue;
            // Grass on a column's top, snow on a high one, dirt under the top and stone deep down.
            bool isTop = y == top - 1;
            uint tile = isTop && y >= int(height) - 3 ? 3u : isTop ? 0u : y >= top - 3 ? 1u : 2u;
            uint first;
            InterlockedAdd(counter[0], 6u, first);
            if (first + 6 > capacity)
                continue;
            float3 base = float3(float(x) - float(size) * 0.5, float(y), float(z) - float(size) * 0.5);
            for (uint v = 0; v < 6; v++)
            {
                uint corner = Corners[v];
                float3 p = base + Faces[face * 4 + corner];
                uint at = first + v;
                positions[at * 3] = p.x;
                positions[at * 3 + 1] = p.y;
                positions[at * 3 + 2] = p.z;
                packed[at] = face | corner << 3 | tile << 5;
            }
        }
}
