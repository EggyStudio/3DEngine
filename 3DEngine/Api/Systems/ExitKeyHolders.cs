namespace Engine;

/// <summary>
/// The questions a window asks before it acts on the exit key, added by
/// <see cref="E3D.HoldExitKeyWhile"/>, which the world holds and lets go of with the window.
/// </summary>
internal sealed class ExitKeyHolders
{
    private readonly List<Func<bool>> _questions = [];

    /// <summary>Adds a question, asked after those added before it.</summary>
    public void Add(Func<bool> holds) => _questions.Add(holds);

    /// <summary>Whether one of the questions answers true, asked in the order of adding.</summary>
    public bool Holds() => _questions.Exists(holds => holds());
}
