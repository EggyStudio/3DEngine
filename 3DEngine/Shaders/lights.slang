// How the model pass reads its lights' set, each light's arrival and the shadow maps that cut it,
// the metallic-roughness model's specular, the probes and the environment, and global illumination
// and a glossy surface's traced reflection. Imported by modelpass alone, so a shader that
// imports modelpass sees the set and not these.
module lights;

__exported import lightset;
import scenefield;
import gi;
import color;

// Whether this build of the model pass reads global illumination and a glossy surface's traced
// reflection, which it leaves out where it is compiled with NO_BOUNCE, as ModelRenderer draws a
// frame without global illumination, and whether it shades the glow lights, which it leaves out
// where it is compiled with NO_GLOW_LIGHTS or NO_BOUNCE, as it draws a frame that has none. Their
// marches are half of its fragment stage, global illumination most of the rest, and lavapipe
// compiles each pipeline's variants as it first draws with them.
#if defined(NO_BOUNCE)
public static const bool Bouncing = false;
#else
public static const bool Bouncing = true;
#endif
#if defined(NO_GLOW_LIGHTS) || defined(NO_BOUNCE)
public static const bool Glowing = false;
#else
public static const bool Glowing = true;
#endif

// LightKind's values.
public static const int Directional = 0;
public static const int Point = 1;
public static const int Spot = 2;
public static const int Ambient = 3;

// Light arriving at a point with a normal from one light, by Lambert's cosine, divided by the
// square of the distance for a point or a spot, brought smoothly to nothing at its range, and cut
// to a spot's cone.
public float3 arriving(Light light, float3 world, float3 n)
{
    int kind = int(light.positionAndKind.w);
    float3 color = light.colorAndShadow.xyz;
    if (kind == Ambient)
        return color;
    if (kind == Directional)
        return color * max(dot(n, -light.directionAndRange.xyz), 0.0);

    float3 toLight = light.positionAndKind.xyz - world;
    float distanceSquared = max(dot(toLight, toLight), 1e-4);
    float3 l = toLight * rsqrt(distanceSquared);
    float result = max(dot(n, l), 0.0) / distanceSquared;

    float range = light.directionAndRange.w;
    if (range > 0.0)
    {
        // Full near the light and nothing at the range, without the step a plain cut leaves.
        float edge = saturate(1.0 - pow(distanceSquared / (range * range), 2.0));
        result *= edge * edge;
    }

    if (kind == Spot)
        result *= smoothstep(light.cone.y, max(light.cone.x, light.cone.y + 1e-4), dot(-l, light.directionAndRange.xyz));
    return color * result;
}

// How much of the shadowed light reaches this point through cascade c, from 0 to 1, or -1 when the
// cascade's tile does not hold it, or the point lies before its near plane, where its casters are
// clamped onto that plane, so the next cascade, reaching farther toward the light, reads it. Each
// counted cascade is a quarter of the map, two tiles on a side, and the fill after them, c being
// the count, a map of its own. The point is moved off its surface along the normal by a texel and a
// half of that cascade before it is compared, so a surface does not shadow itself where the map's
// texels slant across it, and nine depths around it are compared and averaged, which softens the
// stair steps of the texels into a gradient one texel wide. A point a little inside a tile's edge
// counts as outside, so the nine never reach into a neighboring tile. edge says how far toward the
// tile's edge the point is, from 0 in its middle to 1 at that border.
public float cascadeReached(int c, float3 world, float3 n, float2 texel, out float edge)
{
    edge = 1.0;
    float4 clip = mul(lighting.shadowCascades[c], float4(world + n * lighting.shadowTexels[c] * 1.5, 1.0));
    float3 p = clip.xyz / clip.w;
    if (any(abs(p.xy) > 0.995) || p.z > 1.0 || p.z < 0.0)
        return -1.0;

    edge = max(abs(p.x), abs(p.y)) / 0.995;
    bool fill = c >= lighting.cascadeCount;
    if (fill)
    {
        uint width, height;
        shadowFillMap.GetDimensions(width, height);
        texel = 1.0 / float2(width, height);
    }
    float2 uv = fill ? p.xy * 0.5 + 0.5 : (p.xy * 0.5 + 0.5 + float2(c % 2, c / 2)) * 0.5;
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            float2 at = uv + float2(x, y) * texel;
            float depth = fill ? shadowFillMap.SampleLevel(lightNearest, at, 0.0).r : shadowMap.SampleLevel(lightNearest, at, 0.0).r;
            reached += p.z - 0.0005 <= depth ? 1.0 : 0.0;
        }
    return reached / 9.0;
}

