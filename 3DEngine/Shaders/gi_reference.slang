// A path-traced reference of the window's picture, which the light that bounces is measured
// against: from each pixel, paths through the window's meshes as the device's ray tracing holds
// them (GpuRayScene), each hit a copy's flat color and the light it gives off. The first hit is lit
// as the model pass lights one, each light's diffuse share past what its face reflects and its
// specular share by the material's roughness and metallic, and every hit after it as the light
// that bounces lights one, its color times what arrives. A light that casts shadows reaches a hit
// only where a ray toward it gets through, as the shadow maps and the field let it. A path goes on
// in a direction drawn by the cosine about the face's normal, ends at what Russian roulette draws
// after its third bounce, and brings back the sky, the environment map's or the ambient lights',
// where it meets nothing. Lights are given as the model pass has them, the light a white diffuse
// surface facing one returns, so a surface's diffuse light is its color times what arrives, with
// no pi. Each pass adds its samples to an image of sums, the count in alpha, and its first sample
// of each pixel, through the pixel's middle, writes what it met into the image of regions: the
// copy's index times eight and the axis its face turns toward most, plus one, or 0 for nothing.
// Started at a probe instead, a texel of the image is a texel of the probe's octahedron, its paths
// leaving the probe's middle through it and every face they meet lit as the light that bounces
// lights one, so the image is the light arriving at the probe from each way, as its rays and the
// merge of the cascades above should bring it.

[[vk::binding(0, 0)]]
RaytracingAccelerationStructure scene;

struct RaySurface
{
    // rgb: the copy's color, linear.
    float4 color;
    // rgb: the light it gives off, linear.
    float4 emission;
    // x: its mesh's first corner in corners. y: its roughness's bits. z: its metallic's bits.
    uint4 corners;
};

[[vk::binding(1, 0)]]
StructuredBuffer<RaySurface> surfaces;

[[vk::binding(2, 0)]]
StructuredBuffer<float4> corners;

[[vk::binding(3, 0)]]
RWTexture2D<float4> sums;

[[vk::binding(4, 0)]]
RWTexture2D<uint> regions;

struct Lamp
{
    // xyz: where it is. w: its kind, 1 for a point light and 2 for a spot.
    float4 positionAndKind;
    // xyz: the way a spot points. w: the distance it fades to nothing at, or 0 for none.
    float4 directionAndRange;
    // rgb: its color times its intensity, linear. w: 1 where it casts shadows.
    float4 colorAndShadow;
    // x: the cosine of a spot's inner angle. y: of its outer angle.
    float4 cone;
};

struct ReferenceLights
{
    // xyz: toward the sun, the first directional light. w: 1 where there is one.
    float4 sunDirection;
    // rgb: its color times its intensity. w: 1 where it casts shadows.
    float4 sunColor;
    // rgb: what a ray that meets nothing brings back, the ambient lights' sum, or the environment
    // map's intensity where w is 1, which then brings back the map's light along the ray.
    float4 sky;
    // x: how many lamps.
    float4 counts;
    Lamp lamps[16];
};

[[vk::binding(5, 0)]]
ConstantBuffer<ReferenceLights> lights;

[[vk::binding(6, 0)]]
SamplerCube environment;

struct ReferencePush
{
    // Clip space to the world, the window's camera's.
    float4x4 inverseViewProjection;
    // x, y: the image's size. z: the samples a pixel this pass adds. w: the most bounces a path takes.
    uint4 sizeAndCounts;
    // x: the first sample this pass adds, which seeds its random numbers. y: 1 where the paths
    // start at a probe. zw: unused.
    uint4 first;
    // xyz: the probe's middle, where the paths start at one.
    float4 probe;
};

[[vk::push_constant]]
ConstantBuffer<ReferencePush> push;

static const float Pi = 3.14159265;

// A random number from 0 to 1 and the state moved on, by the PCG hash.
float random(inout uint state)
{
    state = state * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return float((word >> 22u) ^ word) / 4294967296.0;
}

