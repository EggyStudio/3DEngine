using System.Globalization;
using System.Numerics;

namespace Engine;

/// <summary>
/// The modeling commands, one for each operation of an edit mesh's recipe, so a level is blocked
/// out from the terminal, a line typed being a line of the model's recipe.
/// </summary>
/// <remarks>
/// <para>
/// Each takes the model's name first and then the operation's words as the recipe has them, so
/// <c>model.box room 8 3 6</c> runs <c>box 8 3 6</c> on the model called room, and each answers with
/// the model's counts after it, so a script reads what it did. A primitive, a join, an import or a
/// union makes the model where there is none of the name, and any other operation on a name no
/// model has is refused, so a mistyped name makes no model.
/// </para>
/// <para>
/// The models are those the commands made and those a program named with
/// <see cref="E3D.SetEditMeshName"/>, so a game's level answers to them, and they last while the app
/// runs (<see cref="EditMeshNames"/>). <c>model.show</c> draws one in its slots' prototype
/// materials and keeps drawing it as the commands change it. Paths are read from the app's working
/// directory, as the console's other files are.
/// </para>
/// </remarks>
internal static class EditMeshCommands
{
    [Command("model.new", "Makes an empty model: model.new <name>")]
    internal static string New(string name)
    {
        if (!EditMeshNames.IsName(name)) return Refuse("BAD_ARGUMENT", "model.new takes a name of one word.");
        Names.Name(name, EditMeshStore.Add(new EditMeshData()), made: true);
        return $"{name} is an empty model";
    }

    [Command("model.free", "Forgets a model and takes away what draws it: model.free <name>")]
    internal static string Free(string name) =>
        Names.Forget(name, ConsoleHost.World!.TryGetResource<EcsWorld>(out var ecs) ? ecs : null) ? $"forgot {name}" : Missing(name);

    [Command("model.list", "Lists the models with their counts")]
    internal static string List()
    {
        var all = Names.All;
        return all.Count == 0 ? "no models" : string.Join("\n", all.Select(named => Counts(named.Name, Data(named.Mesh))));
    }

    [Command("model.info", "Says a model's counts, bounds, volume, open edges, slots and grid: model.info <name>")]
    internal static string Info(string name)
    {
        if (Names.Find(name) is not { } named) return Missing(name);

        var mesh = Data(named.Mesh);
        var (low, high) = (mesh.Bounds.Min, mesh.Bounds.Max);
        var culture = CultureInfo.InvariantCulture;
        return string.Join("\n",
            Counts(name, mesh),
            string.Create(culture, $"bounds ({low.X}, {low.Y}, {low.Z}) to ({high.X}, {high.Y}, {high.Z})"),
            string.Create(culture, $"volume {mesh.Volume} cubic meters, {mesh.OpenEdges} open edges, {(mesh.OpenEdges == 0 && mesh.FaceCount > 0 ? "closed" : "open")}"),
            $"slots {string.Join(", ", mesh.Slots.Select((slot, index) => mesh.LookAt(index).IsPrototype ? slot : $"{slot} ({mesh.LookAt(index).Words()})"))}",
            string.Create(culture, $"grid {mesh.GridStep}, epsilon {mesh.BooleanEpsilon}, {mesh.Recipe.Count} lines of recipe"));
    }

    [Command("model.recipe", "Prints a model's recipe as its file holds it: model.recipe <name>")]
    internal static string Recipe(string name) =>
        Names.Find(name) is { } named ? Data(named.Mesh).RecipeText.TrimEnd('\n') : Missing(name);

    [Command("model.undo", "Takes back a model's last operation: model.undo <name>")]
    internal static string Undo(string name)
    {
        if (Names.Find(name) is not { } named) return Missing(name);
        var mesh = Data(named.Mesh);
        return mesh.Undo() ? Counts(name, mesh) : Refuse("NOTHING_TO_UNDO", $"{name} has no operation to take back.");
    }

    [Command("model.redo", "Does again a model's operation last taken back: model.redo <name>")]
    internal static string Redo(string name)
    {
        if (Names.Find(name) is not { } named) return Missing(name);
        var mesh = Data(named.Mesh);
        return mesh.Redo() ? Counts(name, mesh) : Refuse("NOTHING_TO_REDO", $"{name} has no operation taken back to do again.");
    }

    [Command("model.save", "Writes a model's recipe to a file: model.save <name> <path>")]
    internal static string Save(string line)
    {
        var (name, path) = NameAndRest(line);
        if (path.Length == 0) return Refuse("BAD_ARGUMENT", "model.save takes a model's name and a path to write it to.");
        if (Names.Find(name) is not { } named) return Missing(name);

        try
        {
            return $"wrote {name} to {Data(named.Mesh).Save(Path.GetFullPath(path))}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Refuse("NOT_WRITTEN", $"{name} was not written to {path}. {error.Message}");
        }
    }

