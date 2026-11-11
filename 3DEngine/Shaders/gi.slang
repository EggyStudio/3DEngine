// What the passes of global illumination share, which are directions laid out on an octahedron,
// rays traced through the scene's distance field, and the light reaching a surface the field holds,
// the sun's where the field lets the sun through and the lamps', each that casts shadows where the
// field lets it through too, which a surface sends back along a ray with the indirect light that
// reached it the frame before (shadeHit in probelight.slang).
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

// A texel's mark as gi_trace.slang writes it for `rays` rays, `nothing` of which met nothing, a
// part of one for a ray that crossed surfaces letting light through, is what most met, 0 a surface,
// a half one before their interval began, a quarter the sky and 1 nothing, moved a sixteenth on for
// each that met nothing, up from 0 or a half and down from 1 for each that met something, so the
// mark stays within the band each reader tells what a texel's rays met by, gi.probe and the
// merge's marks among them; texelNothing reads back how many met nothing.
public float texelMark(float most, float nothing, uint rays)
{
    return most >= 0.75 ? 1.0 - 0.0625 * (float(rays) - nothing) : most + 0.0625 * nothing;
}

public float texelNothing(float mark, uint rays)
{
    if (mark >= 0.75)
        return float(rays) - (1.0 - mark) / 0.0625;
    if (mark >= 0.375)
        return (mark - 0.5) / 0.0625;
    return mark >= 0.1875 ? 0.0 : mark / 0.0625;
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
// Illumination.MostCascades.
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
    // before is added. w: 1 where a probe's ray's hit takes every glow light by its own form, none
    // as one point (gi.toggle points).
    public float4 counts;
    public GiLight lights[16];
    // xyz: the corner each cascade of probes lay at the frame before, where the faces the probes'
    // rays read were gathered, its own corner where it lay nowhere. w: unused.
    public float4 before[MostProbeCascades];
    // x: how many of glows hold a light. yzw: the least corner of the box every glow light's reach
    // lies in.
    public float4 glowCounts;
    // xyz: the most corner of that box. w: unused.
    public float4 glowHigh;
    public GlowLight glows[MostGlowLights];
    // The sun's shadow map as the window's shadow pass drew it the frame before, which a probe's
    // hit reads the sun through where one of its counted cascades holds the hit (SunShadowMap),
    // each cascade's view-projection, a quarter of the map each.
    public float4x4 sunCascades[4];
    // Each of those cascades' texel, in world units.
    public float4 sunTexels;
    // x: how many of sunCascades hold a cascade, 0 where the map holds none. y: a texel of the map,
    // in uv. zw: unused.
    public float4 sunMap;
};

// How much of the sun reaches a surface, from 0 to 1, or -1 where it is to be found through the
// scene's distance field.
public interface ISunShadow
{
    float reached(GiLights lights, float3 p, float3 n, float cell);
}

// The sun found through the field alone.
public struct FieldSun : ISunShadow
{
    public float reached(GiLights lights, float3 p, float3 n, float cell)
    {
        return -1.0;
    }
}

// The sun found through the window's shadow map the frame before where one of its counted cascades
// holds p, its cascade found as the model pass finds it (cascadeReached in lights.slang), the point
// moved off its surface along n by a texel and a half of that cascade and toward the sun by a cell
// of the field, since the field's surface lies within half a cell of the mesh's, and nine depths
// around it compared and averaged. The map is drawn from the meshes themselves, where the field
// holds them at its cells, and through the field alone the probes' hits in the vestibule of
// Sponza's main door see none of the sun a path-traced reference sees there. Moved toward the sun,
// the point is read at the same place in the map and only nearer the sun, where moved a cell and a
// half along n a floor's hit in cells of half a unit stands 0.75 over the floor, which under a sun
// 20 degrees up shortens every shadow a low wall casts on it by 2.1 units. Moved by the texel
// alone, the hits a little behind their surface shadow themselves.
public struct SunShadowMap : ISunShadow
{
    public Sampler2D map;