// The light a lamp sends to a point facing n, as the model pass's arriving gives it, with the way toward it.
float3 lampArriving(Lamp lamp, float3 p, float3 n, out float3 toward, out float distance)
{
    float3 toLamp = lamp.positionAndKind.xyz - p;
    float distanceSquared = max(dot(toLamp, toLamp), 1e-4);
    distance = sqrt(distanceSquared);
    toward = toLamp / distance;
    float result = max(dot(n, toward), 0.0) / distanceSquared;
    float range = lamp.directionAndRange.w;
    if (range > 0.0)
    {
        float edge = saturate(1.0 - pow(distanceSquared / (range * range), 2.0));
        result *= edge * edge;
    }
    if (int(lamp.positionAndKind.w) == 2)
        result *= smoothstep(lamp.cone.y, max(lamp.cone.x, lamp.cone.y + 1e-4), dot(-toward, lamp.directionAndRange.xyz));
    return lamp.colorAndShadow.rgb * result;
}

// Whether a ray from p along way meets any mesh within reach.
bool blocked(float3 p, float3 way, float reach)
{
    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> query;
    RayDesc ray;
    ray.Origin = p;
    ray.Direction = way;
    ray.TMin = 0.0;
    ray.TMax = reach;
    query.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();
    return query.CommittedStatus() == COMMITTED_TRIANGLE_HIT;
}

// How much a light along l reflects toward v off a face of roughness and f0 that faces n, as the
// model pass's specularReflected gives it, times pi as its lights are given, and the share its
// face reflects.
float3 specularReflected(float3 n, float3 v, float3 l, float3 f0, float alpha, out float3 fresnel)
{
    float3 h = normalize(v + l);
    float nl = max(dot(n, l), 1e-4);
    float nv = max(dot(n, v), 1e-4);
    float nh = max(dot(n, h), 0.0);
    float a2 = alpha * alpha;
    float d = nh * nh * (a2 - 1.0) + 1.0;
    float distribution = a2 / (Pi * d * d);
    float visibility = 0.5 / (nl * sqrt(nv * nv * (1.0 - a2) + a2) + nv * sqrt(nl * nl * (1.0 - a2) + a2));
    fresnel = f0 + (1.0 - f0) * pow(1.0 - max(dot(v, h), 0.0), 5.0);
    return distribution * visibility * fresnel * Pi;
}

// The light the sun and the lamps send back from p on a face of a color, toward v where the face is
// the path's first, lit as the model pass lights it, and as the light that bounces lights a face
// otherwise.
float3 direct(float3 p, float3 n, float3 color, float3 v, bool first, float roughness, float metallic)
{
    float3 diffuseColor = first ? color * (1.0 - metallic) : color;
    float3 f0 = lerp(float3(0.04), color, metallic);
    float alpha = roughness * roughness;
    float3 start = p + n * 1e-3;
    float3 sum = float3(0.0);

    if (lights.sunDirection.w > 0.5)
    {
        float3 l = lights.sunDirection.xyz;
        float nl = dot(n, l);
        if (nl > 0.0 && !(lights.sunColor.w > 0.5 && blocked(start, l, 1e5)))
        {
            float3 arrived = lights.sunColor.rgb * nl;
            float3 fresnel = float3(0.0);
            float3 specular = first ? specularReflected(n, v, l, f0, alpha, fresnel) : float3(0.0);
            sum += arrived * (diffuseColor * (1.0 - fresnel) + specular);
        }
    }
    for (int i = 0; i < min(int(lights.counts.x), 16); i++)
    {
        Lamp lamp = lights.lamps[i];
        float3 toward;
        float distance;
        float3 arrived = lampArriving(lamp, p, n, toward, distance);
        if (all(arrived <= float3(0.0)))
            continue;
        if (lamp.colorAndShadow.w > 0.5 && blocked(start, toward, distance - 2e-3))
            continue;
        float3 fresnel = float3(0.0);
        float3 specular = first ? specularReflected(n, v, toward, f0, alpha, fresnel) : float3(0.0);
        sum += arrived * (diffuseColor * (1.0 - fresnel) + specular);
    }
    return sum;
}

// What a ray that meets nothing brings back.
float3 sky(float3 way)
{
    return lights.sky.w > 0.5 ? environment.SampleLevel(way, 0).rgb * lights.sky.rgb : lights.sky.rgb;
}

// A way about n drawn by the cosine.
float3 cosineAbout(float3 n, inout uint state)
{
    float u = random(state), w = random(state);
    float r = sqrt(u);
    float phi = 2.0 * Pi * w;
    float3 side = abs(n.y) < 0.99 ? normalize(cross(n, float3(0, 1, 0))) : float3(1, 0, 0);
    float3 up = cross(n, side);
    return normalize(side * (r * cos(phi)) + up * (r * sin(phi)) + n * sqrt(max(0.0, 1.0 - u)));
}

