namespace Engine;

/// <summary>
/// Names what a component type or one of its fields was called before, so a scene file written
/// under that name still loads.
/// </summary>
/// <remarks>
/// <para>
/// A scene file records each component under its type's name and each value under its field's
/// name. Renaming either would otherwise leave every saved file holding a component this build does
/// not know, or a value no field reads, and the rename would drop what was saved. With the old name
/// kept here, a load reads the value from where the file has it, and the next save writes it under
/// the new name.
/// </para>
/// <para>
/// On a type, a name given alone is taken to be in the type's own namespace, since a rename within
/// one is the common case, and a name holding a dot is a full name, for a type moved to another
/// namespace. On a field, it is the field's own name. A field's own name in the file is read before
/// a former one, and a type's former name that another registered type has since taken goes to that
/// type. The attribute may be repeated for a type or a field renamed more than once.
/// </para>
/// </remarks>
/// <param name="name">What it was called.</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class FormerNameAttribute(string name) : Attribute
{
    /// <summary>What it was called.</summary>
    public string Name { get; } = name;
}
