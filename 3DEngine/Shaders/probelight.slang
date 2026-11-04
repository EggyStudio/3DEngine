// The light the world's probes lend a surface, the eight probes of a cascade around a point
// gathered as the model pass, the screen's probes and the world's probes' own hits read them, and
// the light a surface the field holds sends back along a ray that met it, which reads them.
module probelight;

import scenefield;
import gi;

// The indirect light reaching a surface at p facing n, irradiance, from the finest cascade of
// probes that holds it, fading to the next cascade's across its outer band (cascadeHold) where that
// one is built and is not the far cascade's (fadesOn), or none where no cascade of probes holds p.
// The two cascades are read in a loop kept from unrolling, so a shader holds one read of a
// cascade. The probes are found where the field's cascades they stand in lie (cascadeAt). With the
// share of the sky each probe's faces saw (skies), a probe the surface does not see lends none of
// the sky its rays reached (bouncedIn), as the model pass reads them.
public float3 bouncedAt<V : IVolume>(V cubes, V skies, V reach, SceneFieldInfo info, float3 p, float3 n, float probes, float spacing, float cascades)
{
    int c = fieldCascade(info, p, 1.0, int(cascades));
    if (c < 0 || c >= int(cascades))
        return float3(0.0);
    float own = fadesOn(info, c, int(cascades)) ? cascadeHold(info, c, p, int(cascades)) : 1.0;
    float3 light = float3(0.0);
    [loop]
    for (int k = 0; k < 2; k++)
    {
        float share = k == 0 ? own : 1.0 - own;
        if (share > 0.0)
        {
            float sky;
            light += bouncedIn<V, true, false>(cubes, cubes, skies, reach, info, p, n, n, probes, spacing, c + k, int(cascades), sky) * share;
        }
    }
    return light;
}

// The same without the shares of the sky, as the probes' own hits read them (shadeProbeHit), each
// probe's faces moved toward their light at the surfaces they are read for (towardSurface).
public float3 bouncedHitAt<V : IVolume>(V cubes, V cubesAt, V reach, SceneFieldInfo info, float3 p, float3 n, float probes, float spacing, float cascades)
{
    int c = fieldCascade(info, p, 1.0, int(cascades));
    if (c < 0 || c >= int(cascades))
        return float3(0.0);
    float own = fadesOn(info, c, int(cascades)) ? cascadeHold(info, c, p, int(cascades)) : 1.0;
    float3 light = float3(0.0);
    [loop]
    for (int k = 0; k < 2; k++)
    {
        float share = k == 0 ? own : 1.0 - own;
        if (share > 0.0)
        {
            float sky;
            light += bouncedIn<V, false, true>(cubes, cubesAt, cubes, reach, info, p, n, n, probes, spacing, c + k, int(cascades), sky) * share;
        }
    }
    return light;
}

