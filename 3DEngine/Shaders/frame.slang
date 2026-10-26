// What the pass that brings the window's HDR frame into the window reads, and what it does to a
// color once the curve has brought it under 1, which composite.slang imports and so does a shader of
// the program's own that SetTonemapShader draws in its place. Its inputs are in descriptor set 1,
// so a program's own uniforms and textures keep set 0, set by SetShaderValue as any program's are:
//
//     import frame;
//
//     uniform float3 warmth;
//
//     [shader("fragment")]
//     float4 fragmentMain(FullscreenOutput input) : SV_Target
//     {
//         float3 light = frameLight(input.uv);
//         return frameShown(light / (1.0 + light) * warmth, input.uv);
//     }
module frame;

// sRGB's transfer functions and the engine's curve, and the fullscreen triangle's output.
__exported import color;
__exported import fullscreen;

public struct FramePush
{
    // xy: one texel of the bloom's first level in uv. z: how much of it is added, 0 for none. w: the exposure.
    public float4 bloom;
    // x: contrast. y: saturation. z: the curve, as Tonemap numbers it. w: the vignette's intensity.
    public float4 grade;
    // rgb: the tint, linear. a: where the vignette begins, from the middle toward a corner.
    public float4 tint;
    // x: 1 when the exposure follows the scene. y: the luminance it brings the scene's mean to.
    // z: 1 when the scene is read as the scene drew it, encoded on past 1, and 0 when a pass before
    // decoded it to linear light.
    public float4 exposure;
    // The curve set by its numbers, its exposure, white curve, lower curve and upper curve.
    public float4 curve;
    // x: the knee each channel past which the engine's curve bends toward 1 on its own. y: how much (tonemapKnee).
    public float4 highlight;
};

[[vk::push_constant]]
public ConstantBuffer<FramePush> framePush;

[[vk::binding(0, 1)]]
public Sampler2D frameScene;

[[vk::binding(1, 1)]]
public Sampler2D frameBloom;

// The log2 of the scene's mean luminance as the exposure that follows it has adapted to it, in one texel.
[[vk::binding(2, 1)]]
public Sampler2D frameAdapted;

// The table of Bevy's the curve looks light up in, AgX's, Tony McMapface's or Blender's filmic, or
// a texel standing in where the curve is worked out without one.
[[vk::binding(3, 1)]]
public Sampler3D frameTable;

public float luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// The scene's light at uv, linear, with the bloom chain's added, times the exposure and the one
// that follows the scene, what the curve brings under 1.
public float3 frameLight(float2 uv)
{
    float3 light = frameScene.SampleLevel(uv, 0).rgb;
    if (framePush.exposure.z > 0.0)
        light = srgbToLinear(light);
    if (framePush.bloom.z > 0.0)
        light += tent(frameBloom, uv, framePush.bloom.xy) * framePush.bloom.z;
    float exposure = framePush.bloom.w;
    if (framePush.exposure.x > 0.0)
        exposure *= framePush.exposure.y / exp2(frameAdapted.Load(int3(0, 0, 0)).r);
    return max(light * exposure, 0.0);
}

// A linear color under 1 as the window shows it at uv, with saturation and tint in linear light,
// contrast once encoded, about the middle of what is shown, and the corners darkened from where
// the vignette begins, measured to a corner as 1.
public float4 frameShown(float3 color, float2 uv)
{
    float3 c = max(lerp(luminance(color).xxx, color, framePush.grade.y) * framePush.tint.rgb, 0.0);
    float3 shown = linearToSrgb(c);
    shown = saturate((shown - 0.5) * framePush.grade.x + 0.5);
    float toCorner = length(uv - 0.5) * 1.41421356;
    shown *= 1.0 - framePush.grade.w * smoothstep(framePush.tint.a, 1.0, toCorner);
    return float4(shown, 1.0);
}
