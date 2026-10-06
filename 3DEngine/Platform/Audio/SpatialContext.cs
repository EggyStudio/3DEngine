using System.Numerics;

namespace Engine;

/// <summary>Static per-frame inputs the spatial processor can use (room, occlusion geometry, etc.).</summary>
/// <remarks>
/// <para>
/// <b>Convention:</b> orientation vectors are in world space and right-handed
/// (<see cref="ListenerForward"/> = camera "look" direction; <see cref="ListenerUp"/>
/// = camera "up"). The engine's standard view matrix is
/// <c>Matrix4x4.CreateLookAt(eye, target, +Y)</c>, so the rotated <c>-Z</c> axis is
/// "forward" for both listener and source.
/// </para>
/// <para>
/// All vectors default to the identity orientation (forward = <c>-Z</c>, up = <c>+Y</c>).
/// <see cref="DipoleWeight"/> defaults to <c>0</c> so directivity is neutral when not
/// configured by the caller.
/// </para>
/// </remarks>
public readonly record struct SpatialContext
{
    /// <summary>Source forward direction (unit length, world space).</summary>
    public Vector3 SourceForward { get; init; }

    /// <summary>Source up direction (unit length, world space).</summary>
    public Vector3 SourceUp { get; init; }

    /// <summary>Listener forward direction (unit length, world space).</summary>
    public Vector3 ListenerForward { get; init; }

    /// <summary>Listener up direction (unit length, world space).</summary>
    public Vector3 ListenerUp { get; init; }

    /// <summary>
    /// Directivity dipole weight in <c>[0, 1]</c>. <c>0</c> = omni-directional;
    /// <c>1</c> = pure dipole (cardioid front, silent rear). Mid-values blend.
    /// </summary>
    public float DipoleWeight { get; init; }

    /// <summary>
    /// Directivity dipole exponent (≥ 0). Higher values sharpen the front lobe.
    /// </summary>
    public float DipolePower { get; init; }

    /// <summary>Empty context: identity orientations, omni directivity.</summary>
    public static SpatialContext Empty => new()
    {
        SourceForward = -Vector3.UnitZ, SourceUp = Vector3.UnitY,
        ListenerForward = -Vector3.UnitZ, ListenerUp = Vector3.UnitY,
        DipoleWeight = 0f, DipolePower = 1f,
    };
}
