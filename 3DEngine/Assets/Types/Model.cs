using System.Numerics;
using System.Runtime.InteropServices;
using StbImageSharp;

namespace Engine;

/// <summary>Meshes with their materials, loaded from a file or made from a mesh, drawn as one.</summary>
/// <remarks>
/// <para>
/// The fields are raylib's, <see cref="Meshes"/>, <see cref="Materials"/>, and
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
    public ModelMesh[] Meshes { get => _meshes; init => _meshes = value; }

    // The arrays behind the three properties here, which a model built from an edit mesh replaces
    // when the mesh gains or loses a material slot, the public init kept for a program that makes a
    // model of its own.
    private ModelMesh[] _meshes = [];
    private ModelMaterial[] _materials = [];
    private int[] _meshMaterial = [];
    private ModelSkeleton _skeleton = new();
    private SkinnedMesh[] _skins = [];
    private Texture2D[] _ownedTextures = [];

    /// <summary>The model's materials.</summary>
    public ModelMaterial[] Materials { get => _materials; init => _materials = value; }

    /// <summary>For each mesh, the index of its material in <see cref="Materials"/>.</summary>
    public int[] MeshMaterial { get => _meshMaterial; init => _meshMaterial = value; }

    /// <summary>Gives the model other meshes and materials, each mesh with its material's index, for a model built again from an edit mesh.</summary>
    internal void Replace(ModelMesh[] meshes, ModelMaterial[] materials, int[] meshMaterial) =>
        (_meshes, _materials, _meshMaterial) = (meshes, materials, meshMaterial);

    /// <summary>
    /// Takes the meshes, materials, skeleton, skinned meshes and textures of <paramref name="read"/>,
    /// as a model read again from its file does in place, so the model the program holds draws the
    /// file as it is now, its pose set again.
    /// </summary>
    internal void TakeFrom(Model read)
    {
        (_meshes, _materials, _meshMaterial) = (read._meshes, read._materials, read._meshMaterial);
        (_skeleton, _skins, _ownedTextures) = (read._skeleton, read._skins, read._ownedTextures);
        LastPose = null;
        GpuPoses.Clear();
    }

    /// <summary>Applied before the position, rotation and scale a draw call gives.</summary>
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    /// <summary>The bones of a file's skeletons, which an animation of the same file moves, and their pose at rest.</summary>
    public ModelSkeleton Skeleton { get => _skeleton; init => _skeleton = value; }

    /// <summary>The meshes bones move, with their vertices at rest.</summary>
    internal SkinnedMesh[] Skins { get => _skins; init => _skins = value; }

    // The bones' poses as they were last set, which a morph weight set alone poses the model by again.
    internal Transform[]? LastPose { get; set; }

    /// <summary>
    /// The joints each skinned mesh was last posed with on the GPU, by mesh index, which leaves the
    /// mesh's own vertices at rest, so wires drawn on the CPU pose them the same way.
    /// </summary>
    internal Dictionary<int, Matrix4x4[]> GpuPoses { get; } = [];

    /// <summary>Textures the model loaded itself, which <c>E3D.UnloadModel</c> frees.</summary>
    internal Texture2D[] OwnedTextures { get => _ownedTextures; init => _ownedTextures = value; }

    /// <summary>Whether the model has a mesh to draw.</summary>
    public bool IsValid => Meshes.Length > 0;
}
