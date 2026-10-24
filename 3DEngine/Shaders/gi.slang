// What the passes of global illumination share, which are directions laid out on an octahedron,
// rays traced through the scene's distance field, and the light a surface the field holds sends
// back along a ray, its color times the light that reaches it, the sun's where the field lets the
// sun through, the lamps', each that casts shadows where the field lets it through too, and the
// indirect light that reached it the frame before, with its emission.
module gi;

import scenefield;
__exported import glow;
__exported import cascades;
__exported import screenmarch;

public static const float Pi = 3.14159265358979;

// A unit direction as a point of the square from 0 to 1, the octahedron folded flat, which lays
// every direction on a square with texels of nearly even solid angle.
public float2 octahedron(float3 d)
{
    d /= abs(d.x) + abs(d.y) + abs(d.z);
    float2 p = d.xy;
    if (d.z < 0.0)
        p = (1.0 - abs(p.yx)) * float2(p.x >= 0.0 ? 1.0 : -1.0, p.y >= 0.0 ? 1.0 : -1.0);
    return p * 0.5 + 0.5;
}

// The point of the octahedron a point of the square stands for, not yet a direction.
public float3 octahedronPoint(float2 uv)
{
    float2 p = uv * 2.0 - 1.0;
    float3 d = float3(p, 1.0 - abs(p.x) - abs(p.y));
    if (d.z < 0.0)
        d.xy = (1.0 - abs(d.yx)) * float2(d.x >= 0.0 ? 1.0 : -1.0, d.y >= 0.0 ? 1.0 : -1.0);
    return d;
}

// The direction a point of the square stands for.
public float3 fromOctahedron(float2 uv)
{
    return normalize(octahedronPoint(uv));
}

// How much of the sphere a texel of the square spans at uv, against the others, one over the cube
// of its octahedron point's distance from the middle, as a patch of the octahedron there covers its
// area over that cube. The texels near the octahedron's corners span the least, those at the
// middles of its faces the most, near three times as much.
public float octahedronShare(float2 uv)
{
    float l = length(octahedronPoint(uv));
    return 1.0 / (l * l * l);
}

// The rays a texel of a cascade's octahedron of `texels` along each side sends, four where it has
// 8, one from the middle of each quarter of the texel, and one from its middle where it has 16, so
// every probe sends 256 (gi_trace.slang). The count follows the texels and not the cascade, since a
// texel of 8 a side spans a sixty-fourth of the sphere, which one ray takes a bright spot or misses
// it for, whichever cascade it is, as Low's and Medium's second is (gi_trace.slang).
public uint RaysAt(uint texels)
{
    return texels <= 8u ? 4u : 1u;
}

// A surface's normal from the cross of its ways along two axes of the window's depth, or facing
// the eye along `toEye` where the cross has no length or none a float holds. Far from the world's
// origin, a far texel's neighbor read back from the depth falls on the same point, and the normal
// made from it was a value that is not a number, which the glow lights' light at the screen's
// probes carried and their history spread over the land.
public float3 surfaceNormal(float3 crossed, float3 toEye)
{
    float length2 = dot(crossed, crossed);
    return length2 > 1e-20 && length2 < 1e30 ? crossed * rsqrt(length2) : normalize(toEye);
}

// A texel's mark as gi_trace.slang writes it for `rays` rays, `nothing` of which met nothing, is
// what most of them met, 0 a surface, a half a surface before their interval began, a quarter the
// sky and 1 nothing, moved a sixteenth on for each that met nothing, up from 0 or a half and down
// from 1 for each that met something, so the mark stays within the band each reader tells what a
// texel's rays met by, gi.probe and the merge's marks among them; texelNothing reads back how many
// met nothing.
public float texelMark(float most, uint nothing, uint rays)
{
    return most >= 0.75 ? 1.0 - 0.0625 * float(rays - nothing) : most + 0.0625 * float(nothing);
}

public uint texelNothing(float mark, uint rays)
{
    if (mark >= 0.75)
        return rays - uint((1.0 - mark) / 0.0625 + 0.5);
    if (mark >= 0.375)
        return uint((mark - 0.5) / 0.0625 + 0.5);
    return mark >= 0.1875 ? 0u : uint(mark / 0.0625 + 0.5);
}

// The lights the indirect light is lit by, as the renderer writes them each frame, each laid out
// as the model pass's light is.
public struct GiLight
{
    // xyz: where it is. w: 1 for a point light, 2 for a spot light.
    public float4 positionAndKind;
    // xyz: the way a spot light points. w: how far it reaches, 0 for no limit.
    public float4 directionAndRange;
    // rgb: its color times its intensity, linear.
    public float4 colorAndShadow;
    // x: the cosine of a spot light's inner cone. y: of its outer one.
    public float4 cone;
};

