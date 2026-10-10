// What the passes of light that bounces share: directions laid out on an octahedron, rays traced
// through the scene's distance field, and the light a surface the field holds sends back along a
// ray, its color times the light that reaches it, the sun's where the field lets the sun through,
// the lamps', each that casts shadows where the field lets it through too, and the light that
// bounced to it the frame before, with the light it gives off.
module gi;

import scenefield;

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

// How much of the sphere a texel of the square spans at uv, against the others: one over the cube
// of its octahedron point's distance from the middle, as a patch of the octahedron there covers its
// area over that cube. The texels near the octahedron's corners span the least, those at the
// middles of its faces the most, near three times as much.
public float octahedronShare(float2 uv)
{
    float l = length(octahedronPoint(uv));
    return 1.0 / (l * l * l);
}

// The lights the bounced light is lit by, as the renderer writes them each frame, each laid out
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

public struct GiLights
{
    // xyz: the way toward the sun. w: 1 where there is a sun.
    public float4 sunDirection;
    // rgb: the sun's color times its intensity, linear.
    public float4 sunColor;
    // rgb: the light from all around where a ray reaches nothing, linear, or with w 1 what the
    // environment map is multiplied by, its intensity, where the map stands in for it.
    public float4 sky;
    // x: how many lights. y: the frame's number. z: how strongly the light that bounced the frame
    // before is added. w: unused.
    public float4 counts;
    public GiLight lights[16];
    // xyz: the corner each cascade of probes lay at the frame before, where the faces the probes'
    // rays read were gathered, its own corner where it lay nowhere. w: unused.
    public float4 before[4];
};

// A 2D image a ray is traced through as a depth, as IVolume is for the field's images.
public interface IPicture
{
    public float4 sampleAt(float2 uv, float lod);
}

public extension Sampler2D : IPicture
{
    public float4 sampleAt(float2 uv, float lod) { return this.SampleLevel(uv, lod); }
}

// An image and the sampler it is read through, bound apart.
public struct SplitPicture : IPicture
{
    public Texture2D image;
    public SamplerState sampler;

    public __init(Texture2D image, SamplerState sampler)
    {
        this.image = image;
        this.sampler = sampler;
    }

    public float4 sampleAt(float2 uv, float lod) { return image.SampleLevel(sampler, uv, lod); }
}

// The view the screen's probes are placed through, which gi_screen.slang traces them through and
// gi_screen_filter.slang blends them through.
public struct ScreenView
{
    // World to clip space, and back, as the window's camera has it this frame.
    public float4x4 viewProjection;
    public float4x4 inverseViewProjection;
    // xyz: the eye.
    public float4 eye;
    // x: a tile's pixels along each side. y: the probes across. z: the probes down. w: 1 where the
    // frame before's light is left out, plus 2 where each probe keeps its own light, its neighbors'
    // left out.
    public float4 grid;
    // x: where the first interval ends. y: the world's probes along each side. z: their spacing in
    // cells. w: how many cascades the world's probes have.
    public float4 trace;
    // xy: the depth's texels across and down. zw: the window's pixels across and down.
    public float4 sizes;
    // World to clip space as the frame before had it, which a probe's place then is found through.
    public float4x4 lastViewProjection;
    // xyz: the frame before's eye. w: 1 where the probes of the frame before may be read, as they
    // may not be on the first frame or the first after the probes were laid out again.
    public float4 last;
};

// A camera's matrices and eye, which a ray is traced through its depth with.
public struct ScreenCamera
{
    // World to clip space, and back.
    public float4x4 viewProjection;
    public float4x4 inverseViewProjection;
    public float3 eye;
};

// The world position a point of a depth image at uv and depth d stands for.
public float3 screenWorld(ScreenCamera camera, float2 uv, float d)
{
    float4 world = mul(camera.inverseViewProjection, float4(uv * 2.0 - 1.0, d, 1.0));
    return world.xyz / world.w;
}

