// The screen's probes as cascades traced by the device's rays, a trial beside the screen pass
// (gi.screen_cascades). Each level stands its probes on the window's depth at the middle of tiles
// twice as wide as the level before's, each with twice as many ways along each side of a whole
// octahedron, and traces each way through the window's meshes over a stretch of its own, the first
// from the surface to where the screen probes' rays end, each next one twice as long, and the last
// on to the scene's end. A ray that meets a face within its stretch takes the light the face sends
// back, its sun found through a ray of its own and its indirect light from the world's probes
// around it, and one that meets nothing takes the light the next level's four probes around hold
// along its way, each weighed by how near it stands to the plane of the probe's surface, or past
// the last level the sky. A way that meets a face before its stretch begins holds no light, for
// the reason along gives. Where no probe of the next level holds light along a way the ray goes on
// to the scene's end itself, so no light from beyond is read from the world's probes. The levels
// are traced from the last to the first, a dispatch each, the first a thread a probe summing its
// rays into the light arriving at its surface, which the screen's filter blends within the frame
// and the model pass reads in place of the screen pass's, and nothing is kept from one frame to
// the next.
import gi;
import probelight;
import scenefield;
import meshrays;
import rc_octahedron;

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

// The world probes' faces' light, and that light at the surfaces they are read for, which a hit
// takes its indirect light from (gi_ambient.slang).
[[vk::binding(6, 0)]]
Sampler3D bounced;

[[vk::binding(7, 0)]]
Sampler3D bouncedAtSurface;

// rgb: the light arriving at each first level probe's surface, irradiance. a: 1 for a probe that
// holds it, as gi_screen.slang writes it.
[[vk::binding(8, 0)]] [format("rgba16f")]
RWTexture2D<float4> irradiance;

// The light each probe of the levels past the first holds along each of its ways, merged with the
// levels past it, a level's probes below the level before's, each a square of its ways. a: 1
// where the way holds light, 0 where it runs into the probe's surface, meets a face before its
// stretch begins or the probe stands on none.
[[vk::binding(9, 0)]] [format("rgba16f")]
RWTexture2D<float4> levels;

[[vk::binding(10, 0)]]
ConstantBuffer<ScreenView> view;

// The light from all around, for a ray of the last level that meets nothing, or a black cube.
// Bindings 12 to 17 stand unbound, the ray scene's three at 18 to 20 where meshrays.slang has them.
[[vk::binding(11, 0)]]
SamplerCube environment;

struct CascadePush
{
    // x: the level this dispatch traces, 0 the first. y: how many levels. z: the first level's
    // octahedron's texels along each side. w: unused.
    uint4 level;
};

[[vk::push_constant]]
ConstantBuffer<CascadePush> push;

#if defined(RAY_QUERY)
ScreenCamera camera()
{
    ScreenCamera c;
    c.viewProjection = view.viewProjection;
    c.inverseViewProjection = view.inverseViewProjection;
    c.eye = view.eye.xyz;
    return c;
}

// The point at uv of the depth d as its way from the eye, as gi_screen.slang's fromEyeAt.
float3 fromEyeAt(float2 uv, float d)
{
    float4 way = mul(view.inverseFromEye, float4(uv * 2.0 - 1.0, d, 1.0));
    return way.xyz / way.w;
}

// The normal of the surface the depth holds at uv, as gi_screen.slang's depthNormal.
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

// A tile's pixels along each side at level l.
uint tileOf(uint l)
{
    return uint(view.grid.x + 0.5) << l;
}

// The probes across and down at level l.
uint2 probesOf(uint l)
{
    uint tile = tileOf(l);
    return (uint2(view.sizes.zw + 0.5) + tile - 1u) / tile;
}

// The texels along each side of level l's octahedron.
uint texelsOf(uint l)
{
    return push.level.z << l;
}

// The row of the levels image where level l's probes begin, the first level keeping none.
uint rowOf(uint l)
{
    uint row = 0u;
    for (uint k = 1u; k < l; k++)
        row += probesOf(k).y * texelsOf(k);
    return row;
}