// The most cascades of probes, the four High traces and the far cascade's, as
// GlobalIlluminationRenderer.MostCascades.
public static const int MostProbeCascades = 5;

public struct GiLights
{
    // xyz: the way toward the sun. w: 1 where there is a sun.
    public float4 sunDirection;
    // rgb: the sun's color times its intensity, linear.
    public float4 sunColor;
    // rgb: the light from all around where a ray reaches nothing, linear, or with w 1 what the
    // environment map is multiplied by, its intensity, where the map stands in for it.
    public float4 sky;
    // x: how many lights. y: the frame's number. z: how strongly the indirect light of the frame
    // before is added. w: unused.
    public float4 counts;
    public GiLight lights[16];
    // xyz: the corner each cascade of probes lay at the frame before, where the faces the probes'
    // rays read were gathered, its own corner where it lay nowhere. w: unused.
    public float4 before[MostProbeCascades];
    // x: how many of glows hold a light.
    public float4 glowCounts;
    public GlowLight glows[MostGlowLights];
};

// Where a ray from origin along way first comes within a tenth of a cell of a surface between
// tMin and tMax, through the finest cascade that holds each point along it, or -1 where it reaches
// tMax first or leaves every cascade.
public float traceField<V : IVolume>(V field, SceneFieldInfo info, float3 origin, float3 way, float tMin, float tMax)
{
    float t = tMin;
    for (int i = 0; i < 64 && t < tMax; i++)
    {
        float3 p = origin + way * t;
        int c = fieldCascade(info, p, 1.0);
        if (c < 0)
            return -1.0;
        float cell = info.cascades[c].originAndCell.w;
        float d = fieldDistanceIn(field, info, c, p);
        if (d < cell * 0.1)
            return t;
        t += max(d, cell * 0.25);
    }
    return -1.0;
}

// The same, stopping where the ray leaves cascade `most` and every finer one, as -1, since a wall
// thinner than a coarser cascade's cell may let it through.
public float traceFieldWithin<V : IVolume>(V field, SceneFieldInfo info, float3 origin, float3 way, float tMin, float tMax, int most)
{
    float t = tMin;
    for (int i = 0; i < 64 && t < tMax; i++)
    {
        float3 p = origin + way * t;
        int c = fieldCascade(info, p, 1.0);
        if (c < 0 || c > most)
            return -1.0;
        float cell = info.cascades[c].originAndCell.w;
        float d = fieldDistanceIn(field, info, c, p);
        if (d < cell * 0.1)
            return t;
        t += max(d, cell * 0.25);
    }
    return -1.0;
}

// A color of the field at p, the cells' colors blended and divided by how much of them a surface
// painted, so a cell no surface painted lends nothing, or gray where none around p is painted.
public float3 surfaceAlbedo<V : IVolume>(V albedo, SceneFieldInfo info, float3 p)
{
    int c = fieldCascade(info, p, 1.0);
    if (c < 0)
        return float3(0.5);
    float4 oc = info.cascades[c].originAndCell;
    float size = info.counts.y;
    float3 cell = clamp((p - oc.xyz) / oc.w, 0.5, size - 0.5);
    float3 uvw = float3(cell.xy / size, (cell.z + float(c) * size) / (size * info.counts.x));
    float4 painted = albedo.sampleAt(uvw, 0);
    return painted.a > 0.05 ? painted.rgb / painted.a : float3(0.5);
}

// A color of the field at p on a surface facing n, the eight cells around p blended by hand, each
// kept only where the face whose color it holds looks the hit's way, its way within 60 degrees of
// n, a cosine of a half or more, or surfaceAlbedo's blend where none of them does, as at a thin
// edge, so the hit is not left dark. A cell on a corner's diagonal lies as far from either wall
// and holds whichever painted it, so a red wall's hit within a cell of the white wall it meets
// blended the white in, its green 0.15 where the wall's is 0.012, the light it sends on 0.56 where
// a path-traced reference reads 0.25 (gi.screenprobe); a cell's distance to the hit's plane does
// not tell those cells apart, as they lie as far from both.
public float3 surfaceAlbedoFacing<V : IVolume>(Texture3D<uint> faces, V albedo, SceneFieldInfo info, float3 p, float3 n)
{
    int c = fieldCascade(info, p, 1.0);
    if (c < 0)
        return float3(0.5);
    float4 oc = info.cascades[c].originAndCell;
    int size = int(info.counts.y);
    float3 q = clamp((p - oc.xyz) / oc.w, 0.5, float(size) - 0.5) - 0.5;
    int3 base = int3(floor(q));
    float3 f = q - float3(base);
    float4 sum = float4(0.0);
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 at = min(base + offset, int3(size - 1));
        int3 texel = int3(at.xy, at.z + c * size);
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float facing = dot(faceWay(faces.Load(int4(texel, 0))), n) >= 0.5 ? 1.0 : 0.0;
        sum += albedo.loadAt(texel) * (lerps.x * lerps.y * lerps.z * facing);
    }
    return sum.a > 0.01 ? sum.rgb / sum.a : surfaceAlbedo(albedo, info, p);
}

