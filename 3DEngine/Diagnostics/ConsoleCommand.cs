namespace Engine;

/// <summary>A registered console command.</summary>
/// <param name="Name">The name typed to run it.</param>
/// <param name="Help">What it does.</param>
/// <param name="Usage">Its parameters, as <c>&lt;name&gt;</c> words.</param>
/// <param name="Run">Runs it with the words after the name, and returns its answer.</param>
public sealed record ConsoleCommand(string Name, string Help, string Usage, Func<string[], string?> Run)
{
    /// <summary>The parameters, as the generator described them.</summary>
    public IReadOnlyList<CommandParameter> Parameters { get; init; } = [];
}