    public float reached(GiLights lights, float3 p, float3 n, float cell)
    {
        int count = int(lights.sunMap.x);
        float texel = lights.sunMap.y;
        for (int c = 0; c < count; c++)
        {
            float4 clip = mul(lights.sunCascades[c], float4(p + n * (lights.sunTexels[c] * 1.5) + lights.sunDirection.xyz * cell, 1.0));
            float3 q = clip.xyz / clip.w;
            if (any(abs(q.xy) > 0.995) || q.z > 1.0 || q.z < 0.0)
                continue;
            float2 uv = (q.xy * 0.5 + 0.5 + float2(c % 2, c / 2)) * 0.5;
            float shown = 0.0;
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    shown += q.z - 0.0005 <= map.SampleLevel(uv + float2(x, y) * texel, 0.0).r ? 1.0 : 0.0;
            return shown / 9.0;
        }
        return -1.0;
    }
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
// would blend the white in, and a cell's distance to the hit's plane does not tell those cells
// apart, as they lie as far from both.
public float3 surfaceAlbedoFacing<V : IVolume>(Texture3D<uint> faces, V albedo, SceneFieldInfo info, float3 p, float3 n)
{
    float2 gloss;
    return surfaceFacing(faces, albedo, info, p, n, gloss);
}

// The same, with in gloss the roughness and metallic share of the same cells blended the same way
// (faceGloss), or a material's default where no cell's face looks the way n does.
public float3 surfaceFacing<V : IVolume>(Texture3D<uint> faces, V albedo, SceneFieldInfo info, float3 p, float3 n, out float2 gloss)
{
    gloss = float2(0.5, 0.0);
    int c = fieldCascade(info, p, 1.0);
    if (c < 0)
        return float3(0.5);
    float4 oc = info.cascades[c].originAndCell;
    int size = int(info.counts.y);
    float3 q = clamp((p - oc.xyz) / oc.w, 0.5, float(size) - 0.5) - 0.5;
    int3 base = int3(floor(q));
    float3 f = q - float3(base);
    float4 sum = float4(0.0);
    float2 glossSum = float2(0.0);
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 at = min(base + offset, int3(size - 1));
        int3 texel = int3(at.xy, at.z + c * size);
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        uint bits = faces.Load(int4(texel, 0));
        float facing = dot(faceWay(bits), n) >= 0.5 ? 1.0 : 0.0;
        float4 paint = albedo.loadAt(texel) * (lerps.x * lerps.y * lerps.z * facing);
        sum += paint;
        glossSum += faceGloss(bits) * paint.a;
    }
    if (sum.a <= 0.01)
        return surfaceAlbedo(albedo, info, p);
    gloss = glossSum / sum.a;
    return sum.rgb / sum.a;
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
    return directLight(field, info, lights, p, n, FieldSun());
}