// The emission of the field's surfaces at p, blended as their colors are.
public float3 surfaceGlow<V : IVolume>(V glow, V albedo, SceneFieldInfo info, float3 p)
{
    int c = fieldCascade(info, p, 1.0);
    if (c < 0)
        return float3(0.0);
    float4 oc = info.cascades[c].originAndCell;
    float size = info.counts.y;
    float3 cell = clamp((p - oc.xyz) / oc.w, 0.5, size - 0.5);
    float3 uvw = float3(cell.xy / size, (cell.z + float(c) * size) / (size * info.counts.x));
    float coverage = albedo.sampleAt(uvw, 0).a;
    return coverage > 0.05 ? glow.sampleAt(uvw, 0).rgb / coverage : float3(0.0);
}

// The emission of the field's surfaces at p as surfaceGlow blends it, less that of the surfaces
// global illumination carries as glow lights, whose cells say so in the glow's alpha, so a probe's
// ray that meets one does not take its light a second time. Near such a cell the eight cells around
// p are blended by hand, each carried one's light left out, since the blend of them all mixes the
// two.
public float3 surfaceGlowUncarried<V : IVolume>(V glow, V albedo, SceneFieldInfo info, float3 p)
{
    int c = fieldCascade(info, p, 1.0);
    if (c < 0)
        return float3(0.0);
    float4 oc = info.cascades[c].originAndCell;
    float size = info.counts.y;
    float3 cell = clamp((p - oc.xyz) / oc.w, 0.5, size - 0.5);
    float3 uvw = float3(cell.xy / size, (cell.z + float(c) * size) / (size * info.counts.x));
    float coverage = albedo.sampleAt(uvw, 0).a;
    if (coverage <= 0.05)
        return float3(0.0);
    float4 light = glow.sampleAt(uvw, 0);
    if (light.a <= 0.0)
        return light.rgb / coverage;
    float3 q = cell - 0.5;
    int3 base = int3(floor(q));
    float3 f = q - float3(base);
    float3 sum = float3(0.0);
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 at = min(base + offset, int3(int(size) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float4 one = glow.loadAt(int3(at.xy, at.z + c * int(size)));
        sum += (one.a > 0.5 ? float3(0.0) : one.rgb) * (lerps.x * lerps.y * lerps.z);
    }
    return sum / coverage;
}

// The light that reaches a surface at p facing n from the sun, where the field lets it through,
// and from each point and spot light, unshadowed, falling off as the model pass has it.
public float3 directLight<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n)
{
    float3 arrived = float3(0.0);
    if (lights.sunDirection.w > 0.5)
    {
        float3 l = lights.sunDirection.xyz;
        float facing = dot(n, l);
        if (facing > 0.0)
        {
            float cell = fieldCell(info, p);
            if (traceField(field, info, p + n * (cell * 1.5), l, cell, 1e5) < 0.0)
                arrived += lights.sunColor.rgb * facing;
        }
    }
    for (int i = 0; i < min(int(lights.counts.x), 16); i++)
    {
        GiLight light = lights.lights[i];
        float3 toLight = light.positionAndKind.xyz - p;
        float distance2 = max(dot(toLight, toLight), 1e-4);
        float3 l = toLight * rsqrt(distance2);
        float result = max(dot(n, l), 0.0) / distance2;
        float range = light.directionAndRange.w;
        if (range > 0.0)
        {
            float edge = saturate(1.0 - pow(distance2 / (range * range), 2.0));
            result *= edge * edge;
        }
        if (int(light.positionAndKind.w) == 2)
            result *= smoothstep(light.cone.y, max(light.cone.x, light.cone.y + 1e-4), dot(-l, light.directionAndRange.xyz));
        arrived += light.colorAndShadow.rgb * result;
    }
    return arrived;
}

