using System.Numerics;

namespace Engine;

public static partial class E3D
{
    // -- Edit meshes, shaped by operations on a selection. Each operation acts on the faces and
    // vertices selected and writes its line to the mesh's recipe.

    /// <summary>Makes an empty edit mesh, which primitives and operations then shape.</summary>
    /// <remarks>
    /// An edit mesh is data on the CPU, so it is made and shaped before <see cref="InitWindow"/> as
    /// well as after it. It is freed with <see cref="UnloadEditMesh"/>.
    /// </remarks>
    public static EditMesh LoadEditMesh() => EditMeshStore.Add(new EditMeshData());

    /// <summary>Frees an edit mesh. A model built from it stays as it was built, and is the program's to free.</summary>
    public static void UnloadEditMesh(EditMesh mesh) => EditMeshStore.Remove(mesh);

    /// <summary>Makes an edit mesh from a recipe's file, a <c>.model</c> file of one operation a line, found beside the program or in the working directory.</summary>
    /// <remarks>
    /// A saved model is its recipe, the lines that made it starting with <c>model 1</c>, so a level's
    /// models are short files a reader follows and a diff reads, and the mesh is made again from the
    /// lines as it loads, its undo going back through them. A file that is missing, does not start
    /// with <c>model 1</c> or holds a line that is not an operation gives an edit mesh that is not
    /// loaded, with a warning naming the file and the line.
    /// </remarks>
    public static EditMesh LoadEditMesh(string fileName)
    {
        try
        {
            return EditMeshStore.Add(EditMeshData.Load(fileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            ApiLogger.Warn($"LoadEditMesh: '{fileName}' could not be read as a model's recipe. {ex.Message}");
            return default;
        }
    }

    /// <summary>Makes an edit mesh from a recipe's text, one operation a line, its <c>model 1</c> header first or left out.</summary>
    /// <remarks>Blank lines and comments after <c>#</c> are skipped.</remarks>
    /// <exception cref="FormatException">A line is not an operation or fails, the message naming the line.</exception>
    public static EditMesh LoadEditMeshFromMemory(string recipe) => EditMeshStore.Add(EditMeshData.FromRecipe(recipe));

    /// <summary>Writes an edit mesh's recipe to a <c>.model</c> file, beside the program where the name is relative, and says whether it was written.</summary>
    /// <remarks>
    /// The file is written under a name of its own and moved into place, so a program reading it
    /// while it is written reads the old file or the new one whole. <see cref="LoadEditMesh(string)"/>
    /// makes the mesh again from it.
    /// </remarks>
    public static bool SaveEditMesh(EditMesh mesh, string fileName)
    {
        if (Edited(mesh, nameof(SaveEditMesh)) is not { } data) return false;
        try
        {
            data.Save(fileName);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"SaveEditMesh: '{fileName}' could not be written. {ex.Message}");
            return false;
        }
    }

    /// <summary>Whether <paramref name="mesh"/> names an edit mesh that is loaded.</summary>
    public static bool IsEditMeshValid(EditMesh mesh) => EditMeshStore.TryGet(mesh, out _);

    /// <summary>The mesh's recipe as the text of its file, its <c>model 1</c> header and then the operations that made it, a line each.</summary>
    public static string GetEditMeshRecipe(EditMesh mesh) =>
        Edited(mesh, nameof(GetEditMeshRecipe)) is { } data ? data.RecipeText : "";

    /// <summary>Runs one line of a recipe on the mesh, as though its operation were called, as <c>box 8 3 6</c> or <c>select +y</c>.</summary>
    /// <remarks>
    /// A line written is the line the operation writes, so a line a program reads from a player or a
    /// file is the recipe's own. A comment after <c>#</c> is left out.
    /// </remarks>
    /// <exception cref="FormatException">The line is not an operation, or its measures are not the operation's.</exception>
    /// <exception cref="ArgumentException">A measure is out of the operation's range.</exception>
    public static void EditMeshRun(EditMesh mesh, string line) => Edited(mesh, nameof(EditMeshRun))?.Run(line);

    /// <summary>Takes back the last operation of the mesh's recipe and says whether there was one.</summary>
    /// <remarks>
    /// The mesh is made again from the lines before the last, so an undo is right for every
    /// operation, and the selection is the one those lines left.
    /// </remarks>
    public static bool EditMeshUndo(EditMesh mesh) => Edited(mesh, nameof(EditMeshUndo))?.Undo() ?? false;

    /// <summary>Does again the operation last taken back and says whether there was one.</summary>
    /// <remarks>An operation done after an undo forgets what was taken back.</remarks>
    public static bool EditMeshRedo(EditMesh mesh) => Edited(mesh, nameof(EditMeshRedo))?.Redo() ?? false;

    /// <summary>The mesh's counts of vertices, edges and faces, its open edges, its selection, its volume, its bounds and its grid step.</summary>
    /// <remarks>An edit mesh that is not loaded gives zeros.</remarks>
    public static EditMeshInfo GetEditMeshInfo(EditMesh mesh)
    {
        if (!EditMeshStore.TryGet(mesh, out var data)) return default;
        return new EditMeshInfo(data.VertexCount, data.EdgeCount, data.FaceCount, data.OpenEdges, data.SelectedVertexCount,
            data.SelectedEdgeCount, data.SelectedFaceCount, data.Volume, data.Bounds, data.GridStep);
    }

    /// <summary>Where a vertex of the mesh is, by its index from zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The mesh has no such vertex.</exception>
    public static Vector3 GetEditMeshVertex(EditMesh mesh, int vertex) =>
        Edited(mesh, nameof(GetEditMeshVertex)) is { } data ? data.Position(vertex) : default;

    /// <summary>A face's vertices, by the face's index from zero, in order round it, counterclockwise seen from the side it faces.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The mesh has no such face.</exception>
    public static int[] GetEditMeshFace(EditMesh mesh, int face) =>
        Edited(mesh, nameof(GetEditMeshFace)) is { } data ? data.Corners(face).ToArray() : [];

    /// <summary>The way a face faces, a length of one, or zero for a face with no area.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The mesh has no such face.</exception>
    public static Vector3 GetEditMeshFaceNormal(EditMesh mesh, int face) =>
        Edited(mesh, nameof(GetEditMeshFaceNormal)) is { } data ? data.Normal(face) : default;

    /// <summary>The name of the material slot a face is in.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The mesh has no such face.</exception>
    public static string GetEditMeshFaceSlot(EditMesh mesh, int face) =>
        Edited(mesh, nameof(GetEditMeshFaceSlot)) is { } data ? data.SlotOf(face) : "";

    /// <summary>The names of the mesh's material slots, the first of which, <c>default</c>, every new face takes.</summary>
    /// <remarks>A model built from the mesh has one mesh and one material a slot, in this order.</remarks>
    public static string[] GetEditMeshSlots(EditMesh mesh) =>
        Edited(mesh, nameof(GetEditMeshSlots)) is { } data ? [.. data.Slots] : [];

    /// <summary>Whether a face of the mesh is selected.</summary>
    public static bool IsEditMeshFaceSelected(EditMesh mesh, int face) =>
        EditMeshStore.TryGet(mesh, out var data) && data.IsFaceSelected(face);

    /// <summary>Whether a vertex of the mesh is selected.</summary>
    public static bool IsEditMeshVertexSelected(EditMesh mesh, int vertex) =>
        EditMeshStore.TryGet(mesh, out var data) && data.IsVertexSelected(vertex);

    // -- Primitives, each added to what the mesh holds and selected, so the operation after it acts
    // on it, placed by its lowest corner at at or, where centered is set, by its middle.

    /// <summary>Adds a box as wide along X, tall along Y and deep along Z as <paramref name="size"/>, eight vertices and six faces.</summary>
    /// <remarks>The recipe's line is <c>box X Y Z</c>, ending in <c>at X Y Z</c> where it is placed and <c>center</c> where it is centered there.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero.</exception>
    public static void EditMeshBox(EditMesh mesh, Vector3 size, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshBox))?.Box(size, at, centered);

