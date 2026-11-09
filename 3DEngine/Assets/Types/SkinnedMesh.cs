using System.Numerics;

namespace Engine;

/// <summary>What a mesh needs to be posed, which is its vertices at rest and the bones that move each.</summary>
/// <param name="Mesh">The index of the mesh in <see cref="Model.Meshes"/>.</param>
/// <param name="Rest">The mesh's vertices in the model's space, as loaded.</param>
/// <param name="Joints">Four joint indices per vertex, into <paramref name="BoneOfJoint"/>.</param>
/// <param name="Weights">Four weights per vertex, summing to one.</param>
/// <param name="BoneOfJoint">The model bone each of the mesh's joints is.</param>
/// <param name="FromRest">For each joint, model space at rest to the joint's own space.</param>
internal sealed record SkinnedMesh(int Mesh, ModelVertex[] Rest, ushort[] Joints, float[] Weights, int[] BoneOfJoint, Matrix4x4[] FromRest)
{
    /// <summary>The name of the node the mesh hangs from, which a clip's morph weights name.</summary>
    public string Node { get; init; } = "";

    /// <summary>The mesh's morph targets' names, empty for a mesh with none.</summary>
    public string[] MorphNames { get; init; } = [];

    /// <summary>Each morph target's weight now, which clips and <c>E3D.SetModelMorphWeight</c> change in place.</summary>
    public float[] MorphWeights { get; init; } = [];

    /// <summary>How far each target moves each vertex at full weight, in the model's space.</summary>
    public Vector3[][] MorphPositions { get; init; } = [];

    /// <summary>How each target turns each vertex's normal at full weight, or null for a target with none.</summary>
    public Vector3[]?[] MorphNormals { get; init; } = [];
}
