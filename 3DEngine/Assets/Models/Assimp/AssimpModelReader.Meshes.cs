using System.Numerics;
using A = Assimp;

namespace Engine;

internal sealed partial class AssimpModelReader
{
    // -- Meshes

    private static SceneMeshPayload[] BuildMeshes(A.Scene aScene, SceneMaterialPayload[] materials, CancellationToken ct)
    {
        if (aScene.MeshCount == 0) return Array.Empty<SceneMeshPayload>();
        var result = new SceneMeshPayload[aScene.MeshCount];
        for (int i = 0; i < aScene.MeshCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var am = aScene.Meshes[i];

            // The Triangulate step leaves only triangles, and anything else is skipped (the lines
            // and points SortByPrimitiveType puts in meshes of their own are dropped here).
            if ((am.PrimitiveType & A.PrimitiveType.Triangle) == 0)
            {
                result[i] = EmptyMesh(am.Name);
                continue;
            }

            int vc = am.VertexCount;
            // AssimpNetter exposes Vertices/Normals/Tangents/BiTangents directly as
            // List<System.Numerics.Vector3>, so no element needs converting.
            var positions = vc == 0 ? Array.Empty<Vector3>() : am.Vertices.ToArray();

            // Indices: 3 per triangle face after Triangulate.
            var faces = am.Faces;
            var indices = new int[faces.Count * 3];
            int w = 0;
            for (int f = 0; f < faces.Count; f++)
            {
                var face = faces[f];
                if (face.IndexCount != 3) continue;
                indices[w++] = face.Indices[0];
                indices[w++] = face.Indices[1];
                indices[w++] = face.Indices[2];
            }
            if (w != indices.Length) Array.Resize(ref indices, w);

            Vector3[]? normals = am.HasNormals ? am.Normals.ToArray() : null;

            Vector4[]? tangents = null;
            if (am.HasTangentBasis)
            {
                tangents = new Vector4[vc];
                for (int v = 0; v < vc; v++)
                {
                    var tv = am.Tangents[v];
                    var bv = am.BiTangents[v];
                    var nv = am.HasNormals ? am.Normals[v] : Vector3.UnitZ;
                    // MikkTSpace bitangent sign: w = sign(dot(cross(n, t), b)).
                    float sign = Vector3.Dot(Vector3.Cross(nv, tv), bv) < 0f ? -1f : 1f;
                    tangents[v] = new Vector4(tv, sign);
                }
            }

            Vector2[]? uv0 = ReadUv(am, 0, vc);
            Vector2[]? uv1 = ReadUv(am, 1, vc);

            Vector4[]? colors = null;
            if (am.HasVertexColors(0))
            {
                // VertexColorChannels[k] is List<Vector4> in AssimpNetter.
                colors = am.VertexColorChannels[0].ToArray();
            }

            // Mesh-level material binding via SceneMeshSubset[1] over the whole index range.
            // (A SceneMeshPayload covers one Assimp mesh, which is already split per-material
            // on import, so there are no subsets to emit.)
            IReadOnlyList<SceneMeshSubset> subsets = Array.Empty<SceneMeshSubset>();
            if (am.MaterialIndex >= 0 && am.MaterialIndex < materials.Length)
            {
                var matPath = materials[am.MaterialIndex].SourcePath;
                subsets = new[] { new SceneMeshSubset("__bound", 0, indices.Length, matPath) };
            }

            result[i] = new SceneMeshPayload
            {
                Name = string.IsNullOrEmpty(am.Name) ? $"Mesh_{i}" : am.Name,
                Positions = positions,
                Indices = indices,
                Normals = normals,
                Tangents = tangents,
                Uv0 = uv0,
                Uv1 = uv1,
                Colors = colors,
                Subsets = subsets,
                LocalBounds = SceneBounds.FromPositions(positions),
                Morphs = Morphs(am),
            };
        }
        return result;
    }

    // A mesh's morph targets as how far each vertex moves, since Assimp gives each target's
    // vertices where they end up rather than how far they go.
    private static SceneMorphTarget[] Morphs(A.Mesh am)
    {
        if (am.MeshAnimationAttachments.Count == 0) return [];
        var targets = new SceneMorphTarget[am.MeshAnimationAttachments.Count];
        for (int t = 0; t < targets.Length; t++)
        {
            var target = am.MeshAnimationAttachments[t];
            var positions = new Vector3[am.VertexCount];
            Vector3[]? normals = target.HasNormals && am.HasNormals ? new Vector3[am.VertexCount] : null;
            for (int v = 0; v < am.VertexCount && v < target.VertexCount; v++)
            {
                positions[v] = target.Vertices[v] - am.Vertices[v];
                if (normals is not null) normals[v] = target.Normals[v] - am.Normals[v];
            }
            targets[t] = new SceneMorphTarget(string.IsNullOrEmpty(target.Name) ? t.ToString(System.Globalization.CultureInfo.InvariantCulture) : target.Name,
                positions, normals, target.Weight);
        }
        return targets;
    }

