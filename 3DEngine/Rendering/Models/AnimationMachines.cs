using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// Steps each entity's <see cref="AnimationMachine"/> once a frame, before its
/// <see cref="AnimatedModel"/> is played, choosing the clip by the machine's transitions.
/// </summary>
/// <remarks>
/// A machine is compiled once, a file's by its path, and one made in code or read by a file's id by
/// the object, into the list of transitions tried from each state, so a step with no transition due
/// reads the parameters those transitions name and nothing more. Entering a state sets the model's
/// clip, speed and blend time and plays the clip from its start, and
/// <see cref="AnimatedModelDraws"/> blends to it as it blends any change of clip. A state that
/// plays once is held at its last frame by stopping the model's time there, in the frame it would
/// pass it, so its pose never comes round to the start.
/// </remarks>
internal sealed class AnimationMachines
{
    private static readonly ILogger Logger = Log.Category("Engine.Models");

    private readonly Dictionary<string, Compiled?> _files = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<AnimationStateMachine, Compiled?> _made = [];
    private readonly HashSet<string> _said = new(StringComparer.Ordinal);

    // A machine with each state's transitions in the order they are tried, and the state each enters.
    private sealed class Compiled(AnimationStateMachine machine)
    {
        public readonly AnimationMachineState[] States = [.. machine.States];
        public readonly int Start = machine.Start.Length > 0 ? machine.States.FindIndex(s => s.Name == machine.Start) : 0;
        public readonly (AnimationMachineTransition Transition, int To)[][] From =
            [.. machine.States.Select(state => machine.Transitions
                .Where(t => t.From.Length > 0 ? t.From == state.Name : t.To != state.Name)
                .Select(t => (t, machine.States.FindIndex(s => s.Name == t.To)))
                .ToArray())];

        public int IndexOf(string name) => Array.FindIndex(States, s => s.Name == name);
    }

    /// <summary>Moves the entity's machine one frame, before <paramref name="animated"/> is played over <paramref name="dt"/>.</summary>
    public void Step(EcsWorld ecs, int entity, ref AnimatedModel animated, ModelAnimation[] clips, float dt)
    {
        if (clips.Length == 0 || !ecs.TryGet<AnimationMachine>(entity, out var machine)) return;
        var run = machine.HasParameters ? machine.Parameters : ecs.GetRef<AnimationMachine>(entity).Parameters;
        var source = Source(machine);
        if (!ReferenceEquals(run.Source, source))
        {
            run.Source = source;
            run.Compiled = Compile(machine);
            run.StateIndex = -1;
        }
        if (run.Compiled is not Compiled compiled) return;

        if (run.StateIndex < 0)
        {
            var saved = string.IsNullOrEmpty(machine.State) ? -1 : compiled.IndexOf(machine.State);
            Enter(ecs, entity, ref animated, run, compiled, saved >= 0 ? saved : compiled.Start, 0);
            return;
        }

        var state = compiled.States[run.StateIndex];
        var frames = Find(clips, state.Clip)?.KeyframeCount ?? 0;
        bool finished;
        if (state.Loop)
            finished = frames > 0 && MathF.Abs(animated.Time) >= frames / (float)E3D.AnimationFps;
        else
        {
            // Finished from the frame after the one that drew the last frame, which is held from
            // the frame the clip would pass it, rather than after, when the pose would already have
            // come round to the start.
            finished = run.Ended;
            var last = (frames - 1) / (float)E3D.AnimationFps;
            if (!run.Ended && frames > 0 && MathF.Abs(animated.Time + animated.Speed * dt) >= last)
            {
                animated.Time = MathF.CopySign(last, state.Speed);
                animated.Speed = 0;
                run.Ended = true;
            }
        }

        var tried = compiled.From[run.StateIndex];
        for (int i = 0; i < tried.Length; i++)
        {
            var (transition, to) = tried[i];
            if (!Holds(transition, run, finished)) continue;
            if (transition.When == AnimationCondition.Triggered) run.Triggers.Remove(transition.Parameter);
            Enter(ecs, entity, ref animated, run, compiled, to, transition.Fade);
            return;
        }
    }

    private static bool Holds(AnimationMachineTransition transition, AnimationMachineParameters run, bool finished) => transition.When switch
    {
        AnimationCondition.Finished => finished,
        AnimationCondition.True => run.Values.GetValueOrDefault(transition.Parameter) != 0,
        AnimationCondition.False => run.Values.GetValueOrDefault(transition.Parameter) == 0,
        AnimationCondition.Above => run.Values.GetValueOrDefault(transition.Parameter) > transition.Value,
        AnimationCondition.Below => run.Values.GetValueOrDefault(transition.Parameter) < transition.Value,
        AnimationCondition.Triggered => run.Triggers.Contains(transition.Parameter),
        _ => false,
    };

    private static void Enter(EcsWorld ecs, int entity, ref AnimatedModel animated, AnimationMachineParameters run, Compiled compiled, int index, float fade)
    {
        var state = compiled.States[index];
        animated.Clip = state.Clip;
        animated.Speed = state.Speed;
        animated.Time = 0;
        animated.BlendSeconds = fade;
        run.StateIndex = index;
        run.Ended = false;
        ecs.GetRef<AnimationMachine>(entity).State = state.Name;
    }

    // What a machine is compiled from, the object made in code, the file's value where the id
    // reads one, the id where it reads none, or else the path.
    private static object? Source(in AnimationMachine machine)
    {
        if (machine.Machine is { } made) return made;
        if (machine.File.Id is not { } id) return machine.Path;
        return machine.File.TryGet(out var read) ? read : id;
    }

    // The machine compiled, or null for one whose names do not hold together or whose file cannot
    // be read, which is said once. A file's id that reads none was said as it was read.
    private Compiled? Compile(in AnimationMachine machine)
    {
        if (machine.Machine is { } made) return _made.GetValue(made, m => Checked(m, "made in code"));
        if (machine.File.Id is { } id)
        {
            var named = $"'{machine.File.Path}' ({id})";
            return machine.File.TryGet(out var file) ? _made.GetValue(file, m => Checked(m, named)) : null;
        }
        var path = machine.Path ?? "";
        if (_files.TryGetValue(path, out var known)) return known;
        AnimationStateMachine? read = null;
        try
        {
            read = AnimationStateMachine.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Say(path, $"AnimationMachine: '{path}' could not be read, so it runs nothing. {ex.Message}");
        }
        return _files[path] = read is null ? null : Checked(read, $"'{path}'");
    }

    private Compiled? Checked(AnimationStateMachine machine, string what)
    {
        if (machine.Problem() is not { } problem) return new Compiled(machine);
        Say(what, $"AnimationMachine: the machine {what} runs nothing. {problem}");
        return null;
    }

    private void Say(string what, string message)
    {
        if (_said.Add(what)) Logger.Error(message);
    }

    // A clip by name, or the first for an empty name, looked for each step without a delegate.
    private static ModelAnimation? Find(ModelAnimation[] clips, string? name)
    {
        if (string.IsNullOrEmpty(name)) return clips.Length > 0 ? clips[0] : null;
        foreach (var clip in clips)
            if (clip.Name == name) return clip;
        return null;
    }
}
