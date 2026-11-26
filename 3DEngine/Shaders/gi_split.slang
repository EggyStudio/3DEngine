// The RadianceCascades module's split cascades over the window, the store's probes asked for by
// the points the eye sees, each pixel at full size, their light traced by a ray a point a frame and
// gathered back onto the pixels, every pass of a frame one dispatch of this kernel picked by the
// push (rc_split.slang). The rays meet the window's meshes where the device traces rays and the
// field's march where it does not, and a face they meet is shaded as the screen's probes shade it
// (gi_screen_cascades.slang). The store's places are the frame's taken against an origin of the
// store's own, which moves by whole repeats of the probes' keys, so a probe keeps its key and its
// light as the frame's origin moves with the eye (GpuSplitStore).
import gi;
import probelight;
import scenefield;
import screenmarch;
import meshrays;
import gi_split_store;

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

// The window's lights, and in y of their counts the frame's whole count from the engine's start,
// which the ways of a point's rays turn by (GlobalIlluminationRenderer.Lights).
[[vk::binding(5, 0)]]
ConstantBuffer<GiLights> lights;

// The light the field's cells bounce back, and their light at the surface, as a face a ray meets
// takes it (probelight.slang).
[[vk::binding(6, 0)]]
Sampler3D bounced;

[[vk::binding(7, 0)]]
Sampler3D bouncedAtSurface;

// The window's camera and its eye, its depth's size and the window's, and the world's probes as a
// face's shading reads them (GlobalIlluminationRenderer.ViewBytes).
[[vk::binding(8, 0)]]
ConstantBuffer<ScreenView> view;

[[vk::binding(9, 0)]]
SamplerCube environment;

[[vk::binding(10, 0)]]
Texture3D<float4> fieldSecond;

[[vk::binding(11, 0)]] [format("r32ui")]
Texture3D<uint> fieldFaces;

// The store's uints and its light, laid out by its shape (splitUints, splitLights).
[[vk::binding(12, 0)]]
RWStructuredBuffer<Atomic<uint>> uints;

[[vk::binding(13, 0)]]
RWStructuredBuffer<float4> light;

// rgb: the light each pixel's point takes over pi, as gi_surface.slang writes it for the model
// pass, or -1 where the depth holds no surface or the store holds no probe about the point. a: 0.
[[vk::binding(14, 0)]] [format("rgba16f")]
RWTexture2D<float4> gathered;

struct SplitPush
{
    // x: the pass, as computeMain numbers them. y: the cascade the pass runs over.
    uint4 pass;
    // x: the most probes the store holds. y: its levels. z: its cascades. w: how many frames a
    // probe no point asks for is kept.
    uint4 shape;
    // x: the finest level's spacing. y: how far the finest level reaches from the eye.
    float4 spacing;
    // xyz: the frame's origin against the store's, added to a place of the frame to give the
    // store's place.
    float4 origin;
};

[[vk::push_constant]]
ConstantBuffer<SplitPush> push;

// The window's pixels' points as the module's passes take them, a pixel an index, row by row,
// each read from the window's depth as gi_surface.slang reads its own (WindowPoints).
struct DepthPoints : IVisiblePoints
{
    uint2 pixelOf(uint index) { return uint2(index % uint(view.sizes.z), index / uint(view.sizes.z)); }

