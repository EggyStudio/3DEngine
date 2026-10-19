// The small emissive surfaces global illumination carries as lights of their own, each a box in
// the world, the light one sends a point in closed form, and the marches through the field that say
// how much of it a point sees, which gi.slang sums (GlowLights.cs). Its loops are kept from
// unrolling, since unrolled, with each of the model pass's calls of them inlined apart, they made
// the model pass's compile on lavapipe four times as long, 0.9 s for every app where 0.23 before
// them.
module glow;

import scenefield;

// How many of the small emissive surfaces global illumination carries as lights at most, the
// nearest the eye, as many as a screen probe keeps a bit of each for (GlowLights.Most).
public static const int MostGlowLights = 64;

// The words each screen probe keeps of the glow lights, sixteen of a byte a light, how much of each
// it sees that gives it an eighth of their light or more, three of the light of the fainter ones,
// summed by how much of each it sees, and one spare (gi_screen.slang, GpuScreenProbes.GlowSeenBytes).
public static const int GlowSeenWords = 20;

// The kinds of texel of the window's glow light past the lights the share is of, in 255ths of its
// normal's w, one on a glow light's own surface, which takes no other light's, and one no screen
// probe around holds, whose pixels march to the lights themselves (gi_screen_glow.slang).
public static const int GlowOnLight = 254;
public static const int GlowAsk = 255;

// A small emissive surface, carried through global illumination as a light of its own rather
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
    // rgb: its surface's emission, linear. w: its mesh's surface over that of the faces of its box
    // that emit, which their light is scaled by, as a ball emits from half its box's, below 2, and
    // twice the faces that emit above it, a bit a face, as glowShare and glowFaces read them.
    public float4 glowAndShare;
};

// A glow light's surface over that of its box's faces that emit.
public float glowShare(GlowLight light)
{
    float w = light.glowAndShare.w;
    return w - 2.0 * floor(w * 0.5);
}

// Which faces of a glow light's box emit, a bit a face, two an axis of halfX, halfY and halfZ, the
// side the axis points to the higher, every face where the light names none, as a block showing
// all six does. A block of a voxel game showing its top and one side emits from those alone.
public uint glowFaces(GlowLight light)
{
    uint faces = uint(floor(light.glowAndShare.w * 0.5));
    return faces == 0u ? 63u : faces;
}

// The arc from a to b, both of length one, times the way the plane through them faces, over two
// pi, one edge's term of Lambert's sum for the light a polygon sends a point, by Heitz's fit of
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

// The light reaching a point x facing n from a square that emits one unit of light, its middle
// c and its half sides u and v, of the part of it above x's plane, by Lambert's sum over the edges
// of that part, the square clipped where it crosses the plane.
float squareIrradiance(float3 x, float3 n, float3 c, float3 u, float3 v)
{
    float3 corners[4] = { c + u + v - x, c - u + v - x, c - u - v - x, c + u - v - x };
    float3 kept[5];
    int count = 0;
    [loop]
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
    [loop]
    for (int i = 0; i < count; i++)
    {
        float3 a = kept[i], b = kept[(i + 1) % count];
        if (dot(a, a) > 1e-12 && dot(b, b) > 1e-12)
            sum += edgeIntegral(normalize(a), normalize(b));
    }
    return 3.14159265358979 * abs(dot(sum, n));
}

