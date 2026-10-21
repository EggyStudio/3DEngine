namespace Engine;

public static partial class E3D
{
    /// <summary>
    /// Spreads light brighter than <paramref name="threshold"/> into the pixels around it, as a
    /// bright light spreads through a lens, adding <paramref name="intensity"/> of it to the frame, or
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

    /// <summary>The bloom's intensity and threshold as <see cref="SetBloom"/> left them, 0 and 1 unless set.</summary>
    public static (float Intensity, float Threshold) GetBloom() =>
        TryRes<BloomSettings>(out var bloom) ? (bloom.Intensity, bloom.Threshold) : (0, 1);

    private static FrameEffects Effects => World.GetOrInsertResource(static () => new FrameEffects());

    /// <summary>
    /// Multiplies the scene's light by <paramref name="exposure"/> before it is brought under 1, so
    /// 2 brightens a dark scene and 0.5 dims a bright one, 1 being as it is and the default.
    /// </summary>
    public static void SetExposure(float exposure) => Effects.Exposure = Math.Max(0, exposure);

    /// <summary>What the scene's light is multiplied by before its curve, as <see cref="SetExposure"/> left it, 1 unless set.</summary>
    public static float GetExposure() => Effects.Exposure;

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
    /// Whether the exposure follows the scene, and its bounds and speed, as
    /// <see cref="SetAutoExposure"/> left them, off between 0.25 and 4 at a speed of 2 unless set.
    /// </summary>
    public static (bool Enabled, float Min, float Max, float Speed) GetAutoExposure()
    {
        var effects = Effects;
        return (effects.AutoExposure, effects.AutoExposureMin, effects.AutoExposureMax, effects.AutoExposureSpeed);
    }

    /// <summary>
    /// Chooses the curve that brings the scene's light past 1 under it, the engine's own, which
    /// leaves colors under 0.9 as they are, Narkowicz's fit of ACES, a cut at 1, or one of the
    /// eight Bevy offers, drawn as Bevy draws them.
    /// </summary>
    /// <remarks>
    /// <see cref="Tonemap.AgX"/>, <see cref="Tonemap.TonyMcMapface"/> and
    /// <see cref="Tonemap.BlenderFilmic"/> look the light up in Bevy's own tables, each read the
    /// first time its curve is chosen, which takes that frame a few milliseconds.
    /// </remarks>
    public static void SetTonemap(Tonemap curve) => Effects.Tonemap = curve;

    /// <summary>The curve the scene's light past 1 is brought under by, <see cref="Tonemap.Engine"/> unless set.</summary>
    public static Tonemap GetTonemap() => Effects.Tonemap;

    /// <summary>
    /// Brings the scene's light under 1 by a curve set by four numbers, as the BSL shader draws it,
    /// and chooses that curve, <see cref="Tonemap.Curve"/>.
    /// </summary>
    /// <remarks>
    /// The curve takes the light times <paramref name="exposure"/>, then c / (c^w + 1)^(1/w) with w
    /// the <paramref name="white"/> curve, then c to the power of the <paramref name="lower"/> curve
    /// in the darks and the <paramref name="upper"/> in the lights, mixed by c's square root, shown
    /// with a gamma of 2.2, so the value it gives is the value shown, as the shader writes it to the
    /// screen. The shader's defaults are 1.4, 2, 1.2 and 1.5. Its saturation is
    /// <see cref="SetColorGrading"/>'s, which works in linear light. <see cref="SetExposure"/> and
    /// the exposure that follows the scene multiply the light before the curve's own exposure.
    /// </remarks>
    public static void SetTonemapCurve(float exposure, float white, float lower, float upper)
    {
        var effects = Effects;
        effects.Curve = new System.Numerics.Vector4(Math.Max(0, exposure), Math.Max(0.01f, white), Math.Max(0.01f, lower), Math.Max(0.01f, upper));
        effects.Tonemap = Tonemap.Curve;
    }

    /// <summary>
    /// The numbers of the curve <see cref="SetTonemapCurve"/> sets, as it left them, 1.4, 2, 1.2 and
    /// 1.5 unless set, whether or not that curve is the one chosen.
    /// </summary>
    public static (float Exposure, float White, float Lower, float Upper) GetTonemapCurve()
    {
        var curve = Effects.Curve;
        return (curve.X, curve.Y, curve.Z, curve.W);
    }