// How many cascades the shadowed light's shadow is read through, the counted ones and the fill
// past them where the buffer carries one.
public int shadowCascadesRead()
{
    int counted = lighting.cascadeCount;
    return counted < 4 && lighting.shadowTexels[counted] > 0.0 ? counted + 1 : counted;
}

// How much of the shadowed light reaches this point, from 0 to 1, from the nearest cascade whose
// tile holds it. Across the outer fifth of a tile the next cascade is read too and blended in, so
// where one cascade gives way to the next the shadow's softness changes over a band rather than in
// a step, the last counted cascade giving way to the fill where there is one, and past the last
// cascade's band the shadow fades out rather than stopping at a line.
public float lightReaching(float3 world, float3 n)
{
    float held;
    return lightReaching(world, n, held);
}

// The same, with how much of the point the counted cascades hold, 1 within them, falling to 0
// across the last one's edge as its shadow is let go there, and 0 past it, the fill's coarse
// texels holding none, so the contact shadows kept past the map's reach go on over the fill's.
public float lightReaching(float3 world, float3 n, out float held)
{
    held = 0.0;
    uint width, height;
    shadowMap.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    int counted = lighting.cascadeCount;
    int cascades = shadowCascadesRead();
    for (int c = 0; c < cascades; c++)
    {
        float edge;
        float reached = cascadeReached(c, world, n, texel, edge);
        if (reached < 0.0)
            continue;

        held = c < counted ? 1.0 : 0.0;
        float blend = saturate((edge - 0.8) / 0.2);
        if (blend <= 0.0)
            return reached;
        float nextEdge;
        float next = c + 1 < cascades ? cascadeReached(c + 1, world, n, texel, nextEdge) : -1.0;
        if (c < counted && (next < 0.0 || c + 1 == counted))
            held = 1.0 - blend;
        return lerp(reached, next < 0.0 ? 1.0 : next, blend);
    }
    return 1.0;
}

// The light from all around a surface facing n that global illumination's probes leave to what
// lies past them, past the field's cascades, as a sky over a ground, the sky's light from above,
// `sky`, on the share of the sphere above the surface's horizon, (1 + n.y) / 2 of it, and below its
// horizon the light the ground sends back, the ground's color times the sky's light on it, the
// ground's color taken as the surface's own (`ground`). The directional lights add their share of
// the ground's light (distantSun). Taken as the environment's irradiance along n, a wall past the
// field took the map's light below its horizon whole, and as the ambient lights' whole, all of it,
// where global illumination inside the field gives it half the sky and the ground's light.
public float3 distantAround(float3 sky, float3 n, float3 ground)
{
    float above = saturate(n.y * 0.5 + 0.5);
    return sky * (above + ground * (1.0 - above));
}

// The light the ground under a surface facing n sends it of a directional light, the ground's
// color times the light the lamp gives a floor where `reached` of it reaches the ground, on the
// share of the sphere below the surface's horizon (distantAround).
public float3 distantSun(Light lamp, float3 n, float3 ground, float reached)
{
    return ground * arriving(lamp, float3(0.0), float3(0.0, 1.0, 0.0)) * (reached * saturate(0.5 - n.y * 0.5));
}

