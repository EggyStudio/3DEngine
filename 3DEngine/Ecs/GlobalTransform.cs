namespace Engine;

/// <summary>
/// The world matrix of an entity that has a <see cref="Parent"/>: its own <see cref="Transform"/>
/// composed with every ancestor's, written by <see cref="TransformPropagation"/> each frame.
/// </summary>
/// <remarks>An entity with no parent has none, and its <see cref="Transform"/> is its world transform.</remarks>
public struct GlobalTransform
{
    /// <summary>Model to world space, in <c>System.Numerics</c> order (row vectors).</summary>
    public System.Numerics.Matrix4x4 Matrix;
}
