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

// Its second, merged, or the first again where it has one, which a probe on a surface past the
// first cascade takes the light from beyond its rays from.
[[vk::binding(12, 0)]]
Texture3D<float4> mergedSecond;

// rgb: the light arriving at each probe's surface, irradiance. a: 1 for a probe that holds it.
[[vk::binding(8, 0)]] [format("rgba16f")]
RWTexture2D<float4> irradiance;

// xyz: each probe's surface's normal. w: how far it is from the eye, or -1 for a probe on no surface.
[[vk::binding(9, 0)]] [format("rgba16f")]
RWTexture2D<float4> geometry;

[[vk::binding(10, 0)]]
ConstantBuffer<ScreenView> view;

// Each world probe's reach, as gi_ambient.slang left it, which weighs the light a surface a ray
// meets takes from the probes around it.
[[vk::binding(11, 0)]]
Sampler3D reach;

struct ScreenPush
{
    // x: the world's first cascade's octahedron's texels along each side. y: its second's, or 0
    // where it has none. zw: unused.
    uint4 texels;
};

[[vk::push_constant]]
ConstantBuffer<ScreenPush> push;

// The hemisphere's texels along each side, sixteen rays a probe.
static const uint Rays = 4;

ScreenCamera camera()
{
    ScreenCamera c;
    c.viewProjection = view.viewProjection;
    c.inverseViewProjection = view.inverseViewProjection;
    c.eye = view.eye.xyz;
    return c;
}

float3 worldAt(float2 uv, float d)
{
    return screenWorld(camera(), uv, d);
}

// A direction of the hemisphere around +z for a point of the square, the octahedron's upper half
// unfolded onto it, its length before it was made one in w.
float4 hemisphere(float2 uv)
{
    float2 a = uv * 2.0 - 1.0;
    float x = (a.x + a.y) * 0.5, y = (a.x - a.y) * 0.5;
    float3 d = float3(x, y, 1.0 - abs(x) - abs(y));
    return float4(normalize(d), length(d));
}

// The eight probes of the world's cascade c around p on a surface facing normal, weighed from
// gi.slang's Lean off it, the one nearest the field's origin first, and how far that point lies
// from it toward the last, in probes, or false where it lies outside the cascade.
bool probesAround(float3 p, uint c, float3 normal, out int3 base, out float3 f)
{
    float4 oc = fieldInfo.cascades[c].originAndCell;
    float probes = view.trace.y;
    float3 q = (p + normal * (Lean * oc.w * view.trace.z) - oc.xyz) / (oc.w * view.trace.z) - 0.5;
    base = int3(floor(q));
    f = q - float3(base);
    return oc.w > 0.0 && all(q >= -0.5) && all(q <= probes - 0.5);
}

// Where the probe of cascade c at `at` stands, its spot in the grid moved as its faces say.
float3 probeAt(int3 at, uint c)
{
    float4 oc = fieldInfo.cascades[c].originAndCell;
    int probes = int(view.trace.y);
    float3 moved = float3(0.0);
    for (int axis = 0; axis < 3; axis++)
        moved[axis] = faceMove(bounced.loadAt(int3(axis * 2 * probes + at.x, at.y, int(c) * probes + at.z)).a);
    return oc.xyz + (float3(at) + 0.5 + moved) * (oc.w * view.trace.z);
}

// Which of the eight probes of cascade c around p a surface there sees, a bit a probe, each traced
// through the field from a cell in front of the surface, so a probe beyond a wall or under a floor
// is passed over.
uint probesSeen(float3 p, uint c, float3 normal, float cell)
{
    int3 base;
    float3 f;
    if (!probesAround(p, c, normal, base, f))
        return 0u;
    uint seen = 0u;
    float3 from = p + normal * cell;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 at = clamp(base + int3(corner & 1, (corner >> 1) & 1, corner >> 2), int3(0), int3(int(view.trace.y) - 1));
        if (seesThrough(sceneField, fieldInfo, from, probeAt(at, c), cell * 0.5))
            seen |= 1u << uint(corner);
    }
    return seen;
}

