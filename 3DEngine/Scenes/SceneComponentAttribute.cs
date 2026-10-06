using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>
/// Marks a component struct as one a scene file saves and loads. Its public fields of the types
/// <see cref="SceneFile"/> knows are written by name, and the rest are left out.
/// </summary>
/// <remarks>
/// The generator writes the code that saves and loads it, so no reflection runs when a scene is
/// read. A <c>[Behavior]</c> struct is saved the same way without the attribute, since its fields
/// are its state. A field's value when it is missing from a file is the component's static
/// <c>Default</c> or <c>Identity</c> when it has one, and zero otherwise.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class SceneComponentAttribute : Attribute;
