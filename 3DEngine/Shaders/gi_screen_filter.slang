// Blends each screen probe's light with the probes around it that stand on a surface like its own,
// a thread a probe, five by five of them, each weighed by how near it is and how alike its normal and
// its distance from the eye are to the probe's, so the few rays each probe sends average over many
// and the tiles the window is cut into do not show. Then with what the probes of the frame before
// held where the probe's surface was, a fifth of this frame's to four fifths of theirs, so the light
// does not crawl as the probes slide over the surfaces when the camera moves. The probe's point is
// found from the window's depth, its place in the frame before through that frame's camera, and the
// four probes of the frame before around it blended where each stood on a like surface. A probe
// whose surface the frame before did not show takes this frame's light alone. The frame before's
// light is held first within the spread of this frame's light among the like probes around, so
// where the light changed, a lamp carried or lit, the blend follows it within a frame or two, and
// where it holds still the frame before's light lies inside that spread and keeps its calm.
import gi;

// The light each probe's rays summed, and each probe's surface, as gi_screen.slang left them.
[[vk::binding(0, 0)]]
Texture2D<float4> irradiance;

[[vk::binding(1, 0)]]
Texture2D<float4> geometry;

[[vk::binding(2, 0)]] [format("rgba16f")]
RWTexture2D<float4> blended;

// The light blended and the surfaces of the frame before, as this pass and gi_screen.slang left
// them, and copied after each frame.
[[vk::binding(3, 0)]]
Texture2D<float4> history;

[[vk::binding(4, 0)]]
Texture2D<float4> lastGeometry;

[[vk::binding(5, 0)]]
ConstantBuffer<ScreenView> view;

[[vk::binding(6, 0)]]
Sampler2D depth;

// How much of a probe's light this frame's rays give where the frame before's probes held it.
static const float Fresh = 0.2;

// How many of this frame's spreads among the like probes around the frame before's light may lie
// from their mean.
static const float Spread = 2.0;

// The point a probe stands on, from the depth's texel under its tile's middle, as gi_screen.slang
// placed it.
float3 standsOn(int2 id)
{
    float2 pixel = min((float2(id) + 0.5) * view.grid.x, view.sizes.zw - 0.5);
    float2 uv = (floor(pixel / view.sizes.zw * view.sizes.xy) + 0.5) / view.sizes.xy;
    float4 world = mul(view.inverseViewProjection, float4(uv * 2.0 - 1.0, depth.SampleLevel(uv, 0).r, 1.0));
    return world.xyz / world.w;
}

// The spatial blend of a probe's light with its like neighbors', or its own where it holds none,
// and in spread how far the light of those neighbors lies from that blend, each channel's standard
// deviation by the same weights. A neighbor counts by how near the plane of the probe's surface its
// own point lies, so a floor seen at a slant blends along its slope; weighed by how alike their
// distances from the eye were, as they had been, the probes in rows 8 pixels apart on such a floor
// lay further apart than the 2% allowed, each row kept its own noise, and the floor showed bands.
float4 spatial(uint2 id, uint across, uint down, float4 own, float4 surface, out float3 spread)
{
    float4 sum = float4(0.0);
    float3 squares = float3(0.0);
    float3 here = standsOn(int2(id));
    for (int y = -2; y <= 2; y++)
        for (int x = -2; x <= 2; x++)
        {
            int2 at = clamp(int2(id) + int2(x, y), int2(0), int2(int(across) - 1, int(down) - 1));
            float4 light = irradiance.Load(int3(at, 0));
            float4 other = geometry.Load(int3(at, 0));
            if (light.a < 0.5 || other.w < 0.0)
                continue;
            float off = abs(dot(standsOn(at) - here, surface.xyz));
            float weight = exp(-float(x * x + y * y) / 4.0) * pow(saturate(dot(surface.xyz, other.xyz)), 16.0)
                         * exp(-off / (0.02 * surface.w + 0.05));
            sum += float4(light.rgb, 1.0) * weight;
            squares += light.rgb * light.rgb * weight;
        }
    float3 mean = sum.rgb / max(sum.w, 1e-5);
    spread = sqrt(max(squares / max(sum.w, 1e-5) - mean * mean, 0.0));
    return float4(mean, 1.0);
}

// The frame before's light at the probe's surface, with in a how much of the four probes around
// its place then stood on a surface like it, or 0 where the frame before showed none of it.
float4 earlier(uint2 id, uint across, uint down, float4 surface)
{
    if (view.last.w < 0.5)
        return float4(0.0);
    float3 p = standsOn(int2(id));

    float4 clip = mul(view.lastViewProjection, float4(p, 1.0));
    if (clip.w <= 1e-5)
        return float4(0.0);
    float2 then = (clip.xy / clip.w * 0.5 + 0.5) * view.sizes.zw / view.grid.x - 0.5;
    int2 base = int2(floor(then));
    float2 f = then - float2(base);
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
        float2 lerps = lerp(1.0 - f, f, float2(corner & 1, corner >> 1));
        float weight = lerps.x * lerps.y;
        sum += history.Load(int3(at, 0)).rgb * weight;
        weights += weight;
    }
    return weights > 1e-4 ? float4(sum / weights, weights) : float4(0.0);
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
        blended[id.xy] = own;
        return;
    }
    float3 spread;
    uint off = uint(view.grid.w + 0.5);
    // With the neighbors' light left out the probe keeps its own, the frame before's still held
    // within the spread of its neighbors'.
    float4 now = spatial(id.xy, across, down, own, surface, spread);
    if ((off & 2u) != 0u)
        now = float4(own.rgb, 1.0);
    // Where the frame before's probes cover the place in part, as at the edge of what it showed,
    // this frame's light counts for the rest.
    float4 before = (off & 1u) != 0u ? float4(0.0) : earlier(id.xy, across, down, surface);
    float3 held = clamp(before.rgb, now.rgb - Spread * spread, now.rgb + Spread * spread);
    float fresh = lerp(1.0, Fresh, saturate(before.a));
    // Its alpha one more than the share of its light the frame before's gave, which a view of the
    // history shows.
    blended[id.xy] = float4(lerp(held, now.rgb, fresh), 2.0 - fresh);
}
