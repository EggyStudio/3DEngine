// Temporal antialiasing of the window's HDR frame in linear light. The window's camera is moved by
// a part of a pixel each frame, eight places in turn, so a pixel's samples over the frames fall
// across its square. The resolve finds where the pixel's point lay the frame before through the
// camera then, without its move, reads the history the frames before left there, holds it to the
// colors of the pixel's neighbors this frame, so what moved or came out from behind another
// surface leaves no trail, and blends the frame into it a part at a time, a smaller part the
// longer the pixel has held still, so a still edge, which each move of the camera moves across the
// pixel, settles where an eighth of each frame would keep it moving. Both are done with each
// color brought under 1 as it will be shown, by a curve of its light times the exposure that can
// be undone, so a pixel on the edge of a bright light is the mean of what the eye would see of its
// samples and not of their light, which the one sample of eight that saw the light would
// outweigh. A point the frame before did not see, as something nearer stood in front of it there,
// starts its history again, as a box over a grille in front of a bright wall holds both and would
// keep the grille's ghost. The copy pass then draws the blended frame back into the decoded
// frame, which bloom, the exposure and the lens read as before.
import fullscreen;

struct TaaPush
{
    // This frame's clip space, its camera without its move, to the frame before's, as the model
    // pass takes a view-projection.
    float4x4 reprojection;
    // xy: one texel of the frame, in uv. z: the least share of this frame's light the blend takes,
    // where the pixel has held still. w: 1 where the history holds the frame before, 0 where it
    // starts again.
    float4 texelAndBlend;
    // x: 0 to resolve, 1 to copy the history back. y: the frames a pixel is held to where it moved
    // or its history was held to its neighbors, whose share the blend then takes at least. zw: the
    // pixels the camera's move carried the frame by.
    float4 mode;
    // x: the exposure the program set. y: 1 where the exposure follows the scene, as adapted. z:
    // the luminance that one brings the scene's mean to. w: unused.
    float4 exposure;
};

[[vk::push_constant]]
ConstantBuffer<TaaPush> push;

// The frame's decoded light, read texel for texel, and the resolved history to copy back.
[[vk::binding(0, 0)]]
Sampler2D current;

// The frame's depth.
[[vk::binding(1, 0)]]
Sampler2D depth;

// The history the frame before left, read filtered between its texels, with in alpha the frames
// each pixel has held its history for.
[[vk::binding(2, 0)]]
Sampler2D history;

// The depth the frame before left beside its history, the frame's as the resolve wrote it.
[[vk::binding(3, 0)]]
Sampler2D historyDepth;

struct Resolved
{
    float4 color : SV_Target;
    float depth : SV_Depth;
};

// The log2 of the scene's mean luminance as the exposure that follows it had adapted to it the
// frame before, in one texel, or a texel standing in where it does not follow it.
[[vk::binding(0, 1)]]
Sampler2D adapted;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float3 toYCoCg(float3 c)
{
    return float3(0.25 * c.r + 0.5 * c.g + 0.25 * c.b, 0.5 * c.r - 0.5 * c.b, -0.25 * c.r + 0.5 * c.g - 0.25 * c.b);
}

float3 fromYCoCg(float3 c)
{
    return float3(c.x + c.y - c.z, c.x + c.z, c.x - c.y - c.z);
}

float luma(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// The exposure the frame will be shown at, as frame.slang works it out.
float shownExposure()
{
    float exposure = push.exposure.x;
    if (push.exposure.y > 0.5)
        exposure *= push.exposure.z / exp2(adapted.Load(int3(0, 0, 0)).r);
    return exposure;
}

// Light brought under 1 by one over one more than its luma as shown, as Karis brings it, and back.
float3 toned(float3 c, float exposure)
{
    return c / (1.0 + exposure * luma(max(c, 0.0)));
}

float3 untoned(float3 c, float exposure)
{
    return c / max(1.0 - exposure * luma(c), 1e-4);
}

// The nearest and the farthest the frame before saw in the texels around before, each as one
// less its depth, which goes as one over the distance where the far plane is far.
float2 seenAround(float2 before)
{
    float2 texel = push.texelAndBlend.xy;
    float nearest = 0.0, farthest = 1.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            float seen = 1.0 - historyDepth.SampleLevel(before + float2(x, y) * texel, 0).r;
            nearest = max(nearest, seen);
            farthest = min(farthest, seen);
        }
    return float2(nearest, farthest);
}