    /// <summary>
    /// Draws the window's scene through <paramref name="shader"/>'s fragment stage in the curve's
    /// place, for a curve no other setting gives, or the chosen curve again with the default shader.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shader imports the engine's <c>frame</c> module and draws over a triangle that covers the
    /// window, taking a <c>FullscreenOutput</c>. <c>frameLight(uv)</c> is its input, the scene's
    /// linear light with the bloom added and times the exposure, and <c>frameShown(color, uv)</c> its
    /// output, which grades, encodes and vignettes a linear color under 1 as every curve's is. Its
    /// own uniforms and textures are set as any shader's, and the values set reach the frames drawn
    /// after.
    /// </para>
    /// <para>
    /// A vertex stage of the shader's own is not used. A shader with no fragment stage leaves the
    /// curve in place, with a warning.
    /// </para>
    /// </remarks>
    public static void SetTonemapShader(Shader shader)
    {
        var effects = Effects;
        if (shader.IsValid && Res<ShaderStore>().Get(shader.Id) is { } program && !program.Stages.ContainsKey(ShaderStage.Fragment))
        {
            ApiLogger.Warn($"SetTonemapShader: '{program.Name}' has no fragment stage, so the curve is kept.");
            shader = default;
        }
        effects.TonemapShader = shader.Id;
        (effects.TonemapUniforms, effects.TonemapTextures) = shader.IsValid ? (UniformSnapshot(shader), TextureSnapshot(shader)) : (null, null);
    }

    /// <summary>The shader <see cref="SetTonemapShader"/> draws the scene through in the curve's place, or the default shader for the curve.</summary>
    public static Shader GetTonemapShader() => new(Effects.TonemapShader);

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

