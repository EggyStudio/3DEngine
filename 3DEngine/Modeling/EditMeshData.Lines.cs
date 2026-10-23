using System.Globalization;
using System.Numerics;

namespace Engine;

// The lines an operation writes to the recipe once it has done what it says, in the words the
// recipe's file and the console's model commands take, so running the lines from an empty mesh
// makes the mesh again.
internal sealed partial class EditMeshData
{
    private readonly List<string> _recipe = [];

    /// <summary>The operations that made the mesh, a line each, the oldest first.</summary>
    public IReadOnlyList<string> Recipe => _recipe;

    /// <summary>Writes an operation's line to the recipe, which forgets what was undone unless the line is one done again.</summary>
    private void Did(string line)
    {
        _recipe.Add(line);
        if (!_replaying) _undone.Clear();
    }

    internal static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string V(Vector3 value) => $"{F(value.X)} {F(value.Y)} {F(value.Z)}";

    internal static string Placement(Vector3 at, bool centered) => (at == Vector3.Zero ? "" : " at " + V(at)) + (centered ? " center" : "");

    private static string Pivot(Vector3? pivot) => pivot is { } at ? " at " + V(at) : "";

    private static string Mode(SelectionMode mode) => mode switch
    {
        SelectionMode.Add => "add ",
        SelectionMode.Remove => "remove ",
        _ => "",
    };

    private static string Way((int Axis, bool Negative) way) => (way.Negative ? "-" : "+") + "xyz"[way.Axis];

    /// <summary>An axis written as its letter where it runs along X, Y or Z, and as its three numbers where it does not.</summary>
    private static string AxisWords(Vector3 axis) =>
        axis.Y == 0f && axis.Z == 0f && axis.X > 0f ? "x"
        : axis.X == 0f && axis.Z == 0f && axis.Y > 0f ? "y"
        : axis.X == 0f && axis.Y == 0f && axis.Z > 0f ? "z"
        : V(axis);
}
