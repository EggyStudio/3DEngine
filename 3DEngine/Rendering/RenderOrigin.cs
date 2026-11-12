using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The place the frame's places are taken against before the GPU sees them, in doubles, so a vertex,
/// a light, a probe or a cell's corner stands in numbers as small as its distance from it, and a
/// float's step far from the world's origin is not the step between neighboring points.
/// </summary>
/// <remarks>
/// <para>
/// Every pass and kernel holds one convention. A place a shader reads is the world place less this
/// origin, worked on the CPU in doubles, where the difference of two floats is exact. A view's
/// view-projection is pushed as the origin's translation times it, so it takes such a place to clip
/// space. A grid laid in world numbers, as the scene field's cells or the shadows' texels, is
/// handed as the origin's place within one of its cells, so its snap holds whatever the origin. A
/// kernel may take a place it reads as small, and finds the world place, where it needs one, as
/// the origin it is handed plus that place.
/// </para>
/// <para>
/// One origin serves the whole frame, every camera, light and render target taking places against
/// it, so an instance written once is right through every view.
/// </para>
/// </remarks>
internal readonly record struct RenderOrigin(double X, double Y, double Z)
{
    /// <summary>The world's own origin, against which every place is as given.</summary>
    public static RenderOrigin Zero => default;

    /// <summary>
    /// The origin a test sets in place of the frame's own, to read a scene alike whatever its
    /// origin, or none to take the frame's own.
    /// </summary>
    internal static RenderOrigin? Override { get; set; }

    /// <summary>The origin as floats, which a vertex stage adds back to a place for the world place it hands on.</summary>
    public Vector4 AsVector4 => new((float)X, (float)Y, (float)Z, 0);

    /// <summary>The place given less the origin, worked in doubles.</summary>
    public Vector3 Against(Vector3 place) => new((float)(place.X - X), (float)(place.Y - Y), (float)(place.Z - Z));

    /// <summary>
    /// A view-projection that takes a place against the origin to the clip space the one given
    /// takes the world place to, the origin's translation times it, worked in doubles, its first
    /// three rows as they are and its fourth the origin's weighing of them added to it.
    /// </summary>
    public Matrix4x4 ViewProjection(Matrix4x4 viewProjection)
    {
        var m = viewProjection;
        m.M41 = (float)(X * m.M11 + Y * m.M21 + Z * m.M31 + m.M41);
        m.M42 = (float)(X * m.M12 + Y * m.M22 + Z * m.M32 + m.M42);
        m.M43 = (float)(X * m.M13 + Y * m.M23 + Z * m.M33 + m.M43);
        m.M44 = (float)(X * m.M14 + Y * m.M24 + Z * m.M34 + m.M44);
        return m;
    }

    /// <summary>A view's push to a vertex stage that reads modelpass.slang's ModelPush, through this origin.</summary>
    public ViewPush Push(Matrix4x4 viewProjection) => new() { ViewProjection = ViewProjection(viewProjection), Origin = AsVector4 };
}

/// <summary>
/// modelpass.slang's ModelPush, as the model pass, the shadow pass and the particles push it, a
/// view's view-projection taking a place against the frame's origin to clip space, and the origin,
/// which the vertex stage adds back to a place for the world place it hands on.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ViewPush
{
    /// <summary>The bytes the push takes, which a pipeline's push range names.</summary>
    public const uint Size = 80;

    public Matrix4x4 ViewProjection;
    public Vector4 Origin;
}
