using System.Numerics;

namespace Engine;

/// <summary>Mesh component containing raw vertex position data for GPU upload.</summary>
/// <seealso cref="Material"/>
/// <seealso cref="Transform"/>
/// <seealso cref="MeshGpuRegistry"/>
public struct Mesh
{
    /// <summary>Array of vertex positions in local (model) space.</summary>
    public Vector3[] Positions;

    /// <summary>Creates a mesh with the specified vertex positions.</summary>
    /// <param name="positions">Vertex position data.</param>
    public Mesh(Vector3[] positions)
    {
        Positions = positions;
    }
}
