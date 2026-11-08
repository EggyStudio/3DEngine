// The window's meshes as the device's ray tracing holds them, which gi_trace.slang traces the
// probes' rays through at the top quality where the device traces rays, and how a ray meets them.
module meshrays;

import color;

#if defined(RAY_QUERY)
// Where the device traces rays, the window's meshes as its ray tracing sees them, each copy's color
// and where its mesh's corners lie, and every mesh's corners in its own space with their colors,
// which a probe's ray is traced through at the top quality in place of the field (lightset.slang).
[[vk::binding(17, 0)]]
RaytracingAccelerationStructure rayScene;

public struct RaySurface
{
    float4 color;
    float4 emission;
    uint4 corners;
};

[[vk::binding(18, 0)]]
StructuredBuffer<RaySurface> raySurfaces;

[[vk::binding(19, 0)]]
StructuredBuffer<float4> rayCorners;

// A corner's color, linear, as the model pass brings a vertex's color in.
public float3 meshCornerColor(float4 corner)
{
    uint bits = asuint(corner.w);
    return srgbToLinear(float3(float(bits & 255u), float((bits >> 8) & 255u), float((bits >> 16) & 255u)) / 255.0);
}

// How far a ray from origin along way goes before it meets one of the window's meshes, within
// reach, with that face's normal turned toward the ray and its color as the copy's color times
// its corners', or below zero where it meets none.
public float meshHit(float3 origin, float3 way, float reach, out float3 n, out float3 color, out float2 gloss)
{
    gloss = float2(0.5, 0.0);
    n = float3(0.0, 1.0, 0.0);
    color = float3(0.0);
    RayQuery<RAY_FLAG_FORCE_OPAQUE> query;
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = way;
    ray.TMin = 0.0;
    ray.TMax = reach;
    query.TraceRayInline(rayScene, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();
    if (query.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        return -1.0;
    RaySurface surface = raySurfaces[query.CommittedInstanceID()];
    gloss = meshGloss(query.CommittedInstanceID());
    uint corner = surface.corners.x + query.CommittedPrimitiveIndex() * 3u;
    float3 a = rayCorners[corner].xyz, b = rayCorners[corner + 1u].xyz, c = rayCorners[corner + 2u].xyz;
    n = normalize(mul(cross(b - a, c - a), (float3x3)query.CommittedWorldToObject3x4()));
    if (dot(n, way) > 0.0)
        n = -n;
    float2 between = query.CommittedTriangleBarycentrics();
    color = surface.color.rgb * (meshCornerColor(rayCorners[corner]) * (1.0 - between.x - between.y)
        + meshCornerColor(rayCorners[corner + 1u]) * between.x + meshCornerColor(rayCorners[corner + 2u]) * between.y);
    return query.CommittedRayT();
}

// A face of a copy as its corners lie in the world, its winding's way out, from the copy's
// first corner and the face's index and the transform back to the copy's own space.
public float3 meshFaceOut(uint copy, uint face, float3x4 worldToObject)
{
    uint corner = raySurfaces[copy].corners.x + face * 3u;
    float3 a = rayCorners[corner].xyz, b = rayCorners[corner + 1u].xyz, c = rayCorners[corner + 2u].xyz;
    return mul(cross(b - a, c - a), (float3x3)worldToObject);
}

// A copy's roughness and metallic share, as the reference lights it by (GpuRayScene).
public float2 meshGloss(uint copy)
{
    return saturate(float2(asfloat(raySurfaces[copy].corners.y), asfloat(raySurfaces[copy].corners.z)));
}

// The nearest face a ray from origin along way meets from tMin to reach but those it leaves a copy
// that lets light through by, as gi_reference.slang's nearestEntered, its copy, its normal turned
// toward the ray and its color, or below zero where it meets none.
public float meshEntered(float3 origin, float3 way, float tMin, float reach, out uint copy, out float3 n, out float3 color)
{
    copy = 0u;
    n = float3(0.0, 1.0, 0.0);
    color = float3(0.0);
    RayQuery<RAY_FLAG_FORCE_NON_OPAQUE> query;
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = way;
    ray.TMin = tMin;
    ray.TMax = reach;
    query.TraceRayInline(rayScene, RAY_FLAG_NONE, 0xFF, ray);
    while (query.Proceed())
    {
        uint met = query.CandidateInstanceID();
        if (asfloat(raySurfaces[met].corners.w) > 0.0
            && dot(meshFaceOut(met, query.CandidatePrimitiveIndex(), query.CandidateWorldToObject3x4()), way) > 0.0)
            continue;
        query.CommitNonOpaqueTriangleHit();
    }
    if (query.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        return -1.0;
    copy = query.CommittedInstanceID();
    RaySurface surface = raySurfaces[copy];
    uint corner = surface.corners.x + query.CommittedPrimitiveIndex() * 3u;
    n = normalize(meshFaceOut(copy, query.CommittedPrimitiveIndex(), query.CommittedWorldToObject3x4()));
    if (dot(n, way) > 0.0)
        n = -n;
    float2 between = query.CommittedTriangleBarycentrics();
    color = surface.color.rgb * (meshCornerColor(rayCorners[corner]) * (1.0 - between.x - between.y)
        + meshCornerColor(rayCorners[corner + 1u]) * between.x + meshCornerColor(rayCorners[corner + 2u]) * between.y);
    return query.CommittedRayT();
}

// How far a ray that entered a copy letting light through at t runs inside it, to the nearest of
// the faces it leaves the copy by or of another copy's faces within 256 units, or 0 where it leaves
// by none, as past a sheet, which has no depth, as gi_reference.slang's depthInside.
public float meshDepthInside(float3 origin, float3 way, float t, uint copy)
{
    RayQuery<RAY_FLAG_FORCE_NON_OPAQUE> query;
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = way;
    ray.TMin = t + 1e-4;
    ray.TMax = t + 256.0;
    query.TraceRayInline(rayScene, RAY_FLAG_NONE, 0xFF, ray);
    float leaves = 1e30, other = 1e30;
    while (query.Proceed())
    {
        uint met = query.CandidateInstanceID();
        float at = query.CandidateTriangleRayT();
        if (met != copy)
            other = min(other, at);
        else if (dot(meshFaceOut(met, query.CandidatePrimitiveIndex(), query.CandidateWorldToObject3x4()), way) > 0.0)
            leaves = min(leaves, at);
    }
    return leaves < 1e30 ? min(leaves, other) - t : 0.0;
}

// The share of the light a copy lets through a unit of its depth, 0 for a solid one.
public float meshPassing(uint copy)
{
    return min(asfloat(raySurfaces[copy].corners.w), 1.0);
}
#endif
