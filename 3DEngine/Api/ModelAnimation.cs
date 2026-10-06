using System.Numerics;

namespace Engine;

/// <summary>
/// A clip of a model file, sampled into poses of every bone at <see cref="Engine3D.AnimationFps"/>
/// frames a second, played on a model with <see cref="Engine3D.UpdateModelAnimation"/>.
/// </summary>
/// <remarks>
/// The fields are raylib's. <see cref="FramePoses"/> holds each frame's bones in the model's own
/// space, in the order of <see cref="Bones"/>, which a model loaded from the same file shares.
/// </remarks>
public sealed class ModelAnimation
{
    /// <summary>The clip's name in the file.</summary>
    public string Name { get; init; } = "";

    /// <summary>The bones the clip moves, in the order of each frame's poses.</summary>
    public BoneInfo[] Bones { get; init; } = [];

    /// <summary>For each frame, each bone's pose in the model's space.</summary>
    public Transform[][] FramePoses { get; init; } = [];

    /// <summary>The morph targets the clip moves the weights of, by the node their mesh hangs from and their index among its targets.</summary>
    public (string Node, int Target)[] MorphChannels { get; init; } = [];

    /// <summary>For each frame, each of <see cref="MorphChannels"/>' weights.</summary>
    public float[][] FrameMorphWeights { get; init; } = [];

    /// <summary>How many bones each frame poses.</summary>
    public int BoneCount => Bones.Length;

    /// <summary>How many frames the clip has.</summary>
    public int FrameCount => FramePoses.Length;
}
