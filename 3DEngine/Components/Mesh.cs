using System.Numerics;

namespace Engine;

/// <summary>
/// A mesh component, triangles in the entity's own space, from arrays the engine uploads or a mesh
/// the program uploaded itself.
/// </summary>
/// <remarks>
/// <para>
/// Drawn with its <see cref="Material"/> through each entity with a <see cref="Camera"/>,
/// lit and textured the way <c>DrawModel</c> draws a model, by <see cref="MeshEntityDraws"/>.
/// </para>
/// <para>
/// The arrays are uploaded once, the first frame the entity is drawn, and kept while an entity's
/// mesh holds the same <see cref="Positions"/> array, drawn or not. A changed mesh is a new
/// <see cref="Mesh"/> with new arrays, because writing into the old ones is not seen.
/// </para>
/// <para>
/// A mesh the program uploaded with <c>UploadMesh</c>, set as <see cref="Uploaded"/>, is drawn
/// with its own streams and indices as <c>DrawMesh</c> draws it, so a voxel game's section, its
/// light packed into a stream its shader reads, is an entity. The program frees it with
/// <c>UnloadMesh</c> once no entity holds it.
/// </para>
/// <para>
/// A scene file holds the arrays, so a mesh made in code is saved with its level. A model's meshes
/// are not, since the model's <see cref="ModelRef"/> brings them back, and neither is an uploaded
/// mesh, which lives as long as the program keeps it.
/// </para>
/// </remarks>
/// <seealso cref="Material"/>
/// <seealso cref="Transform"/>
[SceneComponent]
public struct Mesh
{
    /// <summary>Vertex positions, three per triangle, or each vertex once where <see cref="Indices"/> names the triangles.</summary>
    public Vector3[] Positions;

    /// <summary>A normal per position, or null to light each triangle flat, or with indices by the mean of the faces a vertex is a corner of.</summary>
    public Vector3[]? Normals;

    /// <summary>A texture coordinate per position, or null for none.</summary>
    public Vector2[]? Uvs;

    /// <summary>The triangles' corners as indices into <see cref="Positions"/>, three a triangle, or null for the positions in order.</summary>
    public uint[]? Indices;

    /// <summary>A mesh the program uploaded, drawn in place of the arrays, or none.</summary>
    public ModelMesh Uploaded;

    /// <summary>Creates a mesh from positions, three per triangle, with optional normals and texture coordinates.</summary>
    public Mesh(Vector3[] positions, Vector3[]? normals = null, Vector2[]? uvs = null)
    {
        Positions = positions;
        Normals = normals;
        Uvs = uvs;
    }

    /// <summary>Creates a mesh of vertices and the indices of its triangles' corners, with optional normals and texture coordinates.</summary>
    public Mesh(Vector3[] positions, uint[] indices, Vector3[]? normals = null, Vector2[]? uvs = null)
        : this(positions, normals, uvs) => Indices = indices;

    /// <summary>Creates a mesh drawing what the program uploaded with <c>UploadMesh</c>, its streams and indices with it.</summary>
    public Mesh(ModelMesh uploaded)
    {
        Positions = [];
        Uploaded = uploaded;
    }
}
