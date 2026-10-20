using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>
/// The states a model's clips are played in and the conditions that move it from one to the next,
/// run on an entity's <see cref="AnimatedModel"/> by its <see cref="AnimationMachine"/>.
/// </summary>
/// <remarks>
/// <para>
/// A character idles, walks once its speed passes a value, runs past a higher one, and jumps when
/// a trigger fires, landing back where it was once the jump's clip ends. Each of those is a state
/// naming a clip, and each move between them a transition naming a condition on a parameter the
/// game sets, so the game says what the character is doing and the machine says which clip shows
/// it. Transitions are tried in the order they are listed each frame, and the first whose
/// condition holds is taken, so a list puts the more particular before the more general.
/// </para>
/// <para>
/// It is data, kept in a JSON file as a scene is, with a <c>format</c> and a <c>version</c>, each
/// state and transition an object of its fields by name and a condition by its name, written by
/// <see cref="Write()"/> and read by <see cref="Read(string)"/>, and shared by every entity that
/// names the file. <see cref="Save"/> gives the file an id at its first save, by which
/// <see cref="AnimationMachine.File"/> refers to it through a rename, as <see cref="DataAssets"/>
/// keeps it. One made in code runs the same way.
/// </para>
/// </remarks>
public sealed class AnimationStateMachine
{
    /// <summary>The value of a machine file's <c>format</c> key.</summary>
    public const string Format = "3dengine-animation-machine";

    /// <summary>The version this code writes.</summary>
    public const int Version = 1;

    /// <summary>The state a model starts in, by name, or empty for the first of <see cref="States"/>.</summary>
    public string Start = "";

    /// <summary>The states, each a clip and how it plays.</summary>
    public List<AnimationMachineState> States = [];

    /// <summary>The ways from one state to another, tried in order.</summary>
    public List<AnimationMachineTransition> Transitions = [];

    /// <summary>Reads a machine file, found in the working directory, beside the program or under <c>source/</c> beside it.</summary>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="InvalidDataException">The file is not a machine this code reads.</exception>
    public static AnimationStateMachine Load(string fileName) =>
        Read(E3D.ResolveFile(fileName) is { } path ? File.ReadAllText(path)
            : throw new FileNotFoundException($"The animation state machine '{fileName}' was not found in the working directory, beside the program or under source/ beside it.", fileName));

    /// <summary>
    /// Writes the machine to a file, a relative name beside the program, where <see cref="Load"/>
    /// finds it again, keeping the id the file has or giving it one at its first save.
    /// </summary>
    public void Save(string fileName) => DataAssets.Save(fileName, this);

    /// <summary>The machine as the JSON of a machine file, with no id.</summary>
    public string Write()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
               {
                   Indented = true,
                   Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
               }))
            Write(writer, null);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // The machine as a machine file's object, with the file's id after its format and version.
    internal void Write(Utf8JsonWriter writer, string? id)
    {
        writer.WriteStartObject();
        writer.WriteString("format", Format);
        writer.WriteNumber("version", Version);
        if (id is not null) writer.WriteString("id", id);
        writer.WriteString(nameof(Start), Start);
        writer.WriteStartArray(nameof(States));
        foreach (var state in States)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(state.Name), state.Name);
            writer.WriteString(nameof(state.Clip), state.Clip);
            writer.WriteBoolean(nameof(state.Loop), state.Loop);
            writer.WriteNumber(nameof(state.Speed), state.Speed);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartArray(nameof(Transitions));
        foreach (var transition in Transitions)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(transition.From), transition.From);
            writer.WriteString(nameof(transition.To), transition.To);
            writer.WriteString(nameof(transition.When), transition.When.ToString());
            writer.WriteString(nameof(transition.Parameter), transition.Parameter);
            writer.WriteNumber(nameof(transition.Value), transition.Value);
            writer.WriteNumber(nameof(transition.Fade), transition.Fade);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>A machine from the JSON of a machine file, a field it lacks keeping its default.</summary>
    /// <exception cref="InvalidDataException">The text is not a machine this code reads.</exception>
    public static AnimationStateMachine Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Read(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Not an animation state machine: {ex.Message}", ex);
        }
    }

    // The same from a file already parsed, as a data file's reference reads it.
    internal static AnimationStateMachine Read(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("format", out var format) || format.GetString() != Format)
                throw new InvalidDataException($"Not an animation state machine: its \"format\" is not \"{Format}\".");
            if (root.TryGetProperty("version", out var version) && version.GetInt32() > Version)
                throw new InvalidDataException($"The animation state machine is version {version.GetInt32()}, and this engine reads up to {Version}.");

            var machine = new AnimationStateMachine { Start = Text(root, nameof(Start), "") };
            if (root.TryGetProperty(nameof(States), out var states))
                foreach (var e in states.EnumerateArray())
                {
                    var state = new AnimationMachineState();
                    state.Name = Text(e, nameof(state.Name), state.Name);
                    state.Clip = Text(e, nameof(state.Clip), state.Clip);
                    if (e.TryGetProperty(nameof(state.Loop), out var loop)) state.Loop = loop.GetBoolean();
                    if (e.TryGetProperty(nameof(state.Speed), out var speed)) state.Speed = speed.GetSingle();
                    machine.States.Add(state);
                }
            if (root.TryGetProperty(nameof(Transitions), out var transitions))
                foreach (var e in transitions.EnumerateArray())
                {
                    var transition = new AnimationMachineTransition();
                    transition.From = Text(e, nameof(transition.From), transition.From);
                    transition.To = Text(e, nameof(transition.To), transition.To);
                    if (e.TryGetProperty(nameof(transition.When), out var when))
                        transition.When = Enum.TryParse<AnimationCondition>(when.GetString(), out var condition) ? condition
                            : throw new InvalidDataException($"A transition is taken when \"{when.GetString()}\", which is no {nameof(AnimationCondition)}.");
                    transition.Parameter = Text(e, nameof(transition.Parameter), transition.Parameter);
                    if (e.TryGetProperty(nameof(transition.Value), out var value)) transition.Value = value.GetSingle();
                    if (e.TryGetProperty(nameof(transition.Fade), out var fade)) transition.Fade = fade.GetSingle();
                    machine.Transitions.Add(transition);
                }
            return machine;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"Not an animation state machine: {ex.Message}", ex);
        }
    }

    private static string Text(JsonElement e, string name, string fallback) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : fallback;

    /// <summary>What is wrong with the machine's names, or null where nothing is.</summary>
    internal string? Problem()
    {
        if (States.Count == 0) return "The machine has no states.";
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in States)
        {
            if (string.IsNullOrEmpty(state.Name)) return "A state has no name.";
            if (!names.Add(state.Name)) return $"Two states are called {state.Name}.";
        }
        if (Start.Length > 0 && !names.Contains(Start)) return $"The machine starts in {Start}, which is not one of its states.";
        foreach (var transition in Transitions)
        {
            var label = $"The transition from {(transition.From.Length > 0 ? transition.From : "any state")} to {transition.To}";
            if (transition.From.Length > 0 && !names.Contains(transition.From)) return $"{label} leaves a state the machine does not have.";
            if (!names.Contains(transition.To)) return $"{label} enters a state the machine does not have.";
            if (transition.When != AnimationCondition.Finished && transition.Parameter.Length == 0)
                return $"{label} is taken when {transition.When} on a parameter with no name.";
        }
        return null;
    }
}
