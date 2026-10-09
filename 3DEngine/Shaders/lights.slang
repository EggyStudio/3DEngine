// How the model pass reads its lights' set: each light's arrival and the shadow maps that cut it,
// the metallic-roughness model's specular, the probes and the environment, and the light that
// bounced and a glossy surface's traced reflection. Imported by modelpass alone, so a shader that
// imports modelpass sees the set and not these.
module lights;

__exported import lightset;
import scenefield;
import gi;

// LightKind's values.
public static const int Directional = 0;
public static const int Point = 1;
public static const int Spot = 2;
public static const int Ambient = 3;

// Light arriving at a point with a normal from one light: Lambert's cosine, divided by the square
// of the distance for a point or a spot, brought smoothly to nothing at its range, and cut to a
// spot's cone.
public float3 arriving(Light light, float3 world, float3 n)
{
    int kind = int(light.positionAndKind.w);
    float3 color = light.colorAndShadow.xyz;
    if (kind == Ambient)
        return color;
    if (kind == Directional)
        return color * max(dot(n, -light.directionAndRange.xyz), 0.0);

    float3 toLight = light.positionAndKind.xyz - world;
    float distanceSquared = max(dot(toLight, toLight), 1e-4);
    float3 l = toLight * rsqrt(distanceSquared);
    float result = max(dot(n, l), 0.0) / distanceSquared;

    float range = light.directionAndRange.w;
    if (range > 0.0)
    {
        // Full near the light and nothing at the range, without the step a plain cut leaves.
        float edge = saturate(1.0 - pow(distanceSquared / (range * range), 2.0));
        result *= edge * edge;
    }

    if (kind == Spot)
        result *= smoothstep(light.cone.y, max(light.cone.x, light.cone.y + 1e-4), dot(-l, light.directionAndRange.xyz));
    return color * result;
}

