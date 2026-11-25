// The window's surfaces lit by the world's probes as the RadianceCascades module gathers a surface
// point (gatherSurface), a thread a pixel. Each pixel's point and normal are read from the window's
// depth, and the point takes its light from the level of probes the module picks for it, the finest
// whose probes stand about it on every side or within half their spacing past its edge probes, each
// of the eight probes about it weighed by its trilinear weight where it holds light and sees the
// point by its reach, its merged light taken at the point's normal. The model pass reads that light
// in place of the probes' faces where gi.surface asks (GlobalIlluminationSettings.SurfaceGather).
import gi;
import scenefield;
import rc_probe_passes;

[[vk::binding(0, 0)]]
Sampler2D depth;

[[vk::binding(1, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

// The window's camera, its depth's size and the window's, and the world's probes, their spacing
// and cascades, as the screen's probes read them (GlobalIlluminationRenderer.ViewBytes).
[[vk::binding(2, 0)]]
ConstantBuffer<ScreenView> view;

// The world's probes' faces, whose alpha says whether a probe holds light and how far it was moved
// (faceHolds, faceMove), every cascade one after another along z.
[[vk::binding(3, 0)]]
Texture3D<float4> faces;

// How far each probe's rays reached along each of sixteen by sixteen ways, laid out as the faces
// are (gi_ambient.slang).
[[vk::binding(4, 0)]]
Texture3D<float> reach;

// Each cascade's light merged from every distance past its probes, a tile of octahedron texels a
// probe, the last the probes have again where they have fewer than five.
[[vk::binding(5, 0)]]
Texture3D<float4> merged0;
[[vk::binding(6, 0)]]
Texture3D<float4> merged1;
[[vk::binding(7, 0)]]
Texture3D<float4> merged2;
[[vk::binding(8, 0)]]
Texture3D<float4> merged3;
[[vk::binding(9, 0)]]
Texture3D<float4> merged4;

// Each cascade's rays' distances as gi_trace.slang wrote them, and in y the share of light each
// kept across surfaces that let light through before its interval began, which its light is taken
// times, as the probes' faces take it (gi_ambient.slang).
[[vk::binding(10, 0)]]
Texture3D<float2> distances0;
[[vk::binding(11, 0)]]
Texture3D<float2> distances1;
[[vk::binding(12, 0)]]
Texture3D<float2> distances2;
[[vk::binding(13, 0)]]
Texture3D<float2> distances3;
[[vk::binding(14, 0)]]
Texture3D<float2> distances4;

// Each cascade's share of the sky in each texel's merged light.
[[vk::binding(15, 0)]]
Texture3D<float> sky0;
[[vk::binding(16, 0)]]
Texture3D<float> sky1;
[[vk::binding(17, 0)]]
Texture3D<float> sky2;
[[vk::binding(18, 0)]]
Texture3D<float> sky3;
[[vk::binding(19, 0)]]
Texture3D<float> sky4;

// rgb: the light each pixel's point takes over pi, as the model pass reads the probes' faces, or -1
// where the depth holds no surface or the point lies past every level of probes. a: the share of
// the sky in it.
[[vk::binding(20, 0)]] [format("rgba16f")]
RWTexture2D<float4> gathered;

struct SurfacePush
{
    // The texels along each side of the octahedrons of the first four cascades.
    uint4 texels;
    // x: the fifth cascade's texels.
    uint4 more;
};

[[vk::push_constant]]
ConstantBuffer<SurfacePush> push;

float4 mergedOf(int c, int4 at)
{
    switch (c)
    {
    case 0:
        return merged0.Load(at);
    case 1:
        return merged1.Load(at);
    case 2:
        return merged2.Load(at);
    case 3:
        return merged3.Load(at);
    default:
        return merged4.Load(at);
    }
}

float keptOf(int c, int4 at)
{
    switch (c)
    {
    case 0:
        return distances0.Load(at).y;
    case 1:
        return distances1.Load(at).y;
    case 2:
        return distances2.Load(at).y;
    case 3:
        return distances3.Load(at).y;
    default:
        return distances4.Load(at).y;
    }
}

float skyOf(int c, int4 at)
{
    switch (c)
    {
    case 0:
        return sky0.Load(at);
    case 1:
        return sky1.Load(at);
    case 2:
        return sky2.Load(at);
    case 3:
        return sky3.Load(at);
    default:
        return sky4.Load(at);
    }
}

// The world's cascades of probes as the module's gather reads them, each the probes it holds of
// the grid of view.trace.y a side (probeSpan), so the module's first probe of a level is the
// grid's first one it holds.
struct WorldLevels : IGatherSkyLevels
{
    int count;

    int levels() { return count; }

    uint texelsOf(int level) { return level < 4 ? push.texels[level] : push.more.x; }

    int firstOf(int level) { return int(probeSpan(fieldInfo, level, view.trace.y).x); }

    ProbeCascade cascadeOf(int level)
    {
        float4 oc = cascadeAt(fieldInfo, level, count);
        float apart = oc.w * view.trace.z;
        float2 span = probeSpan(fieldInfo, level, view.trace.y);
        ProbeCascade cascade;
        cascade.corner = oc.xyz + (floor(span.x) + 0.5) * apart;
        cascade.spacing = apart;
        cascade.count = int3(int(span.y) - int(span.x) + 1);
        cascade.texels = texelsOf(level);
        cascade.start = 0.0;
        cascade.end = 0.0;
        cascade.stepCap = 0u;
        return cascade;
    }

    // The texel of a probe's octahedron in its cascade's merged images.
    int4 texelAt(int level, int3 probe, uint2 texel)
    {
        int3 at = probe + firstOf(level);
        return int4(at.xy * int(texelsOf(level)) + int2(texel), at.z, 0);
    }

    float3 lightAt(int level, int3 probe, uint2 texel)
    {
        int4 at = texelAt(level, probe, texel);
        return mergedOf(level, at).rgb * keptOf(level, at);
    }

    float skyShareAt(int level, int3 probe, uint2 texel)
    {
        int4 at = texelAt(level, probe, texel);
        return skyOf(level, at) * keptOf(level, at);
    }

    // A probe's face along an axis, in the faces' layout.
    float4 faceOf(int level, int3 probe, int face)
    {
        int probes = int(view.trace.y);
        int3 at = probe + firstOf(level);
        return faces.Load(int4(face * probes + at.x, at.y, level * probes + at.z, 0));
    }

    // Its reach, or -1 for a probe inside a mesh, which holds no light (faceHolds).
    float reachAt(int level, int3 probe, uint2 texel)
    {
        if (faceHolds(faceOf(level, probe, 0).a) <= 0.0)
            return -1.0;
        int3 at = probe + firstOf(level);
        return reach.Load(int4(at.xy * int(ReachTexels) + int2(texel), level * int(view.trace.y) + at.z, 0));
    }

    // Where it stands, its spot in the grid moved as its faces say, as the screen's probes read it.
    float3 placeOf(int level, int3 probe)
    {
        float4 oc = cascadeAt(fieldInfo, level, count);
        float3 moved = float3(0.0);
        for (int axis = 0; axis < 3; axis++)
            moved[axis] = faceMove(faceOf(level, probe, axis * 2).a);
        return oc.xyz + (float3(probe + firstOf(level)) + 0.5 + moved) * (oc.w * view.trace.z);
    }
}

// The window's pixels' points as the module's gather takes them, a pixel an index, row by row.
struct WindowPoints : ISurfaceSkyPoints
{
    uint2 pixelOf(uint index) { return uint2(index % uint(view.sizes.z), index / uint(view.sizes.z)); }

    // The middle of the depth's texel the pixel's middle falls in.
    float2 uvOf(uint index)
    {
        float2 pixel = float2(pixelOf(index)) + 0.5;
        return (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    }

    float3 placeAt(uint index)
    {
        float2 uv = uvOf(index);
        return screenWorld(camera(), uv, depth.SampleLevel(uv, 0).r);
    }

    float3 normalAt(uint index)
    {
        float2 uv = uvOf(index);
        return depthNormal(uv, fromEyeAt(uv, depth.SampleLevel(uv, 0).r));
    }

    void keepTaken(uint index, float3 taken) { gathered[pixelOf(index)] = float4(taken / Pi, 0.0); }

    void keepSkyShare(uint index, float share)
    {
        uint2 at = pixelOf(index);
        gathered[at] = float4(gathered[at].rgb, share);
    }
}

// The field as gatherSurface takes it, which its gather from the probes' merged light never reads.
struct UnreadField : IProbeField
{
    int cascadeOf(float3 p, float margin) { return -1; }
    float4 cornerAndCell(int cascade) { return float4(0.0); }
    int size() { return 0; }
    float4 between(int cascade, float3 cells) { return float4(0.0); }
    float4 cellAt(int cascade, int3 cell) { return float4(0.0); }
    float3 secondAt(int cascade, int3 cell) { return float3(0.0); }
    float3 hitLight(float3 place, float3 way, int cascade, int3 cell) { return float3(0.0); }
    float throughAt(int cascade, int3 cell) { return 0.0; }
}

ScreenCamera camera()
{
    ScreenCamera c;
    c.viewProjection = view.viewProjection;
    c.inverseViewProjection = view.inverseViewProjection;
    c.eye = view.eye.xyz;
    return c;
}

// The point at uv of the depth d as its way from the eye, and the normal of the surface the depth
// holds there toward the eye, from the nearer of the texels on either side along each axis, as the
// screen's probes read them (gi_screen.slang).
float3 fromEyeAt(float2 uv, float d)
{
    float4 way = mul(view.inverseFromEye, float4(uv * 2.0 - 1.0, d, 1.0));
    return way.xyz / way.w;
}

float3 depthNormal(float2 uv, float3 fromEye)
{
    float2 texel = 1.0 / view.sizes.xy;
    float3 right = fromEyeAt(uv + float2(texel.x, 0), depth.SampleLevel(uv + float2(texel.x, 0), 0).r) - fromEye;
    float3 left = fromEye - fromEyeAt(uv - float2(texel.x, 0), depth.SampleLevel(uv - float2(texel.x, 0), 0).r);
    float3 down = fromEyeAt(uv + float2(0, texel.y), depth.SampleLevel(uv + float2(0, texel.y), 0).r) - fromEye;
    float3 up = fromEye - fromEyeAt(uv - float2(0, texel.y), depth.SampleLevel(uv - float2(0, texel.y), 0).r);
    float3 n = surfaceNormal(cross(dot(right, right) < dot(left, left) ? right : left, dot(down, down) < dot(up, up) ? down : up), -fromEye);
    return dot(n, -fromEye) < 0.0 ? -n : n;
}

// Whether the coarsest level holds p within the rim the module's gather reads a level to
// (gatheredLevel), past which no probe stands about it and the model pass reads its own way.
bool held(WorldLevels levels, float3 p)
{
    ProbeCascade cascade = levels.cascadeOf(levels.levels() - 1);
    float3 q = (p - cascade.corner) / cascade.spacing;
    return all(q >= -GatherRim) && all(q <= float3(cascade.count - 1) + GatherRim);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= uint2(view.sizes.zw)))
        return;
    uint index = id.y * uint(view.sizes.z) + id.x;
    WorldLevels levels = { int(view.trace.w) };
    WindowPoints points;
    float2 uv = points.uvOf(index);
    float d = depth.SampleLevel(uv, 0).r;
    if (d >= 1.0 || !held(levels, screenWorld(camera(), uv, d)))
    {
        gathered[points.pixelOf(index)] = float4(-1.0);
        return;
    }
    UnreadField field;
    gatherSurface(field, levels, points, index);
}