    [Command("model.load", "Makes a model from a recipe's file, in place of one of its name: model.load <name> <path>")]
    internal static string Load(string line)
    {
        var (name, path) = NameAndRest(line);
        if (!EditMeshNames.IsName(name) || path.Length == 0) return Refuse("BAD_ARGUMENT", "model.load takes a model's name of one word and the path of its file.");

        try
        {
            var mesh = EditMeshData.Load(Path.GetFullPath(path));
            Names.Name(name, EditMeshStore.Add(mesh), made: true);
            return Counts(name, mesh);
        }
        catch (FileNotFoundException)
        {
            return Refuse("NO_SUCH_FILE", $"There is no model at {path}.");
        }
        catch (Exception error) when (error is FormatException or IOException or UnauthorizedAccessException)
        {
            return Refuse("BAD_FILE", error.Message);
        }
    }

    [Command("model.export", "Writes a model as a glTF file another program opens: model.export <name> <path>")]
    internal static string Export(string line)
    {
        var (name, path) = NameAndRest(line);
        if (path.Length == 0) return Refuse("BAD_ARGUMENT", "model.export takes a model's name and a path to write it to.");
        if (Names.Find(name) is not { } named) return Missing(name);

        var full = Path.GetFullPath(path);
        return E3D.ExportEditMesh(named.Mesh, full) ? $"wrote {name} to {full}" : Refuse("NOT_WRITTEN", $"{name} was not written to {full}, and the log says why.");
    }

    [Command("model.show", "Draws a model where a point says, following its edits: model.show <name> [<x> <y> <z>]")]
    internal static string Show(string line)
    {
        var (name, rest) = NameAndRest(line);
        var numbers = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var at = Vector3.Zero;
        if (numbers.Length != 0 && (numbers.Length != 3 || !Coordinate(numbers[0], out at.X) || !Coordinate(numbers[1], out at.Y) || !Coordinate(numbers[2], out at.Z)))
            return Refuse("BAD_ARGUMENT", "model.show takes a model's name and where to draw it, three numbers, or none for the origin.");
        if (Names.Find(name) is not { } named) return Missing(name);

        Names.Show(ConsoleHost.Ecs, named, name, at);
        return string.Create(CultureInfo.InvariantCulture, $"showing {name} at ({at.X}, {at.Y}, {at.Z})");
    }

    // One command for each line of the recipe, its words after the model's name, so the catalog and
    // e3d list each name every operation with what it takes.
    [Command("model.box", "Adds a box: model.box <name> <x> <y> <z> [at <x> <y> <z>] [center]")]
    internal static string Box(string line) => Run("box", line, makes: true);

    [Command("model.plane", "Adds a plane facing up: model.plane <name> <width> <depth> [at <x> <y> <z>] [center]")]
    internal static string Plane(string line) => Run("plane", line, makes: true);

    [Command("model.cylinder", "Adds a cylinder: model.cylinder <name> <radius> <height> <sides> [at <x> <y> <z>] [center]")]
    internal static string Cylinder(string line) => Run("cylinder", line, makes: true);

    [Command("model.cone", "Adds a cone: model.cone <name> <radius> <height> <sides> [at <x> <y> <z>] [center]")]
    internal static string Cone(string line) => Run("cone", line, makes: true);

    [Command("model.wedge", "Adds a wedge rising along Z: model.wedge <name> <x> <y> <z> [at <x> <y> <z>] [center]")]
    internal static string Wedge(string line) => Run("wedge", line, makes: true);

    [Command("model.stairs", "Adds a flight of stairs: model.stairs <name> <steps> <rise> <run> <width> [at <x> <y> <z>] [center]")]
    internal static string Stairs(string line) => Run("stairs", line, makes: true);

    [Command("model.arch", "Adds an arch: model.arch <name> <radius> <thickness> <depth> <sides> [at <x> <y> <z>] [center]")]
    internal static string Arch(string line) => Run("arch", line, makes: true);

    [Command("model.room", "Adds a hollow room: model.room <name> <x> <y> <z> <thickness> [at <x> <y> <z>] [center]")]
    internal static string Room(string line) => Run("room", line, makes: true);

    [Command("model.ramp", "Adds a ramp rising along Z: model.ramp <name> <rise> <run> <width> [at <x> <y> <z>] [center]")]
    internal static string Ramp(string line) => Run("ramp", line, makes: true);

    [Command("model.select", "Selects: model.select <name> all|none|invert|[add|remove] face <i>...|+x..-z|box <x0 y0 z0 x1 y1 z1>|slot <name>")]
    internal static string Select(string line) => Run("select", line);