    // The middle of the depth's texel the pixel's middle falls in.
    float2 uvOf(uint index)
    {
        float2 pixel = float2(pixelOf(index)) + 0.5;
        return (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    }

    float depthOf(uint index) { return depth.SampleLevel(uvOf(index), 0).r; }

    bool seenAt(uint index) { return depthOf(index) < 1.0; }

    float3 placeAt(uint index) { return screenWorld(camera(), uvOf(index), depthOf(index)) + push.origin.xyz; }

    float3 normalAt(uint index)
    {
        float2 uv = uvOf(index);
        return depthNormal(uv, fromEyeAt(uv, depthOf(index)));
    }

    // The split gather keeps the light the point takes, the irradiance, as the dense gather does,
    // and the model pass reads it over pi.
    void keepTaken(uint index, float3 taken) { gathered[pixelOf(index)] = float4(taken / Pi, 0.0); }
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
// holds there toward the eye, from the nearer of the texels on either side along each axis.
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

// A part of a point's ray, the first of the window's meshes it meets where the device traces rays
// and else the first surface of the field it marches to, the face shaded by its own normal, color
// and gloss or by the field's, its indirect light from the world's probes' bounce. Through the
// meshes a part that meets none passes, as the coarsest cascade's sky stands past its last
// interval. Through the field a part that meets none and ends past every cascade of the field has
// left the world, the field's march not saying where.
struct WindowHits : IRayHits
{
    ProbeRay along(float3 from, float3 way, float start, float end)
    {
        // The ray's start in the frame's places, as the meshes and the field stand in them.
        float3 origin = from - push.origin.xyz;
        ProbeRay ray;
        ray.ended = RayPast;
        ray.t = end;
        ray.carried = 1.0;
        ray.light = float3(0.0);
        float3 own;
#if defined(RAY_QUERY)
        float3 n, color;
        float2 gloss;
        float t = meshHit(origin + way * start, way, end - start, n, color, gloss);
        if (t >= 0.0)
        {
            ray.ended = RayMet;
            ray.t = start + t;
            ray.light = shadeProbeHitAt(sceneField, fieldInfo, fieldInfo, fieldAlbedo, fieldGlow, lights, bounced, bouncedAtSurface, origin + way * ray.t,
                n, color, gloss, way, view.trace.y, view.trace.z, view.trace.w, float3(1.0), MeshSun(), own);
        }
#else
        // The field's march meets the surface a ray leaves within a band of the field's own about
        // it, which the module's start off the surface does not know, so the march begins one of
        // the field's cells along the way at least.
        float t = traceFieldSheets(SheetVolume(sceneField, fieldSecond), fieldInfo, origin, way, max(start, fieldCell(fieldInfo, origin)), end);
        if (t >= 0.0)
        {
            float3 n;
            ray.ended = RayMet;
            ray.t = t;
            ray.light = shadeProbeHitN(sceneField, SheetVolume(sceneField, fieldSecond), fieldInfo, fieldInfo, fieldAlbedo, fieldFaces, fieldGlow, lights,
                bounced, bouncedAtSurface, bounced, origin + way * t, way, view.trace.y, view.trace.z, view.trace.w, true, float3(1.0), FieldSun(), own, n);
        }
        else if (fieldCascade(fieldInfo, origin + way * end, 0.0) < 0)
            ray.ended = RayLeft;
#endif
        return ray;
    }
}

// The window's sky along a way, its environment map times the sky's color where the map stands in
// for it, as the screen's probes take it.
struct WindowSky : ISky
{
    float3 skyAlong(float3 way) { return lights.sky.w > 0.5 ? environment.SampleLevel(way, 0).rgb * lights.sky.rgb : lights.sky.rgb; }
}

// The passes of a frame in the order the module runs them, each a thread an index of what it runs
// over, 1024 groups of 64 a row of the dispatch: 0 empties the table over four slots a probe, 1
// keeps the probes still asked for over the entries, 2 asks for the seen points' probes over the
// pixels, 3 asks for a cascade's probes from the one below over that cascade's entries, 4 traces
// the seen points' rays over the pixels, 5 keeps the ways' light over every cascade's ways, 6
// merges a cascade's light over its ways from the coarsest down, and 7 gathers onto the pixels.
[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint index = id.y * 65536u + id.x;
    uint pass = push.pass.x, cascade = push.pass.y;
    SplitFrame at = { view.eye.xyz + push.origin.xyz, uint(lights.counts.y) };
    SplitStore shape = { push.shape.x, push.spacing.x, push.shape.y, push.shape.z, push.spacing.y, push.shape.w };
    StoreUints store = { uints };
    StoreLight kept = { light };
    DepthPoints points;
    WindowHits hits;
    WindowSky sky;
    bool pixel = index < uint(view.sizes.z) * uint(view.sizes.w);
    if (pass == 0u)
        clearSplitTable(store, shape, index);
    else if (pass == 1u)
        keepSplitProbes(store, at, shape, index);
    else if (pass == 2u && pixel)
        askSeenProbes(points, store, at, shape, index);
    else if (pass == 3u)
        askCascadeProbes(store, at, shape, cascade, index);
    else if (pass == 4u && pixel)
        traceSeenRays(points, hits, sky, store, at, shape, index);
    else if (pass == 5u)
        keepSplitLight(store, kept, at, shape, index);
    else if (pass == 6u)
        mergeSplitCascade(store, kept, sky, at, shape, cascade, index);
    else if (pass == 7u && pixel)
    {
        if (!points.seenAt(index) || !splitHolds(at, shape, points.placeAt(index)))
            gathered[points.pixelOf(index)] = float4(-1.0);
        else
            gatherSurface(store, kept, at, shape, points, index);
    }
}
