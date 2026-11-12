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
/// it, so an instance written once is right through every view. The renderer stands it at the
/// window camera's eye rounded to a whole count of <see cref="Step"/> along each axis
/// (<see cref="Near"/>), so a place near the eye is small, a whole place far out stays whole
/// against it, and what is built again as the origin moves, the ray scene's top level, is built
/// as the eye crosses a step and not each frame the camera moves.
/// </para>
/// <para>
/// A view whose eye is known (<see cref="ViewEye"/>) is pushed as its turn and projection times
/// the way from the origin to the eye, worked in doubles (<see cref="Seen"/>), since a
/// view-projection made in floats far from the world's origin carries the rounding of its
/// translation, which no origin taken after takes back.
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

    /// <summary>How far apart the places the renderer stands the origin at are, along each axis.</summary>
    public const double Step = 16;

    /// <summary>The origin for an eye, the eye rounded to the nearest whole count of <see cref="Step"/> along each axis.</summary>
    public static RenderOrigin Near(Vector3 eye) =>
        new(Math.Floor(eye.X / Step + 0.5) * Step, Math.Floor(eye.Y / Step + 0.5) * Step, Math.Floor(eye.Z / Step + 0.5) * Step);

    /// <summary>The origin as floats, which a shader adds back to a place for its world point.</summary>
    public Vector3 AsVector3 => new((float)X, (float)Y, (float)Z);

    /// <summary>The place given less the origin, worked in doubles.</summary>
    public Vector3 Against(Vector3 place) => new((float)(place.X - X), (float)(place.Y - Y), (float)(place.Z - Z));

    /// <summary>The place in xyz less the origin, worked in doubles, and w as it is.</summary>
    public Vector4 Against(Vector4 place) => new((float)(place.X - X), (float)(place.Y - Y), (float)(place.Z - Z), place.W);

    /// <summary>A model's world matrix with its translation taken against the origin, worked in doubles.</summary>
    public Matrix4x4 Against(Matrix4x4 world) => world with
    {
        M41 = (float)(world.M41 - X), M42 = (float)(world.M42 - Y), M43 = (float)(world.M43 - Z),
    };

    /// <summary>The world place of a place against the origin, the two added in doubles.</summary>
    public Vector3 World(Vector3 place) => new((float)(place.X + X), (float)(place.Y + Y), (float)(place.Z + Z));

    /// <summary>
    /// A matrix that takes a place against the origin where the one given takes the world place,
    /// a view-projection to clip space or a box's world to its own space, the origin's translation
    /// times it, worked in doubles, its first three rows as they are and its fourth the origin's
    /// weighing of them added to it. At the world's origin, the matrix given.
    /// </summary>
    public Matrix4x4 Taking(Matrix4x4 matrix)
    {
        if (this == Zero) return matrix;
        var m = matrix;
        m.M41 = (float)(X * m.M11 + Y * m.M21 + Z * m.M31 + m.M41);
        m.M42 = (float)(X * m.M12 + Y * m.M22 + Z * m.M32 + m.M42);
        m.M43 = (float)(X * m.M13 + Y * m.M23 + Z * m.M33 + m.M43);
        m.M44 = (float)(X * m.M14 + Y * m.M24 + Z * m.M34 + m.M44);
        return m;
    }

    /// <summary>
    /// A matrix that takes a place against this origin where the one given takes a place against
    /// <paramref name="from"/>, the translation from the one origin to the other times it, worked in
    /// doubles, as a matrix fitted in a frame before is read in this one.
    /// </summary>
    public Matrix4x4 Moved(Matrix4x4 matrix, RenderOrigin from) => new RenderOrigin(X - from.X, Y - from.Y, Z - from.Z).Taking(matrix);

    /// <summary>
    /// The origin's place within a cell of a lattice of cells <paramref name="cell"/> wide along the
    /// x and y of a turned frame (<paramref name="turn"/>, a rotation, as a light's view), from 0 to
    /// the cell, worked in doubles. A place in that frame is moved by it before it is snapped to the
    /// lattice and back after, so the lattice lies in world numbers whatever the origin.
    /// </summary>
    public (float X, float Y) Phase(Matrix4x4 turn, float cell)
    {
        var x = X * turn.M11 + Y * turn.M21 + Z * turn.M31;
        var y = X * turn.M12 + Y * turn.M22 + Z * turn.M32;
        return ((float)(x - Math.Floor(x / cell) * cell), (float)(y - Math.Floor(y / cell) * cell));
    }

    /// <summary>
    /// A matrix that takes what the one given takes to a world place, clip space for an inverse
    /// view-projection, to the place against the origin, it times the translation back, worked in
    /// doubles, each of its first three columns less the origin's weighing of its fourth. At the
    /// world's origin, the matrix given.
    /// </summary>
    public Matrix4x4 Inverse(Matrix4x4 inverse)
    {
        if (this == Zero) return inverse;
        var m = inverse;
        (m.M11, m.M12, m.M13) = ((float)(m.M11 - X * m.M14), (float)(m.M12 - Y * m.M14), (float)(m.M13 - Z * m.M14));
        (m.M21, m.M22, m.M23) = ((float)(m.M21 - X * m.M24), (float)(m.M22 - Y * m.M24), (float)(m.M23 - Z * m.M24));
        (m.M31, m.M32, m.M33) = ((float)(m.M31 - X * m.M34), (float)(m.M32 - Y * m.M34), (float)(m.M33 - Z * m.M34));
        (m.M41, m.M42, m.M43) = ((float)(m.M41 - X * m.M44), (float)(m.M42 - Y * m.M44), (float)(m.M43 - Z * m.M44));
        return m;
    }

    /// <summary>
    /// A plane given by its normal in xyz and how far along it it lies in w, the points whose dot
    /// with the normal is w, as it lies among places against the origin, worked in doubles.
    /// </summary>
    public Vector4 Plane(Vector4 plane) =>
        plane with { W = (float)(plane.W - (plane.X * X + plane.Y * Y + plane.Z * Z)) };

    /// <summary>
    /// A view-projection that takes a place against the origin to clip space. Where the eye it was
    /// made from is known, the eye's turn and projection times the translation from the origin to
    /// the eye, worked in doubles from the eye as given; otherwise <see cref="Taking"/> of the one
    /// given.
    /// </summary>
    public Matrix4x4 Seen(Matrix4x4 viewProjection, ViewEye? eye) =>
        eye is { } seen ? new RenderOrigin(X - seen.Eye.X, Y - seen.Eye.Y, Z - seen.Eye.Z).Taking(seen.Turned) : Taking(viewProjection);

    /// <summary>
    /// A view's push to a vertex stage that reads modelpass.slang's ModelPush, through this origin
    /// (<see cref="Seen"/>), with a move of clip space after it where one is given.
    /// </summary>
    public ViewPush Push(Matrix4x4 viewProjection, ViewEye? eye = null, Matrix4x4? after = null) => new()
    {
        ViewProjection = after is { } then ? Seen(viewProjection, eye) * then : Seen(viewProjection, eye),
        Origin = new Vector4(AsVector3, 0),
    };
}

/// <summary>
/// Where a camera stood as the program gave it, and its view-projection with the eye at nothing,
/// its turn and projection alone, from which <see cref="RenderOrigin.Seen"/> works out its
/// view-projection against any origin.
/// </summary>
/// <param name="Eye">The camera's place in the world.</param>
/// <param name="Turned">The view-projection as made with the eye at the world's origin.</param>
internal readonly record struct ViewEye(Vector3 Eye, Matrix4x4 Turned);

/// <summary>
/// modelpass.slang's ModelPush, as the model pass, the shadow pass and the particles push it to
/// both stages, a view's view-projection taking a place against the frame's origin to clip space,
/// and the origin, which a shader adds back to a place for its world point (ModelVertexOutput.world).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ViewPush
{
    /// <summary>The bytes the push takes, which a pipeline's push range names.</summary>
    public const uint Size = 80;

    public Matrix4x4 ViewProjection;
    public Vector4 Origin;
}