// The same with the sun found through sun where it holds p, and through the field elsewhere.
public float3 directLight<V : IVolume, Sun : ISunShadow>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n, Sun sun)
{
    float3 arrived = float3(0.0);
    if (lights.sunDirection.w > 0.5)
    {
        float3 l = lights.sunDirection.xyz;
        float facing = dot(n, l);
        if (facing > 0.0)
        {
            float cell = fieldCell(info, p);
            float mapped = sun.reached(lights, p, n, cell);
            if (mapped >= 0.0)
                arrived += lights.sunColor.rgb * (facing * mapped);
            else if (traceField(field, info, p + n * (cell * 1.5), l, cell, 1e5) < 0.0)
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

// The sixteen places in a texel a moving ray leaves from, in the order the frames take them, each
// four in a row spread over the texel, as the glow lights' points are
// (Illumination.JitterAt), which a carried probe's rays (gi_trace.slang) and the
// screen's probes' (gi_screen.slang) take.
public static const uint TracePlaces[16] = { 0, 10, 2, 8, 5, 15, 7, 13, 1, 11, 3, 9, 4, 14, 6, 12 };

// How much a probe's ray standing for its whole texel widens GGX's alpha squared, the tangent
// squared of the half-angle of the cone a texel of an n by n octahedron spans on the mean, 4 pi over
// n squared, 0.0657 at 8 by 8 and 0.0158 at 16 by 16, so a smooth face it meets reflects a light by
// the lobe's mean over its texel and not by its peak.
public float texelWidening(uint n)
{
    float c = 1.0 - 2.0 / float(n * n);
    return (1.0 - c * c) / (c * c);
}

// GGX's reflection of a light along l toward v off a face of roughness and f0 facing n, its alpha
// squared widened by widen, times pi as the lights are given, as the reference's
// specularReflected, and in fresnel its Fresnel.
public float3 ggxReflected(float3 n, float3 v, float3 l, float3 f0, float roughness, float widen, out float3 fresnel)
{
    float alpha = max(roughness, 0.045) * max(roughness, 0.045);
    float3 h = normalize(v + l);
    float nl = max(dot(n, l), 1e-4);
    float nv = max(dot(n, v), 1e-4);
    float nh = max(dot(n, h), 0.0);
    float a2 = alpha * alpha + widen;
    float d = nh * nh * (a2 - 1.0) + 1.0;
    float distribution = a2 / (Pi * d * d);
    float visibility = 0.5 / (nl * sqrt(nv * nv * (1.0 - a2) + a2) + nv * sqrt(nl * nl * (1.0 - a2) + a2));
    fresnel = f0 + (1.0 - f0) * pow(1.0 - max(dot(v, h), 0.0), 5.0);
    return distribution * visibility * fresnel * Pi;
}

// The light the sun and the lights send back from p on a face facing n toward v, each light's
// diffuse share past its Fresnel and its GGX reflection, as the reference lights a glossy face, the
// sun found as directLight finds it and the lamps unshadowed, falling off as there.
public float3 directReflected<V : IVolume, Sun : ISunShadow>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n, float3 v,
    float3 diffuse, float3 f0, float roughness, float widen, Sun sun)
{
    float3 sent = float3(0.0);
    float3 fresnel;
    if (lights.sunDirection.w > 0.5)
    {
        float3 l = lights.sunDirection.xyz;
        float facing = dot(n, l);
        if (facing > 0.0)
        {
            float cell = fieldCell(info, p);
            float mapped = sun.reached(lights, p, n, cell);
            float reaches = mapped >= 0.0 ? mapped : traceField(field, info, p + n * (cell * 1.5), l, cell, 1e5) < 0.0 ? 1.0 : 0.0;
            float3 specular = ggxReflected(n, v, l, f0, roughness, widen, fresnel);
            sent += lights.sunColor.rgb * (facing * reaches) * (diffuse * (1.0 - fresnel) + specular);
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
        float3 specular = ggxReflected(n, v, l, f0, roughness, widen, fresnel);
        sent += light.colorAndShadow.rgb * result * (diffuse * (1.0 - fresnel) + specular);
    }
    return sent;
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

// The directions each probe's reach is kept along, sixteen by sixteen of its octahedron, as
// gi_ambient.slang lays them out.
public static const int ReachTexels = 16;

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
// face, lends a surface on this side none of the light it sees there. Falling over half the
// spacing, a probe 0.55 beyond a wall 0.1 thick, whose rays reach the wall's far face, would read
// as seeing the screen's probes' hits on this side and lend them the light it sees.
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

// The light the lamps that cast shadows would send a surface at p facing n but for what the field
// holds between them, each found by a march toward the lamp from a cell and a half off the
// surface, looking from its first step and stopping two cells short of the lamp so its own
// fitting does not hide it. Kept two cells clear at its start as well, as seesThrough keeps both
// its ends, the march would begin a unit past its point in cells of half a unit and pass over a
// wall that near the surface, so a lamp outside a closed room of walls 0.3 thick would light the
// floor inside along the wall's foot (GlobalIlluminationTests). The falloff is directLight's,
// written again here, since that loop is kept in the shape lavapipe draws the model pass in.
public float3 hiddenLampLight<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n)
{
    float3 hidden = float3(0.0);
    float cell = fieldCell(info, p);
    float3 from = p + n * (cell * 1.5);
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
        float3 along = light.positionAndKind.xyz - from;
        float apart = length(along);
        if (result > 0.0 && apart > cell * 2.0 && traceField(field, info, from, along / apart, 0.0, apart - cell * 2.0) >= 0.0)
            hidden += light.colorAndShadow.rgb * result;
    }
    return hidden;
}

// The same as directReflected sends it back toward v, each hidden lamp's diffuse share past its
// Fresnel and its GGX reflection, so a lamp the field hides lights none of a glossy hit either.
public float3 hiddenLampReflected<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n, float3 v, float3 diffuse,
    float3 f0, float roughness, float widen)
{
    float3 hidden = float3(0.0);
    float cell = fieldCell(info, p);
    float3 from = p + n * (cell * 1.5);
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
        float3 along = light.positionAndKind.xyz - from;
        float apart = length(along);
        if (result > 0.0 && apart > cell * 2.0 && traceField(field, info, from, along / apart, 0.0, apart - cell * 2.0) >= 0.0)
        {
            float3 fresnel;
            float3 specular = ggxReflected(n, v, l, f0, roughness, widen, fresnel);
            hidden += light.colorAndShadow.rgb * result * (diffuse * (1.0 - fresnel) + specular);
        }
    }
    return hidden;
}

