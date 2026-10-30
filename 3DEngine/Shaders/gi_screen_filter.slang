// Keeps each screen probe's light over the frames, then blends it with the probes around it that
// stand on a surface like its own, two passes of a thread a probe. A probe's rays leave from other
// places in their texels each frame (gi_screen.slang), so a patch narrower than a texel that one
// probe's ray meets one frame another's meets the next. The first pass takes this frame's light into
// what the probes of the frame before kept where the probe's surface was, as one frame more of up to
// Kept, so a probe's light is the mean of its rays over the frames and a patch it met once is a
// share of it. The probe's point is found from the window's depth, its place in the frame before
// through that frame's camera, and the four probes of the frame before around it blended where each
// stood on a like surface. A probe whose surface the frame before did not show starts from this
// frame's light alone. The kept light is held first within the spread of this frame's light among
// the like probes around, so where the light changed, a lamp carried or lit, the mean follows it,
// and where it holds still the kept light lies inside that spread and keeps its calm. The second
// pass blends each probe's kept light with five by five probes around it, each weighed by how near
// it is and how alike its normal and its plane are to the probe's, so the tiles the window is cut
// into do not show.
import gi;

// The light each probe's rays summed and each probe's surface, as gi_screen.slang left them, in the
// first pass, and the light kept over the frames in the second.
[[vk::binding(0, 0)]]
Texture2D<float4> irradiance;

[[vk::binding(1, 0)]]
Texture2D<float4> geometry;

// The light kept over the frames, with in a how many frames it holds, in the first pass, and the
// light blended, which the model pass reads, in the second.
[[vk::binding(2, 0)]] [format("rgba16f")]
RWTexture2D<float4> blended;

// The light kept and the surfaces of the frame before, as the first pass and gi_screen.slang left
// them, and copied after each frame.
[[vk::binding(3, 0)]]
Texture2D<float4> history;

[[vk::binding(4, 0)]]
Texture2D<float4> lastGeometry;

[[vk::binding(5, 0)]]
ConstantBuffer<ScreenView> view;

[[vk::binding(6, 0)]]
Sampler2D depth;

// x: 0 for the first pass, which keeps each probe's light over the frames, and 1 for the second,
// which blends it with the probes' around.
struct FilterPush
{
    uint4 pass;
};

[[vk::push_constant]]
ConstantBuffer<FilterPush> push;

// How many frames' light a probe keeps at most, each frame's rays leaving from one of sixteen places
// in their texels, so a probe that keeps them has sent every ray of a texel's sixteen once.
static const float Kept = 16.0;

// How many of this frame's spreads among the like probes around the frame before's light may lie
// from their mean.
static const float Spread = 2.0;

// Whether a light is one a float holds, neither a value that is not a number nor one past the
// largest. A light that is not is taken as none, by the blend of the probes around and by the frame
// before's, and written as none, so a fault anywhere that gives one darkens a probe for a frame
// where the blends had carried it to every probe around and every frame after. A non-number at the
// glow lights' light turned 215,459 pixels of the voxel game's desert village black, and 596,201
// six hundred frames on, where 320 with the history left out.
bool lightHeld(float3 light)
{
    return all(abs(light) < 1e30);
}