// How much of the shadowed light reaches this point through cascade c, from 0 to 1, or -1 when the
// cascade's tile does not hold it. Each cascade is a quarter of the map, two tiles on a side. The
// point is moved off its surface along the normal by a texel and a half of that cascade before it
// is compared, so a surface does not shadow itself where the map's texels slant across it, and
// nine depths around it are compared and averaged, which softens the stair steps of the texels into
// a gradient one texel wide. A point a little inside a tile's edge counts as outside, so the nine
// never reach into a neighboring tile. edge says how far toward the tile's edge the point is, from
// 0 in its middle to 1 at that border.
public float cascadeReached(int c, float3 world, float3 n, float2 texel, out float edge)
{
    edge = 1.0;
    float4 clip = mul(lighting.shadowCascades[c], float4(world + n * lighting.shadowTexels[c] * 1.5, 1.0));
    float3 p = clip.xyz / clip.w;
    if (any(abs(p.xy) > 0.995) || p.z > 1.0)
        return -1.0;

    edge = max(abs(p.x), abs(p.y)) / 0.995;
    float2 uv = (p.xy * 0.5 + 0.5 + float2(c % 2, c / 2)) * 0.5;
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.0005 <= shadowMap.SampleLevel(lightNearest, uv + float2(x, y) * texel, 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// How much of the shadowed light reaches this point, from 0 to 1, from the nearest cascade whose
// tile holds it. Across the outer fifth of a tile the next cascade is read too and blended in, so
// where one cascade gives way to the next the shadow's softness changes over a band rather than in
// a step, and past the last cascade's band the shadow fades out rather than stopping at a line.
public float lightReaching(float3 world, float3 n)
{
    uint width, height;
    shadowMap.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    for (int c = 0; c < lighting.cascadeCount; c++)
    {
        float edge;
        float reached = cascadeReached(c, world, n, texel, edge);
        if (reached < 0.0)
            continue;

        float blend = saturate((edge - 0.8) / 0.2);
        if (blend <= 0.0)
            return reached;
        float nextEdge;
        float next = c + 1 < lighting.cascadeCount ? cascadeReached(c + 1, world, n, texel, nextEdge) : -1.0;
        return lerp(reached, next < 0.0 ? 1.0 : next, blend);
    }
    return 1.0;
}

// How much of the shadowed spot light in slot (counted from 0) at lightPosition reaches this
// point, from 0 to 1, from its square of the map's last tile, compared as the cascades are. Its
// texels widen with distance from the light, so the offset along the normal does too.
public float spotLightReaching(float3 world, float3 n, float3 lightPosition, int slot)
{
    float offset = lighting.spotShadowTexels[slot / 4][slot % 4] * distance(world, lightPosition) * 1.5;
    float4 clip = mul(lighting.spotShadows[slot], float4(world + n * offset, 1.0));
    if (clip.w <= 0.0)
        return 1.0;
    float3 p = clip.xyz / clip.w;
    if (any(abs(p.xy) > 0.995) || p.z > 1.0 || p.z < 0.0)
        return 1.0;

    uint width, height;
    shadowMap.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    // The last tile is the map's lower right quarter, split by rank as ShadowFit.SpotTileArea
    // splits it: whole for one light, quarters for up to four, and past four quarters for the
    // first two and sixteenths in its lower half for the rest.
    int count = lighting.spotShadowCount;
    float size = 0.5;
    float2 origin = float2(0.5, 0.5);
    if (count > 4 && slot >= 2)
    {
        size = 0.125;
        origin = float2(0.5, 0.75) + float2((slot - 2) % 4, (slot - 2) / 4) * 0.125;
    }
    else if (count > 1)
    {
        size = 0.25;
        origin = float2(0.5, 0.5) + float2(slot % 2, slot / 2) * 0.25;
    }
    // Kept a texel inside the square, so the comparisons around an edge read no other light's.
    float2 uv = clamp(origin + (p.xy * 0.5 + 0.5) * size, origin + texel, origin + size - texel);
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.00002 <= shadowMap.SampleLevel(lightNearest, uv + float2(x, y) * texel, 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// How much of the shadowed point light in slot (counted from 0) at lightPosition reaches this
// point, from 0 to 1, from the face of its six that the point lies furthest along the axis of,
// compared as the cascades are. Its texels widen with distance from the light, as a spot light's.
public float pointLightReaching(float3 world, float3 n, float3 lightPosition, int slot)
{
    float3 away = world - lightPosition;
    float3 size = abs(away);
    int face = size.x >= size.y && size.x >= size.z ? (away.x >= 0.0 ? 0 : 1)
             : size.y >= size.z ? (away.y >= 0.0 ? 2 : 3)
             : (away.z >= 0.0 ? 4 : 5);
    // The lights that matter most have a layer to each face, and the rest a quarter of one at half
    // the size, as ShadowFit.PointFaceArea lays them out, with texels twice as wide.
    int full = int(lighting.pointShadow.z);
    int layer = slot * 6 + face;
    float2 origin = float2(0.0, 0.0);
    float scale = 1.0;
    if (slot >= full)
    {
        int index = (slot - full) * 6 + face;
        layer = full * 6 + index / 4;
        origin = float2(index % 4 % 2, index % 4 / 2) * 0.5;
        scale = 0.5;
    }
    float offset = lighting.pointShadow.x / scale * length(away) * 1.5;
    float4 clip = mul(lighting.pointShadowFaces[slot * 6 + face], float4(world + n * offset, 1.0));
    if (clip.w <= 0.0)
        return 1.0;
    float3 p = clip.xyz / clip.w;
    if (p.z > 1.0 || p.z < 0.0)
        return 1.0;

    uint width, height, layers;
    pointShadowMap.GetDimensions(width, height, layers);
    float2 texel = 1.0 / float2(width, height);
    float2 uv = clamp(origin + (p.xy * 0.5 + 0.5) * scale, origin + texel, origin + scale - texel);
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.00002 <= pointShadowMap.SampleLevel(lightNearest, float3(uv + float2(x, y) * texel, layer), 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// The way light comes from one light to a point, for specular.
public float3 fromLight(Light light, float3 world)
{
    int kind = int(light.positionAndKind.w);
    return kind == Directional ? light.directionAndRange.xyz : normalize(world - light.positionAndKind.xyz);
}

static const float PI = 3.14159265;

// How much of a light reflects toward the eye off microfacets of this roughness, as GGX's
// distribution and Smith's height-correlated shadowing have it, with Schlick's Fresnel. Times pi,
// since a light here is given as the light a white diffuse surface facing it returns, so the
// diffuse term is the albedo itself rather than the albedo over pi.
public float3 specularReflected(float3 n, float3 v, float3 l, float3 f0, float alpha, out float3 fresnel)
{
    float3 h = normalize(v + l);
    float nl = max(dot(n, l), 1e-4);
    float nv = max(dot(n, v), 1e-4);
    float nh = max(dot(n, h), 0.0);
    float a2 = alpha * alpha;
    float d = nh * nh * (a2 - 1.0) + 1.0;
    float distribution = a2 / (PI * d * d);
    float visibility = 0.5 / (nl * sqrt(nv * nv * (1.0 - a2) + a2) + nv * sqrt(nl * nl * (1.0 - a2) + a2));
    fresnel = f0 + (1.0 - f0) * pow(1.0 - max(dot(v, h), 0.0), 5.0);
    return distribution * visibility * fresnel * PI;
}

// The light a white diffuse surface facing n returns from nine spherical harmonic coefficients in
// EnvironmentMap.Irradiance's order.
public float3 harmonics(float4 c[9], float3 n)
{
    float3 sum = c[0].xyz * 0.282095
        + c[1].xyz * (0.488603 * n.y)
        + c[2].xyz * (0.488603 * n.z)
        + c[3].xyz * (0.488603 * n.x)
        + c[4].xyz * (1.092548 * n.x * n.y)
        + c[5].xyz * (1.092548 * n.y * n.z)
        + c[6].xyz * (0.315392 * (3.0 * n.z * n.z - 1.0))
        + c[7].xyz * (1.092548 * n.x * n.z)
        + c[8].xyz * (0.546274 * (n.x * n.x - n.y * n.y));
    return max(sum, float3(0.0));
}

// A bound probe's irradiance from the buffer of its slot.
public float3 probeIrradiance(int i, float3 n)
{
    float4 c[9];
    for (int k = 0; k < 9; k++)
    {
        if (i == 0) c[k] = probeIrradiance0[k];
        else if (i == 1) c[k] = probeIrradiance1[k];
        else if (i == 2) c[k] = probeIrradiance2[k];
        else c[k] = probeIrradiance3[k];
    }
    return harmonics(c, n);
}

// The bound probe whose box holds a point, the smallest where boxes overlap, or -1. A point a
// hundredth past a wall of the box still counts, so a room's wall built on the box's wall is in it
// everywhere rather than in it and out of it pixel by pixel.
public int probeAt(float3 p)
{
    int found = -1;
    float smallest = 1e30;
    for (int i = 0; i < min(int(lighting.probeCount.x), 4); i++)
    {
        float3 h = lighting.probes[i].halfSizeAndMip.xyz;
        float3 d = abs(p - lighting.probes[i].centerAndIntensity.xyz);
        float volume = h.x * h.y * h.z;
        if (all(d <= h * 1.01 + 0.01) && volume < smallest)
        {
            found = i;
            smallest = volume;
        }
    }
    return found;
}

// A probe's cube along a direction at a mip, by its slot.
public float3 probeSample(int i, float3 direction, float mip)
{
    if (i == 0) return probeMap0.SampleLevel(lightLinear, direction, mip).rgb;
    if (i == 1) return probeMap1.SampleLevel(lightLinear, direction, mip).rgb;
    if (i == 2) return probeMap2.SampleLevel(lightLinear, direction, mip).rgb;
    return probeMap3.SampleLevel(lightLinear, direction, mip).rgb;
}

// The way into a probe's cube that a ray from p along r meets the box's wall, from the middle the
// probe was captured at, so a reflection lands where the room's wall is rather than at infinity.
public float3 boxProjected(int i, float3 p, float3 r)
{
    float3 center = lighting.probes[i].centerAndIntensity.xyz;
    float3 h = lighting.probes[i].halfSizeAndMip.xyz;
    float3 far = (center + h - p) / r;
    float3 near = (center - h - p) / r;
    float3 exits = max(far, near);
    float t = min(exits.x, min(exits.y, exits.z));
    return p + r * t - center;
}

// The light a white diffuse surface facing n returns from the environment.
public float3 environmentIrradiance(float3 n)
{
    float4 c[9];
    for (int k = 0; k < 9; k++)
        c[k] = environmentIrradianceBuffer[k];
    return harmonics(c, n);
}

// The share of light from all around that a surface reflects, with no environment to reflect,
// as Brian Karis fitted it for mobile, so a smooth or metal surface under an ambient light is not
// black.
public float3 ambientReflected(float3 f0, float roughness, float nv)
{
    float4 c0 = float4(-1.0, -0.0275, -0.572, 0.022);
    float4 c1 = float4(1.0, 0.0425, 1.04, -0.04);
    float4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * nv)) * r.x + r.y;
    float2 ab = float2(-1.04, 1.04) * a004 + r.zw;
    return f0 * ab.x + ab.y;
}

// The light that bounced to a surface at p facing n, irradiance over pi, from the world's probes
// around it as gi.slang blends them, or -1 where no cascade of probes holds p.
public float3 bounced(float3 p, float3 n)
{
    int c = fieldCascade(bouncedField, p, 1.0);
    if (c < 0 || c >= int(lighting.indirect.w))
        return float3(-1.0);
    return bouncedAt(SplitVolume(bouncedLight, lightNearest), bouncedField, p, n, lighting.indirect.z, lighting.indirect.y, lighting.indirect.w) / 3.14159265;
}

// The light a glossy surface at p facing n reflects along r, traced through the window's depth and
// past it through the scene's distance field, with in a how much of it is known: 1 where a ray met
// a surface, faded at the picture's edge where the frame before's picture gives its light, and 0
// where the ray met nothing, or the surface is too rough to trace for, where the probes and the
// environment reflect alone. A surface met on the screen reflects the light the frame before
// showed where it was, and one past the picture or hidden from it its color times the light
// reaching it, as the light that bounces shades a hit.
public float4 tracedReflection(float3 p, float3 n, float3 r, float roughness, float3 eye)
{
    if (lighting.reflection.x <= 0.0 || roughness >= lighting.reflection.y)
        return float4(0.0);
    float probes = lighting.indirect.z, spacing = lighting.indirect.y, cascades = lighting.indirect.w;
    float cell = max(fieldCell(bouncedField, p), 0.02);
    float3 start = p + n * cell;
    ScreenCamera camera;
    camera.viewProjection = lighting.reflectViewProjection;
    camera.inverseViewProjection = lighting.reflectInverseViewProjection;
    camera.eye = eye;
    float3 hit;
    float left;
    if (traceScreen(SplitPicture(reflectDepth, lightNearest), camera, start, r, lighting.reflectionReach.x, int(lighting.reflection.z), hit, left))
    {
        float3 shaded = shadeHit(SplitVolume(reflectField, lightLinear), bouncedField, SplitVolume(reflectAlbedo, lightLinear), SplitVolume(reflectGlow, lightLinear), reflectLights, SplitVolume(bouncedLight, lightLinear), hit, probes, spacing, cascades);
        float4 last = mul(lighting.reflectLastViewProjection, float4(hit, 1.0));
        float2 uv = last.xy / max(last.w, 1e-4) * 0.5 + 0.5;
        if (lighting.reflection.w <= 0.0 || last.w <= 1e-4 || any(uv < 0.0) || any(uv > 1.0))
            return float4(shaded, 1.0);
        // Whether the frame before showed this surface there, its depth holding a point near the
        // hit, or something in front of it, which the reflection would show in its place.
        ScreenCamera before;
        before.viewProjection = lighting.reflectLastViewProjection;
        before.inverseViewProjection = lighting.reflectLastInverseViewProjection;
        before.eye = eye;
        float3 then = screenWorld(before, uv, reflectHistoryDepth.SampleLevel(lightNearest, uv, 0).r);
        float near = 0.05 + 0.02 * distance(eye, hit);
        float shown = 1.0 - smoothstep(near, near * 3.0, distance(then, hit));
        // The picture's light, blurred down its mips as the surface is rough, and the field's
        // shading where the picture ends or did not show the surface.
        float edge = saturate(min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)) * 12.0);
        float3 seen = reflectHistory.SampleLevel(lightLinear, uv, saturate(roughness / lighting.reflection.y) * 4.0).rgb;
        return float4(lerp(shaded, seen, edge * shown), 1.0);
    }
    float t = traceField(SplitVolume(reflectField, lightLinear), bouncedField, start, r, left, 1e5);
    if (t >= 0.0)
        return float4(shadeHit(SplitVolume(reflectField, lightLinear), bouncedField, SplitVolume(reflectAlbedo, lightLinear), SplitVolume(reflectGlow, lightLinear), reflectLights, SplitVolume(bouncedLight, lightLinear), start + r * t, probes, spacing, cascades), 1.0);
#if defined(RAY_QUERY)
    // What the field misses, past its cascades or too thin for its cells, the GPU's own rays find.
    if (lighting.reflectionReach.y > 0.0)
        return rayReflection(start, r);
#endif
    return float4(0.0);
}

#if defined(RAY_QUERY)
// Whether a ray from p along way meets any of the window's meshes within reach.
bool rayBlocked(float3 p, float3 way, float reach = 1e4)
{
    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> query;
    RayDesc ray;
    ray.Origin = p;
    ray.Direction = way;
    ray.TMin = 0.0;
    ray.TMax = reach;
    query.TraceRayInline(rayScene, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();
    return query.CommittedStatus() == COMMITTED_TRIANGLE_HIT;
}

// The light the first of the window's meshes a ray from p along way meets sends back along it,
// through the device's ray tracing: the copy's color over pi times the sun's light where a second
// ray toward the sun gets through, the lamps', each that casts shadows where a ray toward it gets
// through, and the light that bounced to it where a
// cascade of probes holds it or the light from all around where none does, with the light it gives
// off, and a 1, or a 0 where it meets none.
float4 rayReflection(float3 p, float3 way)
{
    RayQuery<RAY_FLAG_FORCE_OPAQUE> query;
    RayDesc ray;
    ray.Origin = p;
    ray.Direction = way;
    ray.TMin = 0.0;
    ray.TMax = 1e4;
    query.TraceRayInline(rayScene, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();
    if (query.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        return float4(0.0);

    // The triangle's face, from its mesh's corners into the world, turned toward the ray.
    RaySurface surface = raySurfaces[query.CommittedInstanceID()];
    uint corner = surface.corners.x + query.CommittedPrimitiveIndex() * 3u;
    float3 a = rayCorners[corner].xyz, b = rayCorners[corner + 1u].xyz, c = rayCorners[corner + 2u].xyz;
    float3 n = normalize(mul(cross(b - a, c - a), (float3x3)query.CommittedWorldToObject3x4()));
    if (dot(n, way) > 0.0)
        n = -n;
    float3 hit = p + way * query.CommittedRayT();

    // The lamps, falling off as directLight has them, each that casts shadows hidden where a ray
    // toward it meets a mesh short of it. The loop is its own here rather than a function
    // directLight calls too, since the model pass reshaped by that, with no ray query in it,
    // crashed lavapipe at its reflections (build/mesa).
    float3 arrived = float3(0.0);
    for (int i = 0; i < min(int(reflectLights.counts.x), 16); i++)
    {
        GiLight light = reflectLights.lights[i];
        float3 toLight = light.positionAndKind.xyz - hit;
        float distance2 = max(dot(toLight, toLight), 1e-4);
        float3 toward = toLight * rsqrt(distance2);
        float reaching = max(dot(n, toward), 0.0) / distance2;
        float range = light.directionAndRange.w;
        if (range > 0.0)
        {
            float edge = saturate(1.0 - pow(distance2 / (range * range), 2.0));
            reaching *= edge * edge;
        }
        if (int(light.positionAndKind.w) == 2)
            reaching *= smoothstep(light.cone.y, max(light.cone.x, light.cone.y + 1e-4), dot(-toward, light.directionAndRange.xyz));
        if (light.colorAndShadow.w > 0.5 && reaching > 0.0 && rayBlocked(hit + n * 0.01, toward, sqrt(distance2) - 0.05))
            continue;
        arrived += light.colorAndShadow.rgb * reaching;
    }
    float3 l = reflectLights.sunDirection.xyz;
    if (reflectLights.sunDirection.w > 0.5 && dot(n, l) > 0.0 && !rayBlocked(hit + n * 0.01, l))
        arrived += reflectLights.sunColor.rgb * dot(n, l);
    // The lamps and the sun as the light reaching the face, beside the bounced light and the sky,
    // as shadeHit takes them.
    arrived *= Pi;
    if (fieldCascade(bouncedField, hit, 1.0) >= 0 && fieldCascade(bouncedField, hit, 1.0) < int(lighting.indirect.w))
        arrived += bouncedAt(SplitVolume(bouncedLight, lightNearest), bouncedField, hit, n, lighting.indirect.z, lighting.indirect.y, lighting.indirect.w);
    else
        arrived += lighting.environment.z > 0.0 ? environmentIrradiance(n) * lighting.environment.x * Pi : reflectLights.sky.rgb * Pi;
    return float4(surface.color.rgb / Pi * arrived + surface.emission.rgb, 1.0);
}
#endif