// The light the glow lights send a surface at p facing n, irradiance, each that reaches p by as
// much of it as p sees, by glowLightSeen's one march to its middle, as a probe's ray's hit reads
// them, or with halves by glowLightShown's marches to its faces' halves, as the screen's probes and
// a pixel no screen probe holds read them, since the one march, from a cell and a half off the
// surface, sees over a wall half as high as a lamp and lights the floor behind it whole. The
// marches are the cost, so the brightest at p, as many as `marches`, four at most, are kept with
// their light as the lights are read and only they are marched each. The fainter ones are gathered
// by their way from p into as many groups, a quarter of the surface's plane each, every group's
// light summed and shadowed by one march to its brightest, so a point lit by more lights than are
// marched takes them all for twice the marches at most. One way through for every count, the
// lights read and the kept ones marched in loops kept from unrolling, so a shader holds one march
// and one closed form of it, since a second way for few lights would stand in each of the model
// pass's two shadings of a reflection's hit and lengthen its compile for every app. A light gives
// nothing to its own surface, which a hit finds within half a cell of its box (glowLightOn). Where
// the field does not hold p, every light unhidden. This is the model pass's at a pixel, each light
// in closed form or by its pieces, which meet with no step on the floor.
public float3 glowLightsAt<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n, int marches, bool halves)
{
    return glowLightsFrom(field, info, lights, p, n, marches, halves, float3(0.0), false);
}

// The same at a probe's ray's hit, the ray along `way`, a light the ray passed through right before
// it met the field giving the hit none of its light (glowLightPassed). A hit within a cell of a
// light's box takes the light as its mean over a square a cell wide about the hit on its plane, at
// the middles of the square's four quarters, since a light thinner than a cell lights the wall it
// stands against within a few hundredths of a block of its box, a patch one probe's ray meets and
// the next probe's misses. Taken at a cell's distance from the box instead, the hit would lose the
// strip's light on its own wall, which is light a path-traced reference has. A hit's way of none,
// as a reflection's hit shaded by shadeHit gives, takes none of that. At a hit, a light farther
// than eight of its box's half diagonals takes each face it lights the hit from as one point
// (glowLightFaces), where the pieces are up to twelve, since every probe's ray takes every light
// at its hit. Its rough form is the model pass's scale for a share, not a value, so a hit does not
// take it.
public float3 glowLightsAt<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n, int marches, bool halves, float3 way)
{
    return glowLightsFrom(field, info, lights, p, n, marches, halves, way, true);
}

