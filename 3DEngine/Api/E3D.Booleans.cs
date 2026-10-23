using System.Numerics;

namespace Engine;

public static partial class E3D
{
    // -- Booleans of edit meshes, by csg.js's binary space partition, and the openings and rooms made
    // with them. Each makes the mesh again from what the partition leaves, its pieces in one plane
    // joined back into faces.

    /// <summary>Joins another solid into the mesh where they overlap, a model's recipe file moved by <paramref name="at"/>, or a solid primitive's line.</summary>
    /// <remarks>
    /// <para>
    /// The other is a <c>.model</c> file, or a solid primitive's line as the recipe has it, such as
    /// <c>"stairs 16 0.2 0.3 1.2 at 0.2 0.2 0.6"</c>, placed where its own words place it, which a
    /// box, cylinder, cone, wedge, stairs, arch, room or ramp is. The faces where the two overlap
    /// are taken away and the rest joined, so two boxes side by side make one box, and the other's
    /// faces are selected after. Both are closed solids, as a boolean needs, and what is made is too.
    /// </para>
    /// <para>
    /// A flight of stairs joined into a level stands on its floor and against its wall as one solid,
    /// where one added beside them is a second solid whose edges a later boolean welds to theirs.
    /// The recipe's line is <c>union stairs 16 0.2 0.3 1.2 at 0.2 0.2 0.6</c>, or
    /// <c>union models/pillar.model at 2 0 2</c> for a file.
    /// </para>
    /// </remarks>
    /// <returns>Whether the solid was joined, a file that is missing or not a recipe, or a line that is not one, leaving the mesh as it was with a warning naming it.</returns>
    /// <exception cref="ArgumentException">A primitive's line is given with an offset, or a measure of it is out of range.</exception>
    public static bool EditMeshUnion(EditMesh mesh, string fileOrPrimitive, Vector3 at = default) =>
        Boolean(mesh, nameof(EditMeshUnion), data => data.Union(fileOrPrimitive, at), fileOrPrimitive);

    /// <summary>Takes another solid away from the mesh, a model's recipe file moved by <paramref name="at"/>, or a solid primitive's line.</summary>
    /// <remarks>
    /// The faces the other leaves in the mesh are selected after. A solid cutting through a face is
    /// made a little longer than the face is deep, since two faces in one plane are cut as the
    /// partition's planes say. The recipe's line is <c>subtract box 1 1 1 at 1.5 1 -0.25</c>.
    /// </remarks>
    /// <returns>Whether the solid was taken away, a file that is missing or not a recipe, or a line that is not one, leaving the mesh as it was with a warning naming it.</returns>
    /// <exception cref="ArgumentException">A primitive's line is given with an offset, or a measure of it is out of range.</exception>
    public static bool EditMeshSubtract(EditMesh mesh, string fileOrPrimitive, Vector3 at = default) =>
        Boolean(mesh, nameof(EditMeshSubtract), data => data.Subtract(fileOrPrimitive, at), fileOrPrimitive);

    /// <summary>Keeps what the mesh and another solid both hold, a model's recipe file moved by <paramref name="at"/>, or a solid primitive's line.</summary>
    /// <remarks>The recipe's line is <c>intersect box 3 3 3 at -0.5 -0.5 -0.5</c>.</remarks>
    /// <returns>Whether the two were intersected, a file that is missing or not a recipe, or a line that is not one, leaving the mesh as it was with a warning naming it.</returns>
    /// <exception cref="ArgumentException">A primitive's line is given with an offset, or a measure of it is out of range.</exception>
    public static bool EditMeshIntersect(EditMesh mesh, string fileOrPrimitive, Vector3 at = default) =>
        Boolean(mesh, nameof(EditMeshIntersect), data => data.Intersect(fileOrPrimitive, at), fileOrPrimitive);

