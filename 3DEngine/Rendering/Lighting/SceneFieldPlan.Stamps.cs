using System.Numerics;

namespace Engine;

// The shapes the moving meshes are stamped as, and the parts each is stamped as.
internal sealed partial class SceneFieldPlan
{
    /// <summary>
    /// A mesh stamped as a box, from its own space to the world, the box's middle and half extents
    /// there, the least scale from there to the world, the mesh's color in linear light, and its
    /// emission, linear, none where global illumination carries it as a glow light.
    /// </summary>
    internal readonly record struct Shape(Matrix4x4 World, Vector3 Center, Vector3 Extents, float Scale, Vector3 Color = default, float LightThrough = 0,
        Vector3 Emission = default)
    {
        /// <summary>From the world into its own space, or nothing where its matrix has no inverse.</summary>
        public Matrix4x4 ToOwn => Matrix4x4.Invert(World, out var toOwn) ? toOwn : default;

        /// <summary>Whether a matrix has an inverse, which its translation leaves as it is.</summary>
        public static bool Turns(Matrix4x4 world) => Matrix4x4.Invert(world with { M41 = 0, M42 = 0, M43 = 0 }, out _);
    }

    // The parts a mesh is stamped as where there are no more than room for, which leaves a box
    // each for the meshes farther from the eye, and the box around all of it where there are, so a
    // crowd of figures past what a frame stamps is stamped as figures near the eye and boxes beyond.
    private Part[] PartsWithin(Instance instance, int room)
    {
        if ((instance.Parts ?? _parts(instance.Vertices)) is { } parts && parts.Length <= room) return parts;
        if (_whole.TryGetValue(instance.Vertices, out var whole)) return whole;
        if (_whole.Count > 4096) _whole.Clear();
        return _whole[instance.Vertices] = [new Part(_bounds(instance.Vertices), Matrix4x4.Identity)];
    }

    // The box around all of each mesh as its one part, kept for its vertices.
    private readonly Dictionary<ModelVertex[], Part[]> _whole = new(ReferenceEqualityComparer.Instance);
}