// The light from beyond the first interval along way, the world's cascade c's merged light blended
// between its eight probes around p on a surface facing normal, each weighed by how near it is and
// how squarely it stands in front of the surface, each probe inside a mesh or not among those seen
// passed over, or -1 in a where p lies outside that cascade. A texel of the second cascade whose ray
// met a surface before its interval began holds that surface's light, alpha a half, which here is
// the light along way from that probe, taken as any other.
float4 beyond(float3 p, uint c, float3 normal, float3 way, uint seen)
{
    int3 base;
    float3 f;
    if (!probesAround(p, c, normal, base, f))
        return float4(0.0, 0.0, 0.0, -1.0);
    float probes = view.trace.y;
    uint texels = c == 0u ? push.texels.x : push.texels.y;
    uint2 texel = min(uint2(octahedron(way) * float(texels)), uint2(texels - 1u));
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 neighbor = clamp(base + offset, int3(0), int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        int4 at = int4(int2(uint2(neighbor.xy) * texels + texel), neighbor.z, 0);
        float4 light = c == 0u ? merged.Load(at) : mergedSecond.Load(at);
        float seenHere = float((seen >> uint(corner)) & 1u);
        float weight = lerps.x * lerps.y * lerps.z * (light.a > 0.25 ? 1.0 : 0.0) * seenHere * probeFacing(p, normal, probeAt(neighbor, c));
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
    // The world's first cascade's probes give the light from beyond the rays, or past it its second's,
    // so the screen's probes reach as far as the second cascade does. Ending at the first, a surface
    // past it took the world's probes alone, and a step showed along that edge, as a slanted ceiling
    // drew it in stairs.
    int3 base;
    float3 f;
    uint around = 0u;
    if (!probesAround(p, 0u, n, base, f))
    {
        around = 1u;
        if (push.texels.y == 0u || !probesAround(p, 1u, n, base, f))
        {
            irradiance[id.xy] = float4(0.0);
            return;
        }
    }

    float3 side = abs(n.y) < 0.99 ? normalize(cross(n, float3(0, 1, 0))) : float3(1, 0, 0);
    float3 along = cross(n, side);
    float cell = max(fieldCell(fieldInfo, p), 1e-3);
    uint seen = probesSeen(p, around, n, cell);
    float3 start = p + n * (cell * 0.5);
    float length = view.trace.x;
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (uint v = 0u; v < Rays; v++)
        for (uint u = 0u; u < Rays; u++)
        {
            float4 local = hemisphere((float2(u, v) + 0.5) / float(Rays));
            float3 way = normalize(side * local.x + along * local.y + n * local.z);
            // How squarely the ray falls on the surface times the share of the hemisphere its
            // texel spans, one over the cube of its octahedron point's distance, as the gather of
            // the world's probes weighs its texels.
            float weight = local.z / (local.w * local.w * local.w);
            float3 light;
            float3 hit;
            float leftAt;
            // What the surface sends on without the light that bounced, which the screen's probes
            // keep no account of.
            float3 own;
            if (traceScreen(depth, camera(), start, way, length, 12, hit, leftAt))
                light = shadeProbeHit(sceneField, fieldInfo, fieldAlbedo, fieldGlow, lights, bounced, reach, hit, view.trace.y, view.trace.z, view.trace.w, false, float3(1.0), own);
            else
            {
                float t = leftAt < length ? traceField(sceneField, fieldInfo, start, way, leftAt, length) : -1.0;
                light = t >= 0.0
                    ? shadeProbeHit(sceneField, fieldInfo, fieldAlbedo, fieldGlow, lights, bounced, reach, start + way * t, view.trace.y, view.trace.z, view.trace.w, false, float3(1.0), own)
                    : beyond(p, around, n, way, seen).rgb;
            }
            sum += light * weight;
            weights += weight;
        }
    // The weights scaled to sum to pi, the light reaching the surface under a sky of 1, where an
    // equal share for every texel read it at 1.04 of that.
    irradiance[id.xy] = float4(sum * (Pi / max(weights, 1e-6)), 1.0);
}