// How much a probe at probe counts toward the light at a surface at p facing n, besides how near
// it is, more the more squarely it stands in front of the surface, as DDGI weighs its probes.
public float probeFacing(float3 p, float3 n, float3 probe)
{
    float3 toward = probe - p;
    float length2 = dot(toward, toward);
    if (length2 <= 1e-8)
        return 1.0;
    float cosine = dot(toward * rsqrt(length2), n);
    float facing = (cosine + 1.0) * 0.5;
    // A probe behind the surface's plane stands on its other side, under a floor or beyond a wall
    // thinner than the probes are apart, and sees light that never reaches this side, so it lends
    // next to nothing, only enough to light a surface no probe in front of it reaches.
    return lerp(0.02 * facing * facing, facing * facing + 0.2, smoothstep(-0.15, 0.15, cosine));
}

// Whether nothing the field holds stands between a and b, clearance kept clear at either end for
// the surfaces a and b lie near.
public bool seesThrough<V : IVolume>(V field, SceneFieldInfo info, float3 a, float3 b, float clearance)
{
    float3 toward = b - a;
    float length = length(toward);
    return length <= clearance * 2.0 || traceField(field, info, a, toward / length, clearance, length - clearance) < 0.0;
}

// The directions each probe's reach is kept along, eight by eight of its octahedron, as
// gi_ambient.slang lays them out.
public static const int ReachTexels = 8;

// How near a surface a probe may stand, and the most it is moved from its spot in the grid to stand
// so far off one or out of a mesh, in its spacing, so a probe in the plane of a wall or a ceiling
// stands in the room before it rather than inside it, as DDGI moves its probes. A probe moved stays
// within half the spacing of its spot, so the probes keep their order.
public static const float Clearance = 0.1;
public static const float MostMoved = 0.45;

// What a probe's faces carry in alpha, read as a face of each axis, whether the probe holds light,
// 0 where it stands inside a mesh, and where it does, two more than its move along the axis in its
// spacing, which gi_ambient.slang writes from the place gi_trace.slang moved it to.
public float faceHolds(float alpha)
{
    return alpha > 0.5 ? 1.0 : 0.0;
}

public float faceMove(float alpha)
{
    return alpha > 0.5 ? alpha - 2.0 : 0.0;
}

// How much of the frame before's own light a probe's may keep before the indirect light is
// taken at the share it kept, a fall by a fifth or more, which a light switched off or carried
// away gives and the field's cells settling after a camera moves do not, and a rise by a quarter
// or more, its inverse, so a light that flickers moves its indirect light down and up alike.
public static const float Falls = 0.8;

// The most a probe's indirect light is raised by in a frame its own light rose by much, so a
// probe its own light barely lit before gains no more than four times its bounce at once.
public static const float Rises = 4.0;

// The light a color gives the eye, by Rec. 709's weights.
public float luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// The share of the indirect light a probe takes the frame after its own light went from
// before to now, the same share as its own light kept where that fell or rose by much, and the
// whole of it otherwise.
public float3 followed(float3 now, float3 before)
{
    float3 fell = now / max(before, 1e-6);
    return select(fell < Falls || fell > 1.0 / Falls, clamp(fell, 0.0, Rises), float3(1.0));
}

// How much of the light a probe's rays bring back must come straight from the sun, the lights and
// emissive surfaces for the probe to be judged by its own light, a quarter, where a probe lit
// mostly by indirect light, as one down a corridor from a lit room, is judged by the own light
// of the probes around it summed, since its own light is too little of its light to say what went
// out.
public static const float OwnShare = 0.25;

// How much the probe of cascade c at `at`, standing at probe, sees of a surface at p facing n, 1
// where p, taken three tenths of the probes' spacing off the surface along n, lies nearer the probe
// than its rays reached that way the frame before, falling to 0 over an eighth of the spacing past
// it, a cell of the probes' cascade, as DDGI weighs its probes by the distances they traced. Taken
// off the surface, the point comes nearer a probe on its own side and goes further from one past
// it, so a probe under a floor or beyond a wall, whose rays stop at the floor's or the wall's far
// face, lends a surface on this side none of the light it sees there. Over half the spacing, a
// probe 0.55 beyond a wall 0.1 thick, whose rays reached the wall's far face, read as seeing the
// screen's probes' hits on this side and lent them the light it sees. The thin room's right wall,
// its lamp outside, read 1.153 of a path-traced reference on its inner face and 1.072 with that
// lamp off, the reference reading the wall alike either way, and over an eighth it reads 1.098.
// Through walls 0.3 thick the corridor's walls took the light of sunlit probes outside, which
// covered part of their own reading under, 67 to 70% under the reference with it and 78 to 79%
// without (gi.compare at 800 by 450, gi.screenprobe).
public float probeSees<V : IVolume>(V reach, float3 p, float3 n, float3 probe, int3 at, int c, float probes, float apart)
{
    float3 toward = p + n * (0.3 * apart) - probe;
    float d = length(toward);
    if (d < 1e-4)
        return 1.0;
    int2 texel = min(int2(octahedron(toward / d) * float(ReachTexels)), int2(ReachTexels - 1));
    float reached = reach.loadAt(int3(at.xy * ReachTexels + texel, c * int(probes) + at.z)).r;
    float slack = 0.125 * apart;
    return saturate((reached + slack - d) / slack);
}

