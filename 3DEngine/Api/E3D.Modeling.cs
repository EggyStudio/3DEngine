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

    /// <summary>Whether <paramref name="mesh"/> names an edit mesh that is loaded.</summary>
    public static bool IsEditMeshValid(EditMesh mesh) => EditMeshStore.TryGet(mesh, out _);

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

    // The edit mesh a call works on, or null with a warning naming the call where it is not loaded,
    // so an operation on a freed mesh is found in the log and the frame goes on.
    private static EditMeshData? Edited(EditMesh mesh, string call)
    {
        if (EditMeshStore.TryGet(mesh, out var data)) return data;
        ApiLogger.Warn($"{call}: the edit mesh is not loaded.");
        return null;
    }
}