    [Command("model.move", "Moves the selection: model.move <name> <x> <y> <z>")]
    internal static string Move(string line) => Run("move", line);

    [Command("model.rotate", "Turns the selection: model.rotate <name> x|y|z|<ax ay az> <degrees> [at <x> <y> <z>]")]
    internal static string Rotate(string line) => Run("rotate", line);

    [Command("model.scale", "Scales the selection: model.scale <name> <factor>|<x> <y> <z> [at <x> <y> <z>]")]
    internal static string Scale(string line) => Run("scale", line);

    [Command("model.grid", "Sets the grid step: model.grid <name> <step>")]
    internal static string Grid(string line) => Run("grid", line);

    [Command("model.snap", "Puts the selection on the grid: model.snap <name>")]
    internal static string Snap(string line) => Run("snap", line);

    [Command("model.material", "Puts the selected faces in a slot: model.material <name> <slot>")]
    internal static string Material(string line) => Run("material", line);

    [Command("model.shade", "Draws the selected faces smoothed or flat: model.shade <name> smooth [degrees]|flat")]
    internal static string Shade(string line) => Run("shade", line);

    [Command("model.clear", "Empties a model: model.clear <name>")]
    internal static string Clear(string line) => Run("clear", line);

    [Command("model.extrude", "Pushes the selected faces out: model.extrude <name> <distance>|<x> <y> <z>")]
    internal static string Extrude(string line) => Run("extrude", line);

    [Command("model.inset", "Insets each selected face: model.inset <name> <distance>")]
    internal static string Inset(string line) => Run("inset", line);

    [Command("model.delete", "Takes away the selection: model.delete <name> [fill]")]
    internal static string Delete(string line) => Run("delete", line);

    [Command("model.merge", "Joins the selected vertices within a distance: model.merge <name> <distance>")]
    internal static string Merge(string line) => Run("merge", line);

    [Command("model.flip", "Turns the selected faces the other way: model.flip <name>")]
    internal static string Flip(string line) => Run("flip", line);

    [Command("model.triangulate", "Cuts the selected faces into triangles: model.triangulate <name>")]
    internal static string Triangulate(string line) => Run("triangulate", line);

    [Command("model.duplicate", "Copies the selected faces in place: model.duplicate <name>")]
    internal static string Duplicate(string line) => Run("duplicate", line);

    [Command("model.join", "Adds another model from its recipe's file: model.join <name> <path>")]
    internal static string Join(string line) => Run("join", line, makes: true);

    [Command("model.import", "Adds the meshes of a model file as faces: model.import <name> <path>")]
    internal static string Import(string line) => Run("import", line, makes: true);

    [Command("model.bevel", "Bevels the selected edges: model.bevel <name> <width> [segments]")]
    internal static string Bevel(string line) => Run("bevel", line);

    [Command("model.loopcut", "Cuts a ring of faces through the selected edge: model.loopcut <name> <fraction>")]
    internal static string LoopCut(string line) => Run("loopcut", line);

    [Command("model.subdivide", "Cuts each selected face into smaller ones: model.subdivide <name> [cuts]")]
    internal static string Subdivide(string line) => Run("subdivide", line);

    [Command("model.bridge", "Joins two selected loops of edges by a band of faces: model.bridge <name>")]
    internal static string Bridge(string line) => Run("bridge", line);

    [Command("model.fill", "Closes the selected loop of open edges with a face: model.fill <name>")]
    internal static string Fill(string line) => Run("fill", line);

    [Command("model.dissolve", "Joins the selected faces into one, or takes away the selected edges: model.dissolve <name> [edges]")]
    internal static string Dissolve(string line) => Run("dissolve", line);

    [Command("model.knife", "Cuts the selected faces along a plane: model.knife <name> x|y|z|<nx ny nz> [at <x> <y> <z>]")]
    internal static string Knife(string line) => Run("knife", line);

    [Command("model.mirror", "Adds the selection mirrored across a plane: model.mirror <name> x|y|z|<nx ny nz> [at <x> <y> <z>]")]
    internal static string Mirror(string line) => Run("mirror", line);

    [Command("model.spin", "Sweeps the selection round an axis: model.spin <name> x|y|z|<ax ay az> <degrees> <steps> [at <x> <y> <z>]")]
    internal static string Spin(string line) => Run("spin", line);

    [Command("model.smooth", "Moves the selected vertices toward their neighbors: model.smooth <name> <factor>")]
    internal static string Smooth(string line) => Run("smooth", line);

    [Command("model.modifier", "Changes the modifier stack: model.modifier <name> mirror <axes> [at <x> <y> <z>]|array <count> <x> <y> <z>|array curve <x y z>...|bevel <width> [segments] [angle]|subdivision <level>|solidify <thickness>|triangulate|move <from> <to>|remove <i>|apply <i>")]
    internal static string Modifier(string line) => Run("modifier", line);