// The light reaching a point x facing n from a glow light, unhidden, the light from each face of
// its box that x lies before, three at most, in closed form within four of the box's half
// diagonals, which a path-traced floor beside an emissive block reads to within 3% from one block
// out to four, and past them each face's as points, its area by the two cosines over the square of
// its distance, within 1.6% of the closed form there at a fraction of its cost.
// None where x lies on the light's own surface, as glowLightOn finds it within margin.
public float3 glowLightIrradiance(GlowLight light, float3 x, float3 n, float margin)
{
    if (glowLightOn(light, x, n, margin))
        return float3(0.0);
    float3 middle = light.middleAndReach.xyz;
    float4 halves[3] = { light.halfX, light.halfY, light.halfZ };
    float3 offset = x - middle;
    bool near = dot(offset, offset) < 16.0 * (halves[0].w * halves[0].w + halves[1].w * halves[1].w + halves[2].w * halves[2].w);
    uint faces = glowFaces(light);
    float sum = 0.0;
    [loop]
    for (int k = 0; k < 3; k++)
    {
        float3 h = halves[k].xyz;
        float side = dot(offset, h);
        float squared = halves[k].w * halves[k].w;
        if (squared < 1e-12 || abs(side) <= squared || (faces & (1u << uint(2 * k + (side > 0.0 ? 1 : 0)))) == 0u)
            continue;
        float3 face = middle + (side > 0.0 ? h : -h);
        float4 u = halves[(k + 1) % 3], v = halves[(k + 2) % 3];
        if (near)
            sum += squareIrradiance(x, n, face, u.xyz, v.xyz);
        else
        {
            // The face cut along its longer side into pieces about as long as its shorter, four at
            // most, or a face about square into quarters, each a point at its middle. Whole, a face
            // fifteen sixteenths of a block long and two wide, as an emissive rod's side, read 11%
            // over the closed form at four half diagonals and a block's face 7%, a step in the
            // floor's light where the forms met; cut so, 0.6% and 1.6%. Unrolled with the count
            // kept, so no loop nests in the faces' loop.
            bool longer = u.w >= v.w;
            float3 along = longer ? u.xyz : v.xyz, across = longer ? v.xyz : u.xyz;
            int pieces = clamp(int(max(u.w, v.w) / max(min(u.w, v.w), 1e-6) + 0.5), 1, 4);
            int points = pieces == 1 ? 4 : pieces;
            float area = 4.0 * u.w * v.w / float(points);
            [unroll]
            for (int piece = 0; piece < 4; piece++)
                if (piece < points)
                {
                    float3 at = pieces == 1
                        ? face + along * ((piece & 1) != 0 ? 0.5 : -0.5) + across * ((piece & 2) != 0 ? 0.5 : -0.5)
                        : face + along * ((2.0 * float(piece) + 1.0) / float(pieces) - 1.0);
                    float3 toward = at - x;
                    float distance2 = dot(toward, toward);
                    float3 way = toward * rsqrt(distance2);
                    sum += area * abs(dot(way, h)) / halves[k].w * max(dot(way, n), 0.0) / distance2;
                }
        }
    }
    return light.glowAndShare.rgb * (sum * glowShare(light));
}

// The rough unhidden light reaching p facing n from a glow light, half the surface of its box's
// faces that emit over the square of p's distance from the box's nearest point, softened by twice
// its thinnest half side, by how far its middle rises above p's plane. The window's glow light is
// kept as a share of it (gi_screen_glow.slang), read from texels of half the window's size, and the
// model pass scales that share by this at each pixel, so the light rises toward a lamp at each
// pixel as it does across the lamp's own thickness. Taken from the texels whole, the room around an
// emissive strip six hundredths of a block thick read 3.4 to 4% further from its path-traced
// reference at 160 by 90 than with each pixel's own light; with this softened by the box's whole
// size, 3.4 to 3.5%, the strip's two blocks of length keeping it flat across the strip's thickness;
// with this, 1.7 to 2%, and at 1280 by 720 within 0.1%. None where p lies on the light's own
// surface, as glowLightIrradiance gives.
public float3 glowLightRough(GlowLight light, float3 p, float3 n, float margin)
{
    if (glowLightOn(light, p, n, margin))
        return float3(0.0);
    float3 offset = p - light.middleAndReach.xyz;
    float4 halves[3] = { light.halfX, light.halfY, light.halfZ };
    float3 beyond = float3(0.0);
    float thin = 1e6, size2 = 0.0;
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        float side = halves[k].w;
        float3 axis = halves[k].xyz / max(side, 1e-6);
        float along = dot(offset, axis);
        beyond += axis * (along - clamp(along, -side, side));
        thin = min(thin, side);
        size2 += side * side;
    }
    float distance2 = dot(beyond, beyond) + 4.0 * thin * thin;
    float rise = saturate((sqrt(size2) - dot(offset, n)) * rsqrt(dot(offset, offset) + size2));
    // Half the surface of the faces that emit, each face two half sides by two.
    uint faces = glowFaces(light);
    float surface = 0.0;
    [unroll]
    for (int k = 0; k < 3; k++)
        surface += 2.0 * halves[(k + 1) % 3].w * halves[(k + 2) % 3].w * float(((faces >> uint(2 * k)) & 1u) + ((faces >> uint(2 * k + 1)) & 1u));
    return light.glowAndShare.rgb * (glowShare(light) * surface * rise / distance2);
}

