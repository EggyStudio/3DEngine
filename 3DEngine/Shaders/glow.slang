// The small surfaces that give off light the bounce carries as lights of their own, each a box in
// the world, the light one sends a point in closed form, and the marches through the field that say
// how much of it a point sees, which gi.slang sums (GlowLights.cs).
module glow;

import scenefield;

// How many of the small surfaces that give off light the bounce carries as lights at most, the
// nearest the eye, as many as a screen probe keeps a bit of each for (GlowLights.Most).
public static const int MostGlowLights = 64;

// The words each screen probe keeps of the glow lights: eight of four bits a light, how much of each
// it sees that gives it a sixteenth of their light or more, and three of the light of the fainter
// ones, summed by how much of each it sees (gi_screen.slang, GpuScreenProbes.GlowSeenBytes).
public static const int GlowSeenWords = 12;

// A small surface that gives off light, carried through the bounce as a light of its own rather
// than by the rays that meet it, since few of a probe's rays meet a small one and those that do
// take it for a whole texel's worth of the sky, and the probes' faces above a floor do not see a
// lamp lying on it (GlowLights.cs).
public struct GlowLight
{
    // xyz: the middle of the box around it. w: how far its light reaches, past which it gives none.
    public float4 middleAndReach;
    // xyz: half the box's side along each of the box's own axes, in the world. w: its length.
    public float4 halfX;
    public float4 halfY;
    public float4 halfZ;
    // rgb: the light its surface gives off, linear. w: its mesh's surface over its box's, which
    // the light from the box's faces is scaled by, as a ball gives off from half its box's.
    public float4 glowAndShare;
};

// The arc from a to b, both of length one, times the way the plane through them faces, over two
// pi: one edge's term of Lambert's sum for the light a polygon sends a point, by Heitz's fit of
// the arc over two pi times its sine, which keeps the short edges of a far square that acos loses
// near 1.
float3 edgeIntegral(float3 a, float3 b)
{
    float x = dot(a, b);
    float y = abs(x);
    float u = 0.8543985 + (0.4965155 + 0.0145206 * y) * y;
    float v = 3.4175940 + (4.1616724 + y) * y;
    float ratio = u / v;
    float arcOverSine = x > 0.0 ? ratio : 0.5 * rsqrt(max(1.0 - x * x, 1e-7)) - ratio;
    return cross(a, b) * arcOverSine;
}

// The light reaching a point x facing n from a square that gives off one unit of light, its middle
// c and its half sides u and v, of the part of it above x's plane, by Lambert's sum over the edges
// of that part, the square clipped where it crosses the plane.
float squareIrradiance(float3 x, float3 n, float3 c, float3 u, float3 v)
{
    float3 corners[4] = { c + u + v - x, c - u + v - x, c - u - v - x, c + u - v - x };
    float3 kept[5];
    int count = 0;
    for (int i = 0; i < 4; i++)
    {
        float3 a = corners[i], b = corners[(i + 1) & 3];
        float above = dot(a, n), next = dot(b, n);
        if (above >= 0.0 && count < 5)
            kept[count++] = a;
        if ((above >= 0.0) != (next >= 0.0) && count < 5)
            kept[count++] = a + (b - a) * (above / (above - next));
    }
    if (count < 3)
        return 0.0;
    float3 sum = float3(0.0);
    for (int i = 0; i < count; i++)
    {
        float3 a = kept[i], b = kept[(i + 1) % count];
        if (dot(a, a) > 1e-12 && dot(b, b) > 1e-12)
            sum += edgeIntegral(normalize(a), normalize(b));
    }
    return 3.14159265358979 * abs(dot(sum, n));
}

// The light reaching a point x facing n from a glow light, unhidden: the light from each face of
// its box that x lies before, three at most, in closed form within four of the box's half
// diagonals, which a path-traced floor beside a glowing block reads to within 3% from one block
// out to four, and past them each face's as a point at its middle, its area by the two cosines
// over the square of its distance, within 3% of the closed form there at a tenth of its cost.
// None where x lies on the light's own surface, as glowLightOn finds it within margin.
public float3 glowLightIrradiance(GlowLight light, float3 x, float3 n, float margin)
{
    if (glowLightOn(light, x, n, margin))
        return float3(0.0);
    float3 middle = light.middleAndReach.xyz;
    float4 halves[3] = { light.halfX, light.halfY, light.halfZ };
    float3 offset = x - middle;
    bool near = dot(offset, offset) < 16.0 * (halves[0].w * halves[0].w + halves[1].w * halves[1].w + halves[2].w * halves[2].w);
    float sum = 0.0;
    for (int k = 0; k < 3; k++)
    {
        float3 h = halves[k].xyz;
        float side = dot(offset, h);
        float squared = halves[k].w * halves[k].w;
        if (squared < 1e-12 || abs(side) <= squared)
            continue;
        float3 face = middle + (side > 0.0 ? h : -h);
        float4 u = halves[(k + 1) % 3], v = halves[(k + 2) % 3];
        if (near)
            sum += squareIrradiance(x, n, face, u.xyz, v.xyz);
        else
        {
            float3 toward = face - x;
            float distance2 = dot(toward, toward);
            float3 way = toward * rsqrt(distance2);
            sum += 4.0 * u.w * v.w * abs(dot(way, h)) / halves[k].w * max(dot(way, n), 0.0) / distance2;
        }
    }
    return light.glowAndShare.rgb * (sum * light.glowAndShare.w);
}

