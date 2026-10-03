using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic animation clip payload attached to a <see cref="SceneNode"/> via
/// <see cref="SceneNode.Components"/>. Carries sampled keyframe tracks targeting
/// other nodes / joints in the same <see cref="Scene"/> by source path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Snapshot model:</b> animation curves are <i>sampled</i> by the importer (no
/// runtime curve evaluation). Each <see cref="SceneAnimationChannel"/> stores parallel
/// <see cref="SceneAnimationChannel.TimesSeconds"/> + value arrays; the spawn-time
/// system interpolates between samples using <see cref="SceneAnimationChannel.Interpolation"/>.
/// </para>
/// <para>
/// <b>Targeting:</b> channels reference their target node by
/// <see cref="SceneAnimationChannel.TargetNodePath"/> (matched against
/// <see cref="SceneNode.SourcePath"/>) so the same clip can drive multiple nodes /
/// joints without entity ids that don't exist yet at import time.
/// </para>
/// </remarks>
/// <seealso cref="SceneSkeletonPayload"/>
public sealed class SceneAnimationPayload
{
    /// <summary>Display name of the clip (typically the source animation name).</summary>
    public string Name { get; init; } = "Animation";

    /// <summary>Total clip duration in seconds (max time across all channels).</summary>
    public float DurationSeconds { get; init; }

    /// <summary>Channels that make up the clip. Required, never null.</summary>
    public required IReadOnlyList<SceneAnimationChannel> Channels { get; init; }
}

/// <summary>A single animation channel targeting a specific property of a specific node.</summary>
public sealed class SceneAnimationChannel
{
    /// <summary>Source path of the target <see cref="SceneNode"/> / joint (e.g. <c>"/Armature/Hips"</c>).</summary>
    public required string TargetNodePath { get; init; }

    /// <summary>Which property on the target node this channel drives.</summary>
    public required SceneAnimationProperty Property { get; init; }

    /// <summary>Interpolation mode between adjacent samples.</summary>
    public SceneAnimationInterpolation Interpolation { get; init; } = SceneAnimationInterpolation.Linear;

    /// <summary>Sample times in seconds. Strictly increasing. Required.</summary>
    public required float[] TimesSeconds { get; init; }

    /// <summary>
    /// Sample values, packed as <see cref="Vector4"/> per key for uniformity:
    /// translation/scale use <c>(x, y, z, 0)</c>; rotation uses a full quaternion;
    /// weights pack a single morph-target weight in <c>x</c>. Length matches
    /// <see cref="TimesSeconds"/>.
    /// </summary>
    public required Vector4[] Values { get; init; }
}

/// <summary>Property targeted by a <see cref="SceneAnimationChannel"/>.</summary>
public enum SceneAnimationProperty
{
    /// <summary>Local-space translation (<see cref="Vector4"/>.xyz).</summary>
    Translation,

    /// <summary>Local-space rotation as a quaternion (<see cref="Vector4"/>.xyzw).</summary>
    Rotation,

    /// <summary>Local-space scale (<see cref="Vector4"/>.xyz).</summary>
    Scale,

    /// <summary>Morph-target weight (<see cref="Vector4"/>.x), reserved for follow-up.</summary>
    MorphWeight,
}

/// <summary>Interpolation between adjacent animation samples.</summary>
public enum SceneAnimationInterpolation
{
    /// <summary>Hold the previous sample value until the next key.</summary>
    Step,

    /// <summary>Linear (rotations are SLERPed) between adjacent samples.</summary>
    Linear,

    /// <summary>
    /// Cubic spline as authored by the source format (currently treated as Linear by
    /// consumers that don't support tangents; data is preserved for future use).
    /// </summary>
    CubicSpline,
}