// The way through a texel of a probe's octahedron at uv, as gi.slang's fromOctahedron gives it.
float3 fromOctahedron(float2 uv)
{
    float2 p = uv * 2.0 - 1.0;
    float3 d = float3(p, 1.0 - abs(p.x) - abs(p.y));
    if (d.z < 0.0)
        d.xy = (1.0 - abs(d.yx)) * float2(d.x >= 0.0 ? 1.0 : -1.0, d.y >= 0.0 ? 1.0 : -1.0);
    return normalize(d);
}

// The light one path brings back from origin along way, and what its first ray met. From the
// camera the first face is lit as the model pass lights one, and from a probe as the bounce does.
float3 trace(float3 origin, float3 way, bool fromProbe, inout uint state, out uint region)
{
    float3 throughput = float3(1.0);
    float3 light = float3(0.0);
    region = 0u;

    for (uint bounce = 0u; bounce < push.sizeAndCounts.w; bounce++)
    {
        RayQuery<RAY_FLAG_FORCE_OPAQUE> query;
        RayDesc ray;
        ray.Origin = origin;
        ray.Direction = way;
        ray.TMin = 0.0;
        ray.TMax = 1e5;
        query.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);
        query.Proceed();
        if (query.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        {
            light += throughput * sky(way);
            break;
        }

        uint instance = query.CommittedInstanceID();
        RaySurface surface = surfaces[instance];
        uint corner = surface.corners.x + query.CommittedPrimitiveIndex() * 3u;
        float3 a = corners[corner].xyz, b = corners[corner + 1u].xyz, c = corners[corner + 2u].xyz;
        float3 n = normalize(mul(cross(b - a, c - a), (float3x3)query.CommittedWorldToObject3x4()));
        if (dot(n, way) > 0.0)
            n = -n;
        float3 p = origin + way * query.CommittedRayT();
        if (bounce == 0u)
        {
            float3 m = abs(n);
            uint axis = m.x >= m.y && m.x >= m.z ? (n.x > 0.0 ? 0u : 1u) : m.y >= m.z ? (n.y > 0.0 ? 2u : 3u) : (n.z > 0.0 ? 4u : 5u);
            region = instance * 8u + axis + 1u;
        }

        bool first = bounce == 0u && !fromProbe;
        float roughness = clamp(asfloat(surface.corners.y), 0.045, 1.0);
        float metallic = saturate(asfloat(surface.corners.z));
        float3 color = surface.color.rgb;
        light += throughput * (surface.emission.rgb + direct(p, n, color, -way, first, roughness, metallic));

        // The diffuse share goes on, the first face's past its metal, as the light that bounces to
        // a pixel lights it.
        throughput *= first ? color * (1.0 - metallic) : color;
        if (bounce >= 3u)
        {
            float keep = clamp(max(throughput.r, max(throughput.g, throughput.b)), 0.05, 0.95);
            if (random(state) > keep)
                break;
            throughput /= keep;
        }
        if (all(throughput <= float3(0.0)))
            break;
        origin = p + n * 1e-3;
        way = cosineAbout(n, state);
    }
    return light;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint2 size = push.sizeAndCounts.xy;
    if (id.x >= size.x || id.y >= size.y)
        return;
    uint first = push.first.x;
    float3 sum = float3(0.0);
    for (uint s = 0u; s < push.sizeAndCounts.z; s++)
    {
        uint index = first + s;
        uint state = (id.y * size.x + id.x) * 9781u + index * 6271u + 1u;
        random(state);
        // The first sample of all through the pixel's middle, which names what the pixel shows.
        float2 jitter = index == 0u ? float2(0.5) : float2(random(state), random(state));
        float2 uv = (float2(id.xy) + jitter) / float2(size);
        uint region;
        if (push.first.y == 1u)
        {
            sum += trace(push.probe.xyz, fromOctahedron(uv), true, state, region);
            continue;
        }
        float4 near = mul(push.inverseViewProjection, float4(uv * 2.0 - 1.0, 0.0, 1.0));
        float4 far = mul(push.inverseViewProjection, float4(uv * 2.0 - 1.0, 1.0, 1.0));
        float3 origin = near.xyz / near.w;
        sum += trace(origin, normalize(far.xyz / far.w - origin), false, state, region);
        if (index == 0u)
            regions[id.xy] = region;
    }
    sums[id.xy] = sums[id.xy] + float4(sum, float(push.sizeAndCounts.z));
}
