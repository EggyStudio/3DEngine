// Writes the triangles of a mesh whose vertices a compute shader wrote into the scene field's pool
// of corners, as SceneField.Pool writes a mesh's from its vertices on the CPU, a thread a vertex:
// its place, and in w its color, three sRGB bytes, where the mesh's colors or its stream hold four
// bytes of color a vertex, white where neither does, with no edge marked open, since which of its
// edges another triangle shares is not known here. The mesh may lie in a range of larger buffers,
// from its first vertex on.

// The mesh's positions, three floats a vertex, every three vertices a triangle.
[[vk::binding(0, 0)]]
StructuredBuffer<float> positions;

// Its colors, or its stream where that holds colors, four bytes a vertex.
[[vk::binding(1, 0)]]
StructuredBuffer<uint> stream;

// The pool's corners, as field_splat.slang reads them.
[[vk::binding(2, 0)]]
RWStructuredBuffer<uint4> corners;

struct CountedPush
{
    // The mesh's first corner in the pool, how many vertices it has, 1 where it has colors, and its
    // first vertex in its buffers.
    uint first;
    uint vertices;
    uint colored;
    uint source;
};

[[vk::push_constant]]
ConstantBuffer<CountedPush> push;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.vertices)
        return;
    uint vertex = push.source + id.x;
    uint at = vertex * 3;
    uint color = push.colored != 0 ? stream[vertex] & 0xffffffu : 0xffffffu;
    corners[push.first + id.x] = uint4(asuint(positions[at]), asuint(positions[at + 1]), asuint(positions[at + 2]), color);
}