// Where a point projects onto a depth image, in xy, and its depth there, in z, and in w the depth
// the image holds there, or false where it lies behind the eye or off the picture.
bool onScreen<P : IPicture>(P depth, ScreenCamera camera, float3 q, out float4 at)
{
    at = float4(0.0);
    float4 clip = mul(camera.viewProjection, float4(q, 1.0));
    if (clip.w <= 1e-4)
        return false;
    float3 ndc = clip.xyz / clip.w;
    float2 uv = ndc.xy * 0.5 + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0))
        return false;
    at = float4(uv, ndc.z, depth.sampleAt(uv, 0).r);
    return true;
}

// Where a ray from p along way first meets the surface a depth image holds within length, in steps
// more finely spaced near p, as a world position, or false and how far it went where it left the
// picture or met nothing on it. A point behind the depth counts as on the surface while it is
// within two steps of it, or a quarter of a unit, so a ray passing behind a thing in front does
// not stop there, and the step it met the surface in is halved five times to find where.
public bool traceScreen<P : IPicture>(P depth, ScreenCamera camera, float3 p, float3 way, float length, int steps, out float3 hit, out float left)
{
    hit = p;
    left = length;
    float before = 0.0;
    for (int i = 0; i < steps; i++)
    {
        float s = float(i + 1) / float(steps);
        float t = length * s * s;
        float3 q = p + way * t;
        float4 at;
        if (!onScreen(depth, camera, q, at))
        {
            left = t;
            return false;
        }
        if (at.w < 1.0 && at.z > at.w)
        {
            float3 surface = screenWorld(camera, at.xy, at.w);
            float behind = distance(camera.eye, q) - distance(camera.eye, surface);
            if (behind < max(length / float(steps) * 2.0, 0.25))
            {
                float near = before, far = t;
                for (int k = 0; k < 5; k++)
                {
                    float middle = (near + far) * 0.5;
                    float4 m;
                    if (onScreen(depth, camera, p + way * middle, m) && m.w < 1.0 && m.z > m.w)
                    {
                        far = middle;
                        at = m;
                    }
                    else
                        near = middle;
                }
                hit = screenWorld(camera, at.xy, at.w);
                return true;
            }
        }
        before = t;
    }
    return false;
}

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

// The light the field's surfaces give off at p, blended as their colors are.
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
// it is: more the more squarely it stands in front of the surface, as DDGI weighs its probes.
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

// What a probe's faces carry in alpha, read as a face of each axis: whether the probe holds light,
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

// How far off a surface along its normal the probes around it are weighed from, in their spacing.
// A room whose walls lie in planes of probes, as one whose size is a whole number of the spacing
// may, weighed at the surface itself took its walls' and ceiling's light from the probes in their
// planes alone, which stand inside them and hold nothing, and none from the row in front.
public static const float Lean = 0.5;

// How much of the frame before's own light a probe's may keep before the light that bounced is
// taken at the share it kept: a fall by a fifth or more, which a light switched off or carried
// away gives and the field's cells settling after a camera moves do not, and a rise by a quarter
// or more, its inverse, so a light that flickers moves the light it bounces down and up alike.
public static const float Falls = 0.8;

// The most a probe's light that bounced is raised by in a frame its own light rose by much, so a
// probe its own light barely lit before gains no more than four times its bounce at once.
public static const float Rises = 4.0;

// The light a color gives the eye, by Rec. 709's weights.
public float luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// The share of the light that bounced a probe takes the frame after its own light went from
// before to now, the same share as its own light kept where that fell or rose by much, and the
// whole of it otherwise.
public float3 followed(float3 now, float3 before)
{
    float3 fell = now / max(before, 1e-6);
    return select(fell < Falls || fell > 1.0 / Falls, clamp(fell, 0.0, Rises), float3(1.0));
}