// The indirect light reaching a surface at p facing n, irradiance, from the finest cascade of
// probes that holds it, fading to the next cascade's across its outer band (cascadeHold) where that
// one is built, or none where no cascade of probes holds p. The two cascades are read in a loop
// kept from unrolling, so a shader holds one read of a cascade. The probes are found where the
// field's cascades they stand in lie (probesOf).
public float3 bouncedAt<V : IVolume>(V cubes, V reach, SceneFieldInfo field, float3 p, float3 n, float probes, float spacing, float cascades)
{
    SceneFieldInfo info = probesOf(field, int(cascades));
    int c = fieldCascade(info, p, 1.0);
    if (c < 0 || c >= int(cascades))
        return float3(0.0);
    float own = c + 1 < int(cascades) && cascadeAt(info, c + 1).w > 0.0 ? cascadeHold(info, c, p) : 1.0;
    float3 light = float3(0.0);
    [loop]
    for (int k = 0; k < 2; k++)
    {
        float share = k == 0 ? own : 1.0 - own;
        if (share > 0.0)
            light += bouncedIn(cubes, reach, info, p, n, probes, spacing, c + k) * share;
    }
    return light;
}

// The indirect light reaching a surface at p facing n, irradiance, from the faces of the eight
// probes of cascade c around it that the probes reach, each weighed by how near it is, how squarely
// it stands in front of the surface and how much of it it sees (probeSees), a probe inside a mesh
// passed over, the three faces n looks toward weighed by the square of n along each. A probe it does
// not see keeps a twentieth of its weight, as one behind its plane keeps a little, since cut to
// nothing a pixel left with one or two probes around it seen took their light alone and a Cornell
// box showed a notch beside its tall block, and a surface that sees none of them, as one wedged
// between two meshes may, blends them by that twentieth, so it is not left dark.
float3 bouncedIn<V : IVolume>(V cubes, V reach, SceneFieldInfo info, float3 p, float3 n, float probes, float spacing, int c)
{
    float4 oc = cascadeAt(info, c);
    float3 q = clamp((p - oc.xyz) / (oc.w * spacing) - 0.5, 0.0, probes - 1.0);
    int3 base = int3(floor(q));
    float3 f = q - float3(base);
    float3 n2 = n * n;
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 at = min(base + offset, int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float3 light = float3(0.0);
        float valid = 1.0;
        float3 moved = float3(0.0);
        for (int axis = 0; axis < 3; axis++)
        {
            int face = axis * 2 + (n[axis] < 0.0 ? 1 : 0);
            float4 texel = cubes.loadAt(int3(face * int(probes) + at.x, at.y, c * int(probes) + at.z));
            light += texel.rgb * n2[axis];
            valid = min(valid, faceHolds(texel.a));
            moved[axis] = faceMove(texel.a);
        }
        float3 probe = oc.xyz + (float3(at) + 0.5 + moved) * (oc.w * spacing);
        float weight = lerps.x * lerps.y * lerps.z * valid * probeFacing(p, n, probe);
        weight *= lerp(0.05, 1.0, probeSees(reach, p, n, probe, at, c, probes, oc.w * spacing));
        sum += light * weight;
        weights += weight;
    }
    return weights > 1e-5 ? sum / weights : float3(0.0);
}

// The share of the sky a surface at p facing n sees along r, the share its probes' rays reached
// the sky by (gi_ambient.slang), blended between the eight probes around it as bouncedAt blends
// their light, the three faces r looks toward weighed by the square of r along each, or the whole
// sky where no cascade of probes holds p.
public float skyAt<V : IVolume>(V cubes, V skies, V reach, SceneFieldInfo field, float3 p, float3 n, float3 r, float probes, float spacing, float cascades)
{
    SceneFieldInfo info = probesOf(field, int(cascades));
    int c = fieldCascade(info, p, 1.0);
    if (c < 0 || c >= int(cascades))
        return 1.0;
    float4 oc = cascadeAt(info, c);
    float3 q = clamp((p - oc.xyz) / (oc.w * spacing) - 0.5, 0.0, probes - 1.0);
    int3 base = int3(floor(q));
    float3 f = q - float3(base);
    float3 r2 = r * r;
    float sum = 0.0;
    float weights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 at = min(base + offset, int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float sky = 0.0;
        float valid = 1.0;
        float3 moved = float3(0.0);
        for (int axis = 0; axis < 3; axis++)
        {
            int face = axis * 2 + (r[axis] < 0.0 ? 1 : 0);
            int3 texel = int3(face * int(probes) + at.x, at.y, c * int(probes) + at.z);
            float alpha = cubes.loadAt(texel).a;
            sky += skies.loadAt(texel).x * r2[axis];
            valid = min(valid, faceHolds(alpha));
            moved[axis] = faceMove(alpha);
        }
        float3 probe = oc.xyz + (float3(at) + 0.5 + moved) * (oc.w * spacing);
        float weight = lerps.x * lerps.y * lerps.z * valid * probeFacing(p, n, probe);
        weight *= lerp(0.05, 1.0, probeSees(reach, p, n, probe, at, c, probes, oc.w * spacing));
        sum += sky * weight;
        weights += weight;
    }
    return weights > 1e-5 ? sum / weights : 1.0;
}

