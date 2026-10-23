using System.Numerics;

namespace Engine;

// A model file read into the mesh, the meshes of a glTF, an FBX or any file LoadModel reads,
// their triangles joined back into faces.
internal sealed partial class EditMeshData
{
    /// <summary>Adds the meshes of a model file, read as <see cref="E3D.LoadModel"/> reads it, as faces of this mesh, and selects them.</summary>
    /// <remarks>
    /// <para>
    /// A file's meshes are placed as its nodes place them, as a loaded model's are. Their vertices
    /// in one place are joined, since a file splits a vertex where its faces' normals or texture
    /// coordinates differ, and triangles in one plane and one material that share an edge are joined
    /// back into one face round them, so a box from Blender is six faces again. A material is a
    /// slot of its name, the words of a name joined by hyphens, and a mesh with none is in the first
    /// slot.
    /// </para>
    /// <para>
    /// The recipe keeps the file's path, so the mesh is read again from the file each time the recipe
    /// is run, as a join reads its model's recipe.
    /// </para>
    /// </remarks>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="FormatException">The file is not a model a reader of the engine's takes.</exception>
    public void Import(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (path.Any(char.IsWhiteSpace)) throw new ArgumentException("An imported model's path is one word, with no space in it.", nameof(path));
        if (E3D.ResolveFile(path) is null) throw new FileNotFoundException($"There is no model at {path}.", path);
        var scene = E3D.ReadSceneForEditMesh(path) ?? throw new FormatException($"{path} is not a model this engine reads.");

        var first = _faces.Count;
        // A vertex for each place, within a hundredth of a millimeter.
        var welded = new Dictionary<(long, long, long), int>();
        int Weld(Vector3 point)
        {
            var key = ((long)MathF.Round(point.X * 1e5f), (long)MathF.Round(point.Y * 1e5f), (long)MathF.Round(point.Z * 1e5f));
            if (!welded.TryGetValue(key, out var vertex)) welded[key] = vertex = AddVertex(point);
            return vertex;
        }

        void Visit(SceneNode node, Matrix4x4 parent)
        {
            var t = node.LocalTransform;
            var world = Matrix4x4.CreateScale(t.Scale) * Matrix4x4.CreateFromQuaternion(t.Rotation) * Matrix4x4.CreateTranslation(t.Position) * parent;
            var materials = node.Components.OfType<SceneMaterialPayload>().GroupBy(material => material.SourcePath).ToDictionary(group => group.Key, group => group.First());
            foreach (var mesh in node.Components.OfType<SceneMeshPayload>())
            {
                var named = mesh.Subsets.FirstOrDefault()?.MaterialPath is { } materialPath && materials.TryGetValue(materialPath, out var material) ? material.Name : null;
                var slot = named is null ? 0 : SlotIndex(SlotName(named));
                var vertices = mesh.Positions.Select(position => Weld(Vector3.Transform(position, world))).ToArray();
                for (int i = 0; i + 2 < mesh.Indices.Length; i += 3)
                {
                    var (a, b, c) = (vertices[mesh.Indices[i]], vertices[mesh.Indices[i + 1]], vertices[mesh.Indices[i + 2]]);
                    if (a != b && b != c && a != c) AddFace([a, b, c], slot);
                }
            }
            foreach (var child in node.Children) Visit(child, world);
        }
        foreach (var root in scene.Roots) Visit(root, Matrix4x4.Identity);

        // Triangles in one plane and one slot sharing an edge, each group one face round it where one
        // loop goes round it.
        var imported = Enumerable.Range(first, _faces.Count - first).ToHashSet();
        var planes = imported.ToDictionary(face => face, face => (Normal: Normal(face), Offset: Vector3.Dot(Normal(face), _positions[_faces[face].Corners[0]])));
        bool InOnePlane(int a, int b) => _faces[a].Slot == _faces[b].Slot
            && Vector3.Dot(planes[a].Normal, planes[b].Normal) > 1 - 1e-5f && MathF.Abs(planes[a].Offset - planes[b].Offset) < 1e-4f;

        var going = new HashSet<int>();
        var joined = new List<Face>();
        foreach (var group in Groups(imported, InOnePlane))
        {
            if (group.Length < 2 || Loops(group) is not [var loop]) continue;
            going.UnionWith(group);
            joined.Add(new Face([.. loop], _faces[group[0]].Slot));
        }
        RemoveFaces(going);
        _faces.AddRange(joined);

        _selectedFaces.Clear();
        _selectedFaces.UnionWith(Enumerable.Range(first, _faces.Count - first));
        _selectedVertices.Clear();
        foreach (var face in _selectedFaces) _selectedVertices.UnionWith(_faces[face].Corners);
        Compact([]);
        Did($"import {path}");
    }

    /// <summary>A material's name as a slot's, one word, its words joined by hyphens, or the first slot's where it has none.</summary>
    private static string SlotName(string name)
    {
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? DefaultSlot : string.Join('-', words);
    }
}
