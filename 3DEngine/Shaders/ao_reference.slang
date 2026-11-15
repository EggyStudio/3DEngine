// A traced reference of the window's ambient occlusion: from the face the eye's ray through each
// texel of the window's depth at half its size meets, where the depth holds a surface, rays drawn
// by the cosine about that face, each traced through the device's rays against the window's meshes, a ray that meets a
// face within the radius closed off and one that meets none open. The share of the rays open is
// the occlusion the horizons and the circular occluders are measured against, summed over the
// dispatches as the rays open and the rays traced.

[[vk::binding(0, 0)]]
RaytracingAccelerationStructure scene;

struct RaySurface
{
    float4 color;
    float4 emission;
    // x: its mesh's first corner in corners.
    uint4 corners;
};

[[vk::binding(1, 0)]]
StructuredBuffer<RaySurface> surfaces;

// Every mesh's corners in its own space (GpuRayScene).
[[vk::binding(2, 0)]]
StructuredBuffer<float4> corners;

[[vk::binding(3, 0)]]
Sampler2D depth;

// x: the rays open, y: the rays traced, summed over the dispatches.
[[vk::binding(4, 0)]] [format("rg32f")]
RWTexture2D<float2> sums;

struct ReferencePush
{
    // Clip space to the scene's places, against the ray scene's origin.
    float4x4 inverseViewProjection;
    // xyz: the eye against the same origin. w: the radius.
    float4 eyeAndRadius;
    // x: the dispatch's seed. y: the rays a pixel each dispatch.
    uint4 seedAndRays;
};

[[vk::push_constant]] ConstantBuffer<ReferencePush> push;

static const float Pi = 3.14159265359;

float random(inout uint state)
{
    state = state * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return float((word >> 22u) ^ word) / 4294967296.0;
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

// Whether a face lies along a ray within reach.
bool closed(float3 origin, float3 way, float reach)
{
    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> query;
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = way;
    ray.TMin = 0.0;
    ray.TMax = reach;
    query.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();
    return query.CommittedStatus() == COMMITTED_TRIANGLE_HIT;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint width, height;
    sums.GetDimensions(width, height);
    if (id.x >= width || id.y >= height)
        return;
    float d = depth.Load(int3(id.xy, 0)).r;
    // Nothing was drawn here, as the sky, which the sums leave at none traced.
    if (d >= 1.0)
        return;
    float2 uv = (float2(id.xy) + 0.5) / float2(width, height);
    float4 world = mul(push.inverseViewProjection, float4(uv * 2.0 - 1.0, d, 1.0));
    float3 p = world.xyz / world.w;
    float3 eye = push.eyeAndRadius.xyz;
    float3 toward = p - eye;
    float distance = length(toward);
    float3 way = toward / max(distance, 1e-6);

    // The face the eye's ray through the point meets, turned toward the eye.
    RayQuery<RAY_FLAG_FORCE_OPAQUE> query;
    RayDesc ray;
    ray.Origin = eye;
    ray.Direction = way;
    ray.TMin = 0.0;
    ray.TMax = 1e5;
    query.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();
    if (query.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        return;
    uint corner = surfaces[query.CommittedInstanceID()].corners.x + query.CommittedPrimitiveIndex() * 3u;
    float3x4 toWorld = query.CommittedObjectToWorld3x4();
    float3 a = mul(toWorld, float4(corners[corner].xyz, 1.0));
    float3 b = mul(toWorld, float4(corners[corner + 1u].xyz, 1.0));
    float3 c = mul(toWorld, float4(corners[corner + 2u].xyz, 1.0));
    float3 n = normalize(cross(b - a, c - a));
    if (dot(n, way) > 0.0)
        n = -n;

    // From the point met, lifted off the face by a little of its distance from the eye.
    float met = query.CommittedRayT();
    float3 origin = eye + way * met + n * (1e-3 * met + 1e-4);
    uint state = (id.y * width + id.x) * 9781u + push.seedAndRays.x * 6271u + 1u;
    uint rays = push.seedAndRays.y;
    float open = 0.0;
    for (uint r = 0u; r < rays; r++)
        if (!closed(origin, cosineAbout(n, state), push.eyeAndRadius.w))
            open += 1.0;
    sums[id.xy] = sums[id.xy] + float2(open, float(rays));
}
