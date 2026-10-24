namespace Engine;

/// <summary>
/// Runs a stage method after another system of its stage, one named as <c>AddSystem</c> named it,
/// or a behavior's method by its type and name, as <see cref="SystemDescriptor.After"/> orders one.
/// </summary>
/// <remarks>
/// More than one asks for each of them. A behavior's method is found by its type and name, so the
/// name the generator gives its system is never written out, and one the generator finds no stage
/// method of is reported (E3D009). <see cref="BeforeAttribute"/> is the other side of it.
/// </remarks>
/// <example>
/// <code>
/// [OnUpdate]
/// [After(typeof(Spawner), nameof(Spawner.Spawn))]
/// public static void Count(BehaviorContext ctx) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class AfterAttribute : Attribute
{
    /// <summary>After the system of this name, as <c>AddSystem</c> named it.</summary>
    public AfterAttribute(string system) => System = system;

    /// <summary>After the method of this name of the behavior <paramref name="behavior"/>.</summary>
    public AfterAttribute(Type behavior, string method)
    {
        Behavior = behavior;
        Method = method;
    }

    /// <summary>The system's name, or null where a behavior's method is named.</summary>
    public string? System { get; }

    /// <summary>The behavior whose method is named, or null.</summary>
    public Type? Behavior { get; }

    /// <summary>The behavior's method's name, or null.</summary>
    public string? Method { get; }
}