    /// <summary>Takes a solid primitive, made where its line places it, away from the mesh, as <c>"box 1 1 1 at 1.5 1 -0.25"</c>.</summary>
    /// <remarks>The faces the primitive left in the mesh are selected after. The recipe's line is <c>cut box 1 1 1 at 1.5 1 -0.25</c>.</remarks>
    /// <exception cref="ArgumentException">The line is not a solid primitive's, or a measure of it is out of range.</exception>
    /// <exception cref="FormatException">The primitive's words are not its measures.</exception>
    public static void EditMeshCut(EditMesh mesh, string primitive) => Edited(mesh, nameof(EditMeshCut))?.Cut(primitive);

    /// <summary>Cuts a window through the wall whose face was selected first, measured on every selected face in its plane, as deep as the wall.</summary>
    /// <remarks>
    /// <para>
    /// Its middle is <paramref name="along"/> from the wall's left edge seen from in front and its
    /// bottom <paramref name="sill"/> above the wall's bottom. The opening is a box cut a centimeter
    /// past the wall on each side, as deep as a ray from the face finds the wall's other side, so a
    /// room's wall is cut through and the wall across the room is not. A wall an earlier opening cut
    /// into pieces is measured whole where its pieces are selected.
    /// </para>
    /// <para>The opening's sides are selected after. The recipe's line is <c>cut window 1.6 1.2 2.9 1</c>.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">No face is selected, or the face is a floor or a ceiling rather than a wall.</exception>
    public static void EditMeshCutWindow(EditMesh mesh, float width, float height, float along, float sill) =>
        Edited(mesh, nameof(EditMeshCutWindow))?.Window(width, height, along, sill);

    /// <summary>Cuts a door through the wall whose face was selected first, standing on the wall's bottom, as deep as the wall.</summary>
    /// <remarks>As <see cref="EditMeshCutWindow"/>, from the wall's bottom. The recipe's line is <c>cut door 1.2 2.2 3.8</c>.</remarks>
    /// <exception cref="InvalidOperationException">No face is selected, or the face is a floor or a ceiling rather than a wall.</exception>
    public static void EditMeshCutDoor(EditMesh mesh, float width, float height, float along) =>
        Edited(mesh, nameof(EditMeshCutDoor))?.Door(width, height, along);

    /// <summary>Adds a room, a box hollowed out by a thickness of wall, floor and ceiling, which windows and doors are cut through.</summary>
    /// <remarks>
    /// The outside faces out and the inside in, a closed solid of sixteen vertices and twelve faces.
    /// The recipe's line is <c>room 6 3 5 0.2</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero, or the walls are half as thick as the narrowest side.</exception>
    public static void EditMeshRoom(EditMesh mesh, Vector3 size, float thickness, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshRoom))?.Room(size, thickness, at, centered);

    /// <summary>Adds a ramp rising along Z, a wedge given in the words of a flight of stairs, its rise, its run and its width.</summary>
    /// <remarks>The recipe's line is <c>ramp 1 3 2</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A measure is not above zero.</exception>
    public static void EditMeshRamp(EditMesh mesh, float rise, float run, float width, Vector3 at = default, bool centered = false) =>
        Edited(mesh, nameof(EditMeshRamp))?.Ramp(rise, run, width, at, centered);

    /// <summary>Sets how near a point is to a plane to lie in it for the mesh's booleans, a hundredth of a millimeter until set, up to a centimeter.</summary>
    /// <remarks>Ten times it is how near two points are to be one. The recipe's line is <c>epsilon 0.0001</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The distance is not above zero and up to a centimeter.</exception>
    public static void EditMeshEpsilon(EditMesh mesh, float distance) => Edited(mesh, nameof(EditMeshEpsilon))?.Epsilon(distance);

    // A boolean with a file or a primitive's line, answered false with a warning where the file or
    // the line cannot be read, as a loader answers a bad file.
    private static bool Boolean(EditMesh mesh, string call, Action<EditMeshData> run, string fileOrPrimitive)
    {
        if (Edited(mesh, call) is not { } data) return false;
        try
        {
            run(data);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {
            ApiLogger.Warn($"{call}: '{fileOrPrimitive}' could not be read as a solid. {ex.Message}");
            return false;
        }
    }
}