// How near p lies to a glow light for its light to be drawn at each pixel, 1 within its box's half
// diagonal of the box, none past twice that, falling off between, the same at a texel of the
// window's depth as at a pixel (gi_screen_glow.slang, glowLightsHere in modelpass.slang).
public float glowLightNear(GlowLight light, float3 p)
{
    float diagonal = max(length(float3(light.halfX.w, light.halfY.w, light.halfZ.w)), 1e-6);
    float3 toward = p - light.middleAndReach.xyz;
    if (dot(toward, toward) >= 9.0 * diagonal * diagonal)
        return 0.0;
    return saturate(2.0 - glowLightDistance(light, p) / diagonal);
}

// How far along the way from `from` its line enters a glow light's box, 0 where it starts inside.
public float glowLightEntry(GlowLight light, float3 from, float3 way)
{
    float3 offset = from - light.middleAndReach.xyz;
    float3 halves[3] = { light.halfX.xyz, light.halfY.xyz, light.halfZ.xyz };
    float enter = 0.0, leave = 1e6;
    [loop]
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

// Whether p, facing n, lies on a glow light's own surface, within margin of its box with n leading
// out of it, so that a step of margin along n moves p away from the box by half the step or more. A
// ray's hit on the light lies anywhere within a fraction of a cell of the box, inside or out, and
// the field's normal tilts near its edges, so part of a face rose above the hit's plane and lit it.
// On lavapipe, whose hits fell a little outside the faces where those on the development machine's
// GPU fell inside, the light reflected off the block's own faces added 0.018 to the floor two
// blocks from it, 0.044. A floor beside the box, n along the box's side, is not on it.
public bool glowLightOn(GlowLight light, float3 p, float3 n, float margin)
{
    float at = glowLightDistance(light, p);
    return at < margin && glowLightDistance(light, p + n * margin) >= at + margin * 0.5;
}

// Whether a ray along `way` that met the field at p passed through a glow light's box within
// `within` before it, as a ray passes a light thinner than a cell, which the field holds about its
// middle, and meets the wall behind it, where the light reaches it from no distance. A path-traced
// reference's path stops at the light and leaves out its emission there, since the light reaches
// every surface as a light of its own (GlowLights), counted at the surface, and a path that went on
// through it would count it again, so such a hit takes none of that light. A ray through the strip
// 0.06 thick of shaders_bounce_rooms met the wall 0.02 behind it and brought a texel of each of the
// two probes beside the strip 3.7 of light, their faces toward it 211 and 206% over the reference
// at the first bounce on the development machine's GPU, where with this test they read 7 and 10%
// over. Leaving out any light whose box lay within half a cell behind a hit also took the light
// around the strip from rays that never passed it, and its room read 17% under where this test read
// it 14%, both before glowMarch's cone.
public bool glowLightPassed(GlowLight light, float3 p, float3 way, float within)
{
    float3 offset = p - light.middleAndReach.xyz;
    float4 halves[3] = { light.halfX, light.halfY, light.halfZ };
    float enter = 0.0, leave = within;
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        float side = halves[k].w;
        float3 axis = halves[k].xyz / max(side, 1e-6);
        float o = dot(offset, axis), d = -dot(way, axis);
        if (abs(d) < 1e-6)
        {
            if (abs(o) > side)
                return false;
            continue;
        }
        float t0 = (-side - o) / d, t1 = (side - o) / d;
        enter = max(enter, min(t0, t1));
        leave = min(leave, max(t0, t1));
    }
    return enter <= leave;
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
// what the field holds between, from 0 to 1, by a march through the field from `lift` cells off the
// surface toward the target, stopping a cell short of it, at each step the way's clearance from the
// nearest surface against the cone of half-width `radius` at the target seen from p, so a block
// that covers half the cone takes half the light and a shadow's edge is as soft as what is seen is
// wide and as near the block lies to the light, as a distance field gives a penumbra. Hit or miss,
// the shadows had no edge to them and ran in the field's cells. A surface counts only where it lies
// nearer than the plane p lies on and the light's own box, which the field holds as it holds any
// mesh. Counted, the box's own faces a cell from the march's end, where the cone is nearly as wide
// as the box, took a share of the light that came and went with where the last step fell, and the
// floor's light around a lamp ran in dark rings. Between two steps the way's nearest pass is found
// as a sphere tracer's is, from the two distances, so the share does not step with them. Inside a
// surface the share is half and less by how far under its edge the way passes, read by stepping out
// along p's normal, since the distance to its nearest face reads no deeper than half a thin wall
// and let a fifteenth of the light through one. That is read once, after the march, at the step
// that lay deepest inside. Read at each step, a loop inside the march's own, it lost the device on
// the development machine's GPU where the world probes' rays met a closed room's walls with a lamp
// inside, an MMU fault through the constant cache (Xid 31) that the validation layer's GPU-assisted
// checks found no read out of range for and lavapipe never met. The cone is kept within the step's
// height over p's plane, below which glowLightIrradiance has already cut the box's light. Where
// nothing stands nearer, or the field does not hold p, all of it.
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
    // The step that lay deepest inside a surface, its point, cascade and cone.
    float deepest = 0.0;
    float3 deepAt = float3(0.0);
    int deepIn = 0;
    float deepCone = 1.0;
    [loop]
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
            // A radius below zero, the march to the box's middle (glowLightSeen), takes the cone as
            // wide as the box across the way toward the surface the step passes, the box's support
            // along the field's slope there, 0.03 for a strip 0.06 thick toward the wall it is
            // mounted on and 0.5 for a cube a unit wide toward a face. As wide as the box's mean
            // half side in every way, 0.35 for a strip 2 long, a march from a hit above or below
            // the strip ran along its wall within a cell and took the wall, inside the cone, for
            // something between, though the wall stands behind the strip, and the hits in the
            // strip's room of shaders_bounce_rooms read its light 28% under its closed form at the
            // floor, 30% at the ceiling and 35% right over the strip, where with the support the
            // floor reads it, the ceiling 8% under and the hits over the strip 4%, a few hits in
            // the ceiling's back corners 35% under. The slope is read by forward differences, three
            // reads of the field, where central differences' six took the ceiling to 10% under and
            // the side walls to 10 and 21% against 8 and 20%, at the same cost, global illumination
            // taking 0.15 to 0.24 ms more in the Cornell box on the development machine's GPU.
            float wide = radius;
            if (wide < 0.0)
            {
                float e = step * 0.5;
                float3 slope = float3(fieldDistanceIn(field, info, c, q + float3(e, 0, 0)), fieldDistanceIn(field, info, c, q + float3(0, e, 0)),
                    fieldDistanceIn(field, info, c, q + float3(0, 0, e))) - d;
                float3 across = slope - way * dot(slope, way);
                float acrossLength = length(across);
                across = acrossLength > 1e-4 ? across / acrossLength : slope;
                wide = abs(dot(across, light.halfX.xyz)) + abs(dot(across, light.halfY.xyz)) + abs(dot(across, light.halfZ.xyz));
            }
            float cone = max(min(wide * max(t - y, 0.0) / total, height), step * 0.25);
            // Outside a surface, the share is half and more by how far the way passes from it.
            if (d > 0.0)
                seen = min(seen, saturate(0.5 + 0.5 * sqrt(max(d * d - y * y, 0.0)) / cone));
            else
            {
                // Inside one, the way out along p's normal is no shorter than the way to the
                // nearest face, so the share is at most what that gives, and a march into a wall
                // thicker than the cone ends there, where with the depth read after the march alone
                // it ran on to the light and cost global illumination in the Cornell box 0.13 ms
                // more at Low. It ends by its own condition, `t` set to its end, since left by a
                // break there the march lost the device on the development machine's GPU in five
                // runs of five as the loop nested in it had.
                seen = min(seen, saturate(0.5 + 0.5 * d / cone));
                if (seen <= 0.0)
                    t = end;
                else if (d < deepest)
                {
                    deepest = d;
                    deepAt = q;
                    deepIn = c;
                    deepCone = cone;
                }
            }
        }
        before = max(d, 1e-4);
        t += max(d, step * 0.5);
    }
    // Inside one, half and less by how far under its edge the way passes, stepped out along p's
    // normal and, where the field's slope there leans across the way, across it toward the nearest
    // face, the nearer way out kept. Along p's normal alone, a march passing a twentieth of a block
    // inside the side of a block floating over the floor stepped up through the whole block, and
    // the floor under it took 0.29 of the light a reference gives it, where 0.96 with the way
    // across. Each way out ends where it left the surface, the step that overshot taken back by the
    // distance read past it. Ended at the step's end, a way that grazed an edge was at least half a
    // cell under it, and a lamp's light past a pillar in the voxel game fell by a fifth in 20
    // pixels on the wall behind, toothed where the steps crossed the pillar's ends, where a
    // path-traced reference rises smoothly.
    if (deepest < 0.0 && seen > 0.0)
    {
        float step = info.cascades[deepIn].originAndCell.w;
        float e = step * 0.5;
        float3 slope = float3(
            fieldDistanceIn(field, info, deepIn, deepAt + float3(e, 0, 0)) - fieldDistanceIn(field, info, deepIn, deepAt - float3(e, 0, 0)),
            fieldDistanceIn(field, info, deepIn, deepAt + float3(0, e, 0)) - fieldDistanceIn(field, info, deepIn, deepAt - float3(0, e, 0)),
            fieldDistanceIn(field, info, deepIn, deepAt + float3(0, 0, e)) - fieldDistanceIn(field, info, deepIn, deepAt - float3(0, 0, e)));
        float3 across = slope - way * dot(slope, way);
        float acrossLength = length(across);
        float3 aside = acrossLength > 0.5 * length(slope) && acrossLength > 1e-6 ? across / acrossLength : n;
        float under = 0.0, beside = 0.0;
        bool outUnder = false, outBeside = false;
        [loop]
        for (int out = 0; out < 8 && !(outUnder && outBeside) && min(under, beside) < deepCone; out++)
        {
            if (!outUnder)
            {
                float inside = fieldDistanceIn(field, info, deepIn, deepAt + n * under);
                if (inside >= 0.0)
                {
                    outUnder = true;
                    under = max(under - inside, 0.0);
                }
                else
                    under += max(-inside, step * 0.5);
            }
            if (!outBeside)
            {
                float inside = fieldDistanceIn(field, info, deepIn, deepAt + aside * beside);
                if (inside >= 0.0)
                {
                    outBeside = true;
                    beside = max(beside - inside, 0.0);
                }
                else
                    beside += max(-inside, step * 0.5);
            }
        }
        seen = min(seen, saturate(0.5 - 0.5 * min(under, beside) / deepCone));
    }
    return seen;
}

