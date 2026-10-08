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

// The table of Bevy's the curve looks light up in, AgX's, Tony McMapface's or Blender's filmic, or
// a texel standing in where the curve is worked out without one.
[[vk::binding(3, 0)]]
Sampler3D table;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// Bevy's curves, as bevy_core_pipeline 0.19.1's tonemapping_shared.wgsl works them out, each of
// its matrices written as the dot products it takes of a color.

float3 reinhardLuminance(float3 c)
{
    float l = luminance(c);
    return l > 0.0 ? c * (1.0 / (1.0 + l)) : float3(0.0);
}

// Stephen Hill's fit of ACES's reference and output transforms, from MJP's BakingLab.
float3 acesFitted(float3 c)
{
    float3 v = float3(dot(c, float3(0.59719, 0.35458, 0.04823)), dot(c, float3(0.07600, 0.90834, 0.01566)),
        dot(c, float3(0.02840, 0.13383, 0.83777)));
    v = (v * (v + 0.0245786) - 0.000090537) / (v * (0.983729 * v + 0.4329510) + 0.238081);
    return saturate(float3(dot(v, float3(1.60475, -0.53108, -0.07367)), dot(v, float3(-0.10208, 1.10813, -0.00605)),
        dot(v, float3(-0.00327, -0.07276, 1.07602))));
}

float boringCurve(float v)
{
    return 1.0 - exp(-v);
}

// Tomasz Stachowiak's somewhat boring display transform: the luminance bent and the color scaled
// with it, mixed toward each channel bent alone as the color grows saturated, and desaturated as
// it grows bright.
float3 somewhatBoring(float3 c)
{
    float3 ycbcr = float3(luminance(c), dot(c, float3(-0.1146, -0.3854, 0.5)), dot(c, float3(0.5, -0.4542, -0.0458)));
    float bt = boringCurve(length(ycbcr.yz) * 2.4);
    float desaturated = max((bt - 0.7) * 0.8, 0.0);
    desaturated *= desaturated;
    float3 toward = lerp(c, ycbcr.xxx, desaturated);
    float3 scaled = c * max(0.0, boringCurve(ycbcr.x) / max(1e-5, luminance(c)));
    float3 alone = float3(boringCurve(toward.r), boringCurve(toward.g), boringCurve(toward.b));
    return lerp(scaled, alone, bt * bt) * 0.97;
}

// Light as log2 stops about a mid gray of 0.18, from minimumEv to maximumEv as 0 to 1.
float3 normalizedLog2(float3 c, float minimumEv, float maximumEv)
{
    c = max(c, 0.0);
    c = select(c < 0.00003051757, 0.00001525878 + c, c);
    c = clamp(log2(c / 0.18), minimumEv, maximumEv);
    return (c - minimumEv) / (maximumEv - minimumEv);
}

// The table at a point of the unit cube, its texels' middles at the cube's edges, for a table
// size texels a side.
float3 lookUp(float3 at, float size)
{
    return table.SampleLevel(at * ((size - 1.0) / size) + 0.5 / size, 0).rgb;
}

// Troy Sobotka's AgX: the color through AgX's inset, as log2 stops from -10 to 6.5, looked up.
float3 agx(float3 c)
{
    c = float3(dot(c, float3(0.84247906, 0.0784336, 0.07922375)), dot(c, float3(0.04232824, 0.87846864, 0.07916613)),
        dot(c, float3(0.04237565, 0.0784336, 0.87914297)));
    return lookUp(saturate(normalizedLog2(c, -10.0, 6.5)), 32.0);
}

// Tomasz Stachowiak's Tony McMapface, the color over one plus itself looked up in a table 48 a side.
float3 tonyMcMapface(float3 c)
{
    return table.SampleLevel(saturate(c / (c + 1.0) * (47.0 / 48.0) + 0.5 / 48.0), 0).rgb;
}

// Blender's filmic view transform, the color as log2 stops from -11 to 12 looked up.
float3 blenderFilmic(float3 c)
{
    return lookUp(saturate(normalizedLog2(c, -11.0, 12.0)), 64.0);
}

// The curve, as Tonemap numbers it.
float3 curve(float3 c, int which)
{
    switch (which)
    {
    case 1: return c / (1.0 + c);
    case 2: return saturate((c * (2.51 * c + 0.03)) / (c * (2.43 * c + 0.59) + 0.14));
    case 3:
    case 4: return saturate(c);
    case 5: return reinhardLuminance(c);
    case 6: return acesFitted(c);
    case 7: return agx(c);
    case 8: return somewhatBoring(c);
    case 9: return tonyMcMapface(c);
    case 10: return blenderFilmic(c);
    default: return tonemap(c);
    }
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
