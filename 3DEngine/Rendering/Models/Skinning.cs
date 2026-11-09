using System.Numerics;

namespace Engine;

/// <summary>A skinned mesh's vertices morphed and posed on the CPU, which the flat API, physics and the mesh entities read.</summary>
internal static class Skinning
{
    // A skinned mesh's vertices at rest moved toward its morph targets by their weights now.
    internal static ModelVertex[] Morphed(SkinnedMesh skin)
    {
        if (skin.MorphWeights.Length == 0) return skin.Rest;
        var moved = (ModelVertex[])skin.Rest.Clone();
        for (int t = 0; t < skin.MorphWeights.Length; t++)
        {
            var weight = skin.MorphWeights[t];
            if (weight == 0) continue;
            var normals = skin.MorphNormals[t];
            for (int v = 0; v < moved.Length; v++)
                moved[v] = moved[v] with
                {
                    Position = moved[v].Position + skin.MorphPositions[t][v] * weight,
                    Normal = normals is null ? moved[v].Normal : moved[v].Normal + normals[v] * weight,
                };
        }
        return moved;
    }

    // A model's mesh's vertices as it was last posed. A mesh the GPU poses keeps its vertices at
    // rest, so they are posed here from the joints it was handed, and any other mesh's are its own.
    internal static ModelVertex[] Posed(Model model, int index, ModelVertex[] vertices) =>
        model.GpuPoses.TryGetValue(index, out var joints) && model.Skins.FirstOrDefault(s => s.Mesh == index) is { } skin
            ? PoseOnCpu(skin, joints)
            : vertices;

    // A skinned mesh's vertices moved from their rest by its joints, on the CPU.
    internal static ModelVertex[] PoseOnCpu(SkinnedMesh skin, Matrix4x4[] joints)
    {
        var morphed = Morphed(skin);
        var posed = new ModelVertex[skin.Rest.Length];
        for (int v = 0; v < posed.Length; v++)
        {
            var rest = morphed[v];
            Vector3 position = Vector3.Zero, normal = Vector3.Zero;
            for (int k = 0; k < 4; k++)
            {
                float weight = skin.Weights[v * 4 + k];
                if (weight <= 0) continue;
                ref var m = ref joints[skin.Joints[v * 4 + k]];
                position += Vector3.Transform(rest.Position, m) * weight;
                normal += Vector3.TransformNormal(rest.Normal, m) * weight;
            }
            // A vertex no bone holds stays where it rests.
            if (normal == Vector3.Zero) (position, normal) = (rest.Position, rest.Normal);
            posed[v] = rest with { Position = position, Normal = Vector3.Normalize(normal) };
        }
        return posed;
    }
}
