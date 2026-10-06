namespace Engine;

/// <summary>
/// Marks a static method as a console command. The source generator registers it at assembly
/// load, so it is a line in the console, a verb for <c>e3d command</c> and an entry in
/// <c>e3d list</c> at once.
/// </summary>
/// <remarks>
/// The method returns a <see cref="string"/> (the answer) or nothing, and its parameters are
/// <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>,
/// <see cref="float"/> or <see cref="double"/>, read from the words after the name. A single
/// string parameter takes the rest of the line, spaces and all. A word that does not parse is
/// answered with a sentence rather than an exception.
/// </remarks>
/// <example>
/// <code>
/// [Command("enemy.spawn", "Spawns enemies: enemy.spawn &lt;count&gt;")]
/// internal static string Spawn(int count) => $"spawned {count}";
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute(string name = "", string help = "") : Attribute
{
    /// <summary>The name typed to run the command. Defaults to the method's name in lower case.</summary>
    public string Name { get; } = name;

    /// <summary>One sentence saying what the command does and how it is called.</summary>
    public string Help { get; } = help;
}
