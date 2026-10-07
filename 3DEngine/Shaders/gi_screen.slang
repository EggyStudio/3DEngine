// The first cascade of the light that bounces, on the window's screen, a thread a probe. A probe
// stands on the surface the window's depth holds at the middle of each tile of pixels, and sends a
// ray along each texel of a hemisphere of directions around the surface's normal across the first
// interval, as far as the world's probes are apart. A ray is stepped through the depth while it is
// on the screen and through the scene's distance field from where it leaves it, and one that meets
// a surface brings back the light the surface sends along it. One that meets nothing brings back
// the light from beyond, the world's first cascade merged, blended between the world's probes around
// this one. The rays are summed into the light arriving at the probe's surface, which the model
// pass blends between the probes around each pixel that stand on a surface like the pixel's. A
// probe on no surface, or outside the world's first cascade, holds nothing, and the model pass
// reads the world's probes there.
import gi;
import scenefield;

struct ScreenView
{
    // World to clip space, and back, as the window's camera has it this frame.
    float4x4 viewProjection;
    float4x4 inverseViewProjection;
    // xyz: the eye.
    float4 eye;
    // x: a tile's pixels along each side. y: the probes across. z: the probes down. w: unused.
    float4 grid;
    // x: where the first interval ends. y: the world's probes along each side. z: their spacing in
    // cells. w: how many cascades the world's probes have.
    float4 trace;
    // xy: the depth's texels across and down. zw: the window's pixels across and down.
    float4 sizes;
};

[[vk::binding(0, 0)]]
Sampler2D depth;

[[vk::binding(1, 0)]]
Sampler3D sceneField;

