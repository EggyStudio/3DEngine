using System.Numerics;

namespace Engine;

/// <summary>One bone of a model or an animation, by its name and the index of its parent bone, or -1.</summary>
public readonly record struct BoneInfo(string Name, int Parent);
