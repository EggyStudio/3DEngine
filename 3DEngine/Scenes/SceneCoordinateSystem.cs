using System.Numerics;

namespace Engine;

/// <summary>Coordinate system convention for a source scene (engine canonical is Y-up).</summary>
public enum SceneCoordinateSystem
{
    /// <summary>Right-handed, Y-up (engine canonical).</summary>
    YUp,
    /// <summary>Right-handed, Z-up (common for USD, Blender, Unreal).</summary>
    ZUp,
}
