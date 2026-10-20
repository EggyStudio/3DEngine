namespace Engine;

/// <summary>
/// An <see cref="AnimationStateMachine"/> choosing the clip the entity's <see cref="AnimatedModel"/>
/// plays, by the parameters the game sets on it.
/// </summary>
/// <remarks>
/// <para>
/// The machine is read from <see cref="File"/>, the file by its id, which a rename of the file
/// keeps, or from <see cref="Path"/>, once for every entity naming the file, or is
/// <see cref="Machine"/> where one made in code is given. It enters its start once the model has
/// loaded, and each frame takes the first transition whose condition holds, playing the state's
/// clip from its start through the <see cref="AnimatedModel"/> beside it, whose
/// <see cref="AnimatedModel.BlendSeconds"/> it sets to the transition's fade, so the model blends
/// from the clip before as it does when a program changes its clip. While it runs it owns the
/// model's clip, speed and time.
/// </para>
/// <para>
/// The parameters are set through the entity's component by reference, <c>ref</c> in a behavior
/// method or <see cref="EcsWorld.GetRef{T}(int)"/>, and read at the next frame's step. A scene file
/// holds the file's id, the path and the state, so a level saved mid-run starts the machine where
/// it was, and not the parameters, which the game sets again. A machine whose names do not hold
/// together, or whose file cannot be read, is said once in the log and runs nothing.
/// </para>
/// </remarks>
[SceneComponent]
public struct AnimationMachine
{
    /// <summary>The machine file, relative to the program, or empty where <see cref="File"/> or <see cref="Machine"/> is given.</summary>
    public string Path;

    /// <summary>
    /// The machine file by its id, which a rename of the file keeps, run in place of
    /// <see cref="Path"/>'s where it is set. One that reads as none runs nothing.
    /// </summary>
    public DataRef<AnimationStateMachine> File;

    /// <summary>A machine made in code, run in place of a file's. A scene file does not hold it.</summary>
    public AnimationStateMachine? Machine;

    /// <summary>The state the machine is in, by name, empty until it starts, set by the machine as it moves.</summary>
    public string State;

    private AnimationMachineParameters? _parameters;

    /// <summary>A machine read from a file.</summary>
    public AnimationMachine(string path)
    {
        Path = path;
        Machine = null;
        State = "";
        _parameters = new();
    }

    /// <summary>A machine read from a file by its id, as <see cref="DataAssets.Load{T}"/> or <see cref="DataAssets.Find{T}(string)"/> gives it.</summary>
    public AnimationMachine(DataRef<AnimationStateMachine> file)
    {
        Path = "";
        File = file;
        Machine = null;
        State = "";
        _parameters = new();
    }

    /// <summary>A machine made in code.</summary>
    public AnimationMachine(AnimationStateMachine machine)
    {
        Path = "";
        Machine = machine;
        State = "";
        _parameters = new();
    }

    // Made with the component, or the first time a component read from a scene file is stepped
    // or set, through a reference to the entity's own.
    internal AnimationMachineParameters Parameters => _parameters ??= new();

    /// <summary>Whether the component has its parameters, which one read from a scene file has not until first stepped or set.</summary>
    internal readonly bool HasParameters => _parameters is not null;

    /// <summary>Sets a number the machine compares, such as the character's speed.</summary>
    public void Set(string name, float value) => Parameters.Values[name] = value;

    /// <summary>Sets a flag the machine reads, such as whether the character stands on the ground.</summary>
    public void Set(string name, bool value) => Set(name, value ? 1f : 0f);

    /// <summary>Fires a trigger the machine waits on, such as a jump, which stays until a transition takes it.</summary>
    public void Trigger(string name) => Parameters.Triggers.Add(name);

    /// <summary>A parameter as last set, 0 where it never was.</summary>
    public readonly float Get(string name) => _parameters?.Values.GetValueOrDefault(name) ?? 0;
}

/// <summary>A machine's parameters and where it is in its state, kept by reference so copies of the component share them.</summary>
internal sealed class AnimationMachineParameters
{
    public readonly Dictionary<string, float> Values = new(StringComparer.Ordinal);
    public readonly HashSet<string> Triggers = new(StringComparer.Ordinal);

    /// <summary>The state entered, by its index in the machine, or -1 before the machine starts.</summary>
    public int StateIndex = -1;

    /// <summary>Whether a clip that plays once has reached its end, where it holds.</summary>
    public bool Ended;

    /// <summary>The machine object, the file's id or the path it was compiled from, and what it was compiled to, or null where it runs nothing.</summary>
    public object? Source;
    public object? Compiled;
}