// How much of the shadowed light reaches the ground a surface at p facing n looks at, read where
// the point moved along n by a cell of the field's coarsest built cascade lies, as a floor. Read at
// the point itself, a face turned from the sun lies behind its own mesh as the sun sees it and is
// always in shadow, so a wall with the sun behind it took none of the sunlit ground before it,
// which global illumination inside the field gives it; read there, it takes the ground lit where
// its own shadow ends short of the point and shadowed where the shadow reaches past it, as a
// hill's shaded side in its own long shadow at a low sun.
public float groundReached(float3 p, float3 n)
{
    int c = fieldCoarsestBuilt(bouncedField, int(bouncedField.counts.x) - 1);
    float cell = c < 0 ? 0.0 : bouncedField.cascades[c].originAndCell.w;
    return lightReaching(p + n * cell, float3(0.0, 1.0, 0.0));
}

// Where the shadowed spot light in slot (counted from 0) lies in the map. The last tile is the
// map's lower right quarter, split by rank as ShadowFit.SpotTileArea splits it, whole for one light,
// quarters for up to four, and past four quarters for the first two and sixteenths in its lower
// half for the rest.
public void spotTile(int slot, out float2 origin, out float size)
{
    int count = lighting.spotShadowCount;
    size = 0.5;
    origin = float2(0.5, 0.5);
    if (count > 4 && slot >= 2)
    {
        size = 0.125;
        origin = float2(0.5, 0.75) + float2((slot - 2) % 4, (slot - 2) / 4) * 0.125;
    }
    else if (count > 1)
    {
        size = 0.25;
        origin = float2(0.5, 0.5) + float2(slot % 2, slot / 2) * 0.25;
    }
}

// The face of the shadowed point light in slot (counted from 0) that a point the way away from the
// light lies furthest along the axis of, and where it lies in the point map. The lights that matter
// most have a layer to each face, and the rest a quarter of one at half the size, as
// ShadowFit.PointFaceArea lays them out, with texels twice as wide.
public int pointFace(float3 away, int slot, out int layer, out float2 origin, out float scale)
{
    float3 size = abs(away);
    int face = size.x >= size.y && size.x >= size.z ? (away.x >= 0.0 ? 0 : 1)
             : size.y >= size.z ? (away.y >= 0.0 ? 2 : 3)
             : (away.z >= 0.0 ? 4 : 5);
    int full = int(lighting.pointShadow.z);
    layer = slot * 6 + face;
    origin = float2(0.0, 0.0);
    scale = 1.0;
    if (slot >= full)
    {
        int index = (slot - full) * 6 + face;
        layer = full * 6 + index / 4;
        origin = float2(index % 4 % 2, index % 4 / 2) * 0.5;
        scale = 0.5;
    }
    return face;
}

