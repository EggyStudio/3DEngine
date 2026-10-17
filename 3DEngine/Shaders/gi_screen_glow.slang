// The glow lights' light on the window's surfaces, a thread a texel of the window's depth, which
// the model pass reads in place of the screen probes' words. Each texel's point takes how much of
// each glow light it sees from the four screen probes around it, each probe's share marched from a
// point of its tile that moves from frame to frame (gi_screen.slang, JitterAt), and the light each
// sends the point in closed form, and blends that with what the texel held the frame before where
// its surface was. Over a round of the probes' points the blend is their mean, each frame counting
// alike, so a penumbra a probe's tile cuts across reads as the probes standing at every place in
// their tiles would read it, and a turn moves it with the world and not with the window's grid. The
// floor across a lamp's penumbra moves with turns of one to eight pixels at 640 by 360 by 0.45 to
// 0.94% of its light, where a pixel that marches itself moves by 0.69 to 0.91%; blended at the
// probes, the points moving with them, by 1.6 to 2.7%, and read at the tiles' middles by 3.1 to
// 7.9%.
import gi;
import scenefield;

// The window's depth, as window_depth drew it this frame, a texel a thread.
[[vk::binding(0, 0)]]
Sampler2D depth;

// Each screen probe's surface's normal and distance from the eye, as gi_screen.slang left them.
[[vk::binding(1, 0)]]
Texture2D<float4> geometry;

[[vk::binding(2, 0)]]
ConstantBuffer<ScreenView> view;

[[vk::binding(3, 0)]]
ConstantBuffer<GiLights> lights;

