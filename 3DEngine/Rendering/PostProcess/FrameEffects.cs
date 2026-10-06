using System.Numerics;

namespace Engine;

/// <summary>
/// The effects over the frame besides bloom, a world resource the renderer reads each frame, which
/// the flat API's <c>SetExposure</c>, <c>SetAutoExposure</c>, <c>SetTonemap</c>,
/// <c>SetColorGrading</c>, <c>SetVignette</c>, <c>SetFxaa</c>, <c>SetDepthOfField</c> and
/// <c>SetMotionBlur</c> set.
/// </summary>
/// <remarks>
/// Any of them away from its default draws the scene through the HDR frame, as bloom does, and
/// applies it in the pass that brings that frame into the window. What is drawn after the scene, a
/// game's interface and ImGui, is drawn over the result untouched.
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

    /// <summary>How quickly the exposure follows the scene, the share of the way it moves in a second being 1 less e to the minus this.</summary>
    public float AutoExposureSpeed { get; set; } = 2;

    /// <summary>The curve the light is brought under 1 by.</summary>
    public Tonemap Tonemap { get; set; } = Tonemap.Engine;

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

    /// <summary>The distance from the camera that is sharp, for the depth of field.</summary>
    public float FocusDistance { get; set; } = 10;

    /// <summary>How far before and past the focus the blur grows to its widest.</summary>
    public float FocusRange { get; set; } = 5;

    /// <summary>The widest blur, as a share of the picture's height, 0 for no depth of field.</summary>
    public float FocusBlur { get; set; }

    /// <summary>The share of the camera's movement in a frame blurred along, 0 for no motion blur and 0.5 as a film camera's shutter.</summary>
    public float MotionBlur { get; set; }

    /// <summary>Whether any effect is away from its default, which draws the frame through the HDR target.</summary>
    public bool Active => Exposure != 1 || AutoExposure || Tonemap != Tonemap.Engine || Contrast != 1 || Saturation != 1
                          || Tint != Color.White || Vignette > 0 || Fxaa || FocusBlur > 0 || MotionBlur > 0;
}
