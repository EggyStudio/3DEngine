using System.Numerics;

namespace Engine;

/// <summary>A mesh component: triangles in the entity's own space, three vertices each.</summary>
/// <remarks>
/// <para>
/// Drawn with its <see cref="Material"/> through each entity with a <see cref="Camera"/>,
/// lit and textured the way <c>DrawModel</c> draws a model, by <see cref="MeshEntityDraws"/>.
/// </para>
/// <para>
/// The arrays are uploaded once, the first frame the entity is drawn, and kept while the same
/// <see cref="Positions"/> array is in use. A changed mesh is a new <see cref="Mesh"/> with new
/// arrays, because writing into the old ones is not seen.
/// </para>
/// </remarks>
/// <seealso cref="Material"/>
/// <seealso cref="Transform"/>
public struct Mesh
{
    /// <summary>Vertex positions, three per triangle.</summary>
    public Vector3[] Positions;

    /// <summary>A normal per position, or null to light each triangle flat.</summary>
    public Vector3[]? Normals;

    /// <summary>A texture coordinate per position, or null for none.</summary>
    public Vector2[]? Uvs;

    /// <summary>Creates a mesh from positions, three per triangle, with optional normals and texture coordinates.</summary>
    public Mesh(Vector3[] positions, Vector3[]? normals = null, Vector2[]? uvs = null)
    {
        Positions = positions;
        Normals = normals;
        Uvs = uvs;
    }
}
