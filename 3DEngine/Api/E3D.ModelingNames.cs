namespace Engine;

public static partial class E3D
{
    // -- Edit meshes by name, which the console's model commands reach, so a level is shaped from
    // the terminal while the program runs.

    /// <summary>Gives an edit mesh a name the console's model commands reach it by, so a program's level is shaped from the terminal.</summary>
    /// <remarks>
    /// <para>
    /// <c>./e3d command model.cut level window 1 1 2 0.9</c> then cuts a window in the mesh the
    /// program holds, and the program sees the change the next time it reads or builds the mesh.
    /// The name takes the place of the mesh of that name, and the entity <c>model.show</c> drew that
    /// mesh with draws this one in its place.
    /// </para>
    /// <para>
    /// The names last while the app runs, and the next app starts with none. The mesh stays the
    /// program's to free, where a mesh the commands made is freed when its name is forgotten.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The name is empty or more than one word.</exception>
    public static void SetEditMeshName(EditMesh mesh, string name)
    {
        if (!EditMeshNames.IsName(name)) throw new ArgumentException("An edit mesh's name is one word.", nameof(name));
        if (Edited(mesh, nameof(SetEditMeshName)) is null) return;
        EditMeshNames.Of(World).Name(name, mesh, made: false);
    }

    /// <summary>The edit mesh of a name, given by <see cref="SetEditMeshName"/> or by the model commands that made it, or one that is not loaded where none has the name.</summary>
    /// <remarks>
    /// A mesh made from the terminal, as by <c>model.box room 8 3 6</c>, is read here by the program,
    /// to build it into a model of its own or save it. The commands free it when its name is
    /// forgotten, so a program keeping it past that holds a mesh that is not loaded.
    /// </remarks>
    public static EditMesh GetEditMeshByName(string name) =>
        _app is { } app && app.World.TryGetResource<EditMeshNames>(out var names) && names.Find(name) is { } named ? named.Mesh : default;
}
