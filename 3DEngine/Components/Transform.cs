using System.Numerics;

namespace Engine;

/// <summary>World-space transform component (position, rotation, scale).</summary>
/// <seealso cref="Camera"/>
/// <seealso cref="Mesh"/>
public struct Transform
{
    /// <summary>Position in world space.</summary>
    public Vector3 Position;

    /// <summary>Rotation as a quaternion.</summary>
    public Quaternion Rotation;

    /// <summary>Scale factor per axis.</summary>
    public Vector3 Scale;

    /// <summary>Creates a transform at the specified position with identity rotation and unit scale.</summary>
    /// <param name="position">World-space position.</param>
    public Transform(Vector3 position)
    {
        Position = position;
        Rotation = Quaternion.Identity;
        Scale = Vector3.One;
    }
    
    /// <summary>Creates a transform at the specified position and scale with identity rotation.</summary>
    /// <param name="position">World-space position.</param>
    /// <param name="scale">Scale factor per axis.</param>
    public Transform(Vector3 position, Vector3 scale)
    {
        Position = position;
        Rotation = Quaternion.Identity;
        Scale = scale;
    }
    
    /// <summary>Creates a transform at the specified position and rotation with unit scale.</summary>
    /// <param name="position">World-space position.</param>
    /// <param name="scale">Scale factor per axis.</param>
    /// <param name="rotation">Rotation as a quaternion.</param>
    public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}