// The indirect light reaching a surface at p facing n, as bouncedAt gives it, from those of the
// eight probes around it the surface sees, in front of its plane with nothing the field holds
// between, so a probe under a floor or beyond a wall lends the light it sees there to no surface on
// this side. A probe behind the plane is passed over with no march, since the surface itself stands
// between, where bouncedAt lends it a little, which the blend raised to all of it wherever every
// probe in front was hidden. The probes' passes take it, where the model pass's reflections keep
// bouncedAt. The faces are read where the probes stood as gathered says, the frame before's corners
// for the world's probes' rays, and the field marched as info has it.
public float3 bouncedSeenAt<V : IVolume>(V field, SceneFieldInfo info, SceneFieldInfo gatheredField, V cubes, float3 p, float3 n, float probes, float spacing,
    float cascades)
{
    SceneFieldInfo gathered = probesOf(gatheredField, int(cascades));
    int c = fieldCascade(gathered, p, 1.0);
    if (c < 0 || c >= int(cascades))
        return float3(0.0);
    float4 seen = bouncedSeenSum(field, info, gathered, cubes, p, n, probes, spacing, c);
    return seen.w > 1e-5 ? seen.rgb / seen.w : float3(0.0);
}

// The sum bouncedSeenAt divides for cascade c, the light of the probes seen times their weights,
// and the weights, which a reader that takes another light where no probe is seen, or blends two
// cascades, tells apart from dark. gathered places the probes as probesOf does.
public float4 bouncedSeenSum<V : IVolume>(V field, SceneFieldInfo info, SceneFieldInfo gathered, V cubes, float3 p, float3 n, float probes, float spacing,
    int c)
{
    float4 oc = cascadeAt(gathered, c);
    float3 q = clamp((p - oc.xyz) / (oc.w * spacing) - 0.5, 0.0, probes - 1.0);
    int3 base = int3(floor(q));
    float3 f = q - float3(base);
    float3 n2 = n * n;
    float3 from = p + n * oc.w;
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 at = min(base + offset, int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float3 light = float3(0.0);
        float valid = 1.0;
        float3 moved = float3(0.0);
        for (int axis = 0; axis < 3; axis++)
        {
            int face = axis * 2 + (n[axis] < 0.0 ? 1 : 0);
            float4 texel = cubes.loadAt(int3(face * int(probes) + at.x, at.y, c * int(probes) + at.z));
            light += texel.rgb * n2[axis];
            valid = min(valid, faceHolds(texel.a));
            moved[axis] = faceMove(texel.a);
        }
        float3 probe = oc.xyz + (float3(at) + 0.5 + moved) * (oc.w * spacing);
        if (valid < 0.5 || dot(probe - p, n) <= 0.0 || !seesThrough(field, info, from, probe, oc.w * 0.5))
            continue;
        float weight = lerps.x * lerps.y * lerps.z * probeFacing(p, n, probe);
        sum += light * weight;
        weights += weight;
    }
    return float4(sum, weights);
}