    /// <summary>Adds a flat rectangle facing up, as wide along X and deep along Z as given, one face whose four edges are open.</summary>
    /// <remarks>The recipe's line is <c>plane WIDTH DEPTH</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero.</exception>
    public static void EditMeshPlane(EditMesh mesh, float width, float depth, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshPlane))?.Plane(width, depth, at, centered);

    /// <summary>Adds a cylinder standing along Y, its round side made of flat sides.</summary>
    /// <remarks>
    /// Its corners stand half a side round from the axes, so a cylinder of four sides is a box whose
    /// sides face along X and Z. The recipe's line is <c>cylinder RADIUS HEIGHT SIDES</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero, or there are fewer than three sides.</exception>
    public static void EditMeshCylinder(EditMesh mesh, float radius, float height, int sides, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshCylinder))?.Cylinder(radius, height, sides, at, centered);

    /// <summary>Adds a cone standing along Y on a base of flat sides, its point over the middle of the base.</summary>
    /// <remarks>The recipe's line is <c>cone RADIUS HEIGHT SIDES</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero, or there are fewer than three sides.</exception>
    public static void EditMeshCone(EditMesh mesh, float radius, float height, int sides, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshCone))?.Cone(radius, height, sides, at, centered);

    /// <summary>Adds a wedge, a box cut from one bottom edge to the opposite top edge, rising along Z from nothing to its full height.</summary>
    /// <remarks>A wedge laid over a flight of stairs makes their ramp. The recipe's line is <c>wedge X Y Z</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero.</exception>
    public static void EditMeshWedge(EditMesh mesh, Vector3 size, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshWedge))?.Wedge(size, at, centered);

    /// <summary>Adds a flight of stairs rising along Z, each step <paramref name="rise"/> high and <paramref name="run"/> deep, closed underneath.</summary>
    /// <remarks>
    /// A back face as tall as the flight and a floor under it close it, so it stands on the ground
    /// and against a wall. Its volume is the sum of its steps. The recipe's line is
    /// <c>stairs STEPS RISE RUN WIDTH</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero, or there is no step.</exception>
    public static void EditMeshStairs(EditMesh mesh, int steps, float rise, float run, float width, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshStairs))?.Stairs(steps, rise, run, width, at, centered);

    /// <summary>Adds an arch, half a ring standing on its two feet with its opening along Z, its curve made of flat sides.</summary>
    /// <remarks>
    /// Twice the radius wide and the radius tall, so it stands over a doorway as wide as its inside.
    /// The recipe's line is <c>arch RADIUS THICKNESS DEPTH SIDES</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero, the thickness is not under the radius, or there are fewer than two sides.</exception>
    public static void EditMeshArch(EditMesh mesh, float radius, float thickness, float depth, int sides, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshArch))?.Arch(radius, thickness, depth, sides, at, centered);

    /// <summary>Takes away every vertex, face and material slot but the first, and the selection with them.</summary>
    /// <remarks>The recipe's line is <c>clear</c>.</remarks>
    public static void EditMeshClear(EditMesh mesh) => Edited(mesh, nameof(EditMeshClear))?.Clear();

    // -- The selection. Selecting a face selects its corners, and an edge is selected where both its
    // ends are.

    /// <summary>Selects every vertex and every face.</summary>
    /// <remarks>The recipe's line is <c>select all</c>.</remarks>
    public static void EditMeshSelectAll(EditMesh mesh) => Edited(mesh, nameof(EditMeshSelectAll))?.SelectAll();

    /// <summary>Selects nothing.</summary>
    /// <remarks>The recipe's line is <c>select none</c>.</remarks>
    public static void EditMeshSelectNone(EditMesh mesh) => Edited(mesh, nameof(EditMeshSelectNone))?.SelectNone();

    /// <summary>Selects what was not selected and leaves what was, faces with their corners.</summary>
    /// <remarks>The recipe's line is <c>select invert</c>.</remarks>
    public static void EditMeshSelectInvert(EditMesh mesh) => Edited(mesh, nameof(EditMeshSelectInvert))?.SelectInvert();

    /// <summary>Selects faces by their indices from zero.</summary>
    /// <remarks>The recipe's line is <c>select face 0 3</c>, with <c>add</c> or <c>remove</c> after <c>select</c> for the other modes.</remarks>
    /// <exception cref="ArgumentException">No face is given, or the mesh has no such face.</exception>
    public static void EditMeshSelectFaces(EditMesh mesh, ReadOnlySpan<int> faces, SelectionMode mode = SelectionMode.Replace) =>
        Edited(mesh, nameof(EditMeshSelectFaces))?.SelectFaces(faces, mode);

    /// <summary>Selects the faces whose normals are nearest one way along an axis, so <c>Vector3.UnitY</c> selects the floors.</summary>
    /// <remarks>
    /// Every face is a floor, a ceiling or one of four walls by the direction along an axis its
    /// normal is nearest, and a slope rising exactly as far as it runs is a floor. The recipe's line
    /// is <c>select +y</c>, or <c>-x</c> and the rest.
    /// </remarks>
    /// <exception cref="ArgumentException">The direction is zero.</exception>
    public static void EditMeshSelectFacing(EditMesh mesh, Vector3 direction, SelectionMode mode = SelectionMode.Replace) =>
        Edited(mesh, nameof(EditMeshSelectFacing))?.SelectFacing(direction, mode);

    /// <summary>Selects the vertices inside a box given by two opposite corners, and the faces all of whose corners are inside it.</summary>
    /// <remarks>
    /// A point on the box's side is inside it, so a box drawn along a grid line picks the vertices on
    /// it. The recipe's line is <c>select box X Y Z X Y Z</c>.
    /// </remarks>
    public static void EditMeshSelectInside(EditMesh mesh, Vector3 corner, Vector3 opposite, SelectionMode mode = SelectionMode.Replace) =>
        Edited(mesh, nameof(EditMeshSelectInside))?.SelectInside(corner, opposite, mode);

    /// <summary>Selects the faces in a material slot, a name no slot has selecting none.</summary>
    /// <remarks>The recipe's line is <c>select slot NAME</c>.</remarks>
    /// <exception cref="ArgumentException">The name is not one word.</exception>
    public static void EditMeshSelectSlot(EditMesh mesh, string slot, SelectionMode mode = SelectionMode.Replace) =>
        Edited(mesh, nameof(EditMeshSelectSlot))?.SelectSlot(slot, mode);

    // -- Moving, turning and scaling the selection, its grid, and the slot and smoothing of its faces

    /// <summary>Moves the selected vertices by a distance along each axis, in meters.</summary>
    /// <remarks>A measure is taken as given, off the grid where it is off it. The recipe's line is <c>move X Y Z</c>.</remarks>
    public static void EditMeshMove(EditMesh mesh, Vector3 by) => Edited(mesh, nameof(EditMeshMove))?.Move(by);

    /// <summary>Turns the selected vertices about an axis by a number of degrees, through a pivot or the middle of the box holding them.</summary>
    /// <remarks>
    /// Counter-clockwise looking back along the axis, as every turn the engine makes is. The middle
    /// of the box holding the selection, where no pivot is given, keeps a part turned a quarter in
    /// place on the grid it stood on. The recipe's line is <c>rotate y 90</c>, or the axis's three
    /// numbers, ending in <c>at X Y Z</c> where a pivot is given.
    /// </remarks>
    /// <exception cref="ArgumentException">The axis is zero.</exception>
    public static void EditMeshRotate(EditMesh mesh, Vector3 axis, float degrees, Vector3? pivot = null) =>
        Edited(mesh, nameof(EditMeshRotate))?.Rotate(axis, degrees, pivot);

    /// <summary>Scales the selected vertices by a factor along each axis, about a pivot or the middle of the box holding them.</summary>
    /// <remarks>
    /// A negative factor mirrors the selection, its faces wound the other way so they still face
    /// out. The recipe's line is <c>scale 2</c> or <c>scale 1 2 1</c>, ending in <c>at X Y Z</c> where
    /// a pivot is given.
    /// </remarks>
    public static void EditMeshScale(EditMesh mesh, Vector3 factor, Vector3? pivot = null) =>
        Edited(mesh, nameof(EditMeshScale))?.Scale(factor, pivot);

    /// <summary>Sets the mesh's grid step, a meter or a power of two of it from a sixteenth to sixty-four, which <see cref="EditMeshSnap"/> rounds to.</summary>
    /// <remarks>The recipe's line is <c>grid STEP</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The step is not a power of two from a sixteenth to sixty-four.</exception>
    public static void EditMeshGrid(EditMesh mesh, float step) => Edited(mesh, nameof(EditMeshGrid))?.Grid(step);

    /// <summary>Puts each selected vertex on the nearest point of the mesh's grid.</summary>
    /// <remarks>The recipe's line is <c>snap</c>.</remarks>
    public static void EditMeshSnap(EditMesh mesh) => Edited(mesh, nameof(EditMeshSnap))?.Snap();

    /// <summary>Puts the selected faces in a material slot of that name, made where the mesh has none.</summary>
    /// <remarks>
    /// A slot is a name, so faces are sorted into floors, walls and trims before what each is drawn
    /// with is decided, and a model built from the mesh draws each slot's faces with its material.
    /// The recipe's line is <c>material NAME</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">The name is not one word.</exception>
    public static void EditMeshMaterial(EditMesh mesh, string slot) => Edited(mesh, nameof(EditMeshMaterial))?.Material(slot);

    /// <summary>
    /// Smooths the selected faces into one another where two meet at under an angle in degrees, or
    /// draws them flat, so a cylinder's sides are round and its ends stay flat.
    /// </summary>
    /// <remarks>The recipe's line is <c>shade smooth 30</c> or <c>shade flat</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The angle is not above zero and up to 180 degrees.</exception>
    public static void EditMeshShade(EditMesh mesh, bool smooth, float degrees = EditMeshData.DefaultSmoothAngle) =>
        Edited(mesh, nameof(EditMeshShade))?.Shade(smooth, degrees);

    // -- Operations that change which vertices and faces there are

    /// <summary>Pushes the selected faces out along their normals by a distance, filling the sides, the faces moved staying selected.</summary>
    /// <remarks>
    /// The faces move together as one region, so two side by side stay joined, and a corner where
    /// they meet at an angle moves so each goes the whole distance along its own normal. A negative
    /// distance pushes them in. The recipe's line is <c>extrude 1</c>.
    /// </remarks>
    public static void EditMeshExtrude(EditMesh mesh, float distance) => Edited(mesh, nameof(EditMeshExtrude))?.Extrude(distance);

    /// <summary>Pushes the selected faces out by one vector, filling the sides, as a wall is pushed up from a floor's edge.</summary>
    /// <remarks>The recipe's line is <c>extrude 0 1 0</c>.</remarks>
    public static void EditMeshExtrude(EditMesh mesh, Vector3 by) => Edited(mesh, nameof(EditMeshExtrude))?.Extrude(by);

    /// <summary>Makes each selected face a smaller face inside it by a distance, with a band of faces round it, the smaller faces selected after.</summary>
    /// <remarks>An extrude after an inset pulls out a panel or pushes in a recess. The recipe's line is <c>inset 0.25</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The distance is not above zero.</exception>
    public static void EditMeshInset(EditMesh mesh, float distance) => Edited(mesh, nameof(EditMeshInset))?.Inset(distance);

    /// <summary>Takes away the selected faces, or where none is selected the selected vertices and every face using them.</summary>
    /// <remarks>
    /// Where <paramref name="fill"/> is set, each hole left is closed with one face, which is selected
    /// after. A vertex no face uses is taken away too. The recipe's line is <c>delete</c> or
    /// <c>delete fill</c>.
    /// </remarks>
    public static void EditMeshDelete(EditMesh mesh, bool fill = false) => Edited(mesh, nameof(EditMeshDelete))?.Delete(fill);

    /// <summary>Joins the selected vertices nearer one another than a distance into one, faces left with fewer than three corners taken away.</summary>
    /// <remarks>The recipe's line is <c>merge 0.001</c>.</remarks>
    public static void EditMeshMerge(EditMesh mesh, float distance) => Edited(mesh, nameof(EditMeshMerge))?.Merge(distance);

    /// <summary>Turns the selected faces to face the other way.</summary>
    /// <remarks>The recipe's line is <c>flip</c>.</remarks>
    public static void EditMeshFlip(EditMesh mesh) => Edited(mesh, nameof(EditMeshFlip))?.Flip();

    /// <summary>Cuts each selected face of more than three corners into triangles, a concave face inside its outline, the triangles selected after.</summary>
    /// <remarks>The recipe's line is <c>triangulate</c>.</remarks>
    public static void EditMeshTriangulate(EditMesh mesh) => Edited(mesh, nameof(EditMeshTriangulate))?.Triangulate();

    /// <summary>Copies the selected faces where they are, with vertices of their own, and selects the copy.</summary>
    /// <remarks>The recipe's line is <c>duplicate</c>.</remarks>
    public static void EditMeshDuplicate(EditMesh mesh) => Edited(mesh, nameof(EditMeshDuplicate))?.Duplicate();

    /// <summary>Adds another model, made from its recipe's file, to the mesh and selects it, its faces keeping their slots by name.</summary>
    /// <remarks>
    /// The recipe keeps the file's name rather than what it held, so a change to the joined model
    /// reaches every model that joins it the next time each is made. The recipe's line is
    /// <c>join models/pillar.model</c>.
    /// </remarks>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="FormatException">The file is not a model's recipe, or a model joins itself.</exception>
    public static void EditMeshJoin(EditMesh mesh, string fileName)
    {
        if (Edited(mesh, nameof(EditMeshJoin)) is not { } data) return;
        try
        {
            data.Join(fileName);
        }
        catch (InvalidOperationException ex)
        {
            throw new FormatException(ex.Message, ex);
        }
    }

    // -- Bevels and cuts

    /// <summary>Cuts each selected edge back by a width into a band of faces, round across in a number of segments, and closes each corner with one face.</summary>
    /// <remarks>
    /// <para>
    /// An edge is beveled where both its ends are selected and two faces run along it, so selecting
    /// all of a box bevels its twelve edges. The band follows the arc tangent to both faces at the
    /// width from the edge, a quarter circle across a box's edge, and one segment is a flat chamfer.
    /// Where three or more beveled edges meet, as at a box's corner, the corner is closed with one
    /// face. The bands and the corners are selected after.
    /// </para>
    /// <para>The recipe's line is <c>bevel 0.1 3</c>, the width and the segments, one where it says none.</para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The width is not above zero, or the segments are not from 1 to 64.</exception>
    public static void EditMeshBevel(EditMesh mesh, float width, int segments = 1) => Edited(mesh, nameof(EditMeshBevel))?.Bevel(width, segments);

    /// <summary>Cuts round the ring of four-cornered faces each selected edge crosses, at a fraction along the edges the ring crosses.</summary>
    /// <remarks>
    /// <para>
    /// The ring runs from the selected edge through the faces beside it to their opposite edges and
    /// on, both ways, until it comes round again or meets a face that has not four corners, so a
    /// box's upright edge cut at a half makes a loop round its middle. The fraction is measured from
    /// the selected edge's lower-numbered end. The new loop's vertices are selected after.
    /// </para>
    /// <para>The recipe's line is <c>loopcut 0.5</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The fraction is not between zero and one.</exception>
    public static void EditMeshLoopCut(EditMesh mesh, float fraction = 0.5f) => Edited(mesh, nameof(EditMeshLoopCut))?.LoopCut(fraction);

    /// <summary>Cuts each selected face into a grid, each of its edges into one more part than the count of cuts.</summary>
    /// <remarks>
    /// A face of four corners becomes a grid of them and a triangle a grid of triangles, and a face
    /// of more corners a fan of triangles from its middle. A face beside one subdivided takes the new
    /// vertices on the edge they share, so the mesh stays closed. The grid's faces are selected
    /// after. The recipe's line is <c>subdivide 2</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The count is not from 1 to 64.</exception>
    public static void EditMeshSubdivide(EditMesh mesh, int cuts = 1) => Edited(mesh, nameof(EditMeshSubdivide))?.Subdivide(cuts);

    // -- Joining faces, closing holes, mirroring, sweeping and smoothing

    /// <summary>Joins the loops round two regions of selected faces with a band of four-cornered faces, taking the selected faces away.</summary>
    /// <remarks>
    /// Each region has one loop round it of the same count of vertices, as two faces facing each
    /// other across a gap have, and the band runs straight across between them. The band is
    /// selected after. The recipe's line is <c>bridge</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The selected faces are not two regions with loops of the same count.</exception>
    public static void EditMeshBridge(EditMesh mesh) => Edited(mesh, nameof(EditMeshBridge))?.Bridge();

    /// <summary>Closes each hole whose edges all have both ends selected with one face, facing the way the faces round it face.</summary>
    /// <remarks>The recipe's line is <c>fill</c>.</remarks>
    public static void EditMeshFill(EditMesh mesh) => Edited(mesh, nameof(EditMeshFill))?.Fill();

    /// <summary>Joins each region of selected faces into one face round it, the edges and vertices inside it taken away and its shape kept.</summary>
    /// <remarks>A region with a hole in it is left as it was. The recipe's line is <c>dissolve</c>.</remarks>
    public static void EditMeshDissolve(EditMesh mesh) => Edited(mesh, nameof(EditMeshDissolve))?.Dissolve();

    /// <summary>Takes away each selected edge that two faces run along, joining the faces on either side into one, its shape kept.</summary>
    /// <remarks>The recipe's line is <c>dissolve edges</c>.</remarks>
    public static void EditMeshDissolveEdges(EditMesh mesh) => Edited(mesh, nameof(EditMeshDissolveEdges))?.DissolveEdges();

    /// <summary>Cuts the selected faces along the plane through <paramref name="point"/> square to <paramref name="normal"/>, each face it crosses split in two.</summary>
    /// <remarks>
    /// The faces beside a cut edge take its new vertex, so the mesh stays closed, and the vertices
    /// along the cut are selected after. The recipe's line is <c>knife y at 0 1.5 0</c>, or the
    /// normal's three numbers, the point the origin where it says none.
    /// </remarks>
    /// <exception cref="ArgumentException">The normal is zero.</exception>
    public static void EditMeshKnife(EditMesh mesh, Vector3 normal, Vector3 point = default) => Edited(mesh, nameof(EditMeshKnife))?.Knife(normal, point);

    /// <summary>Copies the selected faces mirrored across the plane through a pivot square to an axis, the vertices on the plane shared, and selects the copy.</summary>
    /// <remarks>
    /// A half whose open side lies on the plane mirrored makes a whole with no vertex twice, and a
    /// face lying in the plane is not copied. The recipe's line is <c>mirror x</c>, ending in
    /// <c>at X Y Z</c> where the plane does not pass through the origin.
    /// </remarks>
    /// <exception cref="ArgumentException">The axis is zero.</exception>
    public static void EditMeshMirror(EditMesh mesh, Vector3 axis, Vector3 pivot = default) => Edited(mesh, nameof(EditMeshMirror))?.Mirror(axis, pivot);

    /// <summary>Sweeps the selected faces, or the selected edges where no face is selected, round an axis through a point by an angle in a number of steps.</summary>
    /// <remarks>
    /// The faces turn as an extrude moves them, a band of faces swept each step, and a whole turn
    /// joins the sweep's end to its start, so a face beside the axis spun round is a closed ring.
    /// Edges alone each sweep a band, as a lathe turns a profile. The recipe's line is
    /// <c>spin y 360 16</c>, ending in <c>at X Y Z</c> where the axis does not pass through the origin.
    /// </remarks>
    /// <exception cref="ArgumentException">The axis is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The angle is zero or past a whole turn, or the steps are not from 1 to 256.</exception>
    public static void EditMeshSpin(EditMesh mesh, Vector3 axis, float degrees, int steps, Vector3 center = default) =>
        Edited(mesh, nameof(EditMeshSpin))?.Spin(axis, degrees, steps, center);

    /// <summary>Moves each selected vertex toward the mean of the vertices it shares an edge with, by a factor from zero to one.</summary>
    /// <remarks>The recipe's line is <c>smooth 0.5</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The factor is not from zero to one.</exception>
    public static void EditMeshSmooth(EditMesh mesh, float factor = 0.5f) => Edited(mesh, nameof(EditMeshSmooth))?.Smooth(factor);

    // -- Building a model to draw and collide with

    /// <summary>Builds the mesh into a model, one mesh and one white material for each material slot, in the order of the slots.</summary>
    /// <remarks>
    /// <para>
    /// Each face is cut into triangles by ear clipping, so a concave face is cut inside its outline,
    /// and has corners of its own, so its normal and texture coordinates are its own. A smoothed
    /// face's corners take the mean of the smoothed faces' normals round them within its angle.
    /// Texture coordinates are each corner's place in meters seen along the axis the face's normal
    /// is nearest, so a texture of a meter's grid shows a meter on every face.
    /// </para>
    /// <para>
    /// <c>CreatePhysicsStaticModel(model, position)</c> makes the collider from the built model. A
    /// slot no face is in is a mesh of one triangle with no area, which draws nothing and which the
    /// collider leaves out. <see cref="UpdateModelFromEditMesh"/> writes the meshes again after the
    /// mesh changes.
    /// </para>
    /// </remarks>
    public static Model LoadModelFromEditMesh(EditMesh mesh)
    {
        if (Edited(mesh, nameof(LoadModelFromEditMesh)) is not { } data) return new Model();
        var built = EditMeshBuild.Slots(data);
        return new Model
        {
            Meshes = built.Select(slot => UploadMesh(slot.Vertices, slot.Indices)).ToArray(),
            Materials = built.Select(_ => new ModelMaterial(Color.White)).ToArray(),
            MeshMaterial = Enumerable.Range(0, built.Length).ToArray(),
        };
    }

    /// <summary>Writes a model built by <see cref="LoadModelFromEditMesh"/> again from the mesh, its meshes in place, after the mesh changed.</summary>
    /// <remarks>
    /// Each slot's mesh keeps its id and takes the new shape, so whatever draws it draws the change,
    /// and the materials the program gave the model stay with their slots. A slot added since gets a
    /// mesh and a white material of its own, and the mesh of a slot taken away, as by
    /// <see cref="EditMeshClear"/>, is freed. A body made from the model stays as it was made, so a
    /// collider follows the change by being made again.
    /// </remarks>
    public static void UpdateModelFromEditMesh(Model model, EditMesh mesh)
    {
        if (Edited(mesh, nameof(UpdateModelFromEditMesh)) is not { } data) return;
        var built = EditMeshBuild.Slots(data);
        var meshes = new ModelMesh[built.Length];
        var materials = model.Materials.ToList();
        var meshMaterial = new int[built.Length];
        for (int slot = 0; slot < built.Length; slot++)
        {
            var (vertices, indices) = built[slot];
            var bounds = BoundingBox.Around(vertices.Select(vertex => vertex.Position).ToArray());
            meshes[slot] = slot < model.Meshes.Length && Meshes.Replace(model.Meshes[slot].Id, vertices, indices)
                ? new ModelMesh(model.Meshes[slot].Id, vertices.Length, indices.Length / 3, bounds)
                : UploadMesh(vertices, indices);

            if (slot < model.MeshMaterial.Length && model.MeshMaterial[slot] < materials.Count) meshMaterial[slot] = model.MeshMaterial[slot];
            else
            {
                materials.Add(new ModelMaterial(Color.White));
                meshMaterial[slot] = materials.Count - 1;
            }
        }
        for (int slot = built.Length; slot < model.Meshes.Length; slot++) UnloadMesh(model.Meshes[slot]);
        model.Replace(meshes, [.. materials], meshMaterial);
    }

    // The edit mesh a call works on, or null with a warning naming the call where it is not loaded,
    // so an operation on a freed mesh is found in the log and the frame goes on.
    private static EditMeshData? Edited(EditMesh mesh, string call)
    {
        if (EditMeshStore.TryGet(mesh, out var data)) return data;
        ApiLogger.Warn($"{call}: the edit mesh is not loaded.");
        return null;
    }
}
