namespace Engine;

/// <summary>
/// The behavior registrations the generator has added, one for each assembly with behaviors, from
/// a module initializer as the assembly loads, which <see cref="BehaviorsPlugin"/> runs.
/// </summary>
/// <remarks>
/// A list the generated code adds to, in place of a search through every loaded assembly's types
/// for <see cref="GeneratedBehaviorRegistrationAttribute"/>, so the registrations survive trimming
/// and native AOT, which cannot keep every type for such a search. Public because the generated
/// code is in the game's assembly, and hidden from an editor's completion because a program never
/// calls it.
/// </remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class GeneratedBehaviors
{
    private static readonly List<Action<App>> Registrations = [];

    /// <summary>Adds an assembly's registration. The generated module initializer calls this.</summary>
    public static void Add(Action<App> register)
    {
        lock (Registrations)
            if (!Registrations.Contains(register)) Registrations.Add(register);
    }

    /// <summary>Every registration added so far, in the order the assemblies loaded.</summary>
    public static IReadOnlyList<Action<App>> All
    {
        get
        {
            lock (Registrations) return [.. Registrations];
        }
    }
}
