namespace Engine;

/// <summary>Filter: schedule only for entities that also have all listed component types.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WithAttribute : Attribute
{
    /// <summary>The component types to require.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="WithAttribute"/> requiring all specified component types.</summary>
    /// <param name="types">The component types that must be present on the entity.</param>
    public WithAttribute(params Type[] types) => Types = types;
}
