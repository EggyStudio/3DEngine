// The window's meshes drawn again after their depth into an image of the depth's size, holding at
// each texel the normal the model pass shades the surface there with, the vertex's normal bent by
// the material's normal map, with 1 in alpha, and nothing where the depth shows no surface this
// pass draws, so the passes that read a point of the depth (ao.slang, gi_split.slang) take the
// surface's own normal rather than one worked out from the depth around it. A fragment is kept
// only where its depth lies within two pixels' change of the depth's, before it or behind it, so a
// surface behind another, or one cut out where the depth shows what lies behind it, leaves the
// texels it does not show alone. Of the fragments kept at a texel the pass's depth test keeps the
// one nearest the depth's, so a surface seen at a slant close behind the one the depth shows, its
// depth changing fast across a pixel, gives that one's texels nothing whatever order the meshes are
// drawn in. A see-through surface is not drawn, so one it lies on, which the model pass blends it
// over, keeps its own normal.
import modelpass;

// The depth as the depth pass left it.
[[vk::binding(0, 2)]]
Sampler2D drawnDepth;

struct NormalVertexOutput
{
    ModelStreamsOutput streams;
    // The vertex in clip space, whose place on the picture the depth is read at.
    float4 clip : TEXCOORD6;
};

[shader("vertex")]
NormalVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance,
    [[vk::location(9)]] float4 color : COLOR0, [[vk::location(10)]] float2 uv2 : TEXCOORD1)
{
    NormalVertexOutput output;
    output.streams = transformModelVertex(position, normal, uv, color, uv2, instance);
    output.clip = output.streams.base.position;
    return output;
}

struct NormalFragmentOutput
{
    float4 normal : SV_Target0;
    // How far the fragment's depth lies from the depth's, the least of which the pass keeps.
    float off : SV_Depth;
};

[shader("fragment")]
NormalFragmentOutput fragmentMain(NormalVertexOutput input)
{
    ModelVertexOutput base = input.streams.base;
    // The normal and how far depth changes across a pixel here, worked out before any fragment is
    // dropped.
    float3 n = surfaceNormal(base);
    float slope = fwidth(base.position.z);
    float drawn = drawnDepth.SampleLevel(input.clip.xy / input.clip.w * 0.5 + 0.5, 0).r;
    float off = abs(drawn - base.position.z);
    if (off > 2.0 * slope + 1e-6)
        discard;
    NormalFragmentOutput output;
    output.normal = float4(n, 1.0);
    output.off = off;
    return output;
}
