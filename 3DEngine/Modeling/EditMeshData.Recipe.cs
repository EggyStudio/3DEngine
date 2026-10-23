using System.Globalization;
using System.Numerics;
using System.Text;

namespace Engine;

// The recipe read back, a line at a time, which is the mesh's undo and its file. Running the lines
// from an empty mesh makes the mesh again, so an undo is the mesh made again without its last line
// and a saved model is its recipe, rebuilt when it loads.
internal sealed partial class EditMeshData
{
    /// <summary>What a recipe's file name ends in.</summary>
    public const string Extension = ".model";

    /// <summary>The line a recipe's file starts with, its form and the form's version.</summary>
    public const string Header = "model 1";

    private readonly Stack<string> _undone = new();
    private bool _replaying;

    /// <summary>The recipe as the text of its file, its header first.</summary>
    public string RecipeText
    {
        get
        {
            var text = new StringBuilder(Header).Append('\n');
            foreach (var line in _recipe) text.Append(line).Append('\n');
            return text.ToString();
        }
    }

    /// <summary>Takes back the last operation, by making the mesh again from the lines before it, and says whether there was one.</summary>
    /// <remarks>
    /// The mesh is made again from nothing rather than the last operation reversed, so an undo is
    /// right for every operation by the same code that made it, and costs what the recipe costs to
    /// run. The selection is the one the lines left, since selecting is a line of the recipe too.
    /// </remarks>
    public bool Undo()
    {
        if (_recipe.Count == 0) return false;
        var lines = _recipe.ToArray();
        _undone.Push(lines[^1]);
        Rebuild(lines[..^1]);
        return true;
    }

    /// <summary>Does again the operation last taken back, and says whether there was one, an operation done after an undo forgetting what was taken back.</summary>
    public bool Redo()
    {
        if (!_undone.TryPop(out var line)) return false;
        _replaying = true;
        try
        {
            Run(line);
        }
        finally
        {
            _replaying = false;
        }
        return true;
    }

