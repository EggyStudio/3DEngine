// The light a rough surface reflects from the world's probes along its mirror way, which the model
// pass reads where global illumination is on (probesReflected in lights.slang), from each probe's
// light by way that gi_ambient.slang gathers and its faces.
import gi;
import scenefield;

// The texels along each side of a probe's block of its light by way, eight by eight directions with
// a texel's border about them, each border texel the one across the octahedron's edge from it, so a
// filtered read across a fold of the octahedron blends the directions on either side of it.
public static const int WaysTexels = 10;

// How much of a probe at probe a surface at p facing n sees, as probeSees gives it, its reach read
// filtered between the four directions about the way to p within the probe's block, so the share
// changes smoothly from one direction to the next, where read at the nearest a wall's reflection
// would show the octahedron's facets.
public float probeSeesBlended<V : IVolume>(V reach, float3 p, float3 n, float3 probe, int3 at, int c, float probes, float apart, int cascades)
{
    float3 toward = p + n * (0.3 * apart) - probe;
    float d = length(toward);
    if (d < 1e-4)
        return 1.0;
    float2 along = clamp(octahedron(toward / d) * float(ReachTexels), 0.5, float(ReachTexels) - 0.5);
    float3 uvw = float3((float2(at.xy * ReachTexels) + along) / (probes * float(ReachTexels)), (float(c * int(probes) + at.z) + 0.5) / (probes * float(cascades)));
    float reached = reach.sampleAt(uvw, 0.0).r;
    float slack = 0.125 * apart;
    return saturate((reached + slack - d) / slack);
}

// The light a rough surface at p facing n reflects along r, radiance, from the eight probes of the
// finest cascade around p that holds it, with the share of it the probes the surface sees give, or
// -1 where no cascade of probes holds p (reflectedIn). The finest cascade is read alone, as the
// sky's share is (skyAt), since fading it to the next across its outer band, as bouncedAt fades,
// doubles the reads there.
public float4 reflectedAt<V : IVolume>(V cubes, V ways, V reach, SceneFieldInfo info, float3 p, float3 n, float3 r, float roughness, float probes,
    float spacing, float cascades)
{
    int c = fieldCascade(info, p, 1.0, int(cascades));
    if (c < 0 || c >= int(cascades))
        return float4(-1.0);
    return reflectedIn(cubes, ways, reach, info, p, n, r, roughness, probes, spacing, c, int(cascades));
}

// The light along r the eight probes of cascade c around p hold that the surface sees, each weighed
// as bouncedIn weighs it for a surface facing n, with how much of the probes' weight those hold,
// whole at a twentieth of it, below which the model pass takes the light reaching the surface in
// its place. A probe's light along r is read from its eight by eight directions, blended between
// the four whose middles lie about r, so it spans the 25 degrees or so a texel does, as a surface
// of roughness 0.5 spreads its reflection, and from its faces r looks toward, the light reaching a
// surface facing r, which spreads it as widely as a fully rough surface does, the two mixed by the
// roughness from 0.5 to 1. The directions are sampled within each probe's block, its border holding
// the directions across each fold, and at its slice's middle, so no probe lends its neighbor's
// light, in one filtered read. Clamped to the block's own texels, a way level with the eye in the
// octahedron's folded half would read the texels on either side of the edge far apart, and walls
// would show a seam at the eye's height and shards along the folds. A probe the surface does not
// see is left out, since along a mirror way it lends the light of that one way, and a probe under
// the floor at the foot of a wall reads the ground the sky holds below the scene, in the sun. Kept
// at the twentieth of its weight bouncedIn keeps, even less the share of the sky its faces saw, as
// bouncedIn takes it, such a probe would light the foot of a wall far brighter than the wall above
// it, the face spreading a hemisphere the floor above closes half of.
float4 reflectedIn<V : IVolume>(V cubes, V ways, V reach, SceneFieldInfo info, float3 p, float3 n, float3 r, float roughness, float probes, float spacing,
    int c, int cascades)
{
    float4 oc = cascadeAt(info, c, cascades);
    float3 q = clamp((p - oc.xyz) / (oc.w * spacing) - 0.5, 0.0, probes - 1.0);
    int3 base = int3(floor(q));
    float3 f = q - float3(base);
    float3 r2 = r * r;
    float wide = saturate((roughness - 0.5) * 2.0);
    float2 along = octahedron(r) * float(WaysTexels - 2) + 1.0;
    float2 across = float2(probes * float(WaysTexels));
    float deep = probes * float(cascades);
    float3 sum = float3(0.0);
    float weights = 0.0, all = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 at = min(base + offset, int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float3 faces = float3(0.0);
        float valid = 1.0;
        float3 moved = float3(0.0);
        for (int axis = 0; axis < 3; axis++)
        {
            int face = axis * 2 + (r[axis] < 0.0 ? 1 : 0);
            float4 texel = cubes.loadAt(int3(face * int(probes) + at.x, at.y, c * int(probes) + at.z));
            faces += texel.rgb * r2[axis];
            valid = min(valid, faceHolds(texel.a));
            moved[axis] = faceMove(texel.a);
        }
        float3 probe = oc.xyz + (float3(at) + 0.5 + moved) * (oc.w * spacing);
        float weight = lerps.x * lerps.y * lerps.z * valid * probeFacing(p, n, probe);
        float sees = probeSeesBlended(reach, p, n, probe, at, c, probes, oc.w * spacing, cascades);
        float3 uvw = float3((float2(at.xy * WaysTexels) + along) / across, (float(c * int(probes) + at.z) + 0.5) / deep);
        sum += lerp(ways.sampleAt(uvw, 0.0).rgb, faces / Pi, wide) * weight * sees;
        weights += weight * sees;
        all += weight;
    }
    return weights > 1e-5 ? float4(sum / weights, saturate(weights / max(0.05 * all, 1e-5))) : float4(0.0);
}
