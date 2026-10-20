namespace Engine;

/// <summary>
/// Marks a struct as data kept in a file of its own, a weapon's numbers, a level's rules or an
/// enemy's kind, which components refer to by the file's id through a <see cref="DataRef{T}"/>.
/// </summary>
/// <remarks>
/// The generator writes its fields as it writes a <see cref="SceneComponentAttribute"/> component's,
/// with the same field types, <see cref="FormerNameAttribute"/> and
/// <see cref="DataVersionAttribute"/>, and registers it with <see cref="DataAssets"/> rather than
/// as a component, so a file holds it in the form a scene file holds a component. Many entities
/// refer to one file, which is read once and shared, and it changes without the game being built
/// again. A field's value when it is missing from a file is the type's static <c>Default</c> or
/// <c>Identity</c> when it has one, and zero otherwise.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class DataAssetAttribute : Attribute;
