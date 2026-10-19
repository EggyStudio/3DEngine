// The first cascade of global illumination, on the window's screen, a thread a probe. A probe
// stands on the surface the window's depth holds at the middle of each tile of pixels, and sends a
// ray along each texel of a hemisphere of directions around the surface's normal twice as far as
// the world's probes are apart (GlobalIlluminationRenderer). A ray is stepped through the depth
// while it is on the screen and through the scene's distance field from where it leaves it, and one
// that meets a surface brings back the light the surface sends along it. One that meets nothing
// brings back the light from beyond, the world's first cascade merged, blended between the world's
// probes around where it ended. The rays are summed into the light arriving at the probe's surface,
// which the model pass blends between the probes around each pixel that stand on a surface like
// the pixel's. A probe on no surface, or outside the world's first cascade, holds nothing, and the
// model pass reads the world's probes there.
import gi;
import scenefield;

[[vk::binding(0, 0)]]
Sampler2D depth;

[[vk::binding(1, 0)]]
Sampler3D sceneField;

[[vk::binding(2, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

[[vk::binding(3, 0)]]
Sampler3D fieldAlbedo;

[[vk::binding(4, 0)]]
Sampler3D fieldGlow;

[[vk::binding(5, 0)]]
ConstantBuffer<GiLights> lights;

[[vk::binding(6, 0)]]
Sampler3D bounced;

// The world's first cascade, merged, a tile of octahedron texels a probe.
[[vk::binding(7, 0)]]
Texture3D<float4> merged;

// Its second, merged, or the first again where it has one, which a probe on a surface past the
// first cascade takes the light from beyond its rays from.
[[vk::binding(12, 0)]]
Texture3D<float4> mergedSecond;

// rgb: the light arriving at each probe's surface, irradiance. a: 1 for a probe that holds it.
[[vk::binding(8, 0)]] [format("rgba16f")]
RWTexture2D<float4> irradiance;

// xyz: each probe's surface's normal. w: how far it is from the eye, or -1 for a probe on no surface.
[[vk::binding(9, 0)]] [format("rgba16f")]
RWTexture2D<float4> geometry;

[[vk::binding(10, 0)]]
ConstantBuffer<ScreenView> view;

// How much of each glow light each probe's surface sees, a byte a light from none to all of it, four
// lights a word, and the light of those too faint to be marched to by their faces, in the probe's
// twenty words (GlowSeenWords), which gi_screen_glow.slang takes the window's light from the probes
// around each of its points by, so the model pass marches to no light itself where a probe stands.
[[vk::binding(13, 0)]]
RWStructuredBuffer<uint> glowSeen;

// Each world probe's reach, as gi_ambient.slang left it, which weighs the light a surface a ray
// meets takes from the probes around it.
[[vk::binding(11, 0)]]
Sampler3D reach;

// Where the one probe gi.screenprobe asks about stands and what each of its rays found, which the
// CPU reads back. Four values hold its point and the field's cell there, its normal and which of the
// world's cascades its rays reach beyond into, the point its rays start from, and its light with the
// rays' weights' sum, and four more each ray, its light and where it was met, 0 on the screen, 1 in
// the field and 2 beyond its reach, its way and weight, the point it met or reached and how far, and
// the light that point sends on without the indirect light.
[[vk::binding(14, 0)]]
RWStructuredBuffer<float4> rayLog;

// The way each cell's nearest face looks, which a hit takes its color from cells by.
[[vk::binding(15, 0)]]
Texture3D<uint> fieldFaces;

struct ScreenPush
{
    // x: the world's first cascade's octahedron's texels along each side. y: its second's, or 0
    // where it has none. z: one more than the probe whose rays are written to rayLog, 0 for none.
    // w: unused.
    uint4 texels;
};

[[vk::push_constant]]
ConstantBuffer<ScreenPush> push;

// The hemisphere's texels along each side, sixteen rays a probe.
static const uint Rays = 4;

ScreenCamera camera()
{
    ScreenCamera c;
    c.viewProjection = view.viewProjection;
    c.inverseViewProjection = view.inverseViewProjection;
    c.eye = view.eye.xyz;
    return c;
}

float3 worldAt(float2 uv, float d)
{
    return screenWorld(camera(), uv, d);
}

// The way the surface at p runs along an axis of the depth, `texel` long, toward the side whose
// next two texels go on in a line from p, so a probe at the foot of a wall takes its floor's way
// and not the way up the wall, and away from the depth's edge. Toward the nearer texel, a floor
// seen at a slant took the wall's, and its probe, its normal tilted, saw a lamp past the wall that
// the floor does not.
float3 wayAlong(float2 uv, float3 p, float2 texel)
{
    float3 ahead = worldAt(uv + texel, depth.SampleLevel(uv + texel, 0).r);
    float3 further = worldAt(uv + texel * 2.0, depth.SampleLevel(uv + texel * 2.0, 0).r);
    float3 behind = worldAt(uv - texel, depth.SampleLevel(uv - texel, 0).r);
    float3 beyond = worldAt(uv - texel * 2.0, depth.SampleLevel(uv - texel * 2.0, 0).r);
    float3 aheadMiss = 2.0 * ahead - further - p, behindMiss = 2.0 * behind - beyond - p;
    bool aheadIn = all(uv + texel * 2.0 < float2(1.0)), behindIn = all(uv - texel * 2.0 > float2(0.0));
    return aheadIn && (!behindIn || dot(aheadMiss, aheadMiss) <= dot(behindMiss, behindMiss)) ? ahead - p : p - behind;
}

// A direction of the hemisphere around +z for a point of the square, the octahedron's upper half
// unfolded onto it, its length before it was made one in w.
float4 hemisphere(float2 uv)
{
    float2 a = uv * 2.0 - 1.0;
    float x = (a.x + a.y) * 0.5, y = (a.x - a.y) * 0.5;
    float3 d = float3(x, y, 1.0 - abs(x) - abs(y));
    return float4(normalize(d), length(d));
}

// The eight probes of the world's cascade c around p, the one nearest the field's origin first, and
// how far p lies from it toward the last, in probes, or false where it lies outside the cascade.
bool probesAround(float3 p, uint c, out int3 base, out float3 f)
{
    float4 oc = fieldInfo.cascades[c].originAndCell;
    float probes = view.trace.y;
    float3 q = (p - oc.xyz) / (oc.w * view.trace.z) - 0.5;
    base = int3(floor(q));
    f = q - float3(base);
    return oc.w > 0.0 && all(q >= -0.5) && all(q <= probes - 0.5);
}

// Where the probe of cascade c at `at` stands, its spot in the grid moved as its faces say.
float3 probeAt(int3 at, uint c)
{
    float4 oc = fieldInfo.cascades[c].originAndCell;
    int probes = int(view.trace.y);
    float3 moved = float3(0.0);
    for (int axis = 0; axis < 3; axis++)
        moved[axis] = faceMove(bounced.loadAt(int3(axis * 2 * probes + at.x, at.y, int(c) * probes + at.z)).a);
    return oc.xyz + (float3(at) + 0.5 + moved) * (oc.w * view.trace.z);
}

// The texel of a cascade's octahedron of `texels` along each side at t, which may lie a texel past
// the square's edge. Across an edge the octahedron folds onto the same edge mirrored, and past a
// corner onto the corner across from it, so a ray near the edge is blended with the texels beside it
// on the sphere.
int2 folded(int2 t, int texels)
{
    if (t.x < 0)
        t = int2(0, texels - 1 - t.y);
    else if (t.x >= texels)
        t = int2(texels - 1, texels - 1 - t.y);
    if (t.y < 0)
        t = int2(texels - 1 - t.x, 0);
    else if (t.y >= texels)
        t = int2(texels - 1 - t.x, texels - 1);
    return t;
}

// How much the probe of cascade c at `at`, standing at probe, sees of the point q, 1 where q lies
// nearer it than its rays reached that way the frame before, falling to 0 over a quarter of the
// probes' spacing past it, narrower than probeSees takes for a surface since q lies where a ray
// passed and a probe beyond a wall half the spacing thick should see none of it.
float probeReaches(float3 q, float3 probe, int3 at, uint c, float apart)
{
    float3 toward = q - probe;
    float d = length(toward);
    if (d < 1e-4)
        return 1.0;
    int2 texel = min(int2(octahedron(toward / d) * float(ReachTexels)), int2(ReachTexels - 1));
    float reached = reach.loadAt(int3(at.xy * ReachTexels + texel, int(c) * int(view.trace.y) + at.z)).r;
    float slack = 0.25 * apart;
    return saturate((reached + slack - d) / slack);
}

// The light from beyond a ray's reach along way, the world's merged light blended between the
// eight probes of the finest cascade around the point the ray reached, each weighed by how near it
// is and by whether its rays reached the point the ray passed half their spacing before its end
// the frame before (probeReaches), a probe inside a mesh passed over and one that reaches it not
// kept only where none does, or blended around the point the ray left from where its end lies past
// both cascades. Read around the point on the surface the ray left from, the probes there stood
// 1.2 units off, or 2.4 in the second cascade, and saw along the ray's way what the ray itself
// passed by, so in a Cornell box the rays of a probe on the small block's front that met nothing
// in reach read 97% over the reference across the ways they stand for, and read here 3% over
// (gi.screenprobe). A probe beyond a wall that reaches the point by the slack probeSees allows a
// surface lent its dark to rays ending inside a closed emissive box, 11 to 38% under its walls'
// light, and kept at a twentieth of its weight where unseen, the sky to a pillar's face in a closed
// room. A texel of the second cascade whose ray met a surface before its interval began holds that
// surface's light, alpha a half, which here is the light along way from that probe, taken as any
// other. Each probe's light along way is blended between the four texels of its octahedron around
// it, as near as way lies to each. Read from the texel way falls in, a ray on the border between two
// flipped from one to the other as the normal rebuilt from the window's depth wavered by a degree
// or two, and on a wall square to the world's axes half the sixteen rays lie on such borders, so a
// probe's light changed as the camera moved, which the frame before's light averaged while it moved
// and drifted from for a dozen frames after it stopped, 0.19 levels on the fifth frame after a walk
// where it reads 0.07 (GlobalIlluminationTests).
float3 beyond(float3 start, float3 normal, float3 way, float reached, uint around)
{
    float3 end = start + way * reached;
    uint c = 0u;
    int3 base;
    float3 f;
    if (!probesAround(end, 0u, base, f))
    {
        c = 1u;
        if (push.texels.y == 0u || !probesAround(end, 1u, base, f))
        {
            end = start;
            reached = 0.0;
            c = around;
            probesAround(end, c, base, f);
        }
    }
    float probes = view.trace.y;
    float apart = fieldInfo.cascades[c].originAndCell.w * view.trace.z;
    // Where the ray passed half the probes' spacing before its end, or, read around its start,
    // a point before the surface.
    float3 passed = reached > 0.0 ? end - way * min(0.5 * apart, reached * 0.5) : end + normal * (0.3 * apart);
    uint texels = c == 0u ? push.texels.x : push.texels.y;
    float2 o = octahedron(way) * float(texels) - 0.5;
    int2 first = int2(floor(o));
    float2 between = o - float2(first);
    int2 texel[4];
    float share[4];
    for (int k = 0; k < 4; k++)
    {
        int2 side = int2(k & 1, k >> 1);
        texel[k] = folded(first + side, int(texels));
        float2 lerps = lerp(1.0 - between, between, float2(side));
        share[k] = lerps.x * lerps.y;
    }
    float3 sum = float3(0.0), seenSum = float3(0.0);
    float weights = 0.0, seenWeights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 neighbor = clamp(base + offset, int3(0), int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float weight = lerps.x * lerps.y * lerps.z;
        if (weight <= 0.0)
            continue;
        float sees = probeReaches(passed, probeAt(neighbor, c), neighbor, c, apart);
        for (int k = 0; k < 4; k++)
        {
            int4 at = int4(neighbor.xy * int(texels) + texel[k], neighbor.z, 0);
            float4 light = c == 0u ? merged.Load(at) : mergedSecond.Load(at);
            float held = weight * share[k] * (light.a > 0.25 ? 1.0 : 0.0);
            sum += light.rgb * held;
            weights += held;
            seenSum += light.rgb * held * sees;
            seenWeights += held * sees;
        }
    }
    if (seenWeights > 1e-3 * weights)
        return seenSum / seenWeights;
    return weights > 1e-4 ? sum / weights : float3(0.0);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= uint(view.grid.y) || id.y >= uint(view.grid.z))
        return;
    // The middle of the depth's texel under the middle of the tile, since a point between two
    // texels is read as either by one device or another.
    float2 pixel = min((float2(id.xy) + 0.5) * view.grid.x, view.sizes.zw - 0.5);
    float2 uv = (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    float d = depth.SampleLevel(uv, 0).r;
    uint probe = id.y * uint(view.grid.y) + id.x;
    int glows = min(int(lights.glowCounts.x), MostGlowLights);
    if (d >= 1.0)
    {
        for (int word = 0; word < GlowSeenWords; word++)
            glowSeen[probe * uint(GlowSeenWords) + uint(word)] = 0u;
        irradiance[id.xy] = float4(0.0);
        geometry[id.xy] = float4(0.0, 0.0, 0.0, -1.0);
        return;
    }

    // The surface's normal from the nearer of the texels on either side along each axis, toward the eye.
    float3 p = worldAt(uv, d);
    float2 texel = 1.0 / view.sizes.xy;
    float3 right = worldAt(uv + float2(texel.x, 0), depth.SampleLevel(uv + float2(texel.x, 0), 0).r) - p;
    float3 left = p - worldAt(uv - float2(texel.x, 0), depth.SampleLevel(uv - float2(texel.x, 0), 0).r);
    float3 down = worldAt(uv + float2(0, texel.y), depth.SampleLevel(uv + float2(0, texel.y), 0).r) - p;
    float3 up = p - worldAt(uv - float2(0, texel.y), depth.SampleLevel(uv - float2(0, texel.y), 0).r);
    float3 n = normalize(cross(dot(right, right) < dot(left, left) ? right : left, dot(down, down) < dot(up, up) ? down : up));
    if (dot(n, view.eye.xyz - p) < 0.0)
        n = -n;
    geometry[id.xy] = float4(n, distance(view.eye.xyz, p));
    // How much of each glow light in reach the surface sees, a word of four lights at a time, each
    // put in the buffer as it is made, since lavapipe keeps an array indexed by the light in memory
    // of its own and crashed on the one the words were gathered in. A light that gives the surface
    // an eighth of the glow lights' light or more is marched to by the halves of its faces, and its
    // byte says how much of it the surface sees. A fainter one is marched to at its middle alone,
    // as a ray's hit is, its light added to the fainter ones' at the probe and its byte left none,
    // so the model pass reads the light of at most eight at a probe. Every light marched to by its
    // faces and read at each pixel, a room of 64 lamps at 1280 by 720 on the development machine's
    // GPU cost global illumination 5.3 ms and the model pass 2.3 more than none, where so they cost
    // 1.8 and 0.3. Past a sixteenth global illumination cost a room of sixteen lamps eleven blocks
    // square 0.89 ms, where past an eighth 0.68. A light falls to the probes' sum at one probe and
    // not the next where it is far from being the brightest, where its light changes little from
    // probe to probe; split by the light's distance from the probe instead, two of a block's half
    // diagonals, the floor beside a lamp read 0.80 of the path-traced reference where the split
    // fell, a probe's own light standing in for the pixel's on the far side alone. The glow lights
    // take the surface's normal from its way along each axis of the depth, which holds at a wall's
    // foot where the nearer texel's does not. Tilted up the wall, a probe's normal on the floor
    // beside it saw a lamp past the wall that the floor does not. Global illumination keeps the
    // nearer texel's, which put the thin room 0.3 to 0.9% nearer its path-traced reference.
    float3 gn = normalize(cross(wayAlong(uv, p, float2(texel.x, 0.0)), wayAlong(uv, p, float2(0.0, texel.y))));
    if (dot(gn, view.eye.xyz - p) < 0.0)
        gn = -gn;
    float glowCell = fieldCell(fieldInfo, p);
    float glowMargin = glowCell * 0.5;
    float glowTotal = 0.0;
    for (int i = 0; i < glows; i++)
    {
        float faded = glowLightFade(lights.glows[i], p, gn);
        if (faded > 0.0)
            glowTotal += luminance(glowLightIrradiance(lights.glows[i], p, gn, glowMargin)) * faded;
    }
    // Which share of each light the surface sees is found from a point of it moved about the tile
    // from frame to frame, the light's tier from the probe's own, so the window's light that
    // gi_screen_glow.slang takes from the probes around a point, held over the frames, is the same
    // wherever the window's grid lies over the surface. Marched to from the tile's middle alone, a
    // turn of a few pixels moved the floor's light across a glow light's penumbra by up to nine
    // tenths of it, as the grid moved over the floor.
    float2 moved = clamp(pixel + view.jitter.xy, 0.5, view.sizes.zw - 0.5);
    float2 movedUv = (floor(moved / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    float movedDepth = depth.SampleLevel(movedUv, 0).r;
    float3 q = movedDepth < 1.0 ? worldAt(movedUv, movedDepth) : p;
    // The point is kept on the probe's plane with the depth halfway to it, so a point on the floor
    // past a wall the probe stands before, on the same plane, is not taken.
    float2 halfway = (floor((pixel + moved) * 0.5 / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    float3 middle = worldAt(halfway, depth.SampleLevel(halfway, 0).r);
    float plane = 0.02 * distance(view.eye.xyz, p) + 0.05;
    if (abs(dot(q - p, gn)) > plane || abs(dot(middle - p, gn)) > plane)
        q = p;
    float3 faint = float3(0.0);
    uint first = probe * uint(GlowSeenWords);
    for (int word = 0; word * 4 < glows; word++)
    {
        uint levels = 0u;
        for (int j = 0; j < 4 && word * 4 + j < glows; j++)
        {
            GlowLight light = lights.glows[word * 4 + j];
            float faded = glowLightFade(light, p, gn);
            if (faded <= 0.0)
                continue;
            float3 arrived = glowLightIrradiance(light, p, gn, glowMargin) * faded;
            if (luminance(arrived) * 8.0 >= glowTotal)
                levels |= uint(glowLightShown(sceneField, fieldInfo, light, q, gn, glowCell) * 255.0 + 0.5) << uint(j * 8);
            else
                faint += arrived * glowLightSeen(sceneField, fieldInfo, light, q, gn, glowCell);
        }
        glowSeen[first + uint(word)] = levels;
    }
    glowSeen[first + 16u] = asuint(faint.r);
    glowSeen[first + 17u] = asuint(faint.g);
    glowSeen[first + 18u] = asuint(faint.b);
    // The world's first cascade's probes give the light from beyond the rays, or past it its second's,
    // so the screen's probes reach as far as the second cascade does. Ending at the first, a surface
    // past it took the world's probes alone, and a step showed along that edge, as a slanted ceiling
    // drew it in stairs.
    int3 base;
    float3 f;
    uint around = 0u;
    if (!probesAround(p, 0u, base, f))
    {
        around = 1u;
        if (push.texels.y == 0u || !probesAround(p, 1u, base, f))
        {
            irradiance[id.xy] = float4(0.0);
            return;
        }
    }

    float3 side = abs(n.y) < 0.99 ? normalize(cross(n, float3(0, 1, 0))) : float3(1, 0, 0);
    float3 along = cross(n, side);
    float cell = max(fieldCell(fieldInfo, p), 1e-3);
    float3 start = p + n * (cell * 0.5);
    // How far each ray goes before the light beyond it is read, twice as far as the world's first
    // cascade's probes are apart wherever it ends. Half as far past the first cascade, as it went
    // to keep a closed room near the field's side behind the eye from the sky's light, 0.0037 of it
    // where 0.0007, before the light beyond was read where a ray ends, a probe in the second
    // cascade, whose probes stand 2.4 units apart and mark what they meet nearer than that as met
    // before their interval began, read the light beyond 1.2 units out, from the probe over the
    // Cornell box's tall block for the back corner's rays, its lamp-lit top among it, the corner's
    // screen probe 86% over the reference across its hemisphere; reaching 2.4 it reads 5% over,
    // and the closed room keeps 0.0004 of the sky's light at High and 0.0003 at Low, as it did
    // (gi.screenprobe, CascadeEdgeTests).
    float length = view.trace.x;
    float3 sum = float3(0.0);
    float weights = 0.0;
    bool logged = push.texels.z == probe + 1u;
    if (logged)
    {
        rayLog[0] = float4(p, cell);
        rayLog[1] = float4(n, float(around));
    }
    for (uint v = 0u; v < Rays; v++)
        for (uint u = 0u; u < Rays; u++)
        {
            float4 local = hemisphere((float2(u, v) + 0.5) / float(Rays));
            float3 way = normalize(side * local.x + along * local.y + n * local.z);
            // How squarely the ray falls on the surface times the share of the hemisphere its
            // texel spans, one over the cube of its octahedron point's distance, as the gather of
            // the world's probes weighs its texels.
            float weight = local.z / (local.w * local.w * local.w);
            float3 light;
            float3 hit;
            float leftAt;
            // What the surface sends on without the indirect light, which the screen's probes
            // keep no account of.
            float3 own;
            float met = 0.0;
            float3 reached = start + way * length;
            own = float3(0.0);
            if (traceScreen(depth, camera(), start, way, length, 12, hit, leftAt))
            {
                light = shadeProbeHit(sceneField, fieldInfo, fieldInfo, fieldAlbedo, fieldFaces, fieldGlow, lights, bounced, reach, hit, way, view.trace.y, view.trace.z, view.trace.w, false, float3(1.0), own);
                reached = hit;
            }
            else
            {
                float t = leftAt < length ? traceField(sceneField, fieldInfo, start, way, leftAt, length) : -1.0;
                met = t >= 0.0 ? 1.0 : 2.0;
                if (t >= 0.0)
                    reached = start + way * t;
                light = t >= 0.0
                    ? shadeProbeHit(sceneField, fieldInfo, fieldInfo, fieldAlbedo, fieldFaces, fieldGlow, lights, bounced, reach, start + way * t, way, view.trace.y, view.trace.z, view.trace.w, false, float3(1.0), own)
                    : beyond(start, n, way, length, around);
            }
            sum += light * weight;
            weights += weight;
            if (logged)
            {
                uint at = 4u + (v * Rays + u) * 4u;
                rayLog[at] = float4(light, met);
                rayLog[at + 1u] = float4(way, weight);
                rayLog[at + 2u] = float4(reached, distance(start, reached));
                rayLog[at + 3u] = float4(own, 0.0);
            }
        }
    if (logged)
    {
        rayLog[2] = float4(start, 0.0);
        rayLog[3] = float4(sum * (Pi / max(weights, 1e-6)), weights);
    }
    // The weights scaled to sum to pi, the light reaching the surface under a sky of 1, where an
    // equal share for every texel read it at 1.04 of that. The glow lights' light straight to the
    // surface, which the rays that meet one leave out, the model pass takes at each pixel.
    irradiance[id.xy] = float4(sum * (Pi / max(weights, 1e-6)), 1.0);
}