// How far along the way from `from` its line enters a glow light's box, 0 where it starts inside.
public float glowLightEntry(GlowLight light, float3 from, float3 way)
{
    float3 offset = from - light.middleAndReach.xyz;
    float3 halves[3] = { light.halfX.xyz, light.halfY.xyz, light.halfZ.xyz };
    float enter = 0.0, leave = 1e6;
    for (int k = 0; k < 3; k++)
    {
        float squared = max(dot(halves[k], halves[k]), 1e-12);
        float at = dot(offset, halves[k]) / squared, speed = dot(way, halves[k]) / squared;
        if (abs(speed) < 1e-8)
            continue;
        float a = (-1.0 - at) / speed, b = (1.0 - at) / speed;
        enter = max(enter, min(a, b));
        leave = min(leave, max(a, b));
    }
    return enter <= leave ? enter : 0.0;
}

// How much of a glow light's light reaches a surface at p facing n by its reach, fading to none at
// it, or 0 where it lies past its reach or behind the surface.
public float glowLightFade(GlowLight light, float3 p, float3 n)
{
    float3 toward = light.middleAndReach.xyz - p;
    float reach = light.middleAndReach.w;
    float distance2 = dot(toward, toward);
    if (distance2 >= reach * reach || dot(toward, n) <= -light.halfX.w - light.halfY.w - light.halfZ.w)
        return 0.0;
    float edge = saturate(1.0 - pow(distance2 / (reach * reach), 2.0));
    return edge * edge;
}

// Whether p, facing n, lies on a glow light's own surface: within margin of its box, n leading out
// of it, so that a step of margin along n moves p away from the box by half the step or more. A
// ray's hit on the light lies anywhere within a fraction of a cell of the box, inside or out, and
// the field's normal tilts near its edges, so part of a face rose above the hit's plane and lit
// it: on lavapipe, whose hits fell a little outside the faces where the RTX 4070's fell inside, the
// light bounced off the block's own faces added 0.018 to the floor two blocks from it, 0.044. A
// floor beside the box, n along the box's side, is not on it.
public bool glowLightOn(GlowLight light, float3 p, float3 n, float margin)
{
    float at = glowLightDistance(light, p);
    return at < margin && glowLightDistance(light, p + n * margin) >= at + margin * 0.5;
}

// How far q lies from a glow light's box, 0 inside it.
public float glowLightDistance(GlowLight light, float3 q)
{
    float3 offset = q - light.middleAndReach.xyz;
    float3 beyond = float3(
        max(abs(dot(offset, light.halfX.xyz)) / max(light.halfX.w, 1e-6) - light.halfX.w, 0.0),
        max(abs(dot(offset, light.halfY.xyz)) / max(light.halfY.w, 1e-6) - light.halfY.w, 0.0),
        max(abs(dot(offset, light.halfZ.xyz)) / max(light.halfZ.w, 1e-6) - light.halfZ.w, 0.0));
    return length(beyond);
}

