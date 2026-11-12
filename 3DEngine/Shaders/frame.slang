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
//         float4 shown = frameShown(light / (1.0 + light) * warmth, input.uv);
//         return float4(frameDither(shown.rgb, input.position.xy), shown.a);
//     }
module frame;

// sRGB's transfer functions and the engine's curve, and the fullscreen triangle's output.
__exported import color;
__exported import fullscreen;

public struct FramePush
{
    // xy: one texel of the bloom's first level in uv. z: how much of it is added or mixed in, 0 for
    // none. w: the exposure.
    public float4 bloom;
    // x: contrast. y: saturation. z: the curve, as Tonemap numbers it. w: the vignette's intensity.
    public float4 grade;
    // rgb: the tint, linear. a: where the vignette begins, from the middle toward a corner.
    public float4 tint;
    // x: 1 when the exposure follows the scene. y: the luminance it brings the scene's mean to.
    // z: 1 when the scene is read as the scene drew it, encoded on past 1, and 0 when a pass before
    // decoded it to linear light. w: the exposed light past which the bloom takes the place of the
    // light, so the frame keeps the light it had, or -1 where it is added.
    public float4 exposure;
    // The chosen curve's numbers. The engine's, the knee past which each channel bends toward 1 on
    // its own and how much (tonemapKnee). The one set by its numbers, its exposure, white curve,
    // lower curve and upper curve. A table loaded from a file, its share and its size.
    public float4 curve;
    // rgb: the lift, added to the light after the gain. w: 1 when lift, gamma and gain are set.
    public float4 lift;
    // rgb: the power the lifted light is raised to, one over the gamma. w: 1 where what is shown is
    // dithered before the window's eight bits take it (frameDither).
    public float4 gamma;
    // rgb: the gain, the light is multiplied by. w: how far the dither's pattern moves this frame,
    // the frame's number where temporal antialiasing blends the frames and 0 where nothing does.
    public float4 gain;
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

// The table the curve looks light up in, AgX's, Tony McMapface's or Blender's filmic, or
// a texel standing in where the curve is worked out without one.
[[vk::binding(3, 1)]]
public Sampler3D frameTable;

public float luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// The light past threshold, eased in over half of it, so what blooms has no edge.
float3 pastThreshold(float3 c, float threshold)
{
    float peak = max(c.r, max(c.g, c.b));
    float knee = threshold * 0.5 + 1e-5;
    float soft = clamp(peak - threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft * (0.25 / knee);
    return c * (max(soft, peak - threshold) / max(peak, 1e-4));
}

// The scene's light at uv, linear, with the bloom chain's added, times the exposure and the one
// that follows the scene, then lifted, raised and gained, what the curve brings under 1. Where the
// bloom is mixed in, the light past its threshold gives way to the spread light by its intensity,
// so the frame keeps the light it had.
public float3 frameLight(float2 uv)
{
    float3 light = frameScene.SampleLevel(uv, 0).rgb;
    if (framePush.exposure.z > 0.0)
        light = srgbToLinear(light);
    float exposure = framePush.bloom.w;
    if (framePush.exposure.x > 0.0)
        exposure *= framePush.exposure.y / exp2(frameAdapted.Load(int3(0, 0, 0)).r);
    float3 spread = framePush.bloom.z > 0.0 ? tent(frameBloom, uv, framePush.bloom.xy) : float3(0.0);
    if (framePush.exposure.w < 0.0)
        light = max((light + spread * framePush.bloom.z) * exposure, 0.0);
    else
    {
        light = max(light * exposure, 0.0);
        light = lerp(light, light - pastThreshold(light, framePush.exposure.w) + spread * exposure, framePush.bloom.z);
    }
    if (framePush.lift.w > 0.0)
        light = pow(max(light * framePush.gain.rgb + framePush.lift.rgb, 0.0), framePush.gamma.rgb);
    return light;
}

// What is shown at a window's pixel, encoded, with a little noise in it before the window's eight
// bits take it, so a slow gradient in the dark, a few levels across many pixels, falls in a fine
// grain the eye takes as smooth where it fell in hard steps a level apart. The noise is up to a
// level either way, likelier small than large as the sum of two even draws is, which keeps both the
// shown light's mean and how much its grain strays the same at every level. It is drawn from an
// interleaved gradient, which makes no pattern of its own, and is none where a channel is black or
// white, which stay so. It holds still at a pixel from frame to frame, so two pictures of a still
// view are alike, and moves each frame only where temporal antialiasing blends the frames, which
// then take it away.
public float3 frameDither(float3 shown, float2 pixel)
{
    if (framePush.gamma.w <= 0.0)
        return shown;
    float2 at = pixel + framePush.gain.w * 5.588238;
    float even = frac(52.9829189 * frac(dot(at, float2(0.06711056, 0.00583715))));
    float noise = even < 0.5 ? sqrt(2.0 * even) - 1.0 : 1.0 - sqrt(2.0 - 2.0 * even);
    float3 room = saturate(min(shown, 1.0 - shown) * 255.0);
    return shown + noise / 255.0 * room;
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