    private static SceneMeshPayload EmptyMesh(string name) => new()
    {
        Name = string.IsNullOrEmpty(name) ? "Mesh" : name,
        Positions = Array.Empty<Vector3>(),
        Indices = Array.Empty<int>(),
    };

    private static Vector2[]? ReadUv(A.Mesh m, int channel, int vc)
    {
        if (!m.HasTextureCoords(channel)) return null;
        var src = m.TextureCoordinateChannels[channel];
        var dst = new Vector2[vc];
        for (int v = 0; v < vc; v++) dst[v] = new Vector2(src[v].X, src[v].Y);
        return dst;
    }

    // -- Skeletons / skinning

    /// <summary>
    /// Builds one <see cref="SceneSkeletonPayload"/> per <c>aiMesh</c> that has bones,
    /// keyed by a synthesized source-path so <see cref="SceneSkinPayload.SkeletonPath"/>
    /// can reference it. Different aiMeshes may share the same bone set; this keeps the
    /// implementation simple at the cost of duplicate skeletons in that case.
    /// </summary>
    private static Dictionary<string, SceneSkeletonPayload> BuildSkeletons(A.Scene aScene)
    {
        var skeletons = new Dictionary<string, SceneSkeletonPayload>(StringComparer.Ordinal);
        for (int i = 0; i < aScene.MeshCount; i++)
        {
            var am = aScene.Meshes[i];
            if (!am.HasBones) continue;

            int n = am.BoneCount;
            var names = new string[n];
            var ibms = new Matrix4x4[n];
            var parents = new int[n];
            for (int b = 0; b < n; b++)
            {
                var bone = am.Bones[b];
                names[b] = bone.Name ?? $"Bone_{b}";
                ibms[b] = FromAssimp(bone.OffsetMatrix);
                parents[b] = -1; // populated below
            }

            // Resolve parent indices by name lookup against the aiNode tree.
            var nameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int b = 0; b < n; b++) nameToIndex[names[b]] = b;
            for (int b = 0; b < n; b++)
            {
                var nodeForBone = aScene.RootNode.FindNode(names[b]);
                var parent = nodeForBone?.Parent;
                while (parent is not null)
                {
                    if (nameToIndex.TryGetValue(parent.Name, out var pIdx))
                    {
                        parents[b] = pIdx;
                        break;
                    }
                    parent = parent.Parent;
                }
            }

            var skelPath = $"/Skeletons/{(string.IsNullOrEmpty(am.Name) ? $"Mesh_{i}" : am.Name)}";
            skeletons[skelPath] = new SceneSkeletonPayload
            {
                Name = $"{(string.IsNullOrEmpty(am.Name) ? $"Mesh_{i}" : am.Name)}_Skeleton",
                JointNames = names,
                ParentIndices = parents,
                InverseBindMatrices = ibms,
            };
        }
        return skeletons;
    }

    private static SceneSkinPayload? BuildSkin(A.Mesh am, int meshIndex, IReadOnlyDictionary<string, SceneSkeletonPayload> skeletons)
    {
        if (!am.HasBones) return null;
        var skelPath = $"/Skeletons/{(string.IsNullOrEmpty(am.Name) ? $"Mesh_{meshIndex}" : am.Name)}";
        if (!skeletons.ContainsKey(skelPath)) return null;

        int vc = am.VertexCount;
        var idx = new ushort[vc * 4];
        var wts = new float[vc * 4];
        var counts = new byte[vc];

        for (int b = 0; b < am.BoneCount; b++)
        {
            var bone = am.Bones[b];
            for (int wIdx = 0; wIdx < bone.VertexWeightCount; wIdx++)
            {
                var vw = bone.VertexWeights[wIdx];
                int v = vw.VertexID;
                if (v < 0 || v >= vc) continue;
                int slot = counts[v];
                if (slot >= 4) continue; // The LimitBoneWeights step keeps a vertex to four.
                idx[v * 4 + slot] = (ushort)b;
                wts[v * 4 + slot] = vw.Weight;
                counts[v] = (byte)(slot + 1);
            }
        }

        // Per-vertex weights are renormalized, since source files in the wild rarely sum exactly
        // to 1.
        for (int v = 0; v < vc; v++)
        {
            float sum = wts[v * 4] + wts[v * 4 + 1] + wts[v * 4 + 2] + wts[v * 4 + 3];
            if (sum <= 0f) continue;
            float inv = 1f / sum;
            wts[v * 4]     *= inv;
            wts[v * 4 + 1] *= inv;
            wts[v * 4 + 2] *= inv;
            wts[v * 4 + 3] *= inv;
        }

        return new SceneSkinPayload
        {
            SkeletonPath = skelPath,
            JointIndices = idx,
            JointWeights = wts,
        };
    }
}