    [Command("model.union", "Adds another solid: model.union <name> <path>|<a solid primitive's line> [at <x> <y> <z>]")]
    internal static string Union(string line) => Run("union", line, makes: true);

    [Command("model.subtract", "Takes another solid away: model.subtract <name> <path>|<a solid primitive's line> [at <x> <y> <z>]")]
    internal static string Subtract(string line) => Run("subtract", line);

    [Command("model.intersect", "Keeps what both solids hold: model.intersect <name> <path>|<a solid primitive's line> [at <x> <y> <z>]")]
    internal static string Intersect(string line) => Run("intersect", line);

    [Command("model.cut", "Cuts: model.cut <name> window <width> <height> <along> <sill>|door <width> <height> <along>|<a solid primitive's line>")]
    internal static string Cut(string line) => Run("cut", line);

    [Command("model.epsilon", "Sets how near a point is to a plane to lie in it: model.epsilon <name> <distance>")]
    internal static string Epsilon(string line) => Run("epsilon", line);

    // A density typed with one number takes the width of the texture of the first selected face's
    // slot, which the line run is given, so the recipe makes the same coordinates without it.
    [Command("model.uv", "Shapes the selected faces' texture coordinates: model.uv <name> planar +x..-z|<x y z>|box|cylinder x|y|z|<x y z> [at <x> <y> <z>]|sphere [at <x> <y> <z>]|move <u> <v>|scale <s> [<t>] [each]|rotate <degrees> [each]|density <pixels a meter> [<texture width>]|reset")]
    internal static string Uv(string line)
    {
        var (name, rest) = NameAndRest(line);
        var words = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words is ["density", var density] && Names.Find(name) is { } named)
        {
            if (Data(named.Mesh).SelectedTextureWidth() is not { } width)
                return Refuse("BAD_ARGUMENT", $"uv density on {name}: the first selected face's slot has no texture to read the width of, so the width is given after the density.");
            return Run("uv", $"{name} density {density} {width}");
        }
        return Run("uv", line);
    }

    [Command("model.slot", "Changes a material slot: model.slot <name> add|remove <slot>|rename <slot> <new>|color <slot> <r> <g> <b> [a]|texture|normal-map|occlusion-map|metallic-roughness-map|emissive-map <slot> <path>|none|emissive <slot> <r> <g> <b>|metallic|roughness <slot> <value>")]
    internal static string Slot(string line) => Run("slot", line);

    private static EditMeshNames Names => EditMeshNames.Of(ConsoleHost.World!);

    /// <summary>Runs an operation's line on a model, made first where the operation makes one, and answers its counts.</summary>
    private static string Run(string verb, string line, bool makes = false)
    {
        var (name, rest) = NameAndRest(line);
        if (!EditMeshNames.IsName(name)) return Refuse("BAD_ARGUMENT", $"model.{verb} takes a model's name first.");

        EditMeshData mesh;
        if (Names.Find(name) is { } named) mesh = Data(named.Mesh);
        else if (makes) Names.Name(name, EditMeshStore.Add(mesh = new EditMeshData()), made: true);
        else return Missing(name);

        try
        {
            mesh.Run(rest.Length == 0 ? verb : $"{verb} {rest}");
        }
        catch (Exception error) when (error is FormatException or ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return Refuse("BAD_ARGUMENT", $"{verb} on {name}: {error.Message}");
        }

        return Counts(name, mesh);
    }

    private static EditMeshData Data(EditMesh mesh) => EditMeshStore.TryGet(mesh, out var data) ? data : new EditMeshData();

    /// <summary>What a model holds and what of it is selected, the answer of every operation.</summary>
    private static string Counts(string name, EditMeshData mesh) =>
        $"{name}: {mesh.VertexCount} vertices, {mesh.EdgeCount} edges, {mesh.FaceCount} faces, "
        + $"{mesh.SelectedFaceCount} faces and {mesh.SelectedVertexCount} vertices selected";

    private static (string Name, string Words) NameAndRest(string line)
    {
        var trimmed = line.Trim();
        var space = trimmed.IndexOfAny([' ', '\t']);
        return space < 0 ? (trimmed, "") : (trimmed[..space], trimmed[(space + 1)..].Trim());
    }

    private static bool Coordinate(string word, out float value) =>
        float.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);

    private static string Missing(string name) => Refuse("NO_SUCH_MODEL", $"There is no model called {name}. model.list says which there are.");

    private static string Refuse(string code, string message)
    {
        ConsoleHost.Fail(code, message);
        return message;
    }
}
