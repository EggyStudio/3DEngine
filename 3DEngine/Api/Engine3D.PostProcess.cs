namespace Engine;

public static partial class Engine3D
{
    /// <summary>
    /// Spreads light brighter than <paramref name="threshold"/> into the pixels around it, as a
    /// bright light glows through a lens, adding <paramref name="intensity"/> of it to the frame, or
    /// turns bloom off with an intensity of 0, which it is by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With bloom on, the window's models, and the shapes drawn up to the last one inside
    /// <see cref="BeginMode3D"/>, are drawn into a frame that holds light past 1, which bloom
    /// spreads and a last pass tonemaps into the window. Shapes, text and ImGui drawn after that go
    /// on top unchanged, so a game's interface keeps its exact colors.
    /// </para>
    /// <para>
    /// Light past 1 comes from emissive materials and strong lights, so a threshold of 1 blooms only
    /// what is brighter than white. An intensity of 1 adds as much light as was past the threshold,
    /// spread wide, and 0.3 to 1 suits most scenes. A shader of the program's own drawn inside
    /// <see cref="BeginMode3D"/> writes linear light while bloom is on, so its colors look lighter
    /// unless it decodes them from sRGB first.
    /// </para>
    /// </remarks>
    public static void SetBloom(float intensity, float threshold = 1)
    {
        var bloom = World.GetOrInsertResource(() => new BloomSettings());
        bloom.Intensity = Math.Max(0, intensity);
        bloom.Threshold = Math.Max(0, threshold);
    }

    private static FrameEffects Effects => World.GetOrInsertResource(static () => new FrameEffects());

    /// <summary>
    /// Multiplies the scene's light by <paramref name="exposure"/> before it is brought under 1, so
    /// 2 brightens a dark scene and 0.5 dims a bright one, 1 being as it is and the default.
    /// </summary>
    public static void SetExposure(float exposure) => Effects.Exposure = Math.Max(0, exposure);

    /// <summary>
    /// Chooses the curve that brings the scene's light past 1 under it: the engine's own, which
    /// leaves colors under 0.9 as they are, Reinhard's, ACES's filmic one, or a cut at 1.
    /// </summary>
    public static void SetTonemap(Tonemap curve) => Effects.Tonemap = curve;

    /// <summary>
    /// Grades the scene's color. <paramref name="contrast"/> pushes each channel from the middle gray,
    /// <paramref name="saturation"/> takes color away toward gray below 1 and adds it above, and
    /// <paramref name="tint"/> multiplies it, white for none. 1, 1 and white leave it as it is.
    /// </summary>
    public static void SetColorGrading(float contrast, float saturation, Color tint)
    {
        var effects = Effects;
        effects.Contrast = Math.Max(0, contrast);
        effects.Saturation = Math.Max(0, saturation);
        effects.Tint = tint;
    }

    /// <summary>
    /// Darkens the scene toward its corners by <paramref name="intensity"/>, 0 for none and 1 to black,
    /// from <paramref name="radius"/> of the way from the middle to a corner.
    /// </summary>
    public static void SetVignette(float intensity, float radius = 0.5f)
    {
        var effects = Effects;
        effects.Vignette = Math.Clamp(intensity, 0, 1);
        effects.VignetteRadius = Math.Clamp(radius, 0, 1);
    }

    /// <summary>
    /// Turns FXAA on or off, which smooths the jagged edges multisampling leaves: those inside a
    /// surface, as a sharp highlight's or a texture's, and of lines thinner than a pixel.
    /// </summary>
    public static void SetFxaa(bool enabled) => Effects.Fxaa = enabled;
}
