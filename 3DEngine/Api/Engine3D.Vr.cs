using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // The stereo config BeginVrStereoMode set, until EndVrStereoMode.
    private static VrStereoConfig? _stereo;

    // Where the 3D of a BeginMode3D in stereo mode began, the eyes' transforms, the right half and
    // the scissor before it, which EndMode3D draws the right eye with and puts back.
    private static (int From, Matrix4x4 Left, Matrix4x4 Right, ScissorRect RightHalf, ScissorRect? Before)? _stereo3D;

    /// <summary>
    /// Works out how to draw for two eyes from a head-mounted display's measures, each eye's
    /// projection and offset, and the lens parameters raylib's distortion shader reads.
    /// </summary>
    /// <remarks>
    /// The projections are of the field of view the lenses widen the display to, with depth from 0
    /// to 1 and down the screen, as the engine's are, at the near and far distances
    /// <see cref="Camera3D.ProjectionMatrix"/> takes.
    /// </remarks>
    public static VrStereoConfig LoadVrStereoConfig(VrDeviceInfo device)
    {
        var aspect = device.HResolution * 0.5f / Math.Max(1, device.VResolution);
        var lensShift = (device.HScreenSize * 0.25f - device.LensSeparationDistance * 0.5f) / device.HScreenSize;

        // The lens's largest radius, with the shift from -1 to 1, and how far it widens the display.
        var lensRadius = MathF.Abs(-1 - 4 * lensShift);
        var radiusSq = lensRadius * lensRadius;
        var d = device.LensDistortionValues;
        var distortionScale = d.X + d.Y * radiusSq + d.Z * radiusSq * radiusSq + d.W * radiusSq * radiusSq * radiusSq;

        const float normScreenWidth = 0.5f, normScreenHeight = 1f;
        var fovY = 2 * MathF.Atan2(device.VScreenSize * 0.5f * distortionScale, device.EyeToScreenDistance);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fovY, aspect, 0.05f, 4000f);
        projection.M22 = -projection.M22;
        // Each eye's picture is moved by the lens's shift, in clip space from -1 to 1.
        var projectionOffset = 4 * lensShift;
        return new VrStereoConfig
        {
            Projection = [projection * Matrix4x4.CreateTranslation(projectionOffset, 0, 0), projection * Matrix4x4.CreateTranslation(-projectionOffset, 0, 0)],
            // Each eye half the pupils' distance aside, and up and forward from the head's turning
            // point at its base to the eyes, as raylib's are.
            ViewOffset =
            [
                Matrix4x4.CreateTranslation(device.InterpupillaryDistance * 0.5f, 0.075f, 0.045f),
                Matrix4x4.CreateTranslation(-device.InterpupillaryDistance * 0.5f, 0.075f, 0.045f),
            ],
            LeftLensCenter = new Vector2(0.25f + lensShift, 0.5f),
            RightLensCenter = new Vector2(0.75f - lensShift, 0.5f),
            LeftScreenCenter = new Vector2(0.25f, 0.5f),
            RightScreenCenter = new Vector2(0.75f, 0.5f),
            ScaleIn = new Vector2(2 / normScreenWidth, 2 / normScreenHeight / aspect),
            Scale = new Vector2(normScreenWidth * 0.5f / distortionScale, normScreenHeight * 0.5f * aspect / distortionScale),
        };
    }

    /// <summary>Lets a stereo config go, which holds nothing to free, as raylib's holds nothing.</summary>
    public static void UnloadVrStereoConfig(VrStereoConfig config) { }

    /// <summary>
    /// Draws what follows inside <see cref="BeginMode3D"/> once for each eye, the left eye's in the
    /// left half of the window or render texture and the right eye's in the right, through each
    /// eye's offset from the camera and its projection, until <see cref="EndVrStereoMode"/>.
    /// </summary>
    /// <remarks>
    /// The shapes, lines and text drawn in 3D are drawn for both eyes. A model drawn with
    /// <see cref="DrawModel"/> is drawn once, through the camera, since the model pass draws a target
    /// through one camera.
    /// </remarks>
    public static void BeginVrStereoMode(VrStereoConfig config) => _stereo = config;

    /// <summary>Draws in 3D through one camera again, as <see cref="BeginVrStereoMode"/> began otherwise.</summary>
    public static void EndVrStereoMode()
    {
        EndStereo();
        _stereo = null;
    }

    // Records the 3D that follows through the left eye into the left half, and remembers where it
    // began for EndMode3D to draw it again through the right eye into the right half.
    private static void BeginStereo(Camera3D camera, VrStereoConfig stereo)
    {
        EndStereo();
        var (width, height) = DrawingSize();
        var half = width / 2;
        // Each eye's picture squeezed into its half of clip space, x from -1 to 0 or from 0 to 1,
        // and kept to that half's pixels, as rlgl sets a viewport of half the target for each eye.
        Matrix4x4 Projection(int eye) => stereo.Projection[eye] * Matrix4x4.CreateScale(0.5f, 1, 1) * Matrix4x4.CreateTranslation(eye == 0 ? -0.5f : 0.5f, 0, 0);
        var leftView = camera.View * stereo.ViewOffset[0];
        SetRlCamera(leftView, Projection(0), depthTest: true);
        var before = DrawList.Scissor;
        DrawList.SetScissor(PixelsOf(0, 0, half, height));
        _stereo3D = (DrawList.Mark(), leftView * Projection(0), camera.View * stereo.ViewOffset[1] * Projection(1), PixelsOf(half, 0, width - half, height), before);
    }

    // Draws the 3D recorded since BeginStereo again through the right eye, and puts the scissor back.
    private static void EndStereo()
    {
        if (_stereo3D is not { } stereo) return;
        _stereo3D = null;
        DrawList.Repeat(stereo.From, stereo.Left, stereo.Right, stereo.RightHalf);
        DrawList.SetScissor(stereo.Before);
    }
}
