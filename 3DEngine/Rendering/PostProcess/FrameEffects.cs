using System.Numerics;

namespace Engine;

/// <summary>
/// The effects over the frame besides bloom, a world resource the renderer reads each frame, which
/// the flat API's <c>SetExposure</c>, <c>SetAutoExposure</c>, <c>SetTonemap</c>,
/// <c>SetTonemapCurve</c>, <c>SetTonemapLookup</c>, <c>SetTonemapShader</c>, <c>SetLiftGammaGain</c>,
/// <c>SetColorGrading</c>, <c>SetVignette</c>, <c>SetFxaa</c>, <c>SetTemporalAntialiasing</c>,
/// <c>SetDither</c>, <c>SetDepthOfField</c> and <c>SetMotionBlur</c> set.
/// </summary>
/// <remarks>
/// Each is applied in the pass that brings the HDR frame into the window, which the window's scene
/// is drawn through whatever they are set to. What is drawn after the scene, a game's interface and
/// ImGui, is drawn over the result untouched.
/// </remarks>
internal sealed class FrameEffects
{
    /// <summary>What the scene's light is multiplied by before the curve, 1 unless set, and on top of the exposure that follows the scene when that is on.</summary>
    public float Exposure { get; set; } = 1;

    /// <summary>Whether the exposure follows the scene, brightening a dark one and dimming a bright one as an eye adapts.</summary>
    public bool AutoExposure { get; set; }

    /// <summary>The lowest exposure the one that follows the scene goes to, in the brightest scene.</summary>
    public float AutoExposureMin { get; set; } = 0.25f;

    /// <summary>The highest exposure the one that follows the scene goes to, in the darkest scene.</summary>
    public float AutoExposureMax { get; set; } = 4;

    /// <summary>
    /// The scenes' exposure values at ISO 100 the exposure that follows the scene meters between, the
    /// darkest and the brightest, which give <see cref="AutoExposureMin"/> and
    /// <see cref="AutoExposureMax"/> against the camera's, or null where those were given themselves.
    /// </summary>
    public (float Lowest, float Highest)? AutoExposureEv100 { get; set; }

    /// <summary>How quickly the exposure follows a scene that brightens, the share of the way it moves in a second being 1 less e to the minus this.</summary>
    public float AutoExposureSpeed { get; set; } = 2;

    /// <summary>How quickly the exposure follows a scene that darkens, as <see cref="AutoExposureSpeed"/> counts it.</summary>
    public float AutoExposureDarkeningSpeed { get; set; } = 2;

    /// <summary>
    /// The share of the picture's weighted light, from its darkest, below which the exposure that
    /// follows the scene leaves it out, 0 for none.
    /// </summary>
    public float AutoExposureLow { get; set; }

    /// <summary>The share of the picture's weighted light, from its darkest, above which the exposure leaves it out, 1 for none.</summary>
    public float AutoExposureHigh { get; set; } = 1;

    /// <summary>How the exposure that follows the scene weighs the picture.</summary>
    public ExposureMetering AutoExposureMetering { get; set; }

    /// <summary>The stops the exposure that follows the scene is moved by from a mid gray, brighter above 0.</summary>
    public float AutoExposureCompensation { get; set; }

    /// <summary>
    /// The level the measured luminance is brought to, a mid gray, about what the engine's lit
    /// scenes show at an exposure of 1, moved by the compensation.
    /// </summary>
    public float AutoExposureKey => 0.18f * MathF.Pow(2, AutoExposureCompensation);

    /// <summary>The curve the light is brought under 1 by.</summary>
    public Tonemap Tonemap { get; set; } = Tonemap.Engine;

    /// <summary>The level past which the engine's curve bends each channel of the light toward 1 on its own.</summary>
    public float HighlightKnee { get; set; } = 0.8f;

    /// <summary>How much the channels past <see cref="HighlightKnee"/> are bent, from 0 for none to 1.</summary>
    public float HighlightStrength { get; set; } = 1;

    /// <summary>
    /// The numbers of <see cref="Tonemap.Curve"/>, its exposure, white curve, lower curve and upper
    /// curve, BSL's shader's defaults until set.
    /// </summary>
    public Vector4 Curve { get; set; } = new(1.4f, 2, 1.2f, 1.5f);

    /// <summary>The table <see cref="Tonemap.Lookup"/> looks light up in, as SetTonemapLookup read it, or null before one is.</summary>
    public TonemapTables.Table? Lookup { get; set; }

    /// <summary>The file <see cref="Lookup"/> was read from, as the program named it.</summary>
    public string? LookupFile { get; set; }