[[vk::binding(4, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

// How much of each glow light each probe sees, and the fainter lights' light, as gi_screen.slang
// left them (GlowSeenWords).
[[vk::binding(5, 0)]]
StructuredBuffer<uint> glowSeen;

// rgb: the share each texel's point takes of the rough unhidden light of the glow light near it that
// gives it the most of it (glowLightRough, glowLightNear), its light over it, which the model pass
// scales by its rough light at each pixel, so its light falls away from the lamp at each pixel as
// it does. a: the point's distance from the eye, or -1 for a texel on no surface. This frame's,
// which the model pass reads, and the frame before's, copied after each frame.
[[vk::binding(6, 0)]] [format("rgba16f")]
RWTexture2D<float4> glowLight;

[[vk::binding(7, 0)]]
Texture2D<float4> lastGlowLight;

// x: the furthest the frames' light has lain from the blend, in luminance. y: the frames the blend
// holds. This frame's and the frame before's, as the share's.
[[vk::binding(8, 0)]] [format("rgba16f")]
RWTexture2D<float4> glowState;

[[vk::binding(9, 0)]]
Texture2D<float4> lastGlowState;

// rgb: the light of the other glow lights, irradiance, which changes less from texel to texel and
// the model pass takes as it is. This frame's and the frame before's, as the share's. With every
// glow light's rough light summed at each pixel, the model pass cost 1.1 ms more in a room of 64
// lamps at 1280 by 720 on the development machine's GPU, and with that of the lights near each
// pixel 0.55 ms more, its loop over the lights alone.
[[vk::binding(10, 0)]] [format("rgba16f")]
RWTexture2D<float4> glowFar;

[[vk::binding(12, 0)]]
Texture2D<float4> lastGlowFar;

// xyz: each texel's surface's normal, a half and a half of it. w, in 255ths: none for a texel on no
// surface, 1 for one whose share is of no light, 2 and on for the glow light the share is of, counted
// from 2, GlowOnLight for one on a glow light's own surface, and GlowAsk for one no probe around
// holds. The model pass weighs the texels around a pixel by the normal, since at a wall's foot the
// floor's texels lie as far from the eye as the wall's and the floor beside a lamp lent the lamp's
// face its light.
[[vk::binding(11, 0)]] [format("rgba8")]
RWTexture2D<float4> glowNormal;

// The frames of a round of the probes' points (JitterAt), after which the blend counts every
// frame's light alike and may be dropped. The least share of this frame's light in the blend after
// a round where the view holds still, a thirty-second, since at a sixty-fourth what the first
// frames of an app saw before the field held the wall stayed in the blend under its shadow twice as
// long, and a turn read 150 frames on moved the floor's light along the penumbra by 0.9 to 1.8%
// where by 0.44 to 0.92%. And where the view moves, when each frame's blend is read between four
// texels of the frame before's and spreads a little further over the surface.
static const float GlowRound = 16.0;
static const float GlowStill = 1.0 / 32.0;
static const float GlowMoving = 1.0 / 8.0;

// How far this frame's light may lie from the blend before the blend is dropped, a share past the
// furthest the frames' light has lain from it, and a share of the light past that. The furthest is
// let go by GlowLetGo a frame, three quarters of it held over a round, so the furthest a round's
// points lie, met once a round, stays within GlowDeviations of it. Let go by a thirty-second a
// frame, six tenths held, the one point of a round that saw a lamp past a wall's foot dropped the
// blend each round, and a wall taken away was followed at a tenth a frame where the blend held too
// few frames to be dropped.
static const float GlowDeviations = 1.5;
static const float GlowMargin = 0.03;
static const float GlowLetGo = 1.0 / 64.0;

float3 worldAt(float2 uv, float d)
{
    float4 world = mul(view.inverseViewProjection, float4(uv * 2.0 - 1.0, d, 1.0));
    return world.xyz / world.w;
}

// The point of the depth's texel `step` texels from uv along an axis.
float3 pointAt(float2 uv, float2 step)
{
    float2 at = uv + step;
    return worldAt(at, depth.SampleLevel(at, 0).r);
}

// The way the surface at p runs along an axis of the depth, `texel` long, toward the side whose
// next two texels go on in a line from p, so a texel at the foot of a wall takes its floor's way and
// not the way up the wall, and away from the depth's edge. Toward the nearer texel, as
// gi_screen.slang found a probe's, a floor seen at a slant took the wall's, and its texels along a
// wall's foot, their normal tilted, found no probe on a like surface and took the light the wall's
// top saw.
float3 wayAlong(float2 uv, float3 p, float2 texel)
{
    float3 ahead = pointAt(uv, texel), further = pointAt(uv, texel * 2.0);
    float3 behind = pointAt(uv, -texel), beyond = pointAt(uv, -texel * 2.0);
    float3 aheadMiss = 2.0 * ahead - further - p, behindMiss = 2.0 * behind - beyond - p;
    bool aheadIn = all(uv + texel * 2.0 < float2(1.0)), behindIn = all(uv - texel * 2.0 > float2(0.0));
    return aheadIn && (!behindIn || dot(aheadMiss, aheadMiss) <= dot(behindMiss, behindMiss)) ? ahead - p : p - behind;
}

// Where a probe marched to the glow lights from this frame, in the view's share, the depth's texel
// under its tile's middle moved by the frame's jitter, as gi_screen.slang found it.
float2 marchedFrom(int2 probe)
{
    float2 pixel = min((float2(probe) + 0.5) * view.grid.x, view.sizes.zw - 0.5);
    float2 moved = clamp(pixel + view.jitter.xy, 0.5, view.sizes.zw - 0.5);
    return (floor(moved / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
}

// How alike a probe's surface is to the point p's at uv facing n, from 0 to 1: how alike its normal
// is, how near its distance from the eye is to the distance p's plane lies at along its way, within
// `alike`, and how near p's plane the depth lies a third and two thirds of the way to where it
// marched from, so a probe on the floor past a wall, on the floor's plane and seen over the wall's
// top, does not lend the floor before the wall the light it sees. None for a probe off the grid or
// on no surface; and in first the probe's first word of the glow lights.
float probeLike(int2 at, float2 uv, float3 p, float3 n, float alike, out int first)
{
    int2 grid = int2(int(view.grid.y), int(view.grid.z));
    first = (clamp(at.y, 0, grid.y - 1) * grid.x + clamp(at.x, 0, grid.x - 1)) * GlowSeenWords;
    if (any(at < int2(0)) || any(at >= grid))
        return 0.0;
    float4 surface = geometry.Load(int3(at, 0));
    if (surface.w < 0.0)
        return 0.0;
    float2 pixel = min((float2(at) + 0.5) * view.grid.x, view.sizes.zw - 0.5);
    float2 probeUv = (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    float3 way = normalize(worldAt(probeUv, 1.0) - view.eye.xyz);
    float facing = dot(way, n);
    float expected = facing < -1e-4 ? dot(p - view.eye.xyz, n) / facing : 1e20;
    float off = (surface.w - expected) / alike;
    float2 marched = marchedFrom(at);
    float between = max(abs(dot(pointAt(lerp(uv, marched, 1.0 / 3.0), float2(0.0)) - p, n)),
                        abs(dot(pointAt(lerp(uv, marched, 2.0 / 3.0), float2(0.0)) - p, n))) / alike;
    return pow(saturate(dot(n, surface.xyz)), 8.0) * exp(-off * off - between * between);
}

// The light the glow lights send p facing n this frame, irradiance, each by as much of it as the
// probes around see, blended by weight, the dominant light's as near p as it is (glowLightNear)
// given back and the rest in far with the fainter lights' light the probes summed.
float3 glowFromProbes(float3 p, float3 n, int4 first, float4 weight, float total, int dominant, out float3 far)
{
    int count = min(int(lights.glowCounts.x), MostGlowLights);
    float margin = max(fieldCell(fieldInfo, p), 0.0) * 0.5;
    float3 sum = float3(0.0);
    far = float3(0.0);
    [unroll]
    for (int corner = 0; corner < 4; corner++)
        if (weight[corner] > 0.0)
        {
            int faint = first[corner] + 16;
            far += float3(asfloat(glowSeen[faint]), asfloat(glowSeen[faint + 1]), asfloat(glowSeen[faint + 2])) * (weight[corner] / total);
        }
    // Each probe's word of four lights is read from the buffer once for its four, rather than kept
    // in an array indexed by the light, which lavapipe keeps in memory of its own and crashed on.
    [loop]
    for (int word = 0; word * 4 < count; word++)
    {
        uint4 levels = uint4(glowSeen[first.x + word], glowSeen[first.y + word], glowSeen[first.z + word], glowSeen[first.w + word]);
        if (all(levels == uint4(0u)))
            continue;
        [loop]
        for (int j = 0; j < 4 && word * 4 + j < count; j++)
        {
            float shown = dot(weight, float4((levels >> uint(j * 8)) & 255u));
            if (shown <= 0.0)
                continue;
            GlowLight light = lights.glows[min(word * 4 + j, count - 1)];
            float faded = glowLightFade(light, p, n);
            if (faded <= 0.0)
                continue;
            float3 arrived = glowLightIrradiance(light, p, n, margin) * (faded * shown / (255.0 * total));
            float near = word * 4 + j == dominant ? glowLightNear(light, p) : 0.0;
            sum += arrived * near;
            far += arrived * (1.0 - near);
        }
    }
    return sum;
}

// The rough light the glow light near p facing n that gives it the most of it sends it, unhidden
// (glowLightRough, glowLightNear), that light in dominant, -1 where none is near, and in on whether
// p lies on one's own surface.
float3 glowRough(float3 p, float3 n, out bool on, out int dominant)
{
    int count = min(int(lights.glowCounts.x), MostGlowLights);
    float margin = max(fieldCell(fieldInfo, p), 0.0) * 0.5;
    float3 most = float3(0.0);
    on = false;
    dominant = -1;
    [loop]
    for (int i = 0; i < count; i++)
    {
        float near = glowLightNear(lights.glows[i], p);
        float faded = near > 0.0 ? glowLightFade(lights.glows[i], p, n) : 0.0;
        if (faded <= 0.0)
            continue;
        if (glowLightOn(lights.glows[i], p, n, margin))
        {
            on = true;
            continue;
        }
        float3 rough = glowLightRough(lights.glows[i], p, n, margin) * (faded * near);
        if (luminance(rough) > luminance(most))
        {
            most = rough;
            dominant = i;
        }
    }
    return most;
}

// Whether a glow light that reaches p facing n has a place whose light the frame before did not
// have there, as one placed or moved has.
bool glowPlaceNew(float3 p, float3 n)
{
    int count = min(int(lights.glowCounts.x), MostGlowLights);
    uint2 kept = uint2(asuint(view.glows.y), asuint(view.glows.z));
    uint2 used = uint2(count >= 32 ? 0xffffffffu : (1u << uint(count)) - 1u, count <= 32 ? 0u : count >= 64 ? 0xffffffffu : (1u << uint(count - 32)) - 1u);
    if ((kept.x & used.x) == used.x && (kept.y & used.y) == used.y)
        return false;
    [loop]
    for (int i = 0; i < count; i++)
    {
        uint place = uint(i);
        bool same = ((place < 32u ? kept.x >> place : kept.y >> (place - 32u)) & 1u) != 0u;
        if (!same && glowLightFade(lights.glows[i], p, n) > 0.0)
            return true;
    }
    return false;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint width, height;
    glowLight.GetDimensions(width, height);
    if (id.x >= width || id.y >= height)
        return;
    float2 uv = (float2(id.xy) + 0.5) / float2(width, height);
    float d = depth.SampleLevel(uv, 0).r;
    if (d >= 1.0)
    {
        glowLight[id.xy] = float4(0.0, 0.0, 0.0, -1.0);
        glowFar[id.xy] = float4(0.0);
        glowState[id.xy] = float4(0.0);
        glowNormal[id.xy] = float4(0.0);
        return;
    }
    // The surface's normal from its way along each axis, toward the eye.
    float3 p = worldAt(uv, d);
    float2 texel = 1.0 / float2(width, height);
    float3 n = normalize(cross(wayAlong(uv, p, float2(texel.x, 0.0)), wayAlong(uv, p, float2(0.0, texel.y))));
    if (dot(n, view.eye.xyz - p) < 0.0)
        n = -n;
    float range = length(view.eye.xyz - p);
    float alike = 0.02 * range + 0.05;

    // The four probes around the point, where they marched from this frame, the depth's texels the
    // jitter moved them to, each weighed by how near it is and how alike its surface is to the
    // point's (probeLike). Where none of the four by four around stands on a surface like the
    // point's, the point is marked for the model pass's pixels on it to march to the lights' faces'
    // halves themselves, as a probe does. Taken from the probes on any surface by how near each is,
    // the floor along a wall's foot took the light its top saw, and marched from here, the march's
    // code in this pass cost global illumination 0.15 to 0.26 ms more in a room of sixteen lamps,
    // few as its texels are.
    float2 g = (uv - marchedFrom(int2(0))) * view.sizes.zw / view.grid.x;
    int2 base = int2(floor(g));
    float2 f = g - float2(base);
    float4 weight = float4(0.0);
    int4 first = int4(0);
    [unroll]
    for (int corner = 0; corner < 4; corner++)
    {
        int2 offset = int2(corner & 1, corner >> 1);
        float2 lerps = lerp(1.0 - f, f, float2(offset));
        weight[corner] = lerps.x * lerps.y * probeLike(base + offset, uv, p, n, alike, first[corner]);
    }
    float total = weight.x + weight.y + weight.z + weight.w;
    // Where none of the four is, as on the floor behind a lamp whose probes stand on the lamp, the
    // four of the four by four around that count most by how near each is, two tiles off counting
    // nothing. Its pixels marching, the floor along the near lamps' backs cost the model pass 0.6
    // ms in a room of sixteen lamps.
    if (total <= 1e-3)
    {
        [loop]
        for (int k = 0; k < 16; k++)
        {
            int2 at = base + int2(k % 4 - 1, k / 4 - 1);
            float2 apart = abs(g - float2(at));
            float near = max(2.0 - apart.x, 0.0) * max(2.0 - apart.y, 0.0) * 0.25;
            int place = 0;
            float w = near > 0.0 ? near * probeLike(at, uv, p, n, alike, place) : 0.0;
            // Kept in place of the least of the four, a word at a time, since lavapipe keeps an
            // array indexed by a variable in memory of its own.
            float least = min(min(weight.x, weight.y), min(weight.z, weight.w));
            if (w <= least)
                continue;
            if (weight.x == least) { weight.x = w; first.x = place; }
            else if (weight.y == least) { weight.y = w; first.y = place; }
            else if (weight.z == least) { weight.z = w; first.z = place; }
            else { weight.w = w; first.w = place; }
        }
        total = weight.x + weight.y + weight.z + weight.w;
    }
    bool onLight;
    int dominant;
    float3 rough = glowRough(p, n, onLight, dominant);
    if (onLight || total <= 1e-3)
    {
        glowLight[id.xy] = float4(0.0, 0.0, 0.0, range);
        glowFar[id.xy] = float4(0.0);
        glowState[id.xy] = float4(0.0);
        glowNormal[id.xy] = float4(n * 0.5 + 0.5, float(onLight ? GlowOnLight : GlowAsk) / 255.0);
        return;
    }
    float3 far;
    float3 light = glowFromProbes(p, n, first, weight, total, dominant, far);
    glowNormal[id.xy] = float4(n * 0.5 + 0.5, float(dominant + 2) / 255.0);
    float3 now = float3(rough.r > 1e-8 ? light.r / rough.r : 0.0, rough.g > 1e-8 ? light.g / rough.g : 0.0, rough.b > 1e-8 ? light.b / rough.b : 0.0);

    // What the texels of the frame before around the point's place then held, each where its point
    // lay as far from the frame before's eye as this one does.
    float3 before = float3(0.0), farBefore = float3(0.0);
    float furthest = 0.0, held = 0.0, still = 0.0, frames = 1.0 / GlowStill;
    if (view.last.w > 0.5 && (uint(view.grid.w + 0.5) & 1u) == 0u)
    {
        float4 clip = mul(view.lastViewProjection, float4(p, 1.0));
        if (clip.w > 1e-5)
        {
            float2 then = (clip.xy / clip.w * 0.5 + 0.5) * float2(width, height) - 0.5;
            int2 from = int2(floor(then));
            float2 part = then - float2(from);
            float expected = distance(p, view.last.xyz);
            float most = 0.0;
            [unroll]
            for (int corner = 0; corner < 4; corner++)
            {
                int2 offset = int2(corner & 1, corner >> 1);
                int2 at = from + offset;
                if (any(at < int2(0)) || at.x >= int(width) || at.y >= int(height))
                    continue;
                float4 was = lastGlowLight.Load(int3(at, 0));
                if (was.a < 0.0 || abs(was.a - expected) > 0.02 * expected + 0.05)
                    continue;
                float2 lerps = lerp(1.0 - part, part, float2(offset));
                float share = lerps.x * lerps.y;
                float4 state = lastGlowState.Load(int3(at, 0));
                before += was.rgb * share;
                farBefore += lastGlowFar.Load(int3(at, 0)).rgb * share;
                furthest += state.x * share;
                frames = min(frames, state.y);
                held += share;
                most = max(most, share);
            }
            if (held > 1e-4)
            {
                before /= held;
                farBefore /= held;
                furthest /= held;
                still = most / held;
            }
        }
    }
    // Dropped where no texel held the point, where a light that reaches it is new to its place, or,
    // after a round, where the light this frame's share and far light give the point lies further
    // from the blend's than the frames' has.
    float lit = luminance(now * rough + far), litBefore = luminance(before * rough + farBefore);
    float apart = abs(lit - litBefore);
    // A blend still in its first round is dropped too where the field's cascade under the point was
    // built this frame as meshes came or went around the point, or for the first time, since what
    // the probes saw before the field held what stands between is no part of what they see. Held
    // whole, the frames of an app before the field built its meshes in place of their boxes lay in
    // the blend under a wall's shadow, and the floor across a lamp's penumbra strayed from where it
    // settles by 13% a round after the app began and by 6% two rounds after. Dropped wherever the
    // cascade was built, the blends begun the frame a lamp was placed began again when its own mesh
    // was built eight frames on, and the penumbra was 2% from where it settles a round after the lamp.
    int under = fieldCascade(fieldInfo, p, 1.0);
    bool rebuilt = under >= 0 && ((asuint(view.glows.w) >> uint(under)) & 1u) != 0u
        && all(p >= view.changedMin.xyz) && all(p <= view.changedMax.xyz);
    bool dropped = held <= 1e-4 || glowPlaceNew(p, n) || (rebuilt && frames < GlowRound)
        || (frames >= GlowRound && apart > GlowDeviations * furthest + GlowMargin * max(lit, litBefore) + 1e-6);
    if (dropped)
        frames = 0.0;
    // steady is 1 where the view held still and one texel of the frame before gives the whole, less
    // as the view moves and four share it; and where they cover the place in part, as at the edge
    // of what the frame before showed, this frame's light counts for the rest. A moving view's
    // blend counts as one frame's light, so where the view stops the frames after it make the mean
    // anew and reach a round's within a round. Counted as all the frames it held, the blend the
    // moving view had spread over the surface took 64 frames to leave, and the 99th part of a
    // turn's pixels changed after the stop by 5.6% of the picture's mean light in the frame after
    // and 2.7% fifteen frames on, where so by 0.8% and 0.3%.
    float steady = saturate(2.0 * still - 1.0);
    float least = lerp(GlowMoving, GlowStill, steady);
    float fresh = dropped ? 1.0 : lerp(1.0, max(1.0 / (frames + 1.0), least), saturate(held));
    glowLight[id.xy] = float4(lerp(before, now, fresh), range);
    glowFar[id.xy] = float4(lerp(farBefore, far, fresh), 0.0);
    glowState[id.xy] = float4(dropped ? 0.0 : max(furthest * (1.0 - GlowLetGo), apart), min(frames * steady + 1.0, 1.0 / GlowStill), 0.0, 0.0);
}
