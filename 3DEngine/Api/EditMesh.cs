namespace Engine;

/// <summary>
/// A polygon mesh shaped by primitives and by operations on its selection, by its id. It is made
/// by <see cref="E3D.LoadEditMesh()"/> and freed by <see cref="E3D.UnloadEditMesh"/>.
/// </summary>
/// <remarks>
/// <para>
/// A vertex is a point, and a face is a loop of three or more vertices, wound counterclockwise
/// seen from the side it faces, as every front face the engine draws is. A face of any count is
/// kept whole, so a box's side is one face of four corners and a staircase's side one face of many,
/// and is cut into triangles only when the mesh is built into a model. Edges are read from the
/// faces, each pair of corners that follow one another round a face.
/// </para>
/// <para>
/// Each face has a material slot, a name, the first of which every new face takes, and a smoothing
/// angle. The operations act on the selection, a set of vertices and a set of faces, and each
/// writes its line to the mesh's recipe, so the mesh is made again from its lines.
/// </para>
/// <para>
/// A default edit mesh has id 0 and is not loaded. An operation on it does nothing, with a warning.
/// </para>
/// </remarks>
public readonly record struct EditMesh(int Id)
{
    /// <summary>Whether this names an edit mesh that was made.</summary>
    public bool IsValid => Id > 0;
}
