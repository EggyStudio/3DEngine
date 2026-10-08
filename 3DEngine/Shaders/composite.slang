// The HDR frame onto the window, drawn every frame the window shows a scene. The scene's light,
// decoded where the frame holds it encoded as the scene drew it, with the bloom chain's added,
// scaled by the exposure and the one that follows the scene, brought under 1 by the chosen curve,
// graded, encoded, and darkened toward the corners.
// With every effect at its default it is the engine's own curve and the encoding, which a render
// texture's view applies in the model pass, so the window's scene looks as a render texture's.
import color;
import fullscreen;

struct CompositePush
{
    // xy: one texel of the bloom's first level in uv. z: how much of it is added, 0 for none. w: the exposure.
    float4 bloom;
    // x: contrast. y: saturation. z: the curve, as Tonemap numbers it. w: the vignette's intensity.
    float4 grade;
    // rgb: the tint, linear. a: where the vignette begins, from the middle toward a corner.
    float4 tint;
    // x: 1 when the exposure follows the scene. y: the luminance it brings the scene's mean to.
    // z: 1 when the scene is read as the scene drew it, encoded on past 1, and 0 when a pass before
    // decoded it to linear light.
    float4 exposure;
};

[[vk::push_constant]]
ConstantBuffer<CompositePush> push;

[[vk::binding(0, 0)]]
Sampler2D scene;

[[vk::binding(1, 0)]]
Sampler2D bloom;

// The log2 of the scene's mean luminance as the exposure that follows it has adapted to it, in one texel.
[[vk::binding(2, 0)]]
Sampler2D adapted;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

float3 curve(float3 c, int which)
{
    if (which == 1)
        return c / (1.0 + luminance(c));
    if (which == 2)
        return saturate((c * (2.51 * c + 0.03)) / (c * (2.43 * c + 0.59) + 0.14));
    if (which == 3)
        return saturate(c);
    return tonemap(c);
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    float3 light = scene.SampleLevel(input.uv, 0).rgb;
    if (push.exposure.z > 0.0)
        light = srgbToLinear(light);
    if (push.bloom.z > 0.0)
        light += tent(bloom, input.uv, push.bloom.xy) * push.bloom.z;
    float exposure = push.bloom.w;
    if (push.exposure.x > 0.0)
        exposure *= push.exposure.y / exp2(adapted.Load(int3(0, 0, 0)).r);
    float3 c = curve(max(light * exposure, 0.0), int(push.grade.z));

    // Grading after the curve, saturation and tint in linear light and contrast once encoded,
    // about the middle of what is shown.
    c = max(lerp(luminance(c).xxx, c, push.grade.y) * push.tint.rgb, 0.0);
    float3 shown = linearToSrgb(c);
    shown = saturate((shown - 0.5) * push.grade.x + 0.5);

    // The corners darkened from where the vignette begins, measured to a corner as 1.
    float toCorner = length(input.uv - 0.5) * 1.41421356;
    shown *= 1.0 - push.grade.w * smoothstep(push.tint.a, 1.0, toCorner);
    return float4(shown, 1.0);
}