// The glow lights' light at p for glowLightsAt, the far ones by one point a face where `hit` says.
float3 glowLightsFrom<V : IVolume>(V field, SceneFieldInfo info, GiLights lights, float3 p, float3 n, int marches, bool halves, float3 way, bool hit)
{
    // A point past every light's reach takes none of their light, found before the loop over them,
    // which would otherwise run at each pixel of the far land and the clouds, whose surfaces are
    // blended and so missing from the window's depth the screen's texels are taken from.
    if (any(p < lights.glowCounts.yzw) || any(p > lights.glowHigh.xyz))
        return float3(0.0);
    int count = min(int(lights.glowCounts.x), MostGlowLights);
    float cell = fieldCell(info, p);
    float margin = max(cell, 0.0) * 0.5;
    float3 unhidden = float3(0.0);
    // The lights marched each in the first four places and the groups of the rest in the last
    // four, each with its light and its brightest light's.
    float3 kept[8] = { float3(0.0), float3(0.0), float3(0.0), float3(0.0), float3(0.0), float3(0.0), float3(0.0), float3(0.0) };
    float bright[8] = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
    int which[8] = { -1, -1, -1, -1, -1, -1, -1, -1 };
    int most = min(marches, 4);
    float3 tangent = normalize(abs(n.y) < 0.9 ? cross(n, float3(0.0, 1.0, 0.0)) : cross(n, float3(1.0, 0.0, 0.0)));
    float3 bitangent = cross(n, tangent);
    // At a hit, each light taken as one point (glowLightPoint), kept so where it gives under a 32nd
    // of the largest such point of the lights before it, and otherwise taken by its own form. A
    // hit in a voxel game's lit cave takes many lights, the brightest few of them most of its
    // light, so the many faint ones take one point where their faces were up to three. Measured
    // against the largest so far, a light kept as a point gives under a 32nd of the largest of all,
    // and a light read before the largest takes its own form at worst, a second pass to find the
    // largest first costing more than the points save.
    float largest = 0.0;
    [loop]
    for (int i = 0; i < count; i++)
    {
        GlowLight light = lights.glows[i];
        float faded = glowLightFade(light, p, n);
        if (faded <= 0.0 || (dot(way, way) > 0.0 && glowLightPassed(light, p, way, 2.0 * margin)))
            continue;
        float3 arrived;
        float3 point = hit ? glowLightPoint(light, p, n, margin) * faded : float3(0.0);
        float pointed = luminance(point);
        largest = max(largest, pointed);
        if (hit && pointed * 32.0 <= largest && lights.counts.w < 0.5)
            arrived = point;
        else if (dot(way, way) > 0.0 && cell > 0.0 && glowLightDistance(light, p) < cell)
        {
            if (glowLightOn(light, p, n, margin))
                continue;
            float3 along = normalize(abs(n.y) < 0.9 ? cross(n, float3(0.0, 1.0, 0.0)) : cross(n, float3(1.0, 0.0, 0.0)));
            float3 across = cross(n, along);
            float q = cell * 0.25;
            arrived = (glowLightIrradiance(light, p + (along + across) * q, n, margin) + glowLightIrradiance(light, p + (along - across) * q, n, margin)
                + glowLightIrradiance(light, p - (along + across) * q, n, margin) + glowLightIrradiance(light, p - (along - across) * q, n, margin)) * (0.25 * faded);
        }
        else if (hit && glowLightFar(light, p))
            arrived = glowLightFaces(light, p, n, margin) * faded;
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
        // The light no place keeps, into the group of its way from p across the surface's plane,
        // as many groups as places, which its brightest light stands for in the march.
        if (one >= 0 && b > 0.0)
        {
            float3 toward = lights.glows[clamp(one, 0, MostGlowLights - 1)].middleAndReach.xyz - p;
            int g = most >= 4 ? (dot(toward, tangent) > 0.0 ? 1 : 0) + (dot(toward, bitangent) > 0.0 ? 2 : 0)
                : most >= 2 ? (dot(toward, tangent) > 0.0 ? 1 : 0) : 0;
            kept[4 + g] += arrived;
            if (b > bright[4 + g])
            {
                bright[4 + g] = b;
                which[4 + g] = one;
            }
        }
    }
    if (cell <= 0.0)
        return unhidden;
    float3 sum = float3(0.0);
    [loop]
    for (int k = 0; k < 8; k++)
        if ((k & 3) < most && which[k] >= 0)
        {
            GlowLight light = lights.glows[clamp(which[k], 0, MostGlowLights - 1)];
            sum += kept[k] * (halves ? glowLightShown(field, info, light, p, n, cell) : glowLightSeen(field, info, light, p, n, cell));
        }
    return sum;
}
