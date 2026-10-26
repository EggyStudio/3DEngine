// How soft the sun's shadow is where it falls far from what casts it, read from a cascade of its
// map as percentage-closer soft shadows read one. The depths over a width the point's own depth in
// the map sets are searched for what shades it, and the light the map lets through is taken over
// the width that the sun's disc, as far off as what shades it, spreads to (SetShadowSoftness).
module penumbra;

import lightset;

// The most texels of a cascade the search and the filter reach out from the point, past which a
// cascade's shadow is as soft as it gets.
public static const float MostTexels = 24.0;

// How many depths the search reads and the filter compares, each on a disc.
static const int Taps = 16;

// The tap i of Taps on a disc a unit across turned by `turn`, a golden-angle spiral from the middle
// outward, so the taps cover the disc evenly at any count.
float2 tapOn(int i, float turn)
{
    float r = sqrt((float(i) + 0.5) / float(Taps));
    float a = float(i) * 2.39996323 + turn;
    return float2(cos(a), sin(a)) * r;
}

// How much of a cascade's depth changes from one of its texels to the next across the map along a
// surface facing n, through the cascade's transform m, which the depth a tap is compared with
// follows, so a wide filter on a surface the sun slants across compares each tap with the surface's
// own depth there and the surface does not shadow itself. A displacement within the surface moves
// the map's x, y and depth by the rows of m, and solved for the depth by x and y it gives the slope;
// held to the slope of a surface 80 degrees from facing the sun.
float2 receiverSlope(float4x4 m, float3 n, float ndcPerTexel, float most)
{
    float3 a = m[0].xyz, b = m[1].xyz, z = m[2].xyz;
    float3 bn = cross(b, n);
    float3 na = cross(n, a);
    float det = dot(a, bn);
    if (abs(det) < 1e-8)
        return float2(0.0);
    return clamp(float2(dot(z, bn), dot(z, na)) / det * ndcPerTexel, -most, most);
}

// Whether the cascade's depth at `at` lets the light through to a point at `depth`, taken between
// the four texels around it as near as it lies to each, so a filter of few taps over many texels
// draws a gradient rather than steps.
float litBetween(float2 at, float depth, float2 texel, bool fill)
{
    float4 d = fill ? shadowFillMap.Gather(lightNearest, at) : shadowMap.Gather(lightNearest, at);
    float4 s = step(float4(depth), d);
    float2 f = frac(at / texel - 0.5);
    return lerp(lerp(s.w, s.z, f.x), lerp(s.x, s.y, f.x), f.y);
}

float depthAt(float2 at, bool fill)
{
    return fill ? shadowFillMap.SampleLevel(lightNearest, at, 0.0).r : shadowMap.SampleLevel(lightNearest, at, 0.0).r;
}

// How much of the shadowed light reaches a point at p, in cascade c's clip space after the
// divide, its depth in the map at uv between lo and hi, the tile's texels short of its edges, on a
// surface facing n at world. The search reads over the width a caster at the cascade's near plane
// would spread the sun to, held to MostTexels either side, and where it finds nothing in front of
// the point the point is lit; where it does, the filter reads over the width the casters it found,
// at their mean depth, spread the sun to, a texel either side at least, so the edge is sharp where a
// caster meets the ground and widens with its height over it, by the sun's width across at a
// caster's height over the point. Read over twice that width, the edge near a pole's foot was 0.14
// units wide between a quarter and three quarters of the light where a hard one was 0.04. The taps turn by a hash of the point, which leaves a grain
// in a wide edge where a fixed disc left rings.
public float cascadeSoftly(int c, float3 p, float2 uv, float2 texel, float2 lo, float2 hi, bool fill, float3 n, float3 world)
{
    float spread = lighting.shadowSoftness.x;
    float4x4 m = lighting.shadowCascades[c];
    float zPerWorld = length(m[2].xyz);
    float texelWorld = lighting.shadowTexels[c];
    float ndcPerTexel = (fill ? 2.0 : 4.0) * texel.x;
    float2 slope = receiverSlope(m, n, ndcPerTexel, 5.67 * texelWorld * zPerWorld);
    float turn = frac(sin(dot(world, float3(12.9898, 78.233, 37.719))) * 43758.5453) * 6.2831853;

    float reach = clamp(0.5 * spread * (p.z / zPerWorld) / texelWorld, 1.0, MostTexels);
    float blockers = 0.0;
    float found = 0.0;
    for (int i = 0; i < Taps; i++)
    {
        float2 o = tapOn(i, turn) * reach;
        float depth = depthAt(clamp(uv + o * texel, lo, hi), fill);
        if (depth < p.z + dot(slope, o) - 0.0005)
        {
            blockers += depth;
            found += 1.0;
        }
    }
    // Found nothing in front of it, the point is lit; found in front of it at every tap, it lies
    // in the shadow's core, where the filter's narrower disc would find the same.
    if (found == 0.0)
        return 1.0;
    if (found == float(Taps))
        return 0.0;

    float width = clamp(0.5 * spread * ((p.z - blockers / found) / zPerWorld) / texelWorld, 1.0, MostTexels);
    float lit = 0.0;
    for (int i = 0; i < Taps; i++)
    {
        float2 o = tapOn(i, turn) * width;
        lit += litBetween(clamp(uv + o * texel, lo, hi), p.z + dot(slope, o) - 0.0005, texel, fill);
    }
    return lit / float(Taps);
}