// The light the lamps that cast shadows would send a surface at p facing n but for what the field
// holds between them, each found by a march toward the lamp from a cell and a half off the
// surface, stopping two cells short of the lamp so its own fitting does not hide it. The falloff
// is directLight's, written again here, since that loop is kept in the shape lavapipe draws the
// model pass in.
public float3 hiddenLampLight<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n)
{
    float3 hidden = float3(0.0);
    float cell = fieldCell(info, p);
    for (int i = 0; i < min(int(lights.counts.x), 16); i++)
    {
        GiLight light = lights.lights[i];
        if (light.colorAndShadow.w < 0.5)
            continue;
        float3 toLight = light.positionAndKind.xyz - p;
        float distance2 = max(dot(toLight, toLight), 1e-4);
        float3 l = toLight * rsqrt(distance2);
        float result = max(dot(n, l), 0.0) / distance2;
        float range = light.directionAndRange.w;
        if (range > 0.0)
        {
            float edge = saturate(1.0 - pow(distance2 / (range * range), 2.0));
            result *= edge * edge;
        }
        if (int(light.positionAndKind.w) == 2)
            result *= smoothstep(light.cone.y, max(light.cone.x, light.cone.y + 1e-4), dot(-l, light.directionAndRange.xyz));
        if (result > 0.0 && !seesThrough(field, info, p + n * (cell * 1.5), light.positionAndKind.xyz, cell * 2.0))
            hidden += light.colorAndShadow.rgb * result;
    }
    return hidden;
}

// The light the glow lights send a surface at p facing n, irradiance, each that reaches p by as
// much of it as p sees, by glowLightSeen's one march to its middle, as a probe's ray's hit reads
// them, or with halves by glowLightShown's marches to its faces' halves, as the screen's probes and
// a pixel no screen probe holds read them, since the one march, from a cell and a half off the
// surface, sees over a wall half as high as a lamp and lit the floor behind it whole. The marches
// are the cost, so the brightest at p, as many as `marches`, four at most, are kept with their
// light as the lights are read and only they are marched, a fainter light giving nothing. One way
// through for every count, the lights read and the kept ones marched in loops kept from unrolling,
// so a shader holds one march and one closed form of it. A second way for few lights, and both in
// each of the model pass's two shadings of a reflection's hit, made its compile on lavapipe 0.68 s
// longer for each app. A light gives nothing to its own surface, which a hit finds within half a
// cell of its box (glowLightOn). Where the field does not hold p, every light unhidden.
public float3 glowLightsAt<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n, int marches, bool halves)
{
    return glowLightsAt(field, info, lights, p, n, marches, halves, float3(0.0));
}

// The same at a probe's ray's hit, the ray along `way`, a light the ray passed through right before
// it met the field giving the hit none of its light (glowLightPassed). A hit within a cell of a
// light's box takes the light as its mean over a square a cell wide about the hit on its plane, at
// the middles of the square's four quarters, since a light thinner than a cell lights the wall it
// stands against within a few hundredths of a block of its box, a patch one probe's ray meets and
// the next probe's misses. Taken at the hit alone, the strip's room of shaders_bounce_rooms read
// 0.233 and 0.213 of its light apart from its path-traced reference at Medium and High at 160 by
// 90, where with the mean 0.217 and 0.201; taken at a cell's distance from the box, 0.263 and
// 0.279, since the strip's light on its own wall is light the reference has.
public float3 glowLightsAt<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n, int marches, bool halves, float3 way)
{
    int count = min(int(lights.glowCounts.x), MostGlowLights);
    float cell = fieldCell(info, p);
    float margin = max(cell, 0.0) * 0.5;
    float3 unhidden = float3(0.0);
    float3 kept[4] = { float3(0.0), float3(0.0), float3(0.0), float3(0.0) };
    float bright[4] = { 0.0, 0.0, 0.0, 0.0 };
    int which[4] = { -1, -1, -1, -1 };
    int most = min(marches, 4);
    [loop]
    for (int i = 0; i < count; i++)
    {
        GlowLight light = lights.glows[i];
        float faded = glowLightFade(light, p, n);
        if (faded <= 0.0 || (dot(way, way) > 0.0 && glowLightPassed(light, p, way, 2.0 * margin)))
            continue;
        float3 arrived;
        if (dot(way, way) > 0.0 && cell > 0.0 && glowLightDistance(light, p) < cell)
        {
            if (glowLightOn(light, p, n, margin))
                continue;
            float3 along = normalize(abs(n.y) < 0.9 ? cross(n, float3(0.0, 1.0, 0.0)) : cross(n, float3(1.0, 0.0, 0.0)));
            float3 across = cross(n, along);
            float q = cell * 0.25;
            arrived = (glowLightIrradiance(light, p + (along + across) * q, n, margin) + glowLightIrradiance(light, p + (along - across) * q, n, margin)
                + glowLightIrradiance(light, p - (along + across) * q, n, margin) + glowLightIrradiance(light, p - (along - across) * q, n, margin)) * (0.25 * faded);
        }
        else
            arrived = glowLightIrradiance(light, p, n, margin) * faded;
        unhidden += arrived;
        float b = luminance(arrived);
        int one = i;
        [unroll]
        for (int k = 0; k < 4; k++)
            if (k < most && b > bright[k])
            {
                float3 movedLight = kept[k];
                float movedBright = bright[k];
                int movedOne = which[k];
                kept[k] = arrived;
                bright[k] = b;
                which[k] = one;
                arrived = movedLight;
                b = movedBright;
                one = movedOne;
            }
    }
    if (cell <= 0.0)
        return unhidden;
    float3 sum = float3(0.0);
    [loop]
    for (int k = 0; k < most; k++)
        if (which[k] >= 0)
        {
            GlowLight light = lights.glows[clamp(which[k], 0, MostGlowLights - 1)];
            sum += kept[k] * (halves ? glowLightShown(field, info, light, p, n, cell) : glowLightSeen(field, info, light, p, n, cell));
        }
    return sum;
}