    /// <summary>The contrast, saturation and tint <see cref="SetColorGrading"/> left, 1, 1 and white unless set.</summary>
    public static (float Contrast, float Saturation, Color Tint) GetColorGrading()
    {
        var effects = Effects;
        return (effects.Contrast, effects.Saturation, effects.Tint);
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

    /// <summary>How much the corners darken and from how far out, as <see cref="SetVignette"/> left them, 0 and 0.5 unless set.</summary>
    public static (float Intensity, float Radius) GetVignette()
    {
        var effects = Effects;
        return (effects.Vignette, effects.VignetteRadius);
    }

    /// <summary>
    /// Turns FXAA on or off, which smooths the jagged edges multisampling leaves, those inside a
    /// surface, as a sharp highlight's or a texture's, and those of lines thinner than a pixel.
    /// </summary>
    public static void SetFxaa(bool enabled) => Effects.Fxaa = enabled;

    /// <summary>Whether FXAA smooths the frame's edges, as <see cref="SetFxaa"/> left it, off unless set.</summary>
    public static bool IsFxaaEnabled() => Effects.Fxaa;

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
    /// The depth of field's focus distance, range and blur, as <see cref="SetDepthOfField"/> left
    /// them, 10, 5 and 0, which is off, unless set.
    /// </summary>
    public static (float FocusDistance, float FocusRange, float Blur) GetDepthOfField()
    {
        var effects = Effects;
        return (effects.FocusDistance, effects.FocusRange, effects.FocusBlur);
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

    /// <summary>
    /// The share of the movement motion blur blurs along and whether mesh entities blur by their own,
    /// as <see cref="SetMotionBlur(float, bool)"/> left them, 0, which is off, and false unless set.
    /// </summary>
    public static (float Amount, bool Objects) GetMotionBlur()
    {
        var effects = Effects;
        return (effects.MotionBlur, effects.MotionBlurObjects);
    }

    /// <summary>
    /// Draws the effects over the frame in an ImGui window of its own, each shown as it is set and set
    /// at once as it is changed, so the picture is tuned while it is looked at. They are the
    /// curve, the exposure and the one that follows the scene, the grading, the vignette, FXAA, the
    /// depth of field, motion blur and bloom.
    /// </summary>
    /// <remarks>
    /// Called between <c>BeginDrawing</c> and <c>EndDrawing</c>, as any ImGui window is. Each control
    /// calls the setter of its effect, so it sets the effect as the program's own call of that setter
    /// would, and the program's own calls after it set over it.
    /// </remarks>
    public static void DrawPostProcessWindow()
    {
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(420, 520), ImGuiNET.ImGuiCond.FirstUseEver);
        if (!ImGuiNET.ImGui.Begin("Effects over the frame"))
        {
            ImGuiNET.ImGui.End();
            return;
        }
        var logarithmic = ImGuiNET.ImGuiSliderFlags.Logarithmic;

        var curves = Enum.GetValues<Tonemap>();
        var names = curves.Select(curve => curve.ToString()).ToArray();
        var curve = Array.IndexOf(curves, GetTonemap());
        if (ImGuiNET.ImGui.Combo("Curve", ref curve, names, names.Length)) SetTonemap(curves[curve]);
        if (GetTonemap() == Tonemap.Curve)
        {
            var (curveExposure, white, lower, upper) = GetTonemapCurve();
            var numbered = ImGuiNET.ImGui.SliderFloat("Curve's exposure", ref curveExposure, 0.05f, 8, "%.2f", logarithmic);
            numbered |= ImGuiNET.ImGui.SliderFloat("White curve", ref white, 0.1f, 4, "%.2f");
            numbered |= ImGuiNET.ImGui.SliderFloat("Lower curve", ref lower, 0.1f, 3, "%.2f");
            numbered |= ImGuiNET.ImGui.SliderFloat("Upper curve", ref upper, 0.1f, 3, "%.2f");
            if (numbered) SetTonemapCurve(curveExposure, white, lower, upper);
        }
        var exposure = GetExposure();
        if (ImGuiNET.ImGui.SliderFloat("Exposure", ref exposure, 0.05f, 8, "%.2f", logarithmic)) SetExposure(exposure);

        if (ImGuiNET.ImGui.CollapsingHeader("Exposure that follows the scene"))
        {
            var (follows, min, max, speed) = GetAutoExposure();
            var changed = ImGuiNET.ImGui.Checkbox("Follows the scene", ref follows);
            changed |= ImGuiNET.ImGui.SliderFloat("Lowest", ref min, 0.01f, 16, "%.2f", logarithmic);
            changed |= ImGuiNET.ImGui.SliderFloat("Highest", ref max, 0.01f, 16, "%.2f", logarithmic);
            changed |= ImGuiNET.ImGui.SliderFloat("Speed", ref speed, 0, 10);
            if (changed) SetAutoExposure(follows, min, max, speed);
        }

        if (ImGuiNET.ImGui.CollapsingHeader("Grading", ImGuiNET.ImGuiTreeNodeFlags.DefaultOpen))
        {
            var (contrast, saturation, tint) = GetColorGrading();
            var shade = new System.Numerics.Vector3(tint.R, tint.G, tint.B) / 255f;
            var changed = ImGuiNET.ImGui.SliderFloat("Contrast", ref contrast, 0, 2);
            changed |= ImGuiNET.ImGui.SliderFloat("Saturation", ref saturation, 0, 2);
            changed |= ImGuiNET.ImGui.ColorEdit3("Tint", ref shade);
            if (changed) SetColorGrading(contrast, saturation, new Color((byte)MathF.Round(shade.X * 255), (byte)MathF.Round(shade.Y * 255), (byte)MathF.Round(shade.Z * 255), tint.A));
            var (vignette, radius) = GetVignette();
            changed = ImGuiNET.ImGui.SliderFloat("Vignette", ref vignette, 0, 1);
            changed |= ImGuiNET.ImGui.SliderFloat("Vignette's radius", ref radius, 0, 1);
            if (changed) SetVignette(vignette, radius);
            var fxaa = IsFxaaEnabled();
            if (ImGuiNET.ImGui.Checkbox("FXAA", ref fxaa)) SetFxaa(fxaa);
        }

        if (ImGuiNET.ImGui.CollapsingHeader("Depth of field"))
        {
            var (distance, range, blur) = GetDepthOfField();
            var changed = ImGuiNET.ImGui.SliderFloat("Focus distance", ref distance, 0.1f, 200, "%.2f", logarithmic);
            changed |= ImGuiNET.ImGui.SliderFloat("Focus range", ref range, 0.01f, 100, "%.2f", logarithmic);
            changed |= ImGuiNET.ImGui.SliderFloat("Blur, 0 for none", ref blur, 0, 0.1f, "%.3f");
            if (changed) SetDepthOfField(distance, range, blur);
        }

        if (ImGuiNET.ImGui.CollapsingHeader("Motion blur"))
        {
            var (amount, objects) = GetMotionBlur();
            var changed = ImGuiNET.ImGui.SliderFloat("Amount, 0 for none", ref amount, 0, 1);
            changed |= ImGuiNET.ImGui.Checkbox("Each mesh entity by its own movement", ref objects);
            if (changed) SetMotionBlur(amount, objects);
        }

        if (ImGuiNET.ImGui.CollapsingHeader("Bloom", ImGuiNET.ImGuiTreeNodeFlags.DefaultOpen))
        {
            var (intensity, threshold) = GetBloom();
            var changed = ImGuiNET.ImGui.SliderFloat("Intensity, 0 for none", ref intensity, 0, 2);
            changed |= ImGuiNET.ImGui.SliderFloat("Threshold", ref threshold, 0, 4);
            if (changed) SetBloom(intensity, threshold);
        }
        ImGuiNET.ImGui.End();
    }
}