    /// <summary>Runs one line of a recipe on the mesh, as though its operation were called, a comment after <c>#</c> left out.</summary>
    /// <exception cref="FormatException">The line is not an operation, or its measures are not the operation's.</exception>
    /// <exception cref="ArgumentException">A measure is out of the operation's range.</exception>
    public void Run(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var hash = line.IndexOf('#');
        var words = (hash >= 0 ? line[..hash] : line).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0) new Reader(words, line).Apply(this);
    }

    /// <summary>Makes a mesh from a recipe's text, its lines run in order from an empty mesh, blank lines and comments skipped.</summary>
    /// <param name="text">The recipe, a line an operation, with its header line or without it.</param>
    /// <param name="name">What the text is called in an error, its file's path where it came from one.</param>
    /// <exception cref="FormatException">A line is not an operation or fails, or the header names another version.</exception>
    public static EditMeshData FromRecipe(string text, string name = "the recipe")
    {
        ArgumentNullException.ThrowIfNull(text);
        var mesh = new EditMeshData();
        var number = 0;
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            number++;
            var hash = line.IndexOf('#');
            var meaningful = (hash >= 0 ? line[..hash] : line).Trim();
            if (meaningful.Length == 0) continue;
            if (meaningful.StartsWith("model ", StringComparison.Ordinal))
            {
                if (meaningful != Header) throw new FormatException($"Line {number} of {name} is {meaningful}, and this engine reads {Header}.");
                continue;
            }

            try
            {
                mesh.Run(meaningful);
            }
            catch (Exception error) when (error is FormatException or ArgumentException or InvalidOperationException or IOException)
            {
                throw new FormatException($"Line {number} of {name}, {meaningful}, {Lowered(error.Message)}", error);
            }
        }

        // A recipe read is the mesh's history from here, nothing undone.
        mesh._undone.Clear();
        return mesh;
    }

    /// <summary>Makes a mesh from a recipe's file, found beside the program or in the working directory, which starts with <see cref="Header"/>.</summary>
    /// <remarks>The header tells a recipe from another file of text, so a file without it is refused rather than read as lines that fail.</remarks>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="FormatException">The file does not start with the header, or a line of it is not an operation.</exception>
    public static EditMeshData Load(string path)
    {
        var found = E3D.ResolveFile(path) ?? throw new FileNotFoundException($"There is no model at {path}.", path);
        var text = File.ReadAllText(found);
        var first = text.ReplaceLineEndings("\n").Split('\n').Select(line => (line.IndexOf('#') is var hash and >= 0 ? line[..hash] : line).Trim())
            .FirstOrDefault(line => line.Length > 0);
        if (first is null || !first.StartsWith("model ", StringComparison.Ordinal))
            throw new FormatException($"{path} does not start with the line {Header}, so it is not a model's recipe.");
        return FromRecipe(text, path);
    }

    /// <summary>Writes the recipe to a file, beside the program where the path is relative, and answers its full path.</summary>
    /// <remarks>
    /// The recipe rather than the vertices, so a level's model is a short text a reader follows and a
    /// diff reads line by line. Written under a name of its own and moved into place, so a program
    /// reading it while it is written reads the old file or the new one whole.
    /// </remarks>
    public string Save(string path)
    {
        var full = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        if (Path.GetDirectoryName(full) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        var part = full + ".part";
        File.WriteAllText(part, RecipeText, new UTF8Encoding(false));
        File.Move(part, full, overwrite: true);
        return full;
    }

    /// <summary>Makes the mesh again from nothing, from the lines given.</summary>
    private void Rebuild(string[] lines)
    {
        _positions.Clear();
        _faces.Clear();
        _slots.RemoveRange(1, _slots.Count - 1);
        _selectedVertices.Clear();
        _selectedFaces.Clear();
        _recipe.Clear();
        GridStep = 1;
        Changed();

        _replaying = true;
        try
        {
            foreach (var line in lines) Run(line);
        }
        finally
        {
            _replaying = false;
        }
    }

    // An exception's message carried on after a comma, its first letter lowered unless it starts a name.
    private static string Lowered(string message) =>
        message.Length > 1 && char.IsUpper(message[0]) && !char.IsUpper(message[1]) ? char.ToLowerInvariant(message[0]) + message[1..] : message;

    /// <summary>Reads one line's words into the operation they name, and calls it.</summary>
    private sealed class Reader(string[] words, string line)
    {
        private int _at = 1;

        public void Apply(EditMeshData mesh)
        {
            switch (words[0])
            {
                case "box": mesh.Box(Vector(), Where(out var centered), centered); break;
                case "plane": mesh.Plane(Number(), Number(), Where(out centered), centered); break;
                case "wedge": mesh.Wedge(Vector(), Where(out centered), centered); break;
                case "cylinder": mesh.Cylinder(Number(), Number(), Count(), Where(out centered), centered); break;
                case "cone": mesh.Cone(Number(), Number(), Count(), Where(out centered), centered); break;
                case "stairs": mesh.Stairs(Count(), Number(), Number(), Number(), Where(out centered), centered); break;
                case "arch": mesh.Arch(Number(), Number(), Number(), Count(), Where(out centered), centered); break;
                case "select": Select(mesh); break;
                case "move": mesh.Move(Vector()); break;
                case "rotate": mesh.Rotate(Axis(), Number(), Pivot()); break;
                case "scale": Scale(mesh); break;
                case "grid": mesh.Grid(Number()); break;
                case "snap": mesh.Snap(); break;
                case "material": mesh.Material(Word("a slot's name")); break;
                case "shade": Shade(mesh); break;
                case "clear": mesh.Clear(); break;
                case "extrude" when words.Length == 2: mesh.Extrude(Number()); break;
                case "extrude": mesh.Extrude(Vector()); break;
                case "inset": mesh.Inset(Number()); break;
                case "delete": mesh.Delete(Optional("fill")); break;
                case "merge": mesh.Merge(Number()); break;
                case "flip": mesh.Flip(); break;
                case "triangulate": mesh.Triangulate(); break;
                case "duplicate": mesh.Duplicate(); break;
                case "join": mesh.Join(Word("a model's path")); break;
                case "bevel": mesh.Bevel(Number(), _at < words.Length ? Count() : 1); break;
                case "loopcut": mesh.LoopCut(Number()); break;
                case "subdivide": mesh.Subdivide(_at < words.Length ? Count() : 1); break;
                case "bridge": mesh.Bridge(); break;
                case "fill": mesh.Fill(); break;
                case "dissolve" when Optional("edges"): mesh.DissolveEdges(); break;
                case "dissolve": mesh.Dissolve(); break;
                case "knife": mesh.Knife(Axis(), Pivot() ?? Vector3.Zero); break;
                case "mirror": mesh.Mirror(Axis(), Pivot() ?? Vector3.Zero); break;
                case "spin": mesh.Spin(Axis(), Number(), Count(), Pivot() ?? Vector3.Zero); break;
                case "smooth": mesh.Smooth(Number()); break;
                default: throw new FormatException($"{words[0]} is not an operation of a model's recipe.");
            }

            if (_at < words.Length) throw new FormatException($"{line.Trim()} has words after its operation's, from {words[_at]}.");
        }

        private void Select(EditMeshData mesh)
        {
            var what = Word("what to select");
            switch (what)
            {
                case "all": mesh.SelectAll(); return;
                case "none": mesh.SelectNone(); return;
                case "invert": mesh.SelectInvert(); return;
            }

            var mode = SelectionMode.Replace;
            if (what is "add" or "remove")
            {
                mode = what == "add" ? SelectionMode.Add : SelectionMode.Remove;
                what = Word("what to select");
            }

            switch (what)
            {
                case "face":
                    var faces = new List<int>();
                    while (_at < words.Length) faces.Add(Count());
                    if (faces.Count == 0) throw new FormatException("select face takes a face's index or more.");
                    mesh.SelectFaces([.. faces], mode);
                    return;
                case "box":
                    mesh.SelectInside(Vector(), Vector(), mode);
                    return;
                case "slot":
                    mesh.SelectSlot(Word("a slot's name"), mode);
                    return;
                case { Length: 2 } way when way[0] is '+' or '-' && way[1] is 'x' or 'y' or 'z':
                    var axis = way[1] - 'x';
                    var sign = way[0] == '-' ? -1f : 1f;
                    mesh.SelectFacing(new Vector3(axis == 0 ? sign : 0, axis == 1 ? sign : 0, axis == 2 ? sign : 0), mode);
                    return;
                default:
                    throw new FormatException($"select {what} is none of all, none, invert, face, box, slot or a way along an axis such as +y.");
            }
        }

        private void Scale(EditMeshData mesh)
        {
            // One factor or three, told apart by whether a number follows the first.
            var first = Number();
            var factor = _at < words.Length && words[_at] != "at" ? new Vector3(first, Number(), Number()) : new Vector3(first);
            mesh.Scale(factor, Pivot());
        }

        private void Shade(EditMeshData mesh)
        {
            switch (Word("smooth or flat"))
            {
                case "flat":
                    mesh.Shade(false, 0);
                    return;
                case "smooth":
                    mesh.Shade(true, _at < words.Length ? Number() : DefaultSmoothAngle);
                    return;
                default:
                    throw new FormatException("shade takes smooth, with an angle in degrees where one is given, or flat.");
            }
        }

        /// <summary>An axis as its letter, x, y or z, or as three numbers.</summary>
        private Vector3 Axis()
        {
            if (_at >= words.Length || words[_at] is not ("x" or "y" or "z")) return Vector();
            var named = words[_at++][0] - 'x';
            return new Vector3(named == 0 ? 1 : 0, named == 1 ? 1 : 0, named == 2 ? 1 : 0);
        }

        private Vector3 Where(out bool centered)
        {
            var at = Vector3.Zero;
            if (_at < words.Length && words[_at] == "at")
            {
                _at++;
                at = Vector();
            }
            centered = Optional("center");
            return at;
        }

        private Vector3? Pivot()
        {
            if (_at >= words.Length || words[_at] != "at") return null;
            _at++;
            return Vector();
        }

        private bool Optional(string word)
        {
            if (_at >= words.Length || words[_at] != word) return false;
            _at++;
            return true;
        }

        private Vector3 Vector() => new(Number(), Number(), Number());

        private float Number()
        {
            if (_at >= words.Length) throw new FormatException($"{words[0]} needs another number after {words[_at - 1]}.");
            var word = words[_at++];
            return float.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value)
                ? value
                : throw new FormatException($"{word} is not a number, which {words[0]} takes there.");
        }

        private int Count()
        {
            if (_at >= words.Length) throw new FormatException($"{words[0]} needs another whole number after {words[_at - 1]}.");
            var word = words[_at++];
            return int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException($"{word} is not a whole number, which {words[0]} takes there.");
        }

        private string Word(string what)
        {
            if (_at >= words.Length) throw new FormatException($"{words[0]} needs {what} after {words[_at - 1]}.");
            return words[_at++];
        }
    }
}
