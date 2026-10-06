using System.Numerics;

namespace Engine;

/// <summary>
/// Spawn-time policy passed to <see cref="SceneSpawner.Spawn"/>. Defaults match the
/// runtime profile (Y-up target, meters, render purposes, white default material).
/// </summary>
public sealed class SceneSpawnSettings
{
    /// <summary>Engine canonical coordinate system (target basis after the root-level swap).</summary>
    public SceneCoordinateSystem TargetCoordinateSystem { get; init; } = SceneCoordinateSystem.YUp;

    /// <summary>Engine canonical scale; the spawner divides the source mpu by this value.</summary>
    public double TargetMetersPerUnit { get; init; } = 1.0;

    /// <summary>
    /// Authoring purposes to materialize. Nodes whose <see cref="SceneNode.Purpose"/>
    /// is not in the mask are skipped, and their children are still visited, since a child
    /// may have another purpose, with the parent's transform carried down.
    /// </summary>
    public ScenePurposeMask IncludePurposes { get; init; } = ScenePurposeMask.Runtime;

    /// <summary>
    /// Optional placement matrix applied <i>after</i> the basis-change + unit scale, so
    /// callers can position / orient a spawned scene in world space without re-authoring.
    /// Identity by default.
    /// </summary>
    public Matrix4x4 Placement { get; init; } = Matrix4x4.Identity;

    /// <summary>
    /// Default <see cref="Material.Albedo"/> applied to mesh entities whose source had no
    /// <see cref="SceneMaterialPayload"/> bound. Defaults to opaque white so the renderer
    /// always receives a (Mesh, Material) pair.
    /// </summary>
    public Vector4 DefaultAlbedo { get; init; } = Vector4.One;

    /// <summary>
    /// When <c>true</c> (the default), every spawned entity gets a <see cref="SceneInstance"/>
    /// marker recording its source path. Disable for transient / scratch spawns where
    /// provenance isn't needed.
    /// </summary>
    public bool AttachSceneInstanceMarker { get; init; } = true;

    /// <summary>Reusable default settings.</summary>
    public static SceneSpawnSettings Default { get; } = new();
}