// How much of a glow light a surface at p facing n, in cells of `cell`, sees, from 0 to 1: one
// march from a cell and a half off the surface to the middle of its box, the cone as wide as the
// box across the way toward each surface the march passes (glowMarch), which a probe's ray's hit
// and a pixel no screen probe holds take, the light that bounces on and a far pixel's asking less
// than the shadows near the eye. Where the field does not hold p, all of it.
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
    float seen = glowMarch(field, info, light, p, n, cell, from + toward / total * entry, -1.0, 1.5);
    return lerp(1.0, seen, fieldHold(info, p));
}

// How much of a glow light's light a surface at p facing n, in cells of `cell`, sees, from 0 to 1,
// which the screen's probes take, by each face of its box p lies before, two or three, halved
// across the face's axis that rises most from p's surface, and a march from a tenth of a cell off
// the surface to the middle of each half, the cone the half's own rise, each half's share by its
// own light at p. Past a wall half as high as a lamp beside it the floor reads a path-traced
// reference to within a tenth of its light, the lamp's side hidden and then its upper half seen
// from two blocks out, where one march to the box's middle, level with the wall's top, found the
// lamp hidden from the whole floor past the wall, and marches from a cell and a half off saw over
// the wall where the floor does not.
public float glowLightShown<V : IVolume>(V field, SceneFieldInfo info, GlowLight light, float3 p, float3 n, float cell)
{
    if (cell <= 0.0)
        return 1.0;
    float3 middle = light.middleAndReach.xyz;
    float4 halves[3] = { light.halfX, light.halfY, light.halfZ };
    float3 offset = p - middle;
    float lit = 0.0, seen = 0.0;
    [loop]
    for (int k = 0; k < 3; k++)
    {
        float3 h = halves[k].xyz;
        float side = dot(offset, h);
        float squared = halves[k].w * halves[k].w;
        if (squared < 1e-12 || abs(side) <= squared)
            continue;
        float3 face = middle + (side > 0.0 ? h : -h);
        float4 u = halves[(k + 1) % 3], v = halves[(k + 2) % 3];
        // The face's axis that rises most from p's surface is halved, the other kept whole, and the
        // longer where neither rises more, as from a floor under a level face. Halved along the
        // first, a row of three lamps under a ceiling, one light of a face three blocks long, was
        // marched to two points on its middle line, and a block floating under the middle lamp hid
        // the whole row from the floor beneath all three, which read none of its light where a
        // path-traced reference gives it two fifths of the row's unhidden light.
        float riseU = abs(dot(u.xyz, n)) * v.w, riseV = abs(dot(v.xyz, n)) * u.w;
        bool alongU = abs(riseU - riseV) > 0.05 * u.w * v.w ? riseU > riseV : u.w >= v.w;
        float3 rise = alongU ? u.xyz : v.xyz;
        float radius = (alongU ? u.w : v.w) * 0.5;
        [loop]
        for (int half = 0; half < 2; half++)
        {
            float3 target = face + rise * (half == 0 ? 0.5 : -0.5);
            float3 toward = target - p;
            float distance2 = max(dot(toward, toward), 1e-8);
            float3 way = toward * rsqrt(distance2);
            // Each half by its own light as a point, its area by the two cosines over the square
            // of its distance. Weighed by the box's half size along the face's normal in place of
            // the face's area, as for a cube the two are alike, a light of four blocks two by two
            // weighed the face a pillar beside it hid at a quarter of its due against the face it
            // left open, and a wall past the pillar read 0.385 to 0.684 of the light seen past it
            // where a path-traced reference reads 0.235 to 0.550, and 0.234 to 0.521 weighed so.
            float given = abs(dot(way, h)) / halves[k].w * u.w * v.w * max(dot(way, n), 0.0) / distance2;
            if (given <= 0.0)
                continue;
            lit += given;
            seen += given * glowMarch(field, info, light, p, n, cell, target, radius, 0.1);
        }
    }
    return lit > 0.0 ? lerp(1.0, seen / lit, fieldHold(info, p)) : 1.0;
}