// The point a probe stands on, from the depth's texel under its tile's middle, as gi_screen.slang
// placed it.
float3 standsOn(int2 id)
{
    float2 pixel = min((float2(id) + 0.5) * view.grid.x, view.sizes.zw - 0.5);
    float2 uv = (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    float4 world = mul(view.inverseViewProjection, float4(uv * 2.0 - 1.0, depth.SampleLevel(uv, 0).r, 1.0));
    return world.xyz / world.w;
}

// The window's pixel a probe stands on, the middle of the depth's texel under its tile's middle, as
// standsOn and gi_screen.slang place it, half a depth texel past the tile's middle where the depth is
// half the window's size.
float2 probePixel(int2 id)
{
    float2 pixel = min((float2(id) + 0.5) * view.grid.x, view.sizes.zw - 0.5);
    return (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy * view.sizes.zw;
}

// The spatial blend of a probe's light with its like neighbors', or its own where it holds none,
// in spread how far the light of those neighbors lies from that blend, each channel's standard
// deviation by the same weights, and in error how far the blend itself may lie from the light it
// stands for, the spread over the root of the neighbors the weights count. A neighbor counts by how near the plane of the probe's surface its
// own point lies, so a floor seen at a slant blends along its slope; weighed by how alike their
// distances from the eye were, as they had been, the probes in rows 8 pixels apart on such a floor
// lay further apart than the 2% allowed, each row kept its own noise, and the floor showed bands.
float4 spatial(uint2 id, uint across, uint down, float4 own, float4 surface, out float3 spread, out float3 error)
{
    float4 sum = float4(0.0);
    float3 squares = float3(0.0);
    float weightSquares = 0.0;
    float3 here = standsOn(int2(id));
    for (int y = -2; y <= 2; y++)
        for (int x = -2; x <= 2; x++)
        {
            int2 at = clamp(int2(id) + int2(x, y), int2(0), int2(int(across) - 1, int(down) - 1));
            float4 light = irradiance.Load(int3(at, 0));
            float4 other = geometry.Load(int3(at, 0));
            if (light.a < 0.5 || other.w < 0.0 || !lightHeld(light.rgb))
                continue;
            float off = abs(dot(standsOn(at) - here, surface.xyz));
            float weight = exp(-float(x * x + y * y) / 4.0) * pow(saturate(dot(surface.xyz, other.xyz)), 16.0)
                         * exp(-off / (0.02 * surface.w + 0.05));
            sum += float4(light.rgb, 1.0) * weight;
            squares += light.rgb * light.rgb * weight;
            weightSquares += weight * weight;
        }
    float3 mean = sum.rgb / max(sum.w, 1e-5);
    spread = sqrt(max(squares / max(sum.w, 1e-5) - mean * mean, 0.0));
    error = spread * sqrt(weightSquares) / max(sum.w, 1e-5);
    return float4(mean, 1.0);
}

// The light the frame before kept at the probe's surface, with in a how much of the four probes
// around its place then stood on a surface like it, or 0 where the frame before showed none of it,
// and in frames how many frames that light held, blended alike.
float4 earlier(uint2 id, uint across, uint down, float4 surface, out float frames)
{
    frames = 0.0;
    if (view.last.w < 0.5)
        return float4(0.0);
    float3 p = standsOn(int2(id));

    float4 clip = mul(view.lastViewProjection, float4(p, 1.0));
    if (clip.w <= 1e-5)
        return float4(0.0);
    // The four probes around the point's pixel in the frame before, blended by where they stand
    // rather than by their tiles' middles, since read by the middles a still view's blend took the
    // next probe along each axis by a pixel's share, a sixteenth of a tile at Low and an eighth at
    // High, every frame.
    float2 then = (clip.xy / clip.w * 0.5 + 0.5) * view.sizes.zw;
    int2 base = int2(floor(then / view.grid.x - 0.5));
    base -= select(then < probePixel(base), int2(1), int2(0));
    float2 low = probePixel(base);
    float2 f = saturate((then - low) / max(probePixel(base + 1) - low, float2(1e-3)));
    float expected = distance(p, view.last.xyz);
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (int corner = 0; corner < 4; corner++)
    {
        int2 at = base + int2(corner & 1, corner >> 1);
        if (any(at < int2(0)) || at.x >= int(across) || at.y >= int(down))
            continue;
        float4 before = lastGeometry.Load(int3(at, 0));
        if (before.w < 0.0 || dot(before.xyz, surface.xyz) < 0.9 || abs(before.w - expected) > 0.03 * expected + 0.05)
            continue;
        float4 was = history.Load(int3(at, 0));
        if (!lightHeld(was.rgb) || was.a < 0.5)
            continue;
        float2 lerps = lerp(1.0 - f, f, float2(corner & 1, corner >> 1));
        float weight = lerps.x * lerps.y;
        sum += was.rgb * weight;
        frames += was.a * weight;
        weights += weight;
    }
    if (weights <= 1e-4)
        return float4(0.0);
    frames /= weights;
    return float4(sum / weights, weights);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint across, down;
    irradiance.GetDimensions(across, down);
    if (id.x >= across || id.y >= down)
        return;
    float4 own = irradiance.Load(int3(id.xy, 0));
    float4 surface = geometry.Load(int3(id.xy, 0));
    if (own.a < 0.5 || surface.w < 0.0)
    {
        blended[id.xy] = float4(own.rgb, 0.0);
        return;
    }
    float3 spread, error;
    uint off = uint(view.grid.w + 0.5);
    float4 now = spatial(id.xy, across, down, own, surface, spread, error);
    if (push.pass.x == 1u)
    {
        // With the neighbors' light left out the probe keeps its own. Its alpha one more than the
        // share of its light the frames before gave, which a view of the history shows.
        float3 result = (off & 2u) != 0u ? own.rgb : now.rgb;
        blended[id.xy] = float4(lightHeld(result) ? result : float3(0.0), 2.0 - 1.0 / max(own.a, 1.0));
        return;
    }
    // Where the frame before's probes cover the place in part, as at the edge of what it showed,
    // the frames they kept count for that part.
    float frames;
    float4 before = (off & 1u) != 0u ? float4(0.0) : earlier(id.xy, across, down, surface, frames);
    float3 held = clamp(before.rgb, now.rgb - Spread * spread, now.rgb + Spread * spread);
    // Where the kept light lies further from this frame's blend of the like probes than three times
    // the blend's own error, the light changed rather than the rays, and the probe keeps fewer
    // frames, a quarter as many at twice that, so it follows a change within the spread, as a roof
    // of leaves made to let light through, where the noise of its rays and a light that falls off
    // evenly across its neighbors leave it all sixteen. Keeping sixteen whatever the light did, the
    // floor under such a roof took 0.167 of the sky's light thirty frames after where it takes
    // about 0.30; at twice the error, a camera sliding through a Cornell box read the change as the
    // light's and added 0.25 sRGB levels a frame to the picture's change where 0.08 at three times.
    float3 apart = abs(held - now.rgb) / max(error * 3.0, 1e-4 + now.rgb * 0.01);
    float most = max(apart.r, max(apart.g, apart.b));
    float allowed = Kept * min(1.0, 1.0 / max(most * most, 1e-4));
    float kept = min(frames * saturate(before.a), max(allowed, 1.0) - 1.0) + 1.0;
    float3 result = lerp(held, own.rgb, 1.0 / kept);
    blended[id.xy] = float4(lightHeld(result) ? result : float3(0.0), kept);
}