    /// <summary>The share of the light the looked-up light takes the place of, from 0 for none to 1.</summary>
    public float LookupShare { get; set; } = 1;

    /// <summary>
    /// The lift, gamma and gain, each a color in x, y and z and an offset in w, white and 0 for
    /// none.
    /// </summary>
    public Vector4 Lift { get; set; } = new(1, 1, 1, 0);

    /// <inheritdoc cref="Lift"/>
    public Vector4 Gamma { get; set; } = new(1, 1, 1, 0);

    /// <inheritdoc cref="Lift"/>
    public Vector4 Gain { get; set; } = new(1, 1, 1, 0);

    /// <summary>Whether lift, gamma and gain change the light, any of them set to other than white and 0.</summary>
    public bool Grades => Lift != new Vector4(1, 1, 1, 0) || Gamma != new Vector4(1, 1, 1, 0) || Gain != new Vector4(1, 1, 1, 0);

    /// <summary>
    /// What the light is lifted by, raised to and gained by, each color decoded from sRGB, scaled,
    /// its luminance taken away and its offset added, so a white color moves every channel by its
    /// offset alone and a hue tints its part of the light without changing how bright it is.
    /// </summary>
    public (Vector3 Lift, Vector3 Gamma, Vector3 Gain) LiftGammaGain()
    {
        static Vector3 Hue(Vector4 color, float scale)
        {
            var c = new Vector3(BloomRenderer.SrgbToLinear(color.X), BloomRenderer.SrgbToLinear(color.Y), BloomRenderer.SrgbToLinear(color.Z)) * scale;
            return c - new Vector3(Vector3.Dot(c, new Vector3(0.2126f, 0.7152f, 0.0722f)));
        }
        var lift = Hue(Lift, 0.15f) + new Vector3(Lift.W);
        var gamma = Hue(Gamma, 0.8f) + new Vector3(Gamma.W + 1);
        var gain = Hue(Gain, 0.8f) + new Vector3(Gain.W + 1);
        return (lift, Vector3.One / Vector3.Max(gamma, new Vector3(1e-3f)), gain);
    }

    /// <summary>The program's shader drawn in the curve's place, by its id, or 0 for the curve.</summary>
    public int TonemapShader { get; set; }

    /// <summary>The values of that shader's uniforms as the program last set them, the bytes of its uniform buffer.</summary>
    public byte[]? TonemapUniforms { get; set; }

    /// <summary>The textures and buffers that shader was last given, as a draw through it is given them.</summary>
    public int[]? TonemapTextures { get; set; }

    /// <summary>How far each channel is pushed from the middle gray after the curve, 1 for as it is.</summary>
    public float Contrast { get; set; } = 1;

    /// <summary>How colorful the frame is, 0 for gray, 1 for as it is, more for stronger color.</summary>
    public float Saturation { get; set; } = 1;

    /// <summary>A color the frame is multiplied by, white for none.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>How much the corners darken, 0 for none and 1 to black.</summary>
    public float Vignette { get; set; }

    /// <summary>Where the darkening begins, as a share of the way from the middle to a corner.</summary>
    public float VignetteRadius { get; set; } = 0.5f;

    /// <summary>Whether FXAA smooths the edges multisampling leaves, those inside a surface and of thin lines.</summary>
    public bool Fxaa { get; set; }

    /// <summary>
    /// Whether what the window shows is dithered before its eight bits take it, so a slow gradient
    /// in the dark falls in a fine grain rather than in steps a level apart; off unless set.
    /// </summary>
    public bool Dither { get; set; }

    /// <summary>
    /// Whether the window's camera is moved by a part of a pixel each frame and each frame's light
    /// resolved against those before (<see cref="TemporalRenderer"/>).
    /// </summary>
    public bool Temporal { get; set; }

    /// <summary>The distance from the camera that is sharp, for the depth of field.</summary>
    public float FocusDistance { get; set; } = 10;

    /// <summary>How far before and past the focus the blur grows to its widest.</summary>
    public float FocusRange { get; set; } = 5;

    /// <summary>The widest blur, as a share of the picture's height, 0 for no depth of field.</summary>
    public float FocusBlur { get; set; }

    /// <summary>The share of the camera's movement in a frame blurred along, 0 for no motion blur and 0.5 as a film camera's shutter.</summary>
    public float MotionBlur { get; set; }

    /// <summary>Whether motion blur blurs each mesh entity by its own movement as well as the camera's.</summary>
    public bool MotionBlurObjects { get; set; }

}