// Where level l's stretch begins and ends along a ray, the first ending where the screen probes'
// rays end and each next one twice as long, the last reaching the scene's end.
float startOf(uint l)
{
    return view.trace.x * float((1u << l) - 1u);
}

float endOf(uint l)
{
    return l + 1u >= push.level.y ? 1e5 : view.trace.x * float((2u << l) - 1u);
}

// The middle of the depth's texel under the middle of level l's probe's tile, as gi_screen.slang
// stands its probes.
float2 uvOf(uint l, uint2 probe)
{
    float2 pixel = min((float2(probe) + 0.5) * float(tileOf(l)), view.sizes.zw - 0.5);
    return (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
}

// The point level l's probe stands on, false where the depth holds no surface under it.
bool probePoint(uint l, uint2 probe, out float3 p)
{
    float2 uv = uvOf(l, probe);
    float d = depth.SampleLevel(uv, 0).r;
    p = d < 1.0 ? screenWorld(camera(), uv, d) : float3(0.0);
    return d < 1.0;
}

// The point a ray of the probe leaves its texel's square from, one of sixteen places a texel, every
// four probes by four holding each place once, the same from frame to frame, so the screen's filter
// blends the ways of every place across the probes around a pixel and a still view holds still.
float2 squareOf(uint2 probe, uint2 texel, uint texels)
{
    uint place = TracePlaces[(probe.x & 3u) + 4u * (probe.y & 3u)];
    float2 within = (float2(place & 3u, place >> 2u) + 0.5) / 4.0;
    return (float2(texel) + within) / float(texels);
}

float3 skyAlong(float3 way)
{
    return lights.sky.w > 0.5 ? environment.SampleLevel(way, 0).rgb * lights.sky.rgb : lights.sky.rgb;
}

// The light the face a ray from origin along way meets at t sends back, lit by its own normal,
// color and gloss.
float3 litAt(float3 origin, float3 way, float t, float3 n, float3 color, float2 gloss)
{
    float3 own;
    return shadeProbeHitAt(sceneField, fieldInfo, fieldInfo, fieldAlbedo, fieldGlow, lights, bounced, bouncedAtSurface, origin + way * t, n, color, gloss,
        way, view.trace.y, view.trace.z, view.trace.w, float3(1.0), MeshSun(), own);
}

// The next level's four probes around a probe and how much each counts, its bilinear share times
// how near it stands to the plane of the probe's surface against half that level's tile there,
// nothing where the depth holds no surface under it, found once for all of a thread's ways.
struct Corners
{
    uint2 probe[4];
    float weight[4];
};

Corners cornersOf(uint l, uint2 probe, float3 p, float3 n, float pixel)
{
    Corners c;
    uint up = l + 1u;
    uint2 across = probesOf(up);
    float2 at = (float2(probe) + 0.5) * 0.5 - 0.5;
    int2 base = int2(floor(at));
    float2 f = at - float2(base);
    float apart = 0.5 * float(tileOf(up)) * pixel;
    for (uint k = 0u; k < 4u; k++)
    {
        uint2 corner = uint2(k & 1u, k >> 1u);
        c.probe[k] = uint2(clamp(base + int2(corner), int2(0), int2(across) - 1));
        c.weight[k] = 0.0;
        float3 q;
        // The last level has no level past it.
        if (up >= push.level.y || !probePoint(up, c.probe[k], q))
            continue;
        float2 b = lerp(1.0 - f, f, float2(corner));
        c.weight[k] = b.x * b.y * saturate(1.0 - abs(dot(n, q - p)) / max(apart, 1e-4));
    }
    return c;
}

// The light the next level's four probes around level l's probe hold along its texel's way, the
// four texels of theirs it covers, each probe as much as it counts, false where none holds any.
bool merged(uint l, Corners corners, uint2 texel, out float3 light)
{
    uint up = l + 1u;
    uint texels = texelsOf(up);
    uint row = rowOf(up);
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (uint k = 0u; k < 4u; k++)
    {
        float weight = corners.weight[k];
        if (weight <= 0.0)
            continue;
        float4 ways = float4(0.0);
        for (uint j = 0u; j < 4u; j++)
        {
            float4 held = levels[corners.probe[k] * texels + texel * 2u + uint2(j & 1u, j >> 1u) + uint2(0u, row)];
            ways += float4(held.rgb * held.a, held.a);
        }
        if (ways.a <= 0.0)
            continue;
        sum += ways.rgb / ways.a * weight;
        weights += weight;
    }
    light = weights > 0.0 ? sum / weights : float3(0.0);
    return weights > 1e-4;
}

// The light along a way of level l's probe over its stretch and from beyond it, a 0 where the way
// meets a face before its stretch begins. Such a face can stand by this probe and clear of the
// finer probes' rays that read it, a tile or more away, and the stretch, beginning past it, would
// read the light behind it, the sky where nothing lies behind, which the finer probes do not see.
// Without it such ways let in up to twenty times the traced light in a shaded room by an opening.
float4 along(uint l, Corners corners, float3 origin, float3 way, uint2 texel)
{
    float3 n, color;
    float2 gloss;
    float t = meshHit(origin, way, endOf(l), n, color, gloss);
    if (t >= startOf(l))
        return float4(litAt(origin, way, t, n, color, gloss), 1.0);
    if (t >= 0.0)
        return float4(0.0);
    if (l + 1u >= push.level.y)
        return float4(skyAlong(way), 1.0);
    float3 light;
    if (merged(l, corners, texel, light))
        return float4(light, 1.0);
    float end = endOf(l);
    t = meshHit(origin + way * end, way, 1e5 - end, n, color, gloss);
    return float4(t >= 0.0 ? litAt(origin, way, end + t, n, color, gloss) : skyAlong(way), 1.0);
}
#endif

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
#if defined(RAY_QUERY)
    uint l = push.level.x;
    uint texels = texelsOf(l);
    // The first level a thread a probe, the others a thread a way.
    uint2 probe = l == 0u ? id.xy : id.xy / texels;
    uint2 texel = id.xy % texels;
    if (any(probe >= probesOf(l)))
        return;
    uint2 kept = id.xy + uint2(0u, rowOf(l));
    float2 uv = uvOf(l, probe);
    float d = depth.SampleLevel(uv, 0).r;
    if (d >= 1.0)
    {
        if (l == 0u)
            irradiance[id.xy] = float4(0.0);
        else
            levels[kept] = float4(0.0);
        return;
    }
    float3 fromEye = fromEyeAt(uv, d);
    float3 p = screenWorld(camera(), uv, d);
    float3 n = depthNormal(uv, fromEye);
    // A window's pixel's width at the probe's distance, the rays leaving two of them off the
    // surface.
    float pixel = length(fromEyeAt(uv + float2(1.0 / view.sizes.z, 0.0), d) - fromEye);
    float3 origin = p + n * max(2.0 * pixel, 1e-3);
    Corners corners = cornersOf(l, probe, p, n, pixel);
    if (l > 0u)
    {
        float3 way = fromOctahedron(squareOf(probe, texel, texels));
        levels[kept] = dot(way, n) < 0.0 ? float4(0.0) : along(l, corners, origin, way, texel);
        return;
    }
    // Each way weighed by how squarely it falls on the surface times the share of the sphere its
    // texel spans, scaled to sum to pi, as gi_screen.slang sums its rays.
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (uint v = 0u; v < texels; v++)
        for (uint u = 0u; u < texels; u++)
        {
            float2 square = squareOf(probe, uint2(u, v), texels);
            float3 way = fromOctahedron(square);
            float weight = dot(way, n) * octahedronShare(square);
            if (weight <= 0.0)
                continue;
            sum += along(0u, corners, origin, way, uint2(u, v)).rgb * weight;
            weights += weight;
        }
    irradiance[id.xy] = float4(sum * (Pi / max(weights, 1e-6)), 1.0);
#endif
}
