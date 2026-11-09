namespace Engine;

/// <summary>A model's bones and their pose at rest, which its animations move, as raylib's model holds them.</summary>
public sealed class ModelSkeleton
{
    /// <summary>The bones of a file's skeletons, or none.</summary>
    public BoneInfo[] Bones { get; init; } = [];

    /// <summary>Each bone's pose in the model's space as the file rests, in the order of <see cref="Bones"/>.</summary>
    public Transform[] BindPose { get; init; } = [];

    /// <summary>How many bones the skeleton has.</summary>
    public int BoneCount => Bones.Length;
}
