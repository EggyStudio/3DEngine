using System.Numerics;
using System.Runtime.InteropServices;
using StbImageSharp;

namespace Engine;

/// <summary>Meshes with their materials, loaded from a file or made from a mesh, drawn as one.</summary>
/// <remarks>
/// <para>
/// The fields are raylib's: <see cref="Meshes"/>, <see cref="Materials"/>, and
/// <see cref="MeshMaterial"/>, which gives the material index of each mesh. A program changes a
/// material by assigning to <see cref="Materials"/>, as in
/// <c>model.Materials[0].Texture = texture;</c>.
/// </para>
/// <para>
/// A file's node hierarchy is baked into the vertices when it loads, so every mesh is in the
/// model's own space and <see cref="Transform"/> is the only transform left.
/// </para>
/// </remarks>
public sealed class Model
{
    /// <summary>The model's meshes.</summary>
    public ModelMesh[] Meshes { get; init; } = [];

    /// <summary>The model's materials.</summary>
    public ModelMaterial[] Materials { get; init; } = [];

    /// <summary>For each mesh, the index of its material in <see cref="Materials"/>.</summary>
    public int[] MeshMaterial { get; init; } = [];

    /// <summary>Applied before the position, rotation and scale a draw call gives.</summary>
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    /// <summary>The bones of a file's skeletons, which an animation of the same file moves, and their pose at rest.</summary>
    public ModelSkeleton Skeleton { get; init; } = new();

    /// <summary>The meshes bones move, with their vertices at rest.</summary>
    internal SkinnedMesh[] Skins { get; init; } = [];

    // The bones' poses as they were last set, which a morph weight set alone poses the model by again.
    internal Transform[]? LastPose { get; set; }

    /// <summary>
    /// The joints each skinned mesh was last posed with on the GPU, by mesh index, which leaves the
    /// mesh's own vertices at rest, so wires drawn on the CPU pose them the same way.
    /// </summary>
    internal Dictionary<int, Matrix4x4[]> GpuPoses { get; } = [];

    /// <summary>Textures the model loaded itself, which <see cref="Engine3D.UnloadModel"/> frees.</summary>
    internal Texture2D[] OwnedTextures { get; init; } = [];

    /// <summary>Whether the model has a mesh to draw.</summary>
    public bool IsValid => Meshes.Length > 0;
}
