// Writes the triangles of a mesh whose vertices a compute shader wrote into the scene field's pool
// of corners, as SceneField.Pool writes a mesh's from its vertices on the CPU, a thread a vertex:
// its place, and in w its color, three sRGB bytes, where the mesh's stream holds four bytes of
// color a vertex, white where it does not, with no edge marked open, since which of its edges
// another triangle shares is not known here.

// The mesh's positions, three floats a vertex, every three vertices a triangle.
[[vk::binding(0, 0)]]
StructuredBuffer<float> positions;

// Its stream, four bytes a vertex, read where it holds colors.
[[vk::binding(1, 0)]]
StructuredBuffer<uint> stream;

// The pool's corners, as field_splat.slang reads them.
[[vk::binding(2, 0)]]
RWStructuredBuffer<uint4> corners;

struct CountedPush
{
    // The mesh's first corner in the pool, how many vertices it has, and 1 where its stream holds colors.
    uint first;
    uint vertices;
    uint colored;
};

[[vk::push_constant]]
ConstantBuffer<CountedPush> push;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.vertices)
        return;
    uint at = id.x * 3;
    uint color = push.colored != 0 ? stream[id.x] & 0xffffffu : 0xffffffu;
    corners[push.first + id.x] = uint4(asuint(positions[at]), asuint(positions[at + 1]), asuint(positions[at + 2]), color);
}
