namespace Engine;

/// <summary>The edit meshes the flat API made, by id, which a program frees with <see cref="E3D.UnloadEditMesh"/>.</summary>
/// <remarks>
/// An edit mesh is data on the CPU and needs no window or device, so the store is the process's own
/// rather than a resource of the app, and a program shapes a mesh before <see cref="E3D.InitWindow"/>
/// as well as after it. The lock keeps the ids of two threads apart, since a system may make a mesh
/// while the program's loop makes another.
/// </remarks>
internal static class EditMeshStore
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<int, EditMeshData> Live = [];
    private static int _next = 1;

    public static EditMesh Add(EditMeshData data)
    {
        lock (Gate)
        {
            var id = _next++;
            Live.Add(id, data);
            return new EditMesh(id);
        }
    }

    public static bool TryGet(EditMesh mesh, out EditMeshData data)
    {
        lock (Gate) return Live.TryGetValue(mesh.Id, out data!);
    }

    public static bool Remove(EditMesh mesh)
    {
        lock (Gate) return Live.Remove(mesh.Id);
    }
}