// How much of the shadowed spot light in slot (counted from 0) at lightPosition reaches this
// point, from 0 to 1, from its square of the map's last tile, compared as the cascades are. Its
// texels widen with distance from the light, so the offset along the normal does too.
public float spotLightReaching(float3 world, float3 n, float3 lightPosition, int slot)
{
    float offset = lighting.spotShadowTexels[slot / 4][slot % 4] * distance(world, lightPosition) * 1.5;
    float4 clip = mul(lighting.spotShadows[slot], float4(world + n * offset, 1.0));
    if (clip.w <= 0.0)
        return 1.0;
    float3 p = clip.xyz / clip.w;
    if (any(abs(p.xy) > 0.995) || p.z > 1.0 || p.z < 0.0)
        return 1.0;

    uint width, height;
    shadowMap.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    float2 origin;
    float size;
    spotTile(slot, origin, size);
    // Kept a texel inside the square, so the comparisons around an edge read no other light's.
    float2 uv = clamp(origin + (p.xy * 0.5 + 0.5) * size, origin + texel, origin + size - texel);
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.00002 <= shadowMap.SampleLevel(lightNearest, uv + float2(x, y) * texel, 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// How much of the shadowed point light in slot (counted from 0) at lightPosition reaches this
// point, from 0 to 1, from the face of its six that the point lies furthest along the axis of,
// compared as the cascades are. Its texels widen with distance from the light, as a spot light's.
public float pointLightReaching(float3 world, float3 n, float3 lightPosition, int slot)
{
    float3 away = world - lightPosition;
    int layer;
    float2 origin;
    float scale;
    int face = pointFace(away, slot, layer, origin, scale);
    float offset = lighting.pointShadow.x / scale * length(away) * 1.5;
    float4 clip = mul(lighting.pointShadowFaces[slot * 6 + face], float4(world + n * offset, 1.0));
    if (clip.w <= 0.0)
        return 1.0;
    float3 p = clip.xyz / clip.w;
    if (p.z > 1.0 || p.z < 0.0)
        return 1.0;

    uint width, height, layers;
    pointShadowMap.GetDimensions(width, height, layers);
    float2 texel = 1.0 / float2(width, height);
    float2 uv = clamp(origin + (p.xy * 0.5 + 0.5) * scale, origin + texel, origin + scale - texel);
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.00002 <= pointShadowMap.SampleLevel(lightNearest, float3(uv + float2(x, y) * texel, layer), 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// The way light comes from one light to a point, for specular.
public float3 fromLight(Light light, float3 world)
{
    int kind = int(light.positionAndKind.w);
    return kind == Directional ? light.directionAndRange.xyz : normalize(world - light.positionAndKind.xyz);
}

static const float PI = 3.14159265;

// How much of a light reflects toward the eye off microfacets of this roughness, as GGX's
// distribution and Smith's height-correlated shadowing have it, with Schlick's Fresnel. Times pi,
// since a light here is given as the light a white diffuse surface facing it returns, so the
// diffuse term is the albedo itself rather than the albedo over pi.
public float3 specularReflected(float3 n, float3 v, float3 l, float3 f0, float alpha, out float3 fresnel)
{
    float3 h = normalize(v + l);
    float nl = max(dot(n, l), 1e-4);
    float nv = max(dot(n, v), 1e-4);
    float nh = max(dot(n, h), 0.0);
    float a2 = alpha * alpha;
    float d = nh * nh * (a2 - 1.0) + 1.0;
    float distribution = a2 / (PI * d * d);
    float visibility = 0.5 / (nl * sqrt(nv * nv * (1.0 - a2) + a2) + nv * sqrt(nl * nl * (1.0 - a2) + a2));
    fresnel = f0 + (1.0 - f0) * pow(1.0 - max(dot(v, h), 0.0), 5.0);
    return distribution * visibility * fresnel * PI;
}

// The light a white diffuse surface facing n returns from nine spherical harmonic coefficients in
// EnvironmentMap.Irradiance's order.
public float3 harmonics(float4 c[9], float3 n)
{
    float3 sum = c[0].xyz * 0.282095
        + c[1].xyz * (0.488603 * n.y)
        + c[2].xyz * (0.488603 * n.z)
        + c[3].xyz * (0.488603 * n.x)
        + c[4].xyz * (1.092548 * n.x * n.y)
        + c[5].xyz * (1.092548 * n.y * n.z)
        + c[6].xyz * (0.315392 * (3.0 * n.z * n.z - 1.0))
        + c[7].xyz * (1.092548 * n.x * n.z)
        + c[8].xyz * (0.546274 * (n.x * n.x - n.y * n.y));
    return max(sum, float3(0.0));
}

// A bound probe's irradiance from the buffer of its slot.
public float3 probeIrradiance(int i, float3 n)
{
    float4 c[9];
    for (int k = 0; k < 9; k++)
    {
        if (i == 0) c[k] = probeIrradiance0[k];
        else if (i == 1) c[k] = probeIrradiance1[k];
        else if (i == 2) c[k] = probeIrradiance2[k];
        else c[k] = probeIrradiance3[k];
    }
    return harmonics(c, n);
}

// The bound probe whose box holds a point, the smallest where boxes overlap, or -1. A point a
// hundredth past a wall of the box still counts, so a room's wall built on the box's wall is in it
// everywhere rather than in it and out of it pixel by pixel.
public int probeAt(float3 p)
{
    int found = -1;
    float smallest = 1e30;
    for (int i = 0; i < min(int(lighting.probeCount.x), 4); i++)
    {
        float3 h = lighting.probes[i].halfSizeAndMip.xyz;
        float3 d = abs(p - lighting.probes[i].centerAndIntensity.xyz);
        float volume = h.x * h.y * h.z;
        if (all(d <= h * 1.01 + 0.01) && volume < smallest)
        {
            found = i;
            smallest = volume;
        }
    }
    return found;
}

// A probe's cube along a direction at a mip, by its slot.
public float3 probeSample(int i, float3 direction, float mip)
{
    if (i == 0) return probeMap0.SampleLevel(lightLinear, direction, mip).rgb;
    if (i == 1) return probeMap1.SampleLevel(lightLinear, direction, mip).rgb;
    if (i == 2) return probeMap2.SampleLevel(lightLinear, direction, mip).rgb;
    return probeMap3.SampleLevel(lightLinear, direction, mip).rgb;
}

// The way into a probe's cube that a ray from p along r meets the box's wall, from the middle the
// probe was captured at, so a reflection lands where the room's wall is rather than at infinity.
public float3 boxProjected(int i, float3 p, float3 r)
{
    float3 center = lighting.probes[i].centerAndIntensity.xyz;
    float3 h = lighting.probes[i].halfSizeAndMip.xyz;
    float3 far = (center + h - p) / r;
    float3 near = (center - h - p) / r;
    float3 exits = max(far, near);
    float t = min(exits.x, min(exits.y, exits.z));
    return p + r * t - center;
}

// The light a white diffuse surface facing n returns from the environment.
public float3 environmentIrradiance(float3 n)
{
    float4 c[9];
    for (int k = 0; k < 9; k++)
        c[k] = environmentIrradianceBuffer[k];
    return harmonics(c, n);
}

// The share of light from all around that a surface reflects, with no environment to reflect,
// as Brian Karis fitted it for mobile, so a smooth or metal surface under an ambient light is not
// black.
public float3 ambientReflected(float3 f0, float roughness, float nv)
{
    float4 c0 = float4(-1.0, -0.0275, -0.572, 0.022);
    float4 c1 = float4(1.0, 0.0425, 1.04, -0.04);
    float4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * nv)) * r.x + r.y;
    float2 ab = float2(-1.04, 1.04) * a004 + r.zw;
    return f0 * ab.x + ab.y;
}

// How much of the indirect light reaching a surface at p facing n the screen's probes give rather
// than the world's, which they take the light from beyond their rays from, all of it inside the
// world's second cascade, or its first where global illumination has one, falling to none across
// its outer band (CascadeBand in gi.slang).
public float screenHold(float3 p, float3 n)
{
    return probesHold(bouncedField, p, n, lighting.indirect.y, min(1, int(lighting.indirect.w) - 1), int(lighting.indirect.w));
}

// The indirect light reaching a surface at p facing n, irradiance over pi, from the world's probes
// around it as gi.slang blends them, or -1 where no cascade of probes holds p.
public float3 bounced(float3 p, float3 n)
{
    int c = fieldCascade(bouncedField, p, 1.0, int(lighting.indirect.w));
    if (c < 0 || c >= int(lighting.indirect.w))
        return float3(-1.0);
    return bouncedAt(SplitVolume(bouncedLight, lightNearest), SplitVolume(bouncedReach, lightNearest), bouncedField, p, n, lighting.indirect.z, lighting.indirect.y, lighting.indirect.w) / 3.14159265;
}

// The light bounced gives with the share of the sky the surface sees along r as skySeen gives it,
// from one gather of the probes (bouncedAndSkyAt in gi.slang), or -1 and the whole sky where no
// cascade of probes holds p.
public float3 bouncedAndSky(float3 p, float3 n, float3 r, out float sky)
{
    sky = 1.0;
    int c = fieldCascade(bouncedField, p, 1.0, int(lighting.indirect.w));
    if (c < 0 || c >= int(lighting.indirect.w))
        return float3(-1.0);
    return bouncedAndSkyAt(SplitVolume(bouncedLight, lightNearest), SplitVolume(bouncedSky, lightNearest), SplitVolume(bouncedReach, lightNearest), bouncedField,
        p, n, r, lighting.indirect.z, lighting.indirect.y, lighting.indirect.w, sky) / 3.14159265;
}

// The indirect light reaching a surface at p facing n, irradiance over pi, from those of the world's
// probes around it it sees with nothing the field holds between (bouncedSeenAt), or from them all as
// bounced reads them where it sees none, or -1 where no cascade of probes holds p. A pixel no screen
// probe on a surface like its own stands near, as where two walls meet, reads it, since a probe
// beyond a wall thinner than the probes' spacing is seen by bounced's reach where the point it
// judges from lies in the other wall's cells, and lent the thin room's corner the lamp's light
// outside, 3.25 times the wall beside it where a path-traced reference reads 1.10, and 1.02 so.
// Marched to its probes, faded across a cascade's outer band as bounced fades, it costs those
// pixels' groups, 185 pixels of the room's 360,000 at 800 by 450, the model pass 0.07 ms on the
// development machine's GPU, 0.31 where 0.24, and the Cornell box's 0.09, 0.27 where 0.18.
public float3 bouncedSeen(float3 p, float3 n)
{
    int cascades = int(lighting.indirect.w);
    int c = fieldCascade(bouncedField, p, 1.0, cascades);
    if (c < 0 || c >= cascades)
        return float3(-1.0);
    // Faded to the next cascade across this one's outer band, as bouncedAt fades, since read from
    // one cascade alone a far floor that no screen probe like it holds stepped by 10% where the
    // third cascade ended (CascadeEdgeTests).
    float own = c + 1 < cascades && cascadeAt(bouncedField, c + 1, cascades).w > 0.0 ? cascadeHold(bouncedField, c, p, cascades) : 1.0;
    float3 light = float3(0.0);
    [loop]
    for (int k = 0; k < 2; k++)
    {
        float share = k == 0 ? own : 1.0 - own;
        if (share <= 0.0)
            continue;
        float4 seen = bouncedSeenSum(SplitVolume(reflectField, lightLinear), bouncedField, bouncedField, SplitVolume(bouncedLight, lightNearest), p, n,
            lighting.indirect.z, lighting.indirect.y, c + k, cascades);
        if (seen.w <= 1e-5)
            return bounced(p, n);
        light += seen.rgb / seen.w * share;
    }
    return light / 3.14159265;
}

// The light the glow lights send a surface at p facing n straight, irradiance over pi, which no
// probe above a floor holds of a lamp lying on it, the four brightest at p marched to, with halves
// by their faces' halves as the screen's probes march, or else to their middles (glowLightsAt in
// gi.slang).
public float3 glowLights(float3 p, float3 n, bool halves)
{
    return glowLightsAt(SplitVolume(reflectField, lightLinear), bouncedField, reflectLights, p, n, 4, halves) / 3.14159265;
}

// The share of the sky a surface at p facing n sees along r, as the probes around it saw it, or the
// whole sky where no cascade of probes holds p.
public float skySeen(float3 p, float3 n, float3 r)
{
    return skyAt(SplitVolume(bouncedLight, lightNearest), SplitVolume(bouncedSky, lightNearest), SplitVolume(bouncedReach, lightNearest), bouncedField,
        p, n, r, lighting.indirect.z, lighting.indirect.y, lighting.indirect.w);
}

// The light a glossy surface at p facing n reflects along r, traced through the window's depth and
// past it through the scene's distance field, with in a how much of it is known, 1 where a ray met
// a surface, faded at the picture's edge where the frame before's picture gives its light, and 0
// where the ray met nothing, or the surface is too rough to trace for, where the probes and the
// environment reflect alone. A surface met on the screen reflects the light the frame before
// showed where it was, and one past the picture or hidden from it its color times the light
// reaching it, as global illumination shades a hit.
public float4 tracedReflection(float3 p, float3 n, float3 r, float roughness, float3 eye)
{
    if (!Bouncing || lighting.reflection.x <= 0.0 || roughness >= lighting.reflection.y)
        return float4(0.0);
    float probes = lighting.indirect.z, spacing = lighting.indirect.y, cascades = lighting.indirect.w;
    float cell = max(fieldCell(bouncedField, p), 0.02);
    float3 start = p + n * cell;
    ScreenCamera camera;
    camera.viewProjection = lighting.reflectViewProjection;
    camera.inverseViewProjection = lighting.reflectInverseViewProjection;
    camera.eye = eye;
    float3 hit;
    float left;
    // The surface met, on the screen or past it in the field, shaded once, since each shading of a
    // hit holds the glow lights' marches, and the model pass compiled two of them in a second more
    // on lavapipe for each app.
    bool onScreen = traceScreen(SplitPicture(reflectDepth, lightNearest), camera, start, r, lighting.reflectionReach.x, int(lighting.reflection.z), hit, left);
    if (!onScreen)
    {
        float t = traceField(SplitVolume(reflectField, lightLinear), bouncedField, start, r, left, 1e5);
        if (t < 0.0)
        {
#if defined(RAY_QUERY)
            // What the field misses, past its cascades or too thin for its cells, the GPU's own rays find.
            if (lighting.reflectionReach.y > 0.0)
                return rayReflection(start, r);
#endif
            return float4(0.0);
        }
        hit = start + r * t;
    }
    float3 shaded = shadeHit<SplitVolume, Glowing>(SplitVolume(reflectField, lightLinear), bouncedField, SplitVolume(reflectAlbedo, lightLinear), reflectFaces, SplitVolume(reflectGlow, lightLinear), reflectLights, SplitVolume(bouncedLight, lightLinear), SplitVolume(bouncedReach, lightNearest), hit, probes, spacing, cascades);
    if (!onScreen)
        return float4(shaded, 1.0);
    float4 last = mul(lighting.reflectLastViewProjection, float4(hit, 1.0));
    float2 uv = last.xy / max(last.w, 1e-4) * 0.5 + 0.5;
    if (lighting.reflection.w <= 0.0 || last.w <= 1e-4 || any(uv < 0.0) || any(uv > 1.0))
        return float4(shaded, 1.0);
    // Whether the frame before showed this surface there, its depth holding a point near the
    // hit, or something in front of it, which the reflection would show in its place.
    ScreenCamera before;
    before.viewProjection = lighting.reflectLastViewProjection;
    before.inverseViewProjection = lighting.reflectLastInverseViewProjection;
    before.eye = eye;
    float3 then = screenWorld(before, uv, reflectHistoryDepth.SampleLevel(lightNearest, uv, 0).r);
    float near = 0.05 + 0.02 * distance(eye, hit);
    float shown = 1.0 - smoothstep(near, near * 3.0, distance(then, hit));
    // The picture's light, blurred down its mips as the surface is rough, and the field's
    // shading where the picture ends or did not show the surface.
    float edge = saturate(min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)) * 12.0);
    float3 seen = reflectHistory.SampleLevel(lightLinear, uv, saturate(roughness / lighting.reflection.y) * 4.0).rgb;
    return float4(lerp(shaded, seen, edge * shown), 1.0);
}