// How much of the light a probe's rays bring back must come straight from the sun, the lights and
// what gives off light for the probe to be judged by its own light, a quarter, where a probe lit
// mostly by light that bounced, as one down a corridor from a lit room, is judged by the own light
// of the probes around it summed, since its own light is too little of its light to say what went
// out.
public static const float OwnShare = 0.25;

// How much the probe of cascade c at `at`, standing at probe, sees of a surface at p facing n: 1
// where p, taken three tenths of the probes' spacing off the surface along n, lies nearer the probe
// than its rays reached that way the frame before, falling to 0 over half the spacing past it, as
// DDGI weighs its probes by the distances they traced. Taken off the surface, the point comes
// nearer a probe on its own side and goes further from one past it, so a probe under a floor or
// beyond a wall, whose rays stop at the floor's or the wall's far face, lends a surface on this side
// none of the light it sees there.
public float probeSees<V : IVolume>(V reach, float3 p, float3 n, float3 probe, int3 at, int c, float probes, float apart)
{
    float3 toward = p + n * (0.3 * apart) - probe;
    float d = length(toward);
    if (d < 1e-4)
        return 1.0;
    int2 texel = min(int2(octahedron(toward / d) * float(ReachTexels)), int2(ReachTexels - 1));
    float reached = reach.loadAt(int3(at.xy * ReachTexels + texel, c * int(probes) + at.z)).r;
    float slack = 0.5 * apart;
    return saturate((reached + slack - d) / slack);
}

