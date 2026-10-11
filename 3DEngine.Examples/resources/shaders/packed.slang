// A chunk of voxels whose every vertex is its position and four bytes of the program's own
// (MeshStream), read as CUSTOM0: the face it lies on in the low three bits, which of the face's
// corners it is in the next two, and the tile in the eight above them. The normal, the texture
// coordinate and the tile's color are unpacked here, so the GPU holds sixteen bytes a vertex. The
// bytes are a uint, whose bits are read as they were written, where a float's would be read as a
// float.
import modelpass;

static const float3 Normals[6] = {
    float3(1, 0, 0), float3(-1, 0, 0), float3(0, 1, 0), float3(0, -1, 0), float3(0, 0, 1), float3(0, 0, -1),
};
static const float2 Corners[4] = { float2(0, 1), float2(1, 1), float2(1, 0), float2(0, 0) };

// Each tile's two colors, its ground and its speckle: grass, dirt, stone and snow.
static const float3 Grounds[4] = { float3(0.20, 0.45, 0.12), float3(0.36, 0.24, 0.13), float3(0.38, 0.38, 0.40), float3(0.85, 0.88, 0.92) };
static const float3 Speckles[4] = { float3(0.30, 0.58, 0.18), float3(0.28, 0.18, 0.10), float3(0.30, 0.30, 0.32), float3(0.72, 0.76, 0.82) };

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, uint packed : CUSTOM0, ModelInstance instance)
{
    ModelVertexOutput output = transformModelVertex(position, Normals[packed & 7], Corners[(packed >> 3) & 3], instance);
    output.color = float4(float((packed >> 5) & 0xff), 0, 0, 1);
    return output;
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // A speckle of eight by eight on each face, from the face's own texture coordinate.
    uint tile = uint(input.color.r + 0.5);
    uint2 cell = uint2(input.uv * 8.0);
    uint hash = (cell.x * 73856093u) ^ (cell.y * 19349663u) ^ (tile * 83492791u);
    float3 color = (hash % 5u) == 0 ? Speckles[tile] : Grounds[tile];
    return float4(lit(color, input), 1);
}
