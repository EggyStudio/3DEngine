using System.Numerics;
using System.Runtime.InteropServices;
using StbImageSharp;

namespace Engine;

/// <summary>A mesh on the GPU, by its id in the <see cref="MeshStore"/>, with its size and bounds.</summary>
/// <remarks>A default mesh has id 0 and is not loaded. Drawing it draws nothing.</remarks>
public readonly record struct ModelMesh(int Id, int VertexCount, int TriangleCount, BoundingBox Bounds)
{
    /// <summary>Whether this names a mesh that was loaded.</summary>
    public bool IsValid => Id > 0;
}