// The light bouncedAt gives with the share of the sky the surface sees along r beside it, as skyAt
// gives it from the finest cascade around p, taken from the faces r looks toward in the same gather
// of that cascade's probes, which the two weigh alike, since a face's alpha holds its probe's move
// along the face's axis on both faces of it. Read apart, the share took a second gather of the eight
// probes, and the voxel game's cherry grove at 1920 by 1080 drew its scene pass in 10.1 ms on the
// development machine's GPU where it takes 8.9.
public float3 bouncedAndSkyAt<V : IVolume>(V cubes, V skies, V reach, SceneFieldInfo info, float3 p, float3 n, float3 r, float probes, float spacing,
    float cascades, out float sky)
{
    sky = 1.0;
    int c = fieldCascade(info, p, 1.0, int(cascades));
    if (c < 0 || c >= int(cascades))
        return float3(0.0);
    float own = fadesOn(info, c, int(cascades)) ? cascadeHold(info, c, p, int(cascades)) : 1.0;
    float3 light = float3(0.0);
    // The finest cascade's gather is made for its sky's share where its light takes no share.
    [loop]
    for (int k = 0; k < 2; k++)
    {
        float share = k == 0 ? own : 1.0 - own;
        if (k == 0 || share > 0.0)
        {
            float seen;
            light += bouncedIn<V, true, false>(cubes, cubes, skies, reach, info, p, n, r, probes, spacing, c + k, int(cascades), seen) * share;
            if (k == 0)
                sky = seen;
        }
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
// between two meshes may, blends them by that twentieth, so it is not left dark. With Sky, the share
// of the sky the probes' faces r looks toward saw, blended alike, as skyAt blends it, or the whole
// sky where the weights are none, and a probe the surface does not see lends it its light less the
// share of its faces' light the sky gave and none of its share of the sky. Blended by distance
// alone, the probes around the far end of Sponza's atrium, eight units apart in its last cascade,
// lent a wall by the main door the sky that those under the floor and outside the door saw, and it
// read 5.9 times a path-traced reference and blue, its dark door's reflection the sky. With Sky the
// probes it does not see lend it nothing where those it sees hold a twentieth of the weight, the
// blend of them all taken below that, since what a hidden probe's faces gather past the sky's
// share is the light beyond the wall that hides it, a sunlit court's or the floor's underside lit by
// the ground under the scene. A column of Sponza's nave at 8:00 read 11.8 times its reference with
// that light lent and 1.35 without it, and the line between its dark foot and the rest, which
// speckled pixel by pixel, went with it.
float3 bouncedIn<V : IVolume, let Sky : bool, let At : bool>(V cubes, V cubesAt, V skies, V reach, SceneFieldInfo info, float3 p, float3 n, float3 r, float probes, float spacing, int c,
    int cascades, out float sky)
{
    float4 oc = cascadeAt(info, c, cascades);
    float3 q = clamp((p - oc.xyz) / (oc.w * spacing) - 0.5, 0.0, probes - 1.0);
    int3 base = int3(floor(q));
    float3 f = q - float3(base);
    float3 n2 = n * n;
    float3 r2 = r * r;
    float3 sum = float3(0.0), seenSum = float3(0.0);
    float skySum = 0.0;
    float weights = 0.0, seenWeights = 0.0, allWeights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 at = min(base + offset, int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float3 light = float3(0.0);
        float seen = 0.0;
        float valid = 1.0;
        float3 moved = float3(0.0);
        float skyward = 0.0;
        for (int axis = 0; axis < 3; axis++)
        {
            int face = axis * 2 + (n[axis] < 0.0 ? 1 : 0);
            float4 texel = cubes.loadAt(int3(face * int(probes) + at.x, at.y, c * int(probes) + at.z));
            light += texel.rgb * n2[axis];
            valid = min(valid, faceHolds(texel.a));
            moved[axis] = faceMove(texel.a);
            if (Sky)
            {
                seen += skies.loadAt(int3((axis * 2 + (r[axis] < 0.0 ? 1 : 0)) * int(probes) + at.x, at.y, c * int(probes) + at.z)).x * r2[axis];
                skyward += skies.loadAt(int3(face * int(probes) + at.x, at.y, c * int(probes) + at.z)).x * n2[axis];
            }
        }
        float3 probe = oc.xyz + (float3(at) + 0.5 + moved) * (oc.w * spacing);
        if (At)
            light = towardSurface(cubesAt, light, n, n2, at, c, probes, dot(probe - p, n), oc.w * spacing);
        float weight = lerps.x * lerps.y * lerps.z * valid * probeFacing(p, n, probe);
        float sees = probeSees(reach, p, n, probe, at, c, probes, oc.w * spacing);
        seenSum += light * weight * sees;
        seenWeights += weight * sees;
        allWeights += weight;
        weight *= lerp(0.05, 1.0, sees);
        if (Sky)
        {
            light *= lerp(1.0 - saturate(skyward), 1.0, sees);
            seen *= sees;
        }
        sum += light * weight;
        skySum += seen * weight;
        weights += weight;
    }
    sky = weights > 1e-5 ? skySum / weights : 1.0;
    float3 blended = weights > 1e-5 ? sum / weights : float3(0.0);
    if (Sky && seenWeights > 1e-5)
        return lerp(blended, seenSum / seenWeights, saturate(seenWeights / max(0.05 * allWeights, 1e-5)));
    return blended;
}

// The share of the sky a surface at p facing n sees along r, the share its probes' rays reached
// the sky by (gi_ambient.slang), blended between the eight probes around it as bouncedAt blends
// their light, the three faces r looks toward weighed by the square of r along each, or the whole
// sky where no cascade of probes holds p.
public float skyAt<V : IVolume>(V cubes, V skies, V reach, SceneFieldInfo info, float3 p, float3 n, float3 r, float probes, float spacing, float cascades)
{
    int c = fieldCascade(info, p, 1.0, int(cascades));
    if (c < 0 || c >= int(cascades))
        return 1.0;
    float4 oc = cascadeAt(info, c, int(cascades));
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
public float3 bouncedSeenAt<V : IVolume>(V field, SceneFieldInfo info, SceneFieldInfo gathered, V cubes, V cubesAt, float3 p, float3 n, float probes,
    float spacing, float cascades)
{
    int c = fieldCascade(gathered, p, 1.0, int(cascades));
    if (c < 0 || c >= int(cascades))
        return float3(0.0);
    float4 seen = seenSumIn<V, true>(field, info, gathered, cubes, cubesAt, p, n, probes, spacing, c, int(cascades));
    return seen.w > 1e-5 ? seen.rgb / seen.w : float3(0.0);
}

// A probe's faces at a hit moved toward their light at the surfaces they are read for
// (gi_ambient.slang), by how far the probe lies ahead of the hit along its normal against how far
// the faces' rays went to theirs, a face whose ray met none keeping its own light. A probe behind
// the hit's plane keeps its faces' light, so its faces at the surface go unread. A hit a spacing or
// more in front of the faces' surface lies on another surface than theirs and keeps their own light
// too. Beside an arch, where the probes' rays along the faces' opposite way went through it to a
// wall 10 to 14 units beyond, whose light there read 3.4 to 4.9 times the faces' own, a hit on the
// arch's pier 1.85 ahead of a probe took a fifth of that light.
float3 towardSurface<V : IVolume>(V cubesAt, float3 light, float3 n, float3 n2, int3 at, int c, float probes, float ahead, float apart)
{
    if (ahead <= 0.0)
        return light;
    float3 lightAt = float3(0.0);
    float h = 0.0;
    for (int axis = 0; axis < 3; axis++)
    {
        int face = axis * 2 + (n[axis] < 0.0 ? 1 : 0);
        float4 texelAt = cubesAt.loadAt(int3(face * int(probes) + at.x, at.y, c * int(probes) + at.z));
        lightAt += texelAt.rgb * n2[axis];
        h += texelAt.a * n2[axis];
    }
    return lerp(light, lightAt, h > 1e-3 ? saturate(ahead / h) * saturate(1.0 - (h - ahead) / apart) : 0.0);
}

// The sum bouncedSeenAt divides for cascade c, the light of the probes seen times their weights,
// and the weights, which a reader that takes another light where no probe is seen, or blends two
// cascades, tells apart from dark. gathered places the `cascades` cascades of probes (cascadeAt).
public float4 bouncedSeenSum<V : IVolume>(V field, SceneFieldInfo info, SceneFieldInfo gathered, V cubes, float3 p, float3 n, float probes, float spacing,
    int c, int cascades)
{
    return seenSumIn<V, false>(field, info, gathered, cubes, cubes, p, n, probes, spacing, c, cascades);
}

float4 seenSumIn<V : IVolume, let At : bool>(V field, SceneFieldInfo info, SceneFieldInfo gathered, V cubes, V cubesAt, float3 p, float3 n, float probes,
    float spacing, int c, int cascades)
{
    float4 oc = cascadeAt(gathered, c, cascades);
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
        if (At)
            light = towardSurface(cubesAt, light, n, n2, at, c, probes, dot(probe - p, n), oc.w * spacing);
        float weight = lerps.x * lerps.y * lerps.z * probeFacing(p, n, probe);
        sum += light * weight;
        weights += weight;
    }
    return float4(sum, weights);
}

// The light a surface the field holds at p sends back along a probe's ray that met it, as shadeHit
// gives it, with the lamps that cast shadows hidden where the field stands between, so a lamp's
// light is not reflected off the far side of a wall, and where seen is set the indirect light
// reaching it from the probes it sees alone (bouncedSeenAt). The world's probes set it, since every
// surface's light comes to them. The screen's probes, whose rays are many and short, do not where
// they stand in the first two cascades, because marching to the probes at each of their hits cost
// 0.10 to 0.15 ms of global illumination's 0.3 to 0.4 on the development machine's GPU in
// shaders_cornell_box, and a closed room under a lamp held 3 levels of its light without it, where
// it held 247 before either. Past them they do where the surface is a wall thinner than a cell of
// the field there (thinOnly), since a hit weighs probes 4 to 16 units apart by a reach whose slack
// is a cell, and the far end of a closed corridor took 0.309 of the open one's light where it takes
// 0.062. Marched at every such hit, it took 0.052, and the voxel game's cherry grove, whose walls
// are blocks, took global illumination 8.8 ms at High at 1920 by 1080 where it takes 7.95. Weighed
// by the probes' reach instead, as bouncedAt does, the world's probes saved 0.03 to 0.05 ms and let
// 42.9 levels of a lamp under a closed room's floor into it where the marches hold it to 2.5. The
// model pass's reflections call shadeHit, which hides the lamps as this does and marches to no
// probe. The indirect light is taken at kept's share of it, from the faces where gathered says the
// probes stood, and own holds the light the surface sends on of what reaches it straight from the
// sun, the lights and the two brightest glow lights, with its emission but a glow light's, which a
// world probe weighs the frame before's light by (gi_ambient.slang). The probes' rays meet a
// double-sided face at its plane (fieldDistanceSheetIn), where the held field's gradient cancels
// across the sheet, so the hit's normal is the plane's (fieldNormalSheet), turned up there, the
// voxel game's crops lit as floors from the sun took 29% over a path-traced reference. The sun is
// found through sun where it holds the hit, the window's shadow map for the world's probes, and
// through the field elsewhere.
public float3 shadeProbeHit<V : IVolume, S : ISheetVolume, Sun : ISunShadow>(V field, S sheets, SceneFieldInfo info, SceneFieldInfo gathered, V albedo,
    Texture3D<uint> faces, V glow, GiLights lights, V cubes, V cubesAt, V reach, float3 p, float3 way, float probes, float spacing, float cascades, bool seen, float3 kept,
    Sun sun, out float3 own, bool thinOnly = false)
{
    float3 n;
    return shadeProbeHitN(field, sheets, info, gathered, albedo, faces, glow, lights, cubes, cubesAt, reach, p, way, probes, spacing, cascades, seen, kept, sun, own, n, thinOnly);
}

// The same, handing out the normal it lit the hit by, which the world's probes' trace keeps for
// each texel (gi_trace.slang).
public float3 shadeProbeHitN<V : IVolume, S : ISheetVolume, Sun : ISunShadow>(V field, S sheets, SceneFieldInfo info, SceneFieldInfo gathered, V albedo,
    Texture3D<uint> faces, V glow, GiLights lights, V cubes, V cubesAt, V reach, float3 p, float3 way, float probes, float spacing, float cascades, bool seen, float3 kept,
    Sun sun, out float3 own, out float3 n, bool thinOnly = false)
{
    n = fieldNormalSheet(sheets, info, p);
    float3 color = surfaceAlbedoFacing(faces, albedo, info, p, n);
    // Where thinOnly is set the march is made only where the surface is a wall thinner than a cell
    // of the field there, the point a cell behind it in the open, past which the probes' reach,
    // whose slack is a cell, takes a probe beyond the wall for one that sees the surface.
    bool marched = seen && (!thinOnly || fieldDistance(field, info, p - n * fieldCell(info, p)) > 0.0);
    float3 bounced = marched ? bouncedSeenAt(field, info, gathered, cubes, cubesAt, p, n, probes, spacing, cascades)
                             : bouncedHitAt(cubes, cubesAt, reach, gathered, p, n, probes, spacing, cascades);
    own = color * (directLight(field, info, lights, p, n, sun) - hiddenLampLight(field, info, lights, p, n) + glowLightsAt(field, info, lights, p, n, 2, false, way) / Pi)
        + surfaceGlowUncarried(glow, albedo, info, p);
    return own + color / Pi * bounced * (lights.counts.z * kept);
}

// The same at a face whose normal and color are given, as the device's rays find one, the indirect
// light from the probes it sees alone.
public float3 shadeProbeHitAt<V : IVolume, Sun : ISunShadow>(V field, SceneFieldInfo info, SceneFieldInfo gathered, V albedo, V glow, GiLights lights, V cubes,
    V cubesAt, float3 p, float3 n, float3 color, float3 way, float probes, float spacing, float cascades, float3 kept, Sun sun, out float3 own)
{
    float3 bounced = bouncedSeenAt(field, info, gathered, cubes, cubesAt, p, n, probes, spacing, cascades);
    own = color * (directLight(field, info, lights, p, n, sun) - hiddenLampLight(field, info, lights, p, n) + glowLightsAt(field, info, lights, p, n, 2, false, way) / Pi)
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
    V skies, V reach, float3 p, float probes, float spacing, float cascades)
{
    float3 n = fieldNormal(field, info, p);
    float3 color = surfaceAlbedoFacing(faces, albedo, info, p, n);
    float3 arrived = (directLight(field, info, lights, p, n) - hiddenLampLight(field, info, lights, p, n)) * Pi
        + bouncedAt(cubes, skies, reach, info, p, n, probes, spacing, cascades) * lights.counts.z;
    if (Glowing)
        arrived += glowLightsAt(field, info, lights, p, n, 2, false);
    return color / Pi * arrived + surfaceGlow(glow, albedo, info, p);
}
