namespace Engine;

/// <summary>
/// Numbers the layout a scene component is saved in, so a scene file saved under an earlier
/// number is brought up to this one as it loads.
/// </summary>
/// <remarks>
/// <para>
/// A change a <see cref="FormerNameAttribute"/> cannot describe, a field split in two, a value
/// counted in other units or one moved into another field, is made by raising the number and giving
/// the type a static <c>Migrate(int from, JsonObject value)</c> returning a
/// <see cref="System.Text.Json.Nodes.JsonObject"/>, public or internal. A load calls it once with
/// the version the file was saved at and the component's object as the file holds it, and it gives
/// the object back as this version holds it. Written as a run of checks, <c>from &lt; 2</c> then
/// <c>from &lt; 3</c>, each change is written once and a file several versions old passes through
/// each in order. A type with no such method is warned of (E3D200), and an editor offers to add
/// one.
/// </para>
/// <para>
/// A component past version 1 is saved with its number under <c>"$version"</c>, and one with none
/// is version 1, so a type marked here for the first time reads every file saved before. A file
/// saved by a later build, at a version past this one, is read as far as this build knows its
/// fields and saved back at that version with every value it held, so an older build does not drop
/// what a newer one wrote.
/// </para>
/// </remarks>
/// <param name="version">The layout's number, from 1, raised by one for each change.</param>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class DataVersionAttribute(int version) : Attribute
{
    /// <summary>The layout's number, from 1, raised by one for each change.</summary>
    public int Version { get; } = version;
}
