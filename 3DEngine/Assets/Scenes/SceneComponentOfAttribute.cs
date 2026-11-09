// The ECS's components a scene file holds, which the ECS's assembly, below this one, cannot
// register with the registry here.
[assembly: Engine.SceneComponentOf(typeof(Engine.Transform))]

namespace Engine;

/// <summary>
/// Registers <see cref="Type"/> as a scene component, as <see cref="SceneComponentAttribute"/> on
/// the type does, for a component declared in an assembly below the one that holds the registry.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class SceneComponentOfAttribute(Type type) : Attribute
{
    /// <summary>The component.</summary>
    public Type Type { get; } = type;
}
