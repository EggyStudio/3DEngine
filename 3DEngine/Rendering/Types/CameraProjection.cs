using System.Numerics;

namespace Engine;

/// <summary>How a <see cref="Camera3D"/> projects the world.</summary>
public enum CameraProjection
{
    /// <summary>Things further away are drawn smaller. <see cref="Camera3D.FovY"/> is the vertical angle in degrees.</summary>
    Perspective,

    /// <summary>Things are drawn the same size at any distance. <see cref="Camera3D.FovY"/> is the visible height in world units.</summary>
    Orthographic,
}