[[vk::binding(2, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

[[vk::binding(3, 0)]]
Sampler3D fieldAlbedo;

[[vk::binding(4, 0)]]
Sampler3D fieldGlow;

[[vk::binding(5, 0)]]
ConstantBuffer<GiLights> lights;

[[vk::binding(6, 0)]]
Sampler3D bounced;

// The world's first cascade, merged, a tile of octahedron texels a probe.
[[vk::binding(7, 0)]]
Texture3D<float4> merged;

// rgb: the light arriving at each probe's surface, irradiance. a: 1 for a probe that holds it.
[[vk::binding(8, 0)]] [format("rgba16f")]
RWTexture2D<float4> irradiance;

// xyz: each probe's surface's normal. w: how far it is from the eye, or -1 for a probe on no surface.
[[vk::binding(9, 0)]] [format("rgba16f")]
RWTexture2D<float4> geometry;

[[vk::binding(10, 0)]]
ConstantBuffer<ScreenView> view;

struct ScreenPush
{
    // x: the world's first cascade's octahedron's texels along each side. yzw: unused.
    uint4 texels;
};

[[vk::push_constant]]
ConstantBuffer<ScreenPush> push;

// The hemisphere's texels along each side, sixteen rays a probe.
static const uint Rays = 4;

float3 worldAt(float2 uv, float d)
{
    float4 world = mul(view.inverseViewProjection, float4(uv * 2.0 - 1.0, d, 1.0));
    return world.xyz / world.w;
}

// A direction of the hemisphere around +z for a point of the square, the octahedron's upper half
// unfolded onto it.
float3 hemisphere(float2 uv)
{
    float2 a = uv * 2.0 - 1.0;
    float x = (a.x + a.y) * 0.5, y = (a.x - a.y) * 0.5;
    return normalize(float3(x, y, 1.0 - abs(x) - abs(y)));
}

// Where a ray from p along way first meets the depth's surface within length, stepped more finely
// near p, as a world position, with where it left the screen, or false and that place where it
// met none on the screen.
bool traceDepth(float3 p, float3 way, float length, out float3 hit, out float left)
{
    hit = p;
    left = length;
    static const int Steps = 12;
    for (int i = 0; i < Steps; i++)
    {
        float s = float(i + 1) / float(Steps);
        float t = length * s * s;
        float3 q = p + way * t;
        float4 clip = mul(view.viewProjection, float4(q, 1.0));
        if (clip.w <= 1e-4)
        {
            left = t;
            return false;
        }
        float3 ndc = clip.xyz / clip.w;
        float2 uv = ndc.xy * 0.5 + 0.5;
        if (any(uv < 0.0) || any(uv > 1.0))
        {
            left = t;
            return false;
        }
        float d = depth.SampleLevel(uv, 0).r;
        if (d < 1.0 && ndc.z > d)
        {
            // Behind what the window shows there, and near it, as a surface a few steps thick.
            float3 surface = worldAt(uv, d);
            float behind = distance(view.eye.xyz, q) - distance(view.eye.xyz, surface);
            if (behind < max(length / float(Steps) * 2.0, 0.25))
            {
                hit = surface;
                return true;
            }
        }
    }
    return false;
}

// The world's first cascade's eight probes around p, the one nearest the field's origin first,
// and how far p lies from it toward the last, in probes, or false where p lies outside the cascade.
bool probesAround(float3 p, out int3 base, out float3 f)
{
    float4 oc = fieldInfo.cascades[0].originAndCell;
    float probes = view.trace.y;
    float3 q = (p - oc.xyz) / (oc.w * view.trace.z) - 0.5;
    base = int3(floor(q));
    f = q - float3(base);
    return oc.w > 0.0 && all(q >= -0.5) && all(q <= probes - 0.5);
}

float3 probeAt(int3 at)
{
    float4 oc = fieldInfo.cascades[0].originAndCell;
    return oc.xyz + (float3(at) + 0.5) * (oc.w * view.trace.z);
}

// Which of the eight probes around p a surface there sees, a bit a probe, each traced through the
// field from a cell in front of the surface, so a probe beyond a wall or under a floor is passed
// over.
uint probesSeen(float3 p, float3 normal, float cell)
{
    int3 base;
    float3 f;
    if (!probesAround(p, base, f))
        return 0u;
    uint seen = 0u;
    float3 from = p + normal * cell;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 at = clamp(base + int3(corner & 1, (corner >> 1) & 1, corner >> 2), int3(0), int3(int(view.trace.y) - 1));
        if (seesThrough(sceneField, fieldInfo, from, probeAt(at), cell * 0.5))
            seen |= 1u << uint(corner);
    }
    return seen;
}

// The light from beyond the first interval along way, the world's first cascade's merged light
// blended between its eight probes around p on a surface facing normal, each weighed by how near it
// is and how squarely it stands in front of the surface, each probe inside a mesh or not among
// those seen passed over, or -1 in a where p lies outside that cascade.
float4 beyond(float3 p, float3 normal, float3 way, uint seen)
{
    int3 base;
    float3 f;
    if (!probesAround(p, base, f))
        return float4(0.0, 0.0, 0.0, -1.0);
    float probes = view.trace.y;
    uint texels = push.texels.x;
    uint2 texel = min(uint2(octahedron(way) * float(texels)), uint2(texels - 1u));
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 neighbor = clamp(base + offset, int3(0), int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float4 light = merged.Load(int4(int2(uint2(neighbor.xy) * texels + texel), neighbor.z, 0));
        float seenHere = float((seen >> uint(corner)) & 1u);
        float weight = lerps.x * lerps.y * lerps.z * light.a * seenHere * probeFacing(p, normal, probeAt(neighbor));
        sum += light.rgb * weight;
        weights += weight;
    }
    return float4(weights > 1e-4 ? sum / weights : float3(0.0), 1.0);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= uint(view.grid.y) || id.y >= uint(view.grid.z))
        return;
    // The middle of the depth's texel under the middle of the tile, since a point between two
    // texels is read as either by one device or another.
    float2 pixel = min((float2(id.xy) + 0.5) * view.grid.x, view.sizes.zw - 0.5);
    float2 uv = (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    float d = depth.SampleLevel(uv, 0).r;
    if (d >= 1.0)
    {
        irradiance[id.xy] = float4(0.0);
        geometry[id.xy] = float4(0.0, 0.0, 0.0, -1.0);
        return;
    }

    // The surface's normal from the nearer of the texels on either side along each axis, toward the eye.
    float3 p = worldAt(uv, d);
    float2 texel = 1.0 / view.sizes.xy;
    float3 right = worldAt(uv + float2(texel.x, 0), depth.SampleLevel(uv + float2(texel.x, 0), 0).r) - p;
    float3 left = p - worldAt(uv - float2(texel.x, 0), depth.SampleLevel(uv - float2(texel.x, 0), 0).r);
    float3 down = worldAt(uv + float2(0, texel.y), depth.SampleLevel(uv + float2(0, texel.y), 0).r) - p;
    float3 up = p - worldAt(uv - float2(0, texel.y), depth.SampleLevel(uv - float2(0, texel.y), 0).r);
    float3 n = normalize(cross(dot(right, right) < dot(left, left) ? right : left, dot(down, down) < dot(up, up) ? down : up));
    if (dot(n, view.eye.xyz - p) < 0.0)
        n = -n;
    geometry[id.xy] = float4(n, distance(view.eye.xyz, p));
    int3 base;
    float3 f;
    if (!probesAround(p, base, f))
    {
        irradiance[id.xy] = float4(0.0);
        return;
    }

    float3 side = abs(n.y) < 0.99 ? normalize(cross(n, float3(0, 1, 0))) : float3(1, 0, 0);
    float3 along = cross(n, side);
    float cell = max(fieldCell(fieldInfo, p), 1e-3);
    uint seen = probesSeen(p, n, cell);
    float3 start = p + n * (cell * 0.5);
    float length = view.trace.x;
    float3 sum = float3(0.0);
    for (uint v = 0u; v < Rays; v++)
        for (uint u = 0u; u < Rays; u++)
        {
            float3 local = hemisphere((float2(u, v) + 0.5) / float(Rays));
            float3 way = normalize(side * local.x + along * local.y + n * local.z);
            float3 light;
            float3 hit;
            float leftAt;
            if (traceDepth(start, way, length, hit, leftAt))
                light = shadeHit(sceneField, fieldInfo, fieldAlbedo, fieldGlow, lights, bounced, hit, view.trace.y, view.trace.z, view.trace.w);
            else
            {
                float t = leftAt < length ? traceField(sceneField, fieldInfo, start, way, leftAt, length) : -1.0;
                light = t >= 0.0
                    ? shadeHit(sceneField, fieldInfo, fieldAlbedo, fieldGlow, lights, bounced, start + way * t, view.trace.y, view.trace.z, view.trace.w)
                    : beyond(p, n, way, seen).rgb;
            }
            sum += light * max(dot(way, n), 0.0);
        }
    // Each of the hemisphere's texels spans about an equal share of its 2 pi steradians.
    irradiance[id.xy] = float4(sum * (2.0 * Pi / float(Rays * Rays)), 1.0);
}
