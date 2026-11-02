// The light a rough surface reflects from the world's probes along its mirror way, which the model
// pass reads where global illumination is on (probesReflected in lights.slang), from each probe's
// light by way that gi_ambient.slang gathers and its faces.
import gi;
import scenefield;

// The texels along each side of a probe's block of its light by way, eight by eight directions with
// a texel's border about them, each border texel the one across the octahedron's edge from it, as
// DDGI borders its probes, so a filtered read across a fold of the octahedron blends the directions
// on either side of it.
public static const int WaysTexels = 10;

// How much of a probe at probe a surface at p facing n sees, as probeSees gives it, its reach read
// filtered between the four directions about the way to p within the probe's block, so the share
// changes smoothly from one direction to the next, where read at the nearest the reflection of a
// Cornell box's wall showed the octahedron's facets.
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
// finest cascade around p that holds it, or -1 where no cascade of probes holds p (reflectedIn).
// The finest cascade is read alone, as the sky's share is (skyAt), since faded to the next across
// its outer band as bouncedAt fades, the reads doubled there took the scene pass in Sponza's nave at
// 1920 by 1080 1.5 to 2.0 ms more than before, where read so it takes what it did before within the
// noise of the GPU's other work (./e3d command profile, gpu.hdr_scene).
public float3 reflectedAt<V : IVolume>(V cubes, V ways, V reach, SceneFieldInfo info, float3 p, float3 n, float3 r, float roughness, float probes,
    float spacing, float cascades)
{
    int c = fieldCascade(info, p, 1.0, int(cascades));
    if (c < 0 || c >= int(cascades))
        return float3(-1.0);
    return reflectedIn(cubes, ways, reach, info, p, n, r, roughness, probes, spacing, c, int(cascades));
}

// The light along r the eight probes of cascade c around p hold, each weighed as bouncedIn weighs
// it for a surface facing n. A probe's light along r is read from its eight by eight directions,
// blended between the four whose middles lie about r, so it spans the 25 degrees or so a texel
// does, as a surface of roughness 0.5 spreads its reflection, and from its faces r looks toward,
// the light reaching a surface facing r, which spreads it as widely as a fully rough surface does,
// the two mixed by the roughness from 0.5 to 1. The directions are sampled within each probe's
// block, its border holding the directions across each fold, and at its slice's middle, so no probe
// lends its neighbor's light, in one filtered read. Clamped to the block's own texels, a way level
// with the eye in the octahedron's folded half read the texels on either side of the edge far apart,
// and the Cornell box's walls showed a seam at the eye's height and shards along the folds.
float3 reflectedIn<V : IVolume>(V cubes, V ways, V reach, SceneFieldInfo info, float3 p, float3 n, float3 r, float roughness, float probes, float spacing,
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
    float weights = 0.0;
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
        weight *= lerp(0.05, 1.0, probeSeesBlended(reach, p, n, probe, at, c, probes, oc.w * spacing, cascades));
        float3 uvw = float3((float2(at.xy * WaysTexels) + along) / across, (float(c * int(probes) + at.z) + 0.5) / deep);
        sum += lerp(ways.sampleAt(uvw, 0.0).rgb, faces / Pi, wide) * weight;
        weights += weight;
    }
    return weights > 1e-5 ? sum / weights : float3(0.0);
}
