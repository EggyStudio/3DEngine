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
    /// The window's models, and the shapes drawn up to the last one inside
    /// <see cref="BeginMode3D"/>, are drawn into a frame that holds light past 1, which bloom
    /// spreads and a last pass tonemaps into the window, whether bloom is on or off. Shapes, text
    /// and ImGui drawn after that go on top unchanged, so a game's interface keeps its exact colors.
    /// </para>
    /// <para>
    /// Light past 1 comes from emissive materials and strong lights, so a threshold of 1 blooms only
    /// what is brighter than white. An intensity of 1 adds as much light as was past the threshold,
    /// spread wide, and 0.3 to 1 suits most scenes. A shader of the program's own drawn inside
    /// <see cref="BeginMode3D"/> returns its color sRGB-encoded, with bloom on as with it off, and
    /// returns light past white for bloom through <c>toDisplay</c>.
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
    /// Makes the exposure follow the scene, as an eye adapts, brightening a dark room and dimming a
    /// sunlit field, between <paramref name="min"/> and <paramref name="max"/>, or turns it off. At
    /// a <paramref name="speed"/> of 1 it moves about two thirds of the way to a new scene's exposure
    /// in a second, at 2 most of the way, and at 0 it keeps where the first frame put it.
    /// </summary>
    /// <remarks>
    /// The mean brightness of the scene, weighted toward the middle of the picture and measured on
    /// the GPU each frame, is brought to that of a mid gray. <see cref="SetExposure"/> multiplies
    /// what it chooses, so 1.5 keeps every scene a little brighter than that. The exposure starts
    /// where the first frame puts it, and moves over later frames, so a player walking out of a cave
    /// is dazzled for a moment.
    /// </remarks>
    public static void SetAutoExposure(bool enabled, float min = 0.25f, float max = 4, float speed = 2)
    {
        var effects = Effects;
        effects.AutoExposure = enabled;
        effects.AutoExposureMin = Math.Max(1e-3f, Math.Min(min, max));
        effects.AutoExposureMax = Math.Max(1e-3f, Math.Max(min, max));
        effects.AutoExposureSpeed = Math.Max(0, speed);
    }

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

    /// <summary>
    /// Blurs what is nearer or farther than <paramref name="focusDistance"/> from the camera, as a
    /// camera's lens does, growing over <paramref name="focusRange"/> either side of it to
    /// <paramref name="blur"/> of the picture's height across, or turns it off with a blur of 0.
    /// </summary>
    /// <remarks>
    /// It works from the depth of the window's scene, so what is drawn after <c>EndMode3D</c>, a
    /// game's interface, stays sharp. A blur of 0.01 is about four pixels at raylib's window, and
    /// more than 0.03 spreads thinly. A blurred thing in front spreads over what is sharp behind it,
    /// and one behind never over what is in front.
    /// </remarks>
    public static void SetDepthOfField(float focusDistance, float focusRange, float blur)
    {
        var effects = Effects;
        effects.FocusDistance = Math.Max(0, focusDistance);
        effects.FocusRange = Math.Max(1e-3f, focusRange);
        effects.FocusBlur = Math.Clamp(blur, 0, 0.1f);
    }

    /// <summary>
    /// Blurs the picture along the way the camera moved it since the frame before, by
    /// <paramref name="amount"/> of that movement, 0.5 as a film camera's shutter does, or turns it
    /// off with 0.
    /// </summary>
    /// <remarks>
    /// Only the camera's movement is blurred along, so a thing moving across a still camera stays
    /// sharp, unless <see cref="SetMotionBlur(float, bool)"/> blurs mesh entities by their own too,
    /// and a cut to another camera blurs nothing in its first frame, as long as the camera moved
    /// less than a tenth of the picture.
    /// </remarks>
    public static void SetMotionBlur(float amount) => SetMotionBlur(amount, objects: false);

    /// <summary>
    /// Blurs the window's frame along the camera's movement since the frame before and, with
    /// <paramref name="objects"/>, each mesh entity along its own, an <paramref name="amount"/> of
    /// that movement, or turns it off with 0.
    /// </summary>
    /// <remarks>
    /// An entity's movement is read from where its transform put it the frame before, so a mesh
    /// entity crossing a still camera blurs along its path. A model drawn with <c>DrawModel</c> has
    /// no frame before to be read from and blurs by the camera's movement alone, as do a skinned
    /// mesh's limbs, which move its vertices rather than its transform. It costs a draw of each
    /// entity that moved into an image of the window's size.
    /// </remarks>
    public static void SetMotionBlur(float amount, bool objects)
    {
        Effects.MotionBlur = Math.Clamp(amount, 0, 1);
        Effects.MotionBlurObjects = objects;
    }
}