// How much of a glow light's point `target` a surface at p facing n, in cells of `cell`, sees past
// what the field holds between, from 0 to 1: a march through the field from `lift` cells off the
// surface toward the target, stopping a cell short of it, at each step the way's clearance from the
// nearest surface against the cone of half-width `radius` at the target seen from p, so a block
// that covers half the cone takes half the light and a shadow's edge is as soft as what is seen is
// wide and as near the block lies to the light, as a distance field gives a penumbra. Hit or miss,
// the shadows had no edge to them and ran in the field's cells. A surface counts only where it lies
// nearer than the plane p lies on and the light's own box, which the field holds as it holds any
// mesh: counted, the box's own faces a cell from the march's end, where the cone is nearly as wide
// as the box, took a share of the light that came and went with where the last step fell, and the
// floor's light around a lamp ran in dark rings. Between two steps the way's nearest pass is found
// as a sphere tracer's is, from the two distances, so the share does not step with them. Inside a
// surface the share is half and less by how far under its edge the way passes, read by stepping out
// along p's normal, since the distance to its nearest face reads no deeper than half a thin wall
// and let a fifteenth of the light through one. The cone is kept within the step's height over p's
// plane, below which glowLightIrradiance has already cut the box's light. Where nothing stands
// nearer, or the field does not hold p, all of it.
float glowMarch<V : IVolume>(V field, SceneFieldInfo info, GlowLight light, float3 p, float3 n, float cell, float3 target, float radius, float lift)
{
    float3 from = p + n * (cell * lift);
    float3 toward = target - from;
    float total = max(length(toward), 1e-6);
    float3 way = toward / total;
    float end = total - cell;
    float seen = 1.0;
    float before = 1e20;
    float t = cell * 0.5;
    for (int i = 0; i < 64 && t < end; i++)
    {
        float3 q = from + way * t;
        int c = fieldCascade(info, q, 1.0);
        if (c < 0)
            break;
        float step = info.cascades[c].originAndCell.w;
        float d = fieldDistanceIn(field, info, c, q);
        float height = dot(q - p, n);
        if (d < min(height, glowLightDistance(light, q)) - step * 0.5)
        {
            float y = d > 0.0 ? d * d / (2.0 * before) : 0.0;
            float cone = max(min(radius * max(t - y, 0.0) / total, height), step * 0.25);
            // Outside a surface, the share is half and more by how far the way passes from it;
            // inside one, half and less by how far under its edge the way passes, read by stepping
            // out of it along p's normal, since the distance to its nearest face reads no deeper
            // than half a thin wall and let the light through it.
            float past;
            if (d > 0.0)
                past = sqrt(max(d * d - y * y, 0.0));
            else
            {
                float under = 0.0;
                for (int out = 0; out < 8 && under < cone; out++)
                {
                    float inside = fieldDistanceIn(field, info, c, q + n * under);
                    if (inside >= 0.0)
                        break;
                    under += max(-inside, step * 0.5);
                }
                past = -under;
            }
            seen = min(seen, saturate(0.5 + 0.5 * past / cone));
            if (seen <= 0.0)
                break;
        }
        before = max(d, 1e-4);
        t += max(d, step * 0.5);
    }
    return seen;
}

// How much of a glow light a surface at p facing n, in cells of `cell`, sees, from 0 to 1: one
// march from a cell and a half off the surface to the middle of its box, the cone the box's mean
// half side, which a probe's ray's hit and a pixel no screen probe holds take, the light that
// bounces on and a far pixel's asking less than the shadows near the eye. Where the field does not
// hold p, all of it.
public float glowLightSeen<V : IVolume>(V field, SceneFieldInfo info, GlowLight light, float3 p, float3 n, float cell)
{
    if (cell <= 0.0)
        return 1.0;
    float3 from = p + n * (cell * 1.5);
    float3 toward = light.middleAndReach.xyz - from;
    float total = max(length(toward), 1e-6);
    float entry = glowLightEntry(light, from, toward / total);
    if (entry <= cell * 2.0)
        return 1.0;
    return glowMarch(field, info, light, p, n, cell, from + toward / total * entry, (light.halfX.w + light.halfY.w + light.halfZ.w) / 3.0, 1.5);
}

// How much of a glow light's light a surface at p facing n, in cells of `cell`, sees, from 0 to 1,
// which the screen's probes take: each face of its box p lies before, two or three, halved across
// the face's axis that rises most from p's surface, and a march from a tenth of a cell off the
// surface to the middle of each half, the cone the half's own rise, each half's share by its own
// light at p. Past a wall half as high as a lamp beside it the floor reads a path-traced reference
// to within a tenth of its light, the lamp's side hidden and then its upper half seen from two
// blocks out, where one march to the box's middle, level with the wall's top, found the lamp hidden
// from the whole floor past the wall, and marches from a cell and a half off saw over the wall
// where the floor does not.
public float glowLightShown<V : IVolume>(V field, SceneFieldInfo info, GlowLight light, float3 p, float3 n, float cell)
{
    if (cell <= 0.0)
        return 1.0;
    float3 middle = light.middleAndReach.xyz;
    float4 halves[3] = { light.halfX, light.halfY, light.halfZ };
    float3 offset = p - middle;
    float lit = 0.0, seen = 0.0;
    for (int k = 0; k < 3; k++)
    {
        float3 h = halves[k].xyz;
        float side = dot(offset, h);
        float squared = halves[k].w * halves[k].w;
        if (squared < 1e-12 || abs(side) <= squared)
            continue;
        float3 face = middle + (side > 0.0 ? h : -h);
        float4 u = halves[(k + 1) % 3], v = halves[(k + 2) % 3];
        // The face's axis that rises most from p's surface is halved, the other kept whole.
        bool alongU = abs(dot(u.xyz, n)) * v.w >= abs(dot(v.xyz, n)) * u.w;
        float3 rise = alongU ? u.xyz : v.xyz;
        float radius = (alongU ? u.w : v.w) * 0.5;
        for (int half = 0; half < 2; half++)
        {
            float3 target = face + rise * (half == 0 ? 0.5 : -0.5);
            float3 toward = target - p;
            float distance2 = max(dot(toward, toward), 1e-8);
            float3 way = toward * rsqrt(distance2);
            float given = abs(dot(way, h)) * max(dot(way, n), 0.0) / distance2;
            if (given <= 0.0)
                continue;
            lit += given;
            seen += given * glowMarch(field, info, light, p, n, cell, target, radius, 0.1);
        }
    }
    return lit > 0.0 ? seen / lit : 1.0;
}
