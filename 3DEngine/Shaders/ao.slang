// Ambient occlusion from the window's depth, drawn at half the window's size. Each pixel is put
// back in the world through the inverse of the camera's view-projection, its surface's normal
// found from the pixels beside it, and the share of the light from all around that reaches it,
// weighed by the cosine, is integrated over the horizons the depth gives along two slices through
// the eye's way, as Jimenez, Wu, Pesce and Jarabo's ground-truth ambient occlusion has it, so a
// floor darkens where a wall meets it and a corner more than either wall. The slices turn each frame
// and a history pass blends each pixel with what it held the frame before, found through the frame
// before's view-projection, so the frames together read many slices. A pass across and a pass down
// then blur the result, each tap weighed by how near its distance from the eye is to the pixel's,
// so the darkening keeps to the surface it is on and does not cross an edge.
// Where the scene's distance field is built, the occlusion is also read from it, along the normal
// and four ways leaning from it, which finds what closes a point off beyond what the window shows,
// and the sun's light is traced toward the sun through it for soft contact shadows, which the green
// channel carries to the model pass and the blurs blur with the occlusion.
import fullscreen;
import scenefield;

struct AoPush
{
    // Clip space to the world, as the model pass takes a view-projection.
    float4x4 inverseViewProjection;
    // xyz: the eye. w: how far from a surface, in world units, the surfaces around it are looked for.
    float4 eyeAndRadius;
    // xy: one texel of the depth and of the occlusion, in uv. z: how strongly it darkens. w: the
    // share of the picture's height a unit spans at a unit's distance from the eye.
    float4 texelAndStrength;
    // x: 0 to work the occlusion out, 1 to blur it across, 2 to blur it down, 3 to blend it with
    // the history. y: the picture's width over its height. z: 1 where the occlusion is on with the
    // field's, 2 where it is on without it, 0 where only the sun's contact shadow is worked out, and
    // in the history pass 1 where the history holds the frame before. w: the frame's number, which
    // turns the slices.
    float4 mode;
    // xyz: the way toward the sun. w: how far its contact shadows are traced, 0 for none.
    float4 sunAndReach;
};

// The history pass reads inverseViewProjection as the way from this frame's clip space to the frame
// before's, and sunAndReach as the last column of this frame's inverse view-projection, which gives
// a point's depth along the view, since the push has room for no third matrix.

[[vk::push_constant]]
ConstantBuffer<AoPush> push;

[[vk::binding(0, 0)]]
Sampler2D depth;

// The occlusion being blurred, which the first pass does not read.
[[vk::binding(1, 0)]]
Sampler2D occlusion;

// The scene's distance field and where its cascades lie, a field of nothing where it is off.
[[vk::binding(2, 0)]]
Sampler3D sceneField;

