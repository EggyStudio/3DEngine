// Poses a skinned mesh on the GPU. Each vertex at rest is moved toward its morph targets by their
// weights, then by the up to four joints that hold it, weighted, into the vertex buffer the model
// pass draws. A vertex is eight floats (position, normal, uv), and a joint's matrix is four rows,
// applied as System.Numerics applies a matrix to a row vector, so the CPU's layout is read as it is.

// Each joint's matrix from rest to its pose, four float4 rows a joint, then the morph targets'
// weights, four to a float4.
[[vk::binding(0, 0)]]
StructuredBuffer<float4> jointRows;

// The vertices at rest, eight floats each.
[[vk::binding(1, 0)]]
StructuredBuffer<float> rest;

// Each vertex's four joints, two to a uint, the first in the low half.
[[vk::binding(2, 0)]]
StructuredBuffer<uint2> joints;

// Each vertex's four weights.
[[vk::binding(3, 0)]]
StructuredBuffer<float4> weights;

// The posed vertices, laid out as rest is.
[[vk::binding(4, 0)]]
RWStructuredBuffer<float> posed;

// How far each morph target moves each vertex at full weight, its position's then its normal's
// float4, target after target.
[[vk::binding(5, 0)]]
StructuredBuffer<float4> morphs;

struct SkinPush
{
    uint jointCount;
    uint morphCount;
};

[[vk::push_constant]]
ConstantBuffer<SkinPush> push;

float3 transformPoint(uint joint, float3 p)
{
    return p.x * jointRows[joint * 4].xyz + p.y * jointRows[joint * 4 + 1].xyz + p.z * jointRows[joint * 4 + 2].xyz + jointRows[joint * 4 + 3].xyz;
}

float3 transformDirection(uint joint, float3 d)
{
    return d.x * jointRows[joint * 4].xyz + d.y * jointRows[joint * 4 + 1].xyz + d.z * jointRows[joint * 4 + 2].xyz;
}

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint count, stride;
    rest.GetDimensions(count, stride);
    uint v = id.x;
    if (v * 8 + 7 >= count)
        return;

    float3 position = float3(rest[v * 8], rest[v * 8 + 1], rest[v * 8 + 2]);
    float3 normal = float3(rest[v * 8 + 3], rest[v * 8 + 4], rest[v * 8 + 5]);
    uint vertexCount = count / 8;
    for (uint t = 0; t < push.morphCount; t++)
    {
        float weight = jointRows[push.jointCount * 4 + t / 4][t % 4];
        if (weight == 0.0)
            continue;
        uint at = (t * vertexCount + v) * 2;
        position += morphs[at].xyz * weight;
        normal += morphs[at + 1].xyz * weight;
    }
    uint2 packed = joints[v];
    uint held[4] = { packed.x & 0xFFFF, packed.x >> 16, packed.y & 0xFFFF, packed.y >> 16 };
    float4 w = weights[v];

    float3 movedPosition = float3(0, 0, 0);
    float3 movedNormal = float3(0, 0, 0);
    for (int k = 0; k < 4; k++)
    {
        if (w[k] <= 0.0)
            continue;
        movedPosition += transformPoint(held[k], position) * w[k];
        movedNormal += transformDirection(held[k], normal) * w[k];
    }

    // A vertex no joint holds stays where it rests.
    if (all(movedNormal == float3(0, 0, 0)))
    {
        movedPosition = position;
        movedNormal = normal;
    }
    movedNormal = normalize(movedNormal);

    posed[v * 8] = movedPosition.x;
    posed[v * 8 + 1] = movedPosition.y;
    posed[v * 8 + 2] = movedPosition.z;
    posed[v * 8 + 3] = movedNormal.x;
    posed[v * 8 + 4] = movedNormal.y;
    posed[v * 8 + 5] = movedNormal.z;
    posed[v * 8 + 6] = rest[v * 8 + 6];
    posed[v * 8 + 7] = rest[v * 8 + 7];
}
