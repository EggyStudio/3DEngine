namespace Engine;

/// <summary>An entity's name, for finding it and for tools that list the world.</summary>
public struct Name
{
    /// <summary>The name.</summary>
    public string Value;

    /// <summary>Creates a name.</summary>
    public Name(string value) => Value = value;

    /// <inheritdoc />
    public override readonly string ToString() => Value;
}