// The light a surface the field holds at p sends back along a probe's ray that met it, as shadeHit
// gives it, with the lamps that cast shadows hidden where the field stands between, so a lamp's
// light is not reflected off the far side of a wall, and where seen is set the indirect light
// reaching it from the probes it sees alone (bouncedSeenAt). The world's probes set it, since every
// surface's light comes to them. The screen's probes, whose rays are many and short, do not,
// because marching to the probes at each of their hits cost 0.10 to 0.15 ms of global
// illumination's 0.3 to 0.4 on the development machine's GPU in shaders_cornell_box, and a closed
// room under a lamp held 3 levels of its light without it, where it held 247 before either. Weighed
// by the probes' reach instead, as bouncedAt does, the world's probes saved 0.03 to 0.05 ms and let
// 42.9 levels of a lamp under a closed room's floor into it where the marches hold it to 2.5. The
// model pass's reflections call shadeHit, which hides the lamps as this does and marches to no
// probe. The indirect light is taken at kept's share of it, from the faces where gathered says the
// probes stood, and own holds the light the surface sends on of what reaches it straight from the
// sun, the lights and the two brightest glow lights, with its emission but a glow light's, which a
// world probe weighs the frame before's light by (gi_ambient.slang).
public float3 shadeProbeHit<V : IVolume>(V field, SceneFieldInfo info, SceneFieldInfo gathered, V albedo, Texture3D<uint> faces, V glow, GiLights lights,
    V cubes, V reach, float3 p, float3 way, float probes, float spacing, float cascades, bool seen, float3 kept, out float3 own)
{
    float3 n = fieldNormal(field, info, p);
    float3 color = surfaceAlbedoFacing(faces, albedo, info, p, n);
    float3 bounced = seen ? bouncedSeenAt(field, info, gathered, cubes, p, n, probes, spacing, cascades)
                          : bouncedAt(cubes, reach, gathered, p, n, probes, spacing, cascades);
    own = color * (directLight(field, info, lights, p, n) - hiddenLampLight(field, info, lights, p, n) + glowLightsAt(field, info, lights, p, n, 2, false, way) / Pi)
        + surfaceGlowUncarried(glow, albedo, info, p);
    return own + color / Pi * bounced * (lights.counts.z * kept);
}

// The light a surface the field holds at p sends back along a ray that met it, its color over
// pi times the light that reaches it, the sun's and the lights', the lamps that cast shadows hidden
// where the field stands between as the probes have them, and, at the share given, the indirect
// light that reached it the frame before, with its emission. The sun and the lights are given as
// the model pass has them, the light a white surface facing one returns, which is the light
// reaching it over pi, so they are taken times pi beside the indirect light, which the probes hold
// as the light reaching a face; read as they are, a lit surface sent on a pi-th of its light,
// which left the probes' hits 48 to 75% under a path-traced reference. hiddenLampLight's loop after
// directLight's is the shape lavapipe draws, which the probes' passes already have, where the lamps'
// loop moved ahead of the sun's in directLight is not (build/mesa). Glowing false leaves the glow
// lights' marches out, the model pass's build for a frame with none passing it so (Glowing in
// lights); a parameter of the build, since a constant argument left them in the SPIR-V.
public float3 shadeHit<V : IVolume, let Glowing : bool>(V field, SceneFieldInfo info, V albedo, Texture3D<uint> faces, V glow, GiLights lights, V cubes,
    V reach, float3 p, float probes, float spacing, float cascades)
{
    float3 n = fieldNormal(field, info, p);
    float3 color = surfaceAlbedoFacing(faces, albedo, info, p, n);
    float3 arrived = (directLight(field, info, lights, p, n) - hiddenLampLight(field, info, lights, p, n)) * Pi
        + bouncedAt(cubes, reach, info, p, n, probes, spacing, cascades) * lights.counts.z;
    if (Glowing)
        arrived += glowLightsAt(field, info, lights, p, n, 2, false);
    return color / Pi * arrived + surfaceGlow(glow, albedo, info, p);
}

