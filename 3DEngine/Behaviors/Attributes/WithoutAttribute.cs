namespace Engine;

/// <summary>Filter: skip entities that have any of the listed component types.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WithoutAttribute : Attribute
{
    /// <summary>The component types to exclude.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="WithoutAttribute"/> excluding entities with any of the specified types.</summary>
    /// <param name="types">The component types that must <em>not</em> be present on the entity.</param>
    public WithoutAttribute(params Type[] types) => Types = types;
}