// The light that bounced to a surface at p facing n, irradiance, from the faces of the eight probes
// of the finest cascade around it that the probes reach, each weighed by how near it is, how
// squarely it stands in front of the surface and how much of it it sees (probeSees), a probe inside
// a mesh passed over, the three faces n looks toward weighed by the square of n along each, or none
// where no cascade of probes holds p. A probe it does not see keeps a twentieth of its weight, as
// one behind its plane keeps a little, since cut to nothing a pixel left with one or two probes
// around it seen took their light alone and a Cornell box showed a notch beside its tall block, and
// a surface that sees none of them, as one wedged between two meshes may, blends them by that
// twentieth, so it is not left dark.
public float3 bouncedAt<V : IVolume>(V cubes, V reach, SceneFieldInfo info, float3 p, float3 n, float probes, float spacing, float cascades)
{
    int c = fieldCascade(info, p, 1.0);
    if (c < 0 || c >= int(cascades))
        return float3(0.0);
    float4 oc = info.cascades[c].originAndCell;
    float3 q = clamp((p + n * (Lean * oc.w * spacing) - oc.xyz) / (oc.w * spacing) - 0.5, 0.0, probes - 1.0);
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
public float skyAt<V : IVolume>(V cubes, V skies, V reach, SceneFieldInfo info, float3 p, float3 n, float3 r, float probes, float spacing, float cascades)
{
    int c = fieldCascade(info, p, 1.0);
    if (c < 0 || c >= int(cascades))
        return 1.0;
    float4 oc = info.cascades[c].originAndCell;
    float3 q = clamp((p + n * (Lean * oc.w * spacing) - oc.xyz) / (oc.w * spacing) - 0.5, 0.0, probes - 1.0);
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

// The light that bounced to a surface at p facing n, as bouncedAt gives it, from those of the eight
// probes around it the surface sees: in front of its plane, with nothing the field holds between,
// so a probe under a floor or beyond a wall lends the light it sees there to no surface on this
// side. A probe behind the plane is passed over with no march, since the surface itself stands
// between, where bouncedAt lends it a little, which the blend raised to all of it wherever every
// probe in front was hidden. The probes' passes take it, where the model pass's reflections keep
// bouncedAt. The faces are read where the probes stood as gathered says, the frame before's corners
// for the world's probes' rays, and the field marched as info has it.
public float3 bouncedSeenAt<V : IVolume>(V field, SceneFieldInfo info, SceneFieldInfo gathered, V cubes, float3 p, float3 n, float probes, float spacing,
    float cascades)
{
    int c = fieldCascade(gathered, p, 1.0);
    if (c < 0 || c >= int(cascades))
        return float3(0.0);
    float4 oc = gathered.cascades[c].originAndCell;
    float3 q = clamp((p + n * (Lean * oc.w * spacing) - oc.xyz) / (oc.w * spacing) - 0.5, 0.0, probes - 1.0);
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
    return weights > 1e-5 ? sum / weights : float3(0.0);
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

// The light a surface the field holds at p sends back along a probe's ray that met it, as
// shadeHit gives it, with the lamps that cast shadows hidden where the field stands between, so
// a lamp's light does not bounce off the far side of a wall, and where seen is set the light
// that bounced to it from the probes it sees alone (bouncedSeenAt). The world's probes set it,
// since every surface's light comes to them. The screen's probes, whose rays are many and short,
// do not, because marching to the probes at each of their hits cost 0.10 to 0.15 ms of the
// bounce's 0.3 to 0.4 on the RTX 4070 in shaders_cornell_box, and a closed room under a lamp held
// 3 levels of its light without it, where it held 247 before either. Weighed by the probes' reach
// instead, as bouncedAt does, the world's probes saved 0.03 to 0.05 ms and let 42.9 levels of a lamp
// under a closed room's floor into it where the marches hold it to 2.5. The model pass's
// reflections call shadeHit, which hides the lamps as this does and marches to no probe. The light that bounced is
// taken at kept's share of it, from the faces where gathered says the probes stood, and own holds the light the surface sends on of what reaches it
// straight from the sun and the lights, with the light it gives off, which a world probe weighs
// the frame before's light by (gi_ambient.slang).
public float3 shadeProbeHit<V : IVolume>(V field, SceneFieldInfo info, SceneFieldInfo gathered, V albedo, V glow, GiLights lights, V cubes, V reach,
    float3 p, float probes, float spacing, float cascades, bool seen, float3 kept, out float3 own)
{
    float3 n = fieldNormal(field, info, p);
    float3 color = surfaceAlbedo(albedo, info, p);
    float3 bounced = seen ? bouncedSeenAt(field, info, gathered, cubes, p, n, probes, spacing, cascades)
                          : bouncedAt(cubes, reach, gathered, p, n, probes, spacing, cascades);
    own = color * (directLight(field, info, lights, p, n) - hiddenLampLight(field, info, lights, p, n)) + surfaceGlow(glow, albedo, info, p);
    return own + color / Pi * bounced * (lights.counts.z * kept);
}

// The light a surface the field holds at p sends back along a ray that met it: its color over pi
// times the light that reaches it, the sun's and the lights', the lamps that cast shadows hidden
// where the field stands between as the probes have them, and, at the share given, the light that
// bounced to it the frame before, with the light it gives off. The sun and the lights are given as
// the model pass has them, the light a white surface facing one returns, which is the light
// reaching it over pi, so they are taken times pi beside the bounced light, which the probes hold
// as the light reaching a face; read as they are, a lit surface sent on a pi-th of its light,
// which left the probes' hits 48 to 75% under a path-traced reference. hiddenLampLight's loop after
// directLight's is the shape lavapipe draws, which the probes' passes already have, where the lamps'
// loop moved ahead of the sun's in directLight is not (build/mesa).
public float3 shadeHit<V : IVolume>(V field, SceneFieldInfo info, V albedo, V glow, GiLights lights, V cubes, V reach,
    float3 p, float probes, float spacing, float cascades)
{
    float3 n = fieldNormal(field, info, p);
    float3 color = surfaceAlbedo(albedo, info, p);
    float3 arrived = (directLight(field, info, lights, p, n) - hiddenLampLight(field, info, lights, p, n)) * Pi
        + bouncedAt(cubes, reach, info, p, n, probes, spacing, cascades) * lights.counts.z;
    return color / Pi * arrived + surfaceGlow(glow, albedo, info, p);
}

