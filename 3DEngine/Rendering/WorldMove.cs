namespace Engine;

/// <summary>
/// The whole step a program moved every place it hands the engine by since the last frame was
/// drawn (<c>E3D.MoveWorld</c>), which the parts of the engine that keep places move what
/// they keep by in the frame drawn next, and every step it moved by since the app began, a world
/// resource.
/// </summary>
internal sealed class WorldMove
{
    /// <summary>The step not yet drawn, or null where the world has not moved since the last frame drawn.</summary>
    public WorldStep? Pending { get; private set; }

    /// <summary>Every step the world moved by, added up.</summary>
    public WorldStep Total { get; private set; }

    /// <summary>Adds a step to the one not yet drawn and to the whole.</summary>
    public void Add(WorldStep step) => (Pending, Total) = ((Pending ?? default) + step, Total + step);

    /// <summary>Lets the step go once the frame it moved in has been handed it, drawn or not.</summary>
    public void Taken() => Pending = null;
}

/// <summary>A whole step every place moved by, in doubles.</summary>
internal readonly record struct WorldStep(double X, double Y, double Z)
{
    /// <summary>The two steps one after the other.</summary>
    public static WorldStep operator +(WorldStep a, WorldStep b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Whether it moves nothing.</summary>
    public bool IsZero => X == 0 && Y == 0 && Z == 0;

    /// <summary>A place moved by the step, worked in doubles.</summary>
    public System.Numerics.Vector3 Moved(System.Numerics.Vector3 place) => new((float)(place.X + X), (float)(place.Y + Y), (float)(place.Z + Z));

    /// <summary>
    /// A view-projection of the world before the step as it takes a place of the world moved by
    /// it to clip space, the step's translation back times it, worked in doubles.
    /// </summary>
    public System.Numerics.Matrix4x4 MovedView(System.Numerics.Matrix4x4 viewProjection) => new RenderOrigin(-X, -Y, -Z).Taking(viewProjection);

    /// <summary>A noted eye moved by the step, its view with the eye at nothing as it is.</summary>
    public ViewEye Moved(ViewEye eye) => eye with { Eye = Moved(eye.Eye) };

    /// <summary>A matrix whose translation is moved by the step, worked in doubles.</summary>
    public System.Numerics.Matrix4x4 Moved(System.Numerics.Matrix4x4 matrix)
    {
        (matrix.M41, matrix.M42, matrix.M43) = ((float)(matrix.M41 + X), (float)(matrix.M42 + Y), (float)(matrix.M43 + Z));
        return matrix;
    }
}

/// <summary>
/// The step the world moved by in the frame being rendered, or null where it did not move in it,
/// and every step it moved by since the app began, set in the render world each frame once the
/// world has moved.
/// </summary>
internal sealed record WorldMoved(WorldStep? Step, WorldStep Total);
