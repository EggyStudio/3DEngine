namespace Engine;

/// <summary>Filter: an entity is visited only when it got each listed component since the method's system last ran.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class AddedAttribute : Attribute
{
    /// <summary>The component types to watch for being added.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="AddedAttribute"/> watching the specified component types for being added.</summary>
    /// <param name="types">The component types, each of which the entity must have got since the system last ran.</param>
    public AddedAttribute(params Type[] types) => Types = types;
}
