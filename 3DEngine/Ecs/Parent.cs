namespace Engine;

/// <summary>
/// The entity this one belongs to, as a handle, so a parent despawned and replaced is not mistaken
/// for the new entity with its id.
/// </summary>
/// <remarks>
/// A child's <see cref="Transform"/> is relative to its parent's, and
/// <see cref="TransformPropagation"/> writes the composed world matrix into its
/// <see cref="GlobalTransform"/> each frame.
/// </remarks>
public struct Parent
{
    /// <summary>The parent.</summary>
    public Entity Value;

    /// <summary>Creates a parent link.</summary>
    public Parent(Entity value) => Value = value;
}
