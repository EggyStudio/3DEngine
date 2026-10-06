using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>One vertex of the model pass: a position, a normal and a texture coordinate, 32 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ModelVertex(Vector3 Position, Vector3 Normal, Vector2 Uv);
