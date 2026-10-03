using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>GPU push constant block for per-object mesh data (model matrix + albedo color).</summary>
/// <seealso cref="MeshPipeline"/>
[StructLayout(LayoutKind.Sequential)]
public struct MeshPushConstants
{
    /// <summary>Object-to-world model matrix (64 bytes).</summary>
    public Matrix4x4 Model;

    /// <summary>Material albedo color as RGBA (16 bytes).</summary>
    public Vector4 Albedo;
}