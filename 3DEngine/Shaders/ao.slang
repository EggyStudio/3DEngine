// Ambient occlusion from the window's depth, drawn at half the window's size. Each pixel is put
// back in the world through the inverse of the camera's view-projection, its surface's normal
// found from the pixels beside it, and twelve pixels on a spiral within a radius of it in the
// world are taken as the surfaces around it. Each closes off the light from all around by how far
// it rises over the surface and how near it is, as McGuire, Mara and Luebke's Alchemy occlusion
// has it, so a floor darkens where a wall meets it and a corner more than either wall. A pass
// across and a pass down then blur the result, each tap weighed by how near its distance from the
// eye is to the pixel's, so the darkening keeps to the surface it is on and does not cross an edge.
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
    // x: 0 to work the occlusion out, 1 to blur it across, 2 to blur it down. y: the picture's
    // width over its height. z: 1 where the occlusion is on, 0 where only the sun's contact shadow
    // is worked out. w: unused.
    float4 mode;
    // xyz: the way toward the sun. w: how far its contact shadows are traced, 0 for none.
    float4 sunAndReach;
};

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

static const int Taps = 12;

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
    // every distance sums to five of those.
    return closed / (5.0 * 25.0 / 12.0);
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
            return 0.0;
        t += max(d, 0.5 * fieldCell(fieldInfo, q));
    }
    return saturate(shown);
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
    // The radius on the picture, held to three tenths of its height, so a surface near the eye does
    // not read the whole screen.
    float reach = min(radius * push.texelAndStrength.w / max(distance, 1e-3), 0.3);
    float2 aspect = float2(1.0 / push.mode.y, 1.0);
    float turn = noise(pixel) * 6.28318530718;

    float sum = 0.0;
    for (int i = 0; i < Taps; i++)
    {
        float share = (float(i) + 0.5) / float(Taps);
        float angle = float(i) * 2.39996323 + turn;
        float2 at = uv + float2(cos(angle), sin(angle)) * aspect * reach * share;
        float tapDepth = depthAt(at);
        if (tapDepth >= 1.0)
            continue;
        float3 v = worldAt(at, tapDepth) - p;
        float vv = dot(v, v);
        // A surface past the radius, as a wall far behind an edge, closes off nothing.
        float near = saturate(1.0 - vv / (radius * radius));
        sum += max(0.0, dot(v, n) - 0.01 * distance) / (vv + 1e-4) * near;
    }
    // Alchemy's sum over the taps, times the radius so the strength does not change with it, at
    // twice the paper's scale, which an intensity of 1 shows plainly in a lit room, and the share
    // the field closes off, which a corner of two walls closes half of at an intensity of 1.
    float shown = saturate(1.0 - 4.0 * push.texelAndStrength.z * radius * sum / float(Taps));
    if (fieldOn(fieldInfo))
        shown *= saturate(1.0 - push.texelAndStrength.z * fieldOcclusion(p, n, radius));
    return float2(shown, sun);
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
    if (push.mode.x < 0.5)
        shown = occlude(input.uv, input.position.xy);
    else if (push.mode.x < 1.5)
        shown = blur(input.uv, float2(texel.x, 0));
    else
        shown = blur(input.uv, float2(0, texel.y));
    return float4(shown.x, shown.y, shown.x, 1.0);
}