#if defined(RAY_QUERY)
// A corner's color, linear, as the model pass brings a vertex's color in.
float3 rayCornerColor(float4 corner)
{
    uint bits = asuint(corner.w);
    return srgbToLinear(float3(float(bits & 255u), float((bits >> 8) & 255u), float((bits >> 16) & 255u)) / 255.0);
}

// Whether a ray from p along way meets any of the window's meshes within reach.
bool rayBlocked(float3 p, float3 way, float reach = 1e4)
{
    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> query;
    RayDesc ray;
    ray.Origin = p;
    ray.Direction = way;
    ray.TMin = 0.0;
    ray.TMax = reach;
    query.TraceRayInline(rayScene, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();
    return query.CommittedStatus() == COMMITTED_TRIANGLE_HIT;
}

// The light the first of the window's meshes a ray from p along way meets sends back along it,
// through the device's ray tracing, the copy's color over pi times the sun's light where a second
// ray toward the sun gets through, the lamps', each that casts shadows where a ray toward it gets
// through, and the indirect light reaching it where a cascade of probes holds it or the light from
// all around where none does, with its emission, and a 1, or a 0 where it meets none.
float4 rayReflection(float3 p, float3 way)
{
    RayQuery<RAY_FLAG_FORCE_OPAQUE> query;
    RayDesc ray;
    ray.Origin = p;
    ray.Direction = way;
    ray.TMin = 0.0;
    ray.TMax = 1e4;
    query.TraceRayInline(rayScene, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();
    if (query.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        return float4(0.0);

    // The triangle's face, from its mesh's corners into the world, turned toward the ray.
    RaySurface surface = raySurfaces[query.CommittedInstanceID()];
    uint corner = surface.corners.x + query.CommittedPrimitiveIndex() * 3u;
    float3 a = rayCorners[corner].xyz, b = rayCorners[corner + 1u].xyz, c = rayCorners[corner + 2u].xyz;
    float3 n = normalize(mul(cross(b - a, c - a), (float3x3)query.CommittedWorldToObject3x4()));
    if (dot(n, way) > 0.0)
        n = -n;
    float3 hit = p + way * query.CommittedRayT();

    // The lamps, falling off as directLight has them, each that casts shadows hidden where a ray
    // toward it meets a mesh short of it. The loop is its own here rather than a function
    // directLight calls too, since the model pass reshaped by that, with no ray query in it,
    // crashed lavapipe at its reflections (build/mesa).
    float3 arrived = float3(0.0);
    for (int i = 0; i < min(int(reflectLights.counts.x), 16); i++)
    {
        GiLight light = reflectLights.lights[i];
        float3 toLight = light.positionAndKind.xyz - hit;
        float distance2 = max(dot(toLight, toLight), 1e-4);
        float3 toward = toLight * rsqrt(distance2);
        float reaching = max(dot(n, toward), 0.0) / distance2;
        float range = light.directionAndRange.w;
        if (range > 0.0)
        {
            float edge = saturate(1.0 - pow(distance2 / (range * range), 2.0));
            reaching *= edge * edge;
        }
        if (int(light.positionAndKind.w) == 2)
            reaching *= smoothstep(light.cone.y, max(light.cone.x, light.cone.y + 1e-4), dot(-toward, light.directionAndRange.xyz));
        if (light.colorAndShadow.w > 0.5 && reaching > 0.0 && rayBlocked(hit + n * 0.01, toward, sqrt(distance2) - 0.05))
            continue;
        arrived += light.colorAndShadow.rgb * reaching;
    }
    float3 l = reflectLights.sunDirection.xyz;
    if (reflectLights.sunDirection.w > 0.5 && dot(n, l) > 0.0 && !rayBlocked(hit + n * 0.01, l))
        arrived += reflectLights.sunColor.rgb * dot(n, l);
    // The lamps and the sun as the light reaching the face, beside the indirect light and the sky,
    // as shadeHit takes them.
    arrived *= Pi;
    // The indirect light fading to the sky's across the last cascade's outer band, as the
    // model pass takes it (bouncedHold).
    float held = bouncedHold(bouncedField, hit, n, lighting.indirect.y, lighting.indirect.w);
    if (held > 0.0)
        arrived += bouncedAt(SplitVolume(bouncedLight, lightNearest), SplitVolume(bouncedReach, lightNearest), bouncedField, hit, n, lighting.indirect.z, lighting.indirect.y, lighting.indirect.w) * held;
    arrived += (lighting.environment.z > 0.0 ? environmentIrradiance(n) * lighting.environment.x * lighting.environmentLight.rgb * Pi : reflectLights.sky.rgb * Pi) * (1.0 - held);
    // The copy's color times its mesh's vertices' colors at the hit, as the model pass blends them.
    float2 between = query.CommittedTriangleBarycentrics();
    float3 color = surface.color.rgb * (rayCornerColor(rayCorners[corner]) * (1.0 - between.x - between.y)
        + rayCornerColor(rayCorners[corner + 1u]) * between.x + rayCornerColor(rayCorners[corner + 2u]) * between.y);
    return float4(color / Pi * arrived + surface.emission.rgb, 1.0);
}
#endif
