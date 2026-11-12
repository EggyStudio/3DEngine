using System.Numerics;

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

    /// <summary>
    /// Chooses the bloom drawn, the engine's, which adds every level of the chain alike and is the
    /// default, or the scattered one, which lays each level over the wider one below it by
    /// <see cref="SetBloomScatter"/>.
    /// </summary>
    /// <remarks>
    /// Scattered, the threshold <see cref="SetBloom"/> sets is measured on the light after the
    /// exposure, the one that follows the scene too, easing in over half of it, and the chain halves
    /// the window to the log2 of its longer side less 2 levels, its widest level four pixels along
    /// that side, so the haze can reach across the whole window. The intensity is the share of the
    /// light past the threshold that the spread light takes the place of, so the frame keeps the
    /// light it had where the engine's bloom adds to it.
    /// </remarks>
    public static void SetBloomKind(BloomKind kind) =>
        World.GetOrInsertResource(() => new BloomSettings()).Kind = kind;

    /// <summary>The bloom <see cref="SetBloomKind"/> chose, the engine's added one unless set.</summary>
    public static BloomKind GetBloomKind() => TryRes<BloomSettings>(out var bloom) ? bloom.Kind : BloomKind.Added;

    /// <summary>
    /// Sets how far the scattered bloom spreads, from 0 for a tight glow about what blooms to 1 for
    /// a wide haze across the window, 0.5 unless set.
    /// </summary>
    /// <remarks>
    /// Each level of the chain gives the wider one below it a share of its place running from 0.05
    /// at 0 to 0.95 at 1. The engine's bloom (<see cref="SetBloomKind"/>) does not read it.
    /// </remarks>
    public static void SetBloomScatter(float scatter) =>
        World.GetOrInsertResource(() => new BloomSettings()).Spread = float.IsFinite(scatter) ? Math.Clamp(scatter, 0, 1) : 0.5f;

    /// <summary>How far the scattered bloom spreads, as <see cref="SetBloomScatter"/> left it, 0.5 unless set.</summary>
    public static float GetBloomScatter() => TryRes<BloomSettings>(out var bloom) ? bloom.Spread : 0.5f;

    private static FrameEffects Effects => World.GetOrInsertResource(static () => new FrameEffects());

    /// <summary>
    /// Multiplies the scene's light by <paramref name="exposure"/> before it is brought under 1, so
    /// 2 brightens a dark scene and 0.5 dims a bright one, 1 being as it is and the default.
    /// </summary>
    public static void SetExposure(float exposure) => Effects.Exposure = Math.Max(0, exposure);

    /// <summary>What the scene's light is multiplied by before its curve, as <see cref="SetExposure"/> left it, 1 unless set.</summary>
    public static float GetExposure() => Effects.Exposure;

    /// <summary>
    /// Sets the camera the lights and the sky given in physical units are seen through, by its
    /// sensor's <paramref name="iso"/>, its shutter's time in seconds and its aperture's f-number,
    /// as a photographer's camera is set, ISO 100 at 1/125 s and f/16 for a sunny day.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The camera's exposure value at ISO 100 is log2 of the f-number squared over the shutter's
    /// time, less log2 of the ISO over 100, and its sensor holds up to 1.2 times 2 to that many
    /// candelas a square meter, which reach the frame as 1, so at ISO 200, 1/200 s and f/16, EV
    /// 14.64, a white surface in a sun of 97,050 lux reads 1.006. A sunny day's EV 15 stands until
    /// it is set.
    /// </para>
    /// <para>
    /// It is applied to the lights as they are given (<see cref="SetLightIlluminance"/>,
    /// <see cref="SetLightLuminousIntensity"/>, <see cref="SetLightLuminousFlux"/>) and to the day
    /// sky's (<see cref="SkyDay.SunIlluminance"/>), so the frame holds light near 1 rather than tens
    /// of thousands, and a light given a plain intensity is not seen through it.
    /// <see cref="SetExposure"/> and <see cref="SetAutoExposure"/> multiply what it gives, so auto
    /// exposure between 1/8 and 8 keeps the camera within three stops of what it was set to.
    /// </para>
    /// </remarks>
    public static void SetCameraExposure(float iso, float shutterSeconds, float aperture)
    {
        if (!(iso > 0) || !(shutterSeconds > 0) || !(aperture > 0) || !float.IsFinite(iso * shutterSeconds * aperture)) return;
        var units = World.GetOrInsertResource(static () => new PhysicalUnits());
        units.Ev100 = PhysicalUnits.Ev100Of(iso, shutterSeconds, aperture);
        ApplyPhysicalLights(units);
        BoundExposureByEv100(Effects, units.Ev100);
        if (TryRes<DaySky>(out var sky)) sky.Changed();
    }

    /// <summary>
    /// The exposure value at ISO 100 of the camera <see cref="SetCameraExposure"/> set, 15, a sunny
    /// day's, unless set.
    /// </summary>
    public static float GetCameraEv100() => TryRes<PhysicalUnits>(out var units) ? units.Ev100 : 15;

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
        effects.AutoExposureEv100 = null;
        effects.AutoExposureSpeed = effects.AutoExposureDarkeningSpeed = Math.Max(0, speed);
    }

    /// <summary>
    /// Bounds the exposure that follows the scene by the scenes it meters, from the darkest at an
    /// exposure value of <paramref name="lowest"/> at ISO 100 to the brightest at
    /// <paramref name="highest"/>, in place of <see cref="SetAutoExposure"/>'s multipliers, which a
    /// program that sets no camera exposure keeps.
    /// </summary>
    /// <remarks>
    /// A scene at the camera's own exposure value (<see cref="GetCameraEv100"/>) is shown as the
    /// camera shows it, one a stop darker twice as bright, so the bounds are the multipliers 2 to the
    /// camera's less <paramref name="highest"/> and 2 to the camera's less
    /// <paramref name="lowest"/>, worked out again when the camera changes. A camera's meter covers
    /// about -1 to 14, a dim room by night to a sunlit field. <see cref="SetAutoExposure"/> called
    /// after gives the bounds as multipliers again.
    /// </remarks>
    public static void SetAutoExposureEv100(float lowest, float highest)
    {
        if (!float.IsFinite(lowest) || !float.IsFinite(highest)) return;
        var effects = Effects;
        effects.AutoExposureEv100 = (Math.Min(lowest, highest), Math.Max(lowest, highest));
        BoundExposureByEv100(effects, GetCameraEv100());
    }

    /// <summary>
    /// The exposure values at ISO 100 <see cref="SetAutoExposureEv100"/> bounds the exposure that
    /// follows the scene by, or null where <see cref="SetAutoExposure"/>'s multipliers bound it, as
    /// they do unless set.
    /// </summary>
    public static (float Lowest, float Highest)? GetAutoExposureEv100() => Effects.AutoExposureEv100;

    // The multipliers the exposure that follows the scene is bounded by, from the exposure values
    // it meters between against the camera's, where they were given.
    private static void BoundExposureByEv100(FrameEffects effects, float cameraEv100)
    {
        if (effects.AutoExposureEv100 is not { } ev) return;
        effects.AutoExposureMin = MathF.Pow(2, cameraEv100 - ev.Highest);
        effects.AutoExposureMax = MathF.Pow(2, cameraEv100 - ev.Lowest);
    }

    /// <summary>
    /// Sets how quickly the exposure that follows the scene moves where the scene brightens and where
    /// it darkens, apart, as <see cref="SetAutoExposure"/>'s speed counts them, which that sets
    /// alike.
    /// </summary>
    /// <remarks>
    /// An eye and a camera's metering adapt faster to light than to dark, so a player walking out
    /// of a cave is dazzled briefly and one walking into it waits longer to see, which 3 and 1
    /// give.
    /// </remarks>
    public static void SetAutoExposureSpeeds(float brightening, float darkening)
    {
        var effects = Effects;
        effects.AutoExposureSpeed = Math.Max(0, brightening);
        effects.AutoExposureDarkeningSpeed = Math.Max(0, darkening);
    }

    /// <summary>
    /// How quickly the exposure that follows the scene moves where the scene brightens and where it
    /// darkens, as <see cref="SetAutoExposureSpeeds"/> or <see cref="SetAutoExposure"/> left them, 2
    /// and 2 unless set.
    /// </summary>
    public static (float Brightening, float Darkening) GetAutoExposureSpeeds()
    {
        var effects = Effects;
        return (effects.AutoExposureSpeed, effects.AutoExposureDarkeningSpeed);
    }

    /// <summary>
    /// Sets the part of the picture the exposure that follows the scene measures by its brightness,
    /// from the share <paramref name="low"/> of its weighted light, counted from the darkest, to the
    /// share <paramref name="high"/>, the whole picture with 0 and 1, which it measures unless set.
    /// </summary>
    /// <remarks>
    /// The mean of what lies between is taken, so a sky filling a corner of the view, past the
    /// share left out above, does not dim the walls under it, and a black doorway, past the share
    /// left out below, does not brighten them, 0.4 and 0.9 leaving out the darkest 40% and the
    /// brightest 10%. The light is binned by half a stop.
    /// </remarks>
    public static void SetAutoExposureHistogram(float low, float high)
    {
        var effects = Effects;
        (low, high) = (float.IsFinite(low) ? Math.Clamp(low, 0, 1) : 0, float.IsFinite(high) ? Math.Clamp(high, 0, 1) : 1);
        (effects.AutoExposureLow, effects.AutoExposureHigh) = low < high ? (low, high) : (0, 1);
    }

    /// <summary>
    /// The part of the picture the exposure that follows the scene measures, as
    /// <see cref="SetAutoExposureHistogram"/> left it, 0 and 1 for the whole picture unless set.
    /// </summary>
    public static (float Low, float High) GetAutoExposureHistogram()
    {
        var effects = Effects;
        return (effects.AutoExposureLow, effects.AutoExposureHigh);
    }

    /// <summary>
    /// Sets how the exposure that follows the scene weighs the picture it measures, and the stops it
    /// is moved by from bringing it to a mid gray, brighter above 0, the whole picture and none
    /// unless set.
    /// </summary>
    /// <remarks>
    /// A camera's reflected light meter brings what it reads to 12.5 over 100 times the ISO's
    /// candelas a square meter, which through its exposure (<see cref="SetCameraExposure"/>) is
    /// 0.79 stops under a mid gray, so a compensation of -0.79 meters as one does.
    /// </remarks>
    public static void SetAutoExposureMetering(ExposureMetering metering, float compensation = 0)
    {
        var effects = Effects;
        effects.AutoExposureMetering = metering;
        effects.AutoExposureCompensation = float.IsFinite(compensation) ? Math.Clamp(compensation, -10, 10) : 0;
    }

    /// <summary>
    /// How the exposure that follows the scene weighs the picture and its compensation in stops, as
    /// <see cref="SetAutoExposureMetering"/> left them, the whole picture and 0 unless set.
    /// </summary>
    public static (ExposureMetering Metering, float Compensation) GetAutoExposureMetering()
    {
        var effects = Effects;
        return (effects.AutoExposureMetering, effects.AutoExposureCompensation);
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
    /// leaves colors under 0.9 as they are, Narkowicz's fit of ACES, a cut at 1, or one of eight
    /// more by their usual names.
    /// </summary>
    /// <remarks>
    /// <see cref="Tonemap.AgX"/>, <see cref="Tonemap.TonyMcMapface"/> and
    /// <see cref="Tonemap.BlenderFilmic"/> look the light up in tables of their own, each read the
    /// first time its curve is chosen, which lengthens that frame.
    /// </remarks>
    public static void SetTonemap(Tonemap curve) => Effects.Tonemap = curve;

    /// <summary>The curve the scene's light past 1 is brought under by, <see cref="Tonemap.Engine"/> unless set.</summary>
    public static Tonemap GetTonemap() => Effects.Tonemap;

    /// <summary>
    /// Has the engine's curve bend each channel of the scene's light past <paramref name="knee"/>
    /// toward 1 on its own, by <paramref name="strength"/> from 0 for none to 1, so a light bright in
    /// every channel goes toward white as a camera's film takes it. A knee of 0.8 at full strength
    /// unless set.
    /// </summary>
    /// <remarks>
    /// The engine's curve, <see cref="Tonemap.Engine"/>, bends the brightest channel past 0.9 and
    /// scales the other two with it, so a bright orange lamp stays orange where a path-traced
    /// reference, clipping each channel at white, shows it white. A color whose channels all lie
    /// under the knee is shown as it was. The window's frame and a render texture take it alike,
    /// and the other curves, which bend their highlights as their makers chose, take none of it.
    /// </remarks>
    public static void SetHighlightKnee(float knee, float strength = 1)
    {
        var effects = Effects;
        effects.HighlightKnee = Math.Clamp(knee, 0, 1);
        effects.HighlightStrength = Math.Clamp(strength, 0, 1);
    }

    /// <summary>The knee and strength <see cref="SetHighlightKnee"/> left, 0.8 and 1 unless set.</summary>
    public static (float Knee, float Strength) GetHighlightKnee() => (Effects.HighlightKnee, Effects.HighlightStrength);

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
        effects.Curve = new Vector4(Math.Max(0, exposure), Math.Max(0.01f, white), Math.Max(0.01f, lower), Math.Max(0.01f, upper));
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
    /// Brings the scene's light under 1 by a table read from <paramref name="fileName"/>, a cube of
    /// colors the light is looked up in, the looked-up light taking <paramref name="share"/> of the
    /// light's place, and chooses that curve, <see cref="Tonemap.Lookup"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file is a KTX2 file holding one level of a cube of texels in half-float RGBA or RGB9E5,
    /// as it is or with zlib's supercompression, which <c>build/unity-lut.py</c> writes from a
    /// table drawn as a strip of squares. The light, after the exposure and
    /// <see cref="SetLiftGammaGain(Vector4, Vector4, Vector4)"/>, is looked up as ARRI's LogC encodes it for an exposure index
    /// of 1000, the encoding such tables are made for, which takes 0 to about 55 into the table,
    /// and the table gives linear light, which <see cref="SetColorGrading"/> and the vignette then
    /// take as they take every curve's.
    /// </para>
    /// <para>
    /// The file is read when this is called, and a file that cannot be read leaves the curve as it
    /// was, with a warning.
    /// </para>
    /// </remarks>
    public static void SetTonemapLookup(string fileName, float share = 1)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"SetTonemapLookup: '{fileName}' was not found beside the program or in the working directory.");
            return;
        }
        TonemapTables.Table table;
        try
        {
            table = TonemapTables.Read(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            ApiLogger.Warn($"SetTonemapLookup: '{fileName}' could not be read, so the curve is kept. {ex.Message}");
            return;
        }
        var effects = Effects;
        (effects.Lookup, effects.LookupFile, effects.LookupShare) = (table, fileName, Math.Clamp(share, 0, 1));
        effects.Tonemap = Tonemap.Lookup;
    }

    /// <summary>
    /// The file <see cref="SetTonemapLookup"/> last read its table from and the table's share, null
    /// and 1 before one is read, whether or not that curve is the one chosen.
    /// </summary>
    public static (string? FileName, float Share) GetTonemapLookup() => (Effects.LookupFile, Effects.LookupShare);

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
    /// Lifts the scene's shadows, bends its middle and gains its highlights before the curve. Each
    /// is a color in x, y and z, white for none, whose hue it moves that part of the light toward,
    /// and in w how far it moves that part brighter, 0 for none and below 0 darker.
    /// </summary>
    /// <remarks>
    /// The light after the exposure and the bloom is multiplied by the gain, has the lift added and
    /// is raised to one over the gamma. Each color is decoded from sRGB, scaled by 0.15 for the
    /// lift and 0.8 for the others, so a full hue tints the shadows a little and the rest more, has
    /// its luminance taken away and its w added, and 1 more for the gamma and the gain. A white
    /// trackball moves every channel by its w alone, so a gamma of w 0.225 raises the light to 1
    /// over 1.225, brightening its middle. White and 0 for each leave the light as it is, as they
    /// are by default.
    /// </remarks>
    public static void SetLiftGammaGain(Vector4 lift, Vector4 gamma, Vector4 gain)
    {
        var effects = Effects;
        (effects.Lift, effects.Gamma, effects.Gain) = (lift, gamma, gain);
    }

    /// <summary>
    /// Moves the scene's shadows, middle and highlights brighter by <paramref name="lift"/>,
    /// <paramref name="gamma"/> and <paramref name="gain"/> with no hue, 0 for none and below 0
    /// darker, as the w of <see cref="SetLiftGammaGain(Vector4, Vector4, Vector4)"/>'s white colors.
    /// </summary>
    public static void SetLiftGammaGain(float lift, float gamma, float gain) =>
        SetLiftGammaGain(new Vector4(1, 1, 1, lift), new Vector4(1, 1, 1, gamma), new Vector4(1, 1, 1, gain));

    /// <summary>The lift, gamma and gain <see cref="SetLiftGammaGain(Vector4, Vector4, Vector4)"/> left, each white and 0 unless set.</summary>
    public static (Vector4 Lift, Vector4 Gamma, Vector4 Gain) GetLiftGammaGain()
    {
        var effects = Effects;
        return (effects.Lift, effects.Gamma, effects.Gain);
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
    /// Turns temporal antialiasing on or off, which moves the window's camera by a part of a pixel
    /// each frame, eight places in turn, and blends each frame's light into what the frames before
    /// left where the same point lay, so an edge, a highlight and the noise of global illumination's
    /// probes are each read over eight frames.
    /// </summary>
    /// <remarks>
    /// What the frames before saw is held to the colors around each pixel this frame, so a surface
    /// that moves or comes out from behind another leaves no trail, though fine detail in motion
    /// is a little softer than in a still frame. Render textures and a stereo view are drawn
    /// without it, and the window's 2D drawn after <c>EndMode3D</c> is never moved.
    /// </remarks>
    public static void SetTemporalAntialiasing(bool enabled) => Effects.Temporal = enabled;

    /// <summary>Whether temporal antialiasing resolves the window's frames, as <see cref="SetTemporalAntialiasing"/> left it, off unless set.</summary>
    public static bool IsTemporalAntialiasingEnabled() => Effects.Temporal;

    /// <summary>
    /// Turns the dither on or off, which adds to each pixel a grain of blue noise of about a level
    /// of the window's eight bits before they take it, so a slow gradient in the dark, as a lamp's
    /// light fading across a wall, falls in a fine grain rather than in bands a level apart.
    /// </summary>
    /// <remarks>
    /// The grain holds still from frame to frame, and moves only where temporal antialiasing
    /// blends the frames, which then reads it away. Black and white are left as they are. On
    /// unless a program turns it off, which leaves the bands; <see cref="SetDitherStrength"/> sets
    /// how strong it is.
    /// </remarks>
    public static void SetDither(bool enabled) => Effects.Dither = enabled;

    /// <summary>Whether the window's frames are dithered, as <see cref="SetDither"/> left it, on unless set.</summary>
    public static bool IsDitherEnabled() => Effects.Dither;

    /// <summary>
    /// Sets how strong the dither's grain is, from 0 to 1, a half unless set. At a half the grain is
    /// even across a level, the calmest that leaves no band, at 1 it spans two levels, likelier small
    /// than large, and strays as much at every level of light, and between the two are blended.
    /// </summary>
    /// <remarks>
    /// Under a half the grain spans less than a level, so the bands begin to come back, and at 0
    /// there is none. A value past 0 to 1 is held to it.
    /// </remarks>
    public static void SetDitherStrength(float strength) => Effects.DitherStrength = Math.Clamp(strength, 0, 1);

    /// <summary>How strong the dither's grain is, as <see cref="SetDitherStrength"/> left it, a half unless set.</summary>
    public static float GetDitherStrength() => Effects.DitherStrength;

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
        if (GetTonemap() == Tonemap.Lookup && GetTonemapLookup() is (string file, var share))
        {
            ImGuiNET.ImGui.TextUnformatted(file);
            if (ImGuiNET.ImGui.SliderFloat("Table's share", ref share, 0, 1)) SetTonemapLookup(file, share);
        }
        var exposure = GetExposure();
        if (ImGuiNET.ImGui.SliderFloat("Exposure", ref exposure, 0.05f, 8, "%.2f", logarithmic)) SetExposure(exposure);

        if (ImGuiNET.ImGui.CollapsingHeader("Exposure that follows the scene"))
        {
            var (follows, min, max, _) = GetAutoExposure();
            var (brightening, darkening) = GetAutoExposureSpeeds();
            var changed = ImGuiNET.ImGui.Checkbox("Follows the scene", ref follows);
            changed |= ImGuiNET.ImGui.SliderFloat("Lowest", ref min, 0.01f, 16, "%.2f", logarithmic);
            changed |= ImGuiNET.ImGui.SliderFloat("Highest", ref max, 0.01f, 16, "%.2f", logarithmic);
            changed |= ImGuiNET.ImGui.SliderFloat("Speed to light", ref brightening, 0, 10);
            changed |= ImGuiNET.ImGui.SliderFloat("Speed to dark", ref darkening, 0, 10);
            if (changed)
            {
                SetAutoExposure(follows, min, max);
                SetAutoExposureSpeeds(brightening, darkening);
            }
            var (low, high) = GetAutoExposureHistogram();
            changed = ImGuiNET.ImGui.SliderFloat("Darkest share left out", ref low, 0, 1);
            changed |= ImGuiNET.ImGui.SliderFloat("Brightest share kept to", ref high, 0, 1);
            if (changed) SetAutoExposureHistogram(low, high);
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
            var temporal = IsTemporalAntialiasingEnabled();
            if (ImGuiNET.ImGui.Checkbox("Temporal antialiasing", ref temporal)) SetTemporalAntialiasing(temporal);
            var dither = IsDitherEnabled();
            if (ImGuiNET.ImGui.Checkbox("Dither", ref dither)) SetDither(dither);
            var strength = GetDitherStrength();
            if (ImGuiNET.ImGui.SliderFloat("Dither's strength", ref strength, 0, 1)) SetDitherStrength(strength);
        }

        if (ImGuiNET.ImGui.CollapsingHeader("Lift, gamma and gain"))
        {
            // Each a color and the offset that moves its part brighter or darker.
            static bool Trackball(string name, ref Vector4 ball)
            {
                var color = new Vector3(ball.X, ball.Y, ball.Z);
                var offset = ball.W;
                var changed = ImGuiNET.ImGui.ColorEdit3(name, ref color);
                changed |= ImGuiNET.ImGui.SliderFloat(name + "'s offset", ref offset, -1, 1);
                ball = new Vector4(color, offset);
                return changed;
            }
            var (lift, gamma, gain) = GetLiftGammaGain();
            var changed = Trackball("Lift", ref lift);
            changed |= Trackball("Gamma", ref gamma);
            changed |= Trackball("Gain", ref gain);
            if (changed) SetLiftGammaGain(lift, gamma, gain);
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
            var scattered = GetBloomKind() == BloomKind.Scattered;
            if (ImGuiNET.ImGui.Checkbox("Scattered", ref scattered)) SetBloomKind(scattered ? BloomKind.Scattered : BloomKind.Added);
            var scatter = GetBloomScatter();
            if (ImGuiNET.ImGui.SliderFloat("Scatter", ref scatter, 0, 1)) SetBloomScatter(scatter);
        }
        ImGuiNET.ImGui.End();
    }
}