float4 resolve(float2 uv)
{
    float2 texel = push.texelAndBlend.xy;
    float exposure = shownExposure();
    // The colors around the pixel this frame, as a box in YCoCg, where the luma and the two
    // chromas lie apart, so a history held to it keeps no color its neighbors lack. And the
    // pixel's own light this frame, its neighbors' weighed by how far each lay from its middle
    // before the camera's move, by a bell about a third of a pixel wide, so the frame gives much
    // the same light wherever the move put the edges that cross it, and a still edge holds still.
    float3 least = float3(1e9), most = float3(-1e9), sum = float3(0.0);
    float weights = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            float3 c = toYCoCg(toned(current.SampleLevel(uv + float2(x, y) * texel, 0).rgb, exposure));
            least = min(least, c);
            most = max(most, c);
            float2 apart = float2(x, y) - push.mode.zw;
            float weight = exp(-4.08 * dot(apart, apart));
            sum += c * weight;
            weights += weight;
        }
    float3 now = sum / weights;
    if (push.texelAndBlend.w < 0.5)
        return float4(untoned(fromYCoCg(now), exposure), 1.0);
    float4 clip = mul(push.reprojection, float4(uv * 2.0 - 1.0, depth.SampleLevel(uv, 0).r, 1.0));
    float2 before = clip.xy / clip.w * 0.5 + 0.5;
    if (clip.w <= 0.0 || any(before < 0.0) || any(before > 1.0))
        return float4(untoned(fromYCoCg(now), exposure), 1.0);
    // Where even the farthest the frame before saw around the point stood a twentieth nearer, the
    // point was behind it, and its history starts again.
    float is = 1.0 - clip.z / clip.w;
    float2 around = seenAround(before);
    if (around.y > is * 1.05 + 1e-6)
        return float4(untoned(fromYCoCg(now), exposure), 1.0);
    // Where the pixel held still, the box widened by a quarter of its size, so a thin face
    // brighter than all around it, whose light each move of the camera changes, is not held to
    // this frame's light and drawn so each frame. Where the frame before saw the same surface all
    // around, every depth within a twentieth, the box is a twentieth of the pixel's luma either
    // side at least, so a room's light that steps by a few hundredths as its probes are traced is
    // blended over the frames rather than followed. One where the pixel moved is not widened, so
    // what the camera saw from elsewhere is let go, and neither is one by an edge or where
    // something moved away, as an edge's history holds the colors on both sides of it.
    float speed = length((before - uv) / texel);
    bool moved = speed > 0.1;
    if (!moved)
    {
        float3 margin = (most - least) * 0.25;
        if (around.x <= is * 1.05 + 1e-6 && around.y >= is * 0.95 - 1e-6)
            margin = max(margin, float3(0.05 * max(now.x, 0.0)));
        least -= margin;
        most += margin;
    }
    float4 held = history.SampleLevel(before, 0);
    float3 seen = toYCoCg(toned(held.rgb, exposure));
    float3 kept = clamp(seen, least, most);
    // The frames the pixel has held its history for, held down where it moved by a tenth of a
    // pixel or more, as each frame's sample of it is then read between the history's texels, the
    // further the faster it moved, as what it shows was lit as seen from further off, or where the
    // history was held to its neighbors, which a frame then shows something new.
    float age = held.a;
    if (moved)
        age = min(age, push.mode.y / (1.0 + speed * 0.25));
    if (distance(seen, kept) > 0.02)
        age = min(age, push.mode.y);
    age += 1.0;
    float blend = max(1.0 / age, push.texelAndBlend.z);
    return float4(untoned(fromYCoCg(lerp(kept, now, blend)), exposure), age);
}

// The resolved light into the history and the frame's depth beside it, or, copying, the history
// back into the frame, whose pass writes no depth.
[shader("fragment")]
Resolved fragmentMain(FullscreenOutput input)
{
    Resolved output;
    output.color = push.mode.x > 0.5 ? float4(current.SampleLevel(input.uv, 0).rgb, 1.0) : resolve(input.uv);
    output.depth = depth.SampleLevel(input.uv, 0).r;
    return output;
}
