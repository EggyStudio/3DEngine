namespace Engine;

/// <summary>Filter: an entity is visited only when each listed component changed since the method's system last ran.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ChangedAttribute : Attribute
{
    /// <summary>The component types to watch for changes.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="ChangedAttribute"/> watching the specified component types for changes.</summary>
    /// <param name="types">The component types, each of which must have changed since the system last ran.</param>
    public ChangedAttribute(params Type[] types) => Types = types;
}