[[vk::binding(3, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

// The occlusion the history pass wrote the frame before, its depth along the view in blue, which
// the other passes do not read.
[[vk::binding(4, 0)]]
Sampler2D history;

// The slices through the eye's way each pixel reads a frame, and the steps along each side of one.
static const int Slices = 2;
static const int Steps = 4;

// The share of the history a pixel keeps a frame where the frame before saw its point.
static const float Kept = 0.9;

static const float Pi = 3.14159265359;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float depthAt(float2 uv)
{
    return depth.SampleLevel(uv, 0).r;
}

float3 worldAt(float2 uv, float d)
{
    float4 world = mul(push.inverseViewProjection, float4(uv * 2.0 - 1.0, d, 1.0));
    return world.xyz / world.w;
}

// A threshold from 0 to 1 for each pixel, spread evenly over a small neighborhood (Jorge Jimenez's
// interleaved gradient noise), which turns each pixel's spiral so the blur averages twelve
// different ones rather than showing one twelve times.
float noise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

// How much of the light from all around the field closes off at p on a surface facing n, along the
// normal and four ways leaning from it, each read at four distances out to the radius, a sample
// as nearer a surface than a free one there would be closing off by how much nearer. The samples
// start a cell and a half out, past the surface's own cells.
float fieldOcclusion(float3 p, float3 n, float radius)
{
    float cell = fieldCell(fieldInfo, p);
    if (cell <= 0.0)
        return 0.0;
    float3 side = abs(n.y) < 0.99 ? normalize(cross(n, float3(0, 1, 0))) : float3(1, 0, 0);
    float3 up = cross(side, n);
    float3 start = p + n * (cell * 1.5);
    float reach = max(radius, cell * 4.0);
    float closed = 0.0;
    for (int w = 0; w < 5; w++)
    {
        float3 lean = w == 0 ? float3(0) : w == 1 ? side : w == 2 ? -side : w == 3 ? up : -up;
        float3 way = normalize(n + lean * 0.9);
        float rise = dot(way, n);
        for (int s = 1; s <= 4; s++)
        {
            float h = reach * float(s) / 4.0;
            float free = h * rise + cell * 1.5;
            float d = fieldDistance(sceneField, fieldInfo, start + way * h);
            closed += saturate((free - d) / free) / float(s);
        }
    }
    // The sum of 1/s over four distances is 25/12 a way, so a point closed off along every way at
    // every distance sums to five of those. Faded across the field's last band (fieldHold).
    return closed / (5.0 * 25.0 / 12.0) * fieldHold(fieldInfo, p);
}

// The share of the sun's light that reaches p on a surface facing n, traced toward the sun through
// the field from a cell and a half out, darkened by how near a surface the ray passes as a share
// of how far along it is, as Quilez's soft shadows have it, so a shadow is soft where its caster is
// far and hard where it meets what it falls on.
float sunReaching(float3 p, float3 n)
{
    float reach = push.sunAndReach.w;
    float cell = fieldCell(fieldInfo, p);
    if (reach <= 0.0 || cell <= 0.0)
        return 1.0;
    float3 l = push.sunAndReach.xyz;
    float3 start = p + n * (cell * 1.5);
    float hold = fieldHold(fieldInfo, p);
    float shown = 1.0;
    float t = cell;
    for (int i = 0; i < 40 && t < reach; i++)
    {
        float3 q = start + l * t;
        float d = fieldDistance(sceneField, fieldInfo, q);
        if (d > 1e5)
            break;
        shown = min(shown, 8.0 * d / t);
        if (shown < 0.01)
            return 1.0 - hold;
        t += max(d, 0.5 * fieldCell(fieldInfo, q));
    }
    // Faded across the field's last band (fieldHold).
    return lerp(1.0, saturate(shown), hold);
}

float2 occlude(float2 uv, float2 pixel)
{
    float d = depthAt(uv);
    // Nothing was drawn here, as the sky, which nothing closes off.
    if (d >= 1.0)
        return float2(1.0, 1.0);
    float3 p = worldAt(uv, d);
    float2 texel = push.texelAndStrength.xy;

    // The normal from the nearer of the pixels on either side along each axis, so an edge does not
    // lean it toward what is behind.
    float3 right = worldAt(uv + float2(texel.x, 0), depthAt(uv + float2(texel.x, 0))) - p;
    float3 left = p - worldAt(uv - float2(texel.x, 0), depthAt(uv - float2(texel.x, 0)));
    float3 down = worldAt(uv + float2(0, texel.y), depthAt(uv + float2(0, texel.y))) - p;
    float3 up = p - worldAt(uv - float2(0, texel.y), depthAt(uv - float2(0, texel.y)));
    float3 across = dot(right, right) < dot(left, left) ? right : left;
    float3 along = dot(down, down) < dot(up, up) ? down : up;
    float3 n = normalize(cross(across, along));
    float3 toEye = push.eyeAndRadius.xyz - p;
    if (dot(n, toEye) < 0.0)
        n = -n;

    float sun = fieldOn(fieldInfo) ? sunReaching(p, n) : 1.0;
    if (push.mode.z < 0.5)
        return float2(1.0, sun);

    float radius = push.eyeAndRadius.w;
    float distance = length(toEye);
    float3 v = toEye / max(distance, 1e-6);
    // The radius on the picture, held to three tenths of its height, so a surface near the eye does
    // not read the whole screen, and a step never under a texel.
    float reach = min(radius * push.texelAndStrength.w / max(distance, 1e-3), 0.3);
    float least = texel.y / reach;
    float2 aspect = float2(1.0 / push.mode.y, 1.0);
    float frame = push.mode.w;
    float turn = frac(noise(pixel) + frame * 0.618034);
    float offset = frac(noise(pixel.yx + 11.0) + frame * 0.754878);
    // A surface fades out of a horizon from 0.385 of the radius to the radius (XeGTAO's falloff).
    float fade = 0.615 * radius;

    float seen = 0.0;
    for (int slice = 0; slice < Slices; slice++)
    {
        float angle = (float(slice) + turn) / float(Slices) * Pi;
        float2 way = float2(cos(angle), sin(angle)) * aspect * reach;
        // The slice's way across in the world, square to the eye's way, and the normal laid into
        // the slice, its angle from the eye's way toward that side.
        float3 side = worldAt(uv + way * 0.01, d) - p;
        side = normalize(side - dot(side, v) * v + 1e-9);
        float3 axis = normalize(cross(side, v));
        float3 laid = n - axis * dot(n, axis);
        float laidLength = length(laid);
        float cosNormal = saturate(dot(laid, v) / max(laidLength, 1e-6));
        float normalAngle = (dot(side, laid) < 0.0 ? -1.0 : 1.0) * acos(cosNormal);
        // Each side's horizon starts at the surface's own plane.
        float highest = cos(normalAngle + 0.5 * Pi);
        float lowest = cos(normalAngle - 0.5 * Pi);
        for (int step = 0; step < Steps; step++)
        {
            float share = (float(step) + offset) / float(Steps);
            float2 at = way * max(share * share, least);
            float aheadDepth = depthAt(uv + at);
            if (aheadDepth < 1.0)
            {
                float3 toward = worldAt(uv + at, aheadDepth) - p;
                float far = length(toward);
                float rise = dot(toward, v) / max(far, 1e-6);
                highest = max(highest, lerp(cos(normalAngle + 0.5 * Pi), rise, saturate((radius - far) / fade)));
            }
            float behindDepth = depthAt(uv - at);
            if (behindDepth < 1.0)
            {
                float3 toward = worldAt(uv - at, behindDepth) - p;
                float far = length(toward);
                float rise = dot(toward, v) / max(far, 1e-6);
                lowest = max(lowest, lerp(cos(normalAngle - 0.5 * Pi), rise, saturate((radius - far) / fade)));
            }
        }
        // The horizons' angles from the eye's way, held within the surface's half, and the light
        // between them weighed by the cosine to the normal, integrated over the slice.
        float ahead = normalAngle + min(acos(highest) - normalAngle, 0.5 * Pi);
        float behind = normalAngle + max(-acos(lowest) - normalAngle, -0.5 * Pi);
        float sinNormal = sin(normalAngle);
        seen += laidLength * 0.25 * (2.0 * cosNormal + 2.0 * (ahead + behind) * sinNormal
            - cos(2.0 * ahead - normalAngle) - cos(2.0 * behind - normalAngle));
    }
    // The share seen, raised to the strength, so 1 darkens as the horizons do and 0 not at all,
    // and where the field's occlusion is read, the share the field closes off past the horizons'
    // reach, which a corner of two walls closes half of at an intensity of 1.
    float strength = push.texelAndStrength.z;
    float shown = pow(saturate(seen / float(Slices)), strength);
    if (push.mode.z < 1.5 && fieldOn(fieldInfo))
        shown *= saturate(1.0 - strength * fieldOcclusion(p, n, radius));
    return float2(shown, sun);
}

// The occlusion just worked out, blended with what the pixel's point held the frame before where
// the frame before saw it at its depth along the view, within 5%, with the sun's share as worked
// out and the depth along the view in blue for the next frame.
float4 remember(float2 uv)
{
    float4 now = occlusion.SampleLevel(uv, 0);
    float d = depthAt(uv);
    if (d >= 1.0)
        return float4(1.0, now.g, 0.0, 1.0);
    float4 clip = float4(uv * 2.0 - 1.0, d, 1.0);
    float scale = dot(push.sunAndReach, clip);
    float4 before = mul(push.inverseViewProjection, clip);
    float2 uvBefore = before.xy / before.w * 0.5 + 0.5;
    float depthBefore = before.w / scale;
    float shown = now.r;
    if (push.mode.z > 0.5 && all(uvBefore >= 0.0) && all(uvBefore <= 1.0))
    {
        float4 kept = history.SampleLevel(uvBefore, 0);
        if (abs(kept.b - depthBefore) < 0.05 * depthBefore)
            shown = lerp(now.r, kept.r, Kept);
    }
    return float4(shown, now.g, 1.0 / scale, 1.0);
}

// Nine taps along one axis, a Gaussian's weights times how near each tap's distance from the eye is
// to the pixel's, as a share of the pixel's, of the occlusion and the sun's share both.
float2 blur(float2 uv, float2 step)
{
    float d = depthAt(uv);
    if (d >= 1.0)
        return float2(1.0, 1.0);
    float here = length(worldAt(uv, d) - push.eyeAndRadius.xyz);
    float2 sum = float2(0.0);
    float weights = 0.0;
    for (int i = -4; i <= 4; i++)
    {
        float2 at = uv + step * float(i);
        float tapDepth = depthAt(at);
        float there = tapDepth >= 1.0 ? 1e9 : length(worldAt(at, tapDepth) - push.eyeAndRadius.xyz);
        float weight = exp(-float(i * i) / 8.0) * exp(-abs(there - here) / max(here * 0.02, 1e-3));
        sum += occlusion.SampleLevel(at, 0).rg * weight;
        weights += weight;
    }
    return sum / max(weights, 1e-5);
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    float2 texel = push.texelAndStrength.xy;
    float2 shown;
    if (push.mode.x > 2.5)
        return remember(input.uv);
    if (push.mode.x < 0.5)
        shown = occlude(input.uv, input.position.xy);
    else if (push.mode.x < 1.5)
        shown = blur(input.uv, float2(texel.x, 0));
    else
        shown = blur(input.uv, float2(0, texel.y));
    return float4(shown.x, shown.y, shown.x, 1.0);
}
