using System.Numerics;
using A = Assimp;

namespace Engine;

/// <summary>
/// <see cref="ISceneReader"/> backed by AssimpNetter. Imports any of the ~40 file
/// formats the native Assimp library recognises (FBX, OBJ, COLLADA, 3DS, BLEND, PLY,
/// STL, X, MD2/3/5, IFC, ...) and emits a backend-agnostic <see cref="Scene"/> snapshot
/// in the payloads every reader uses, so the spawn systems after it do not depend on the
/// importer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate / unit policy:</b> the reader
/// records <see cref="Scene.SourceCoordinateSystem"/> + <see cref="Scene.SourceMetersPerUnit"/>
/// from the source metadata where available (<c>aiMetadata</c> "UpAxis" / "UnitScaleFactor"
/// for FBX, <c>up_axis</c> for COLLADA). It does <i>not</i> rotate or rescale vertex data;
/// spawn systems apply a single root-level basis-change matrix.
/// </para>
/// <para>
/// <b>Files:</b> Assimp opens nothing itself. Every file it asks for, the model and those beside
/// it (an OBJ's <c>.mtl</c>, a glTF's <c>.bin</c>), is a C# stream that <see cref="AssimpFiles"/>
/// hands it, from the folder of a path or from the asset reader the model came from, so no native
/// handle is open for a child process to inherit and a model in any reader has its siblings.
/// </para>
/// <para>
/// <b>Coverage (v1):</b> meshes (triangulated, normals, tangents, primary + secondary
/// UVs, vertex colors, joint indices/weights), per-mesh material binding, basic PBR
/// material factors mapped from Assimp's <c>aiMaterial</c> property bag (diffuse,
/// metallic, roughness, normal, emissive and occlusion, as
/// <see cref="SceneMaterialPayload"/> holds them). Skeletons are extracted from
/// <see cref="A.Mesh.Bones"/> into <see cref="SceneSkeletonPayload"/>; animations
/// (<see cref="A.Scene.Animations"/>) are sampled into <see cref="SceneAnimationPayload"/>.
/// Cameras (<see cref="A.Scene.Cameras"/>) and lights (<see cref="A.Scene.Lights"/>) are
/// translated to <see cref="SceneCameraPayload"/> / <see cref="SceneLightPayload"/>.
/// </para>
/// <para>
/// <b>Trimming:</b> a game published trimmed or native is told that the AssimpNetter assembly
/// produced trim and AOT analysis warnings (IL2104, IL3053). They are the package's own, from
/// code inside it that binds Assimp's native functions to delegates and marshals its structures
/// by reflection, and this reader is where the engine calls it. A native game reads its models through it as the workflow's native run shows
/// (<c>build/play-native.sh</c>), and the warnings stand where the publish prints them.
/// </para>
/// </remarks>
internal sealed class AssimpModelReader : ISceneReader
{
    private static readonly ILogger Logger = Log.Category("Engine.Models.Assimp");

    /// <inheritdoc />
    public string[] Extensions => _extensions ??= ResolveExtensions();
    private string[]? _extensions;

    /// <inheritdoc />
    public string FormatId => "assimp";

    /// <summary>
    /// Imports a model from a file on disk, with the files it names beside it (an OBJ's
    /// <c>.mtl</c>, a glTF's <c>.bin</c>) read from its folder. The flat API's <c>LoadModel</c>
    /// has the path and uses this.
    /// </summary>
    internal Scene ReadFile(string path, SceneImportSettings settings, CancellationToken ct = default)
    {
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? "";
        var name = System.IO.Path.GetFileName(path);
        using var context = new AssetLoadContext(Stream.Null, new AssetPath(name), _ => default);
        // Assimp is given the file's name alone, and the folder is added here, so a path with
        // letters outside ASCII never passes through native code as a string.
        return Import(name, new AssimpFiles(file => AssimpFiles.OnDisk(directory, file)), context, settings, ct);
    }

    private static Scene Import(string path, AssimpFiles files, AssetLoadContext context, SceneImportSettings settings, CancellationToken ct)
    {
        using var _ = files;
        using var importer = new A.AssimpContext();
        importer.SetIOSystem(files);
        // A joint that weighs no vertex, as a socket a sword hangs from, is kept among the bones,
        // as raylib keeps every joint of a glTF skin, where Assimp would leave it out.
        importer.SetConfig(new A.Configs.RemoveEmptyBonesConfig(false));

        const A.PostProcessSteps Steps =
            A.PostProcessSteps.Triangulate
            | A.PostProcessSteps.GenerateSmoothNormals
            | A.PostProcessSteps.CalculateTangentSpace
            | A.PostProcessSteps.JoinIdenticalVertices
            | A.PostProcessSteps.ImproveCacheLocality
            | A.PostProcessSteps.LimitBoneWeights      // clamp to 4 influences/vertex
            | A.PostProcessSteps.GenerateUVCoords
            | A.PostProcessSteps.SortByPrimitiveType
            | A.PostProcessSteps.RemoveRedundantMaterials
            | A.PostProcessSteps.FindInvalidData
            // Assimp counts V up from an image's bottom, as OBJ does, and turns glTF's over to
            // match. The engine samples V down from the top row, as glTF and raylib count it.
            | A.PostProcessSteps.FlipUVs;

        // A file the reader failed to give is the reason, ahead of what Assimp made of its absence.
        A.Scene? aScene;
        try
        {
            aScene = importer.ImportFile(path, Steps);
        }
        catch (A.AssimpException)
        {
            files.ThrowIfFailed();
            throw;
        }
        files.ThrowIfFailed();
        if (aScene is null || aScene.RootNode is null)
            throw new InvalidOperationException($"AssimpModelReader: ImportFile returned null for '{context.Path}'.");

        return BuildScene(aScene, context, settings, ct);
    }

    public Task<Scene> ReadAsync(AssetLoadContext context, SceneImportSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        // Assimp opens a model more than once, first to tell its format and then to read it. A
        // stream that can seek is read where it is, each open at a place of its own in it, and one
        // that cannot is read into memory once. The files beside it come from the reader it came
        // from, so a model under any reader has its .mtl and .bin.
        var stream = context.GetStream();
        var shared = stream.CanSeek ? stream : InMemory(stream);
        try
        {
            var files = new AssimpFiles(file => Seekable(context.OpenFromSource(new AssetPath(file))))
            {
                SharedName = context.Path.Path,
                Shared = shared,
            };
            return Task.FromResult(Import(context.Path.Path, files, context, settings, ct));
        }
        finally
        {
            if (shared != stream) shared.Dispose();
        }
    }

    private static MemoryStream InMemory(Stream stream)
    {
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    // Assimp seeks in what it reads, which a reader's stream need not allow.
    private static Stream? Seekable(Stream? stream)
    {
        if (stream is null || stream.CanSeek) return stream;
        using (stream) return InMemory(stream);
    }

    // -- aiScene → Scene

    private static Scene BuildScene(A.Scene aScene, AssetLoadContext context, SceneImportSettings settings, CancellationToken ct)
    {
        var (upAxis, mpu) = ReadSourceBasis(aScene);

        var scene = new Scene
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(context.Path.Path),
            SourceCoordinateSystem = upAxis,
            SourceMetersPerUnit = mpu,
        };

        // Pre-pass: convert materials so each mesh-binding looks up the same shared payload.
        // An OBJ's dissolve (d) is left unread, as raylib's loader leaves it, so a file whose
        // material says d 0, as raylib's character.obj does, draws as it does in raylib.
        var readsOpacity = !string.Equals(System.IO.Path.GetExtension(context.Path.Path), ".obj", StringComparison.OrdinalIgnoreCase);
        var materials = settings.LoadPayloads.HasFlag(LoadPayloads.Materials)
            ? BuildMaterials(aScene, readsOpacity)
            : Array.Empty<SceneMaterialPayload>();

        // The meshes are converted once, first, since aiNode.MeshIndices names them by index.
        var meshes = settings.LoadPayloads.HasFlag(LoadPayloads.Meshes)
            ? BuildMeshes(aScene, materials, ct)
            : Array.Empty<SceneMeshPayload>();

        // Pre-pass: skeletons keyed by source-path of the bone-root node, so a SceneSkinPayload
        // can reference its skeleton by SourcePath without a second pass.
        var skeletons = settings.LoadPayloads.HasFlag(LoadPayloads.Meshes) // skinning is mesh-adjacent
            ? BuildSkeletons(aScene)
            : new Dictionary<string, SceneSkeletonPayload>(StringComparer.Ordinal);

        // Convert nodes recursively.
        var rootNode = ConvertNode(aScene.RootNode, "/", aScene, meshes, materials, skeletons, settings, ct);
        if (rootNode is not null)
        {
            // Assimp wraps everything in a synthetic root ("RootNode" by default). Promote
            // its children to scene roots when the root itself carries no payload, mirroring
            // the conventional handling for FBX / glTF imports.
            if (rootNode.Components.Count == 0 && rootNode.LocalTransform.Position == Vector3.Zero
                && rootNode.LocalTransform.Rotation == Quaternion.Identity
                && rootNode.LocalTransform.Scale == Vector3.One)
            {
                foreach (var child in rootNode.Children) scene.Roots.Add(child);
            }
            else
            {
                scene.Roots.Add(rootNode);
            }
        }

        // Every embedded image is kept, in Assimp's order, because materials name them by index.
        if (aScene.HasTextures)
            foreach (var texture in aScene.Textures)
                scene.EmbeddedTextures.Add(ConvertEmbedded(texture));

        // Animations, a clip per aiAnimation, attached to the scene root, whose channels name
        // the nodes they move. LoadPayloads has no flag of their own, so they are read when the
        // source authored any clips.
        if (aScene.AnimationCount > 0)
        {
            // The clips ride the first root, where Scene.Traverse finds them.
            if (scene.Roots.Count == 0)
            {
                scene.Roots.Add(new SceneNode { Name = "Root", SourcePath = "/" });
            }
            foreach (var anim in aScene.Animations)
            {
                ct.ThrowIfCancellationRequested();
                var clip = ConvertAnimation(anim);
                if (clip is not null) scene.Roots[0].Components.Add(clip);
            }
        }

        LogSummary(context, scene, materials.Length, meshes.Length, skeletons.Count, aScene.AnimationCount);
        return scene;
    }

    // -- Source basis (FBX UpAxis / UnitScaleFactor; COLLADA up_axis)

    private static (SceneCoordinateSystem upAxis, double metersPerUnit) ReadSourceBasis(A.Scene aScene)
    {
        var upAxis = SceneCoordinateSystem.YUp;
        var mpu = 1.0;
        var meta = aScene.Metadata;
        if (meta is null || meta.Count == 0) return (upAxis, mpu);

        if (meta.TryGetValue("UpAxis", out var upEntry) && upEntry.Data is int upInt)
        {
            // FBX convention: 0 = X, 1 = Y, 2 = Z.
            if (upInt == 2) upAxis = SceneCoordinateSystem.ZUp;
        }
        if (meta.TryGetValue("UnitScaleFactor", out var unitEntry) && unitEntry.Data is double unitDouble)
        {
            // FBX authors UnitScaleFactor in centimeters; convert to meters per unit.
            mpu = unitDouble / 100.0;
        }
        return (upAxis, mpu);
    }

    // -- Materials

    // Assimp's AI_DEFAULT_MATERIAL_NAME, the name of the material it adds to a file that has none.
    private const string AssimpDefaultMaterial = "DefaultMaterial";

    private static SceneMaterialPayload[] BuildMaterials(A.Scene aScene, bool readsOpacity)
    {
        if (aScene.MaterialCount == 0) return Array.Empty<SceneMaterialPayload>();
        var result = new SceneMaterialPayload[aScene.MaterialCount];
        for (int i = 0; i < aScene.MaterialCount; i++)
        {
            var m = aScene.Materials[i];
            var name = string.IsNullOrEmpty(m.Name) ? $"Material_{i}" : m.Name;

            // AssimpNetter exposes colors as System.Numerics.Vector4 directly. PBR-aware files
            // (glTF imported via Assimp, FBX from PBR exporters) populate metallic/roughness/
            // emissive too via $mat.* properties.
            // The material Assimp adds to a file that names none, as an OBJ with no MTL, is gray
            // (0.6), and raylib's default for such a file is white, so the texture a program sets
            // on it shows its own colors.
            var diffuse = m.HasColorDiffuse && m.Name != AssimpDefaultMaterial ? m.ColorDiffuse : Vector4.One;
            // glTF's own factors where the file has them. A Phong material has no metal, and its
            // shininess (0 to 1000) stands in for smoothness.
            float metallic = TryGetFloat(m, "$mat.metallicFactor", 0f);
            float roughness = TryGetFloat(m, "$mat.roughnessFactor", float.NaN);
            if (float.IsNaN(roughness))
                roughness = m.HasShininess ? 1f - MathF.Min(1f, MathF.Max(0f, m.Shininess) / 1000f) : 1f;

            var emissive = m.HasColorEmissive
                ? new Vector3(m.ColorEmissive.X, m.ColorEmissive.Y, m.ColorEmissive.Z)
                : Vector3.Zero;

            SceneTextureRef? baseTex = TryGetTexture(m, A.TextureType.Diffuse) ?? TryGetTexture(m, A.TextureType.BaseColor);
            // glTF's packed map, which Assimp lists as metalness, as roughness, or in older
            // versions as unknown. A Phong specular map is not one, since its channels mean
            // something else.
            SceneTextureRef? mrTex = TryGetTexture(m, A.TextureType.Metalness)
                                     ?? TryGetTexture(m, A.TextureType.Roughness)
                                     ?? TryGetTexture(m, A.TextureType.Unknown);
            SceneTextureRef? normalTex = TryGetTexture(m, A.TextureType.Normals) ?? TryGetTexture(m, A.TextureType.Height);
            SceneTextureRef? emissiveTex = TryGetTexture(m, A.TextureType.Emissive);
            SceneTextureRef? occlusionTex = TryGetTexture(m, A.TextureType.AmbientOcclusion) ?? TryGetTexture(m, A.TextureType.Lightmap);

            // glTF says how its alpha is meant, which Assimp passes on as material keys. A format
            // without them blends when its opacity is below one.
            var alphaMode = m.GetNonTextureProperty("$mat.gltf.alphaMode")?.GetStringValue() switch
            {
                "MASK" => SceneAlphaMode.Mask,
                "BLEND" => SceneAlphaMode.Blend,
                "OPAQUE" => SceneAlphaMode.Opaque,
                _ => readsOpacity && m.HasOpacity && m.Opacity < 1f ? SceneAlphaMode.Blend : SceneAlphaMode.Opaque,
            };
            var alphaCutoff = m.GetNonTextureProperty("$mat.gltf.alphaCutoff") is { } cutoff ? cutoff.GetFloatValue() : 0.5f;
            var opacity = readsOpacity && m.HasOpacity ? m.Opacity : 1f;
            diffuse.W = opacity * diffuse.W;

            // A format that does not say, as OBJ, draws both sides, as the engine always did. The
            // flag is read from its bytes, since glTF's importer stores it in a width IsTwoSided
            // misreads as false.
            bool doubleSided = m.GetNonTextureProperty("$mat.twosided") is not { } twoSided
                               || twoSided.RawData is not { } raw || raw.Any(b => b != 0);

            result[i] = new SceneMaterialPayload
            {
                Name = name,
                SourcePath = $"/Materials/{name}#{i}",
                BaseColorFactor = diffuse,
                BaseColorTexture = baseTex,
                MetallicFactor = metallic,
                RoughnessFactor = roughness,
                MetallicRoughnessTexture = mrTex,
                NormalTexture = normalTex,
                EmissiveFactor = emissive,
                EmissiveTexture = emissiveTex,
                OcclusionTexture = occlusionTex,
                AlphaMode = alphaMode,
                AlphaCutoff = alphaCutoff,
                DoubleSided = doubleSided,
            };
        }
        return result;
    }

    private static SceneEmbeddedTexture ConvertEmbedded(A.EmbeddedTexture texture)
    {
        if (texture.IsCompressed)
            return new SceneEmbeddedTexture(texture.Filename, texture.CompressedData, texture.CompressedFormatHint ?? "", 0, 0, null);

        // Assimp stores raw pixels as BGRA.
        var texels = texture.NonCompressedData;
        var rgba = new byte[texels.Length * 4];
        for (int i = 0; i < texels.Length; i++)
        {
            rgba[i * 4] = texels[i].R;
            rgba[i * 4 + 1] = texels[i].G;
            rgba[i * 4 + 2] = texels[i].B;
            rgba[i * 4 + 3] = texels[i].A;
        }
        return new SceneEmbeddedTexture(texture.Filename, null, "", texture.Width, texture.Height, rgba);
    }

    private static SceneTextureRef? TryGetTexture(A.Material m, A.TextureType type)
    {
        if (m.GetMaterialTextureCount(type) == 0) return null;
        if (!m.GetMaterialTexture(type, 0, out var slot)) return null;
        if (string.IsNullOrEmpty(slot.FilePath)) return null;
        return new SceneTextureRef(
            AssetPath: slot.FilePath,
            UvSet: slot.UVIndex,
            WrapS: ConvertWrap(slot.WrapModeU),
            WrapT: ConvertWrap(slot.WrapModeV));
    }

    private static SceneWrapMode ConvertWrap(A.TextureWrapMode mode) => mode switch
    {
        A.TextureWrapMode.Wrap   => SceneWrapMode.Repeat,
        A.TextureWrapMode.Clamp  => SceneWrapMode.Clamp,
        A.TextureWrapMode.Mirror => SceneWrapMode.Mirror,
        A.TextureWrapMode.Decal  => SceneWrapMode.Black,
        _                        => SceneWrapMode.Repeat,
    };

    private static float TryGetFloat(A.Material m, string key, float fallback)
    {
        var prop = m.GetNonTextureProperty(key);
        if (prop is null) return fallback;
        try { return prop.GetFloatValue(); }
        catch { return fallback; }
    }

    private static bool TryGetBool(A.Material m, string baseName, A.TextureType texType, int texIndex, bool fallback)
    {
        var prop = m.GetProperty(baseName, texType, texIndex);
        if (prop is null) return fallback;
        try { return prop.GetIntegerValue() != 0; }
        catch { return fallback; }
    }

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

        // Per-vertex weights are renormalised, since source files in the wild rarely sum exactly
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

    // -- Nodes

    private static SceneNode? ConvertNode(
        A.Node aNode,
        string parentPath,
        A.Scene aScene,
        SceneMeshPayload[] meshes,
        SceneMaterialPayload[] materials,
        IReadOnlyDictionary<string, SceneSkeletonPayload> skeletons,
        SceneImportSettings settings,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (aNode is null) return null;

        var name = string.IsNullOrEmpty(aNode.Name) ? "Node" : aNode.Name;
        var path = parentPath.EndsWith('/') ? parentPath + name : parentPath + "/" + name;

        var node = new SceneNode
        {
            Name = name,
            SourcePath = path,
            LocalTransform = DecomposeMatrix(FromAssimp(aNode.Transform)),
            Purpose = ScenePurpose.Default,
            Enabled = true,
        };

        // Attach all meshes referenced by this aiNode (an aiNode typically references
        // exactly one mesh after SortByPrimitiveType, but multi-material legacy formats
        // can reference several).
        if (settings.LoadPayloads.HasFlag(LoadPayloads.Meshes) && aNode.HasMeshes)
        {
            foreach (int meshIndex in aNode.MeshIndices)
            {
                if (meshIndex < 0 || meshIndex >= meshes.Length) continue;
                var mesh = meshes[meshIndex];
                node.Components.Add(mesh);

                // Also attach the bound material payload(s) so spawners see them on the same node.
                if (settings.LoadPayloads.HasFlag(LoadPayloads.Materials))
                {
                    var am = aScene.Meshes[meshIndex];
                    if (am.MaterialIndex >= 0 && am.MaterialIndex < materials.Length)
                        node.Components.Add(materials[am.MaterialIndex]);
                }

                // Skinning: attach the SceneSkinPayload alongside the mesh.
                var am2 = aScene.Meshes[meshIndex];
                var skin = BuildSkin(am2, meshIndex, skeletons);
                if (skin is not null)
                {
                    node.Components.Add(skin);
                    if (skeletons.TryGetValue(skin.SkeletonPath, out var skel))
                        node.Components.Add(skel);
                }
            }
        }

        // Camera / light: aiCamera/aiLight reference an aiNode by name.
        if (settings.LoadPayloads.HasFlag(LoadPayloads.Cameras))
        {
            for (int i = 0; i < aScene.CameraCount; i++)
            {
                if (aScene.Cameras[i].Name == name)
                {
                    node.Components.Add(ConvertCamera(aScene.Cameras[i]));
                    break;
                }
            }
        }
        if (settings.LoadPayloads.HasFlag(LoadPayloads.Lights))
        {
            for (int i = 0; i < aScene.LightCount; i++)
            {
                if (aScene.Lights[i].Name == name)
                {
                    node.Components.Add(ConvertLight(aScene.Lights[i]));
                    break;
                }
            }
        }

        if (aNode.HasChildren)
        {
            foreach (var child in aNode.Children)
            {
                var childNode = ConvertNode(child, path, aScene, meshes, materials, skeletons, settings, ct);
                if (childNode is not null) node.Children.Add(childNode);
            }
        }

        return node;
    }

    // -- Cameras / lights

    private static SceneCameraPayload ConvertCamera(A.Camera c)
    {
        // Assimp camera units: HorizontalFOV in radians; aspect; clip planes.
        // Convert to a synthesised aperture / focal length so the payload round-trips
        // through the existing UsdPreviewSurface-shaped fields.
        const float ReferenceVertAperture = 15.2908f; // 35mm Academy
        float hfov = c.FieldOfview > 0f ? c.FieldOfview : MathF.PI / 3f;
        float aspect = c.AspectRatio > 0f ? c.AspectRatio : 16f / 9f;
        float vfov = 2f * MathF.Atan(MathF.Tan(hfov * 0.5f) / aspect);
        float focalLength = ReferenceVertAperture / (2f * MathF.Tan(vfov * 0.5f));
        return new SceneCameraPayload
        {
            Name = string.IsNullOrEmpty(c.Name) ? "Camera" : c.Name,
            Projection = SceneProjection.Perspective,
            HorizontalAperture = ReferenceVertAperture * aspect,
            VerticalAperture = ReferenceVertAperture,
            FocalLength = focalLength,
            NearClip = c.ClipPlaneNear,
            FarClip = c.ClipPlaneFar,
        };
    }

    private static SceneLightPayload ConvertLight(A.Light l)
    {
        var spot = l.LightType == A.LightSourceType.Spot;
        return new SceneLightPayload
        {
            Name = string.IsNullOrEmpty(l.Name) ? "Light" : l.Name,
            // An area light has no counterpart in the model pass, so it lights as a point.
            Kind = l.LightType switch
            {
                A.LightSourceType.Directional => LightKind.Directional,
                A.LightSourceType.Spot => LightKind.Spot,
                A.LightSourceType.Ambient => LightKind.Ambient,
                _ => LightKind.Point,
            },
            Color = l.ColorDiffuse,
            Intensity = 1f,
            // Assimp gives a spot's cone angles from its axis in radians, as glTF writes them.
            InnerAngle = spot ? float.RadiansToDegrees(l.AngleInnerCone) : 25f,
            OuterAngle = spot ? float.RadiansToDegrees(l.AngleOuterCone) : 30f,
        };
    }

    // -- Animations

    private static SceneAnimationPayload? ConvertAnimation(A.Animation anim)
    {
        if (anim.NodeAnimationChannelCount == 0 && anim.MeshMorphAnimationChannelCount == 0) return null;

        double tps = anim.TicksPerSecond > 0.0 ? anim.TicksPerSecond : 25.0;
        double durTicks = anim.DurationInTicks;
        var channels = new List<SceneAnimationChannel>(anim.NodeAnimationChannelCount * 3);

        // A morph channel keys every target of a node's mesh at once, each key naming targets and
        // their weights, which become a channel per target, its weight in x.
        foreach (var ch in anim.MeshMorphAnimationChannels)
        {
            var byTarget = new SortedDictionary<int, List<(float Time, float Weight)>>();
            foreach (var key in ch.MeshMorphKeys)
                for (int v = 0; v < key.Values.Count && v < key.Weights.Count; v++)
                {
                    if (!byTarget.TryGetValue(key.Values[v], out var keys)) byTarget[key.Values[v]] = keys = [];
                    keys.Add(((float)(key.Time / tps), (float)key.Weights[v]));
                }
            foreach (var (target, keys) in byTarget)
                channels.Add(new SceneAnimationChannel
                {
                    TargetNodePath = "/" + ch.Name,
                    Property = SceneAnimationProperty.MorphWeight,
                    TimesSeconds = [.. keys.Select(k => k.Time)],
                    Values = [.. keys.Select(k => new Vector4(k.Weight, 0, 0, 0))],
                    MorphTarget = target,
                });
        }

        foreach (var ch in anim.NodeAnimationChannels)
        {
            string targetPath = "/" + ch.NodeName; // resolved by name; spawner does the lookup

            if (ch.PositionKeyCount > 0)
            {
                var times = new float[ch.PositionKeyCount];
                var values = new Vector4[ch.PositionKeyCount];
                for (int k = 0; k < ch.PositionKeyCount; k++)
                {
                    var key = ch.PositionKeys[k];
                    times[k] = (float)(key.Time / tps);
                    values[k] = new Vector4(key.Value.X, key.Value.Y, key.Value.Z, 0f);
                }
                channels.Add(new SceneAnimationChannel
                {
                    TargetNodePath = targetPath,
                    Property = SceneAnimationProperty.Translation,
                    TimesSeconds = times,
                    Values = values,
                });
            }

            if (ch.RotationKeyCount > 0)
            {
                var times = new float[ch.RotationKeyCount];
                var values = new Vector4[ch.RotationKeyCount];
                for (int k = 0; k < ch.RotationKeyCount; k++)
                {
                    var key = ch.RotationKeys[k];
                    times[k] = (float)(key.Time / tps);
                    values[k] = new Vector4(key.Value.X, key.Value.Y, key.Value.Z, key.Value.W);
                }
                channels.Add(new SceneAnimationChannel
                {
                    TargetNodePath = targetPath,
                    Property = SceneAnimationProperty.Rotation,
                    TimesSeconds = times,
                    Values = values,
                });
            }

            if (ch.ScalingKeyCount > 0)
            {
                var times = new float[ch.ScalingKeyCount];
                var values = new Vector4[ch.ScalingKeyCount];
                for (int k = 0; k < ch.ScalingKeyCount; k++)
                {
                    var key = ch.ScalingKeys[k];
                    times[k] = (float)(key.Time / tps);
                    values[k] = new Vector4(key.Value.X, key.Value.Y, key.Value.Z, 0f);
                }
                channels.Add(new SceneAnimationChannel
                {
                    TargetNodePath = targetPath,
                    Property = SceneAnimationProperty.Scale,
                    TimesSeconds = times,
                    Values = values,
                });
            }
        }

        return new SceneAnimationPayload
        {
            Name = string.IsNullOrEmpty(anim.Name) ? "Animation" : anim.Name,
            DurationSeconds = (float)(durTicks / tps),
            Channels = channels,
        };
    }

    // -- Math helpers

    // AssimpNetter hands over Assimp's matrices as they are, which act on column vectors with the
    // translation in the last column. System.Numerics acts on row vectors with it in the last row,
    // so each is transposed once here. Read as they came, every node's translation was zero.
    private static Matrix4x4 FromAssimp(Matrix4x4 m) => Matrix4x4.Transpose(m);

    private static Transform DecomposeMatrix(Matrix4x4 m)
    {
        if (!Matrix4x4.Decompose(m, out var scale, out var rotation, out var translation))
        {
            translation = m.Translation;
            rotation = Quaternion.Identity;
            scale = Vector3.One;
        }
        return new Transform { Position = translation, Rotation = rotation, Scale = scale };
    }

    private static string[] ResolveExtensions()
    {
        try
        {
            using var ctx = new A.AssimpContext();
            var raw = ctx.GetSupportedImportFormats();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in raw)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                var e = r.TrimStart('*');
                if (!e.StartsWith('.')) e = "." + e;
                if (e is ".usd" or ".usda" or ".usdc" or ".usdz") continue;
                set.Add(e);
            }
            return set.ToArray();
        }
        catch
        {
            return new[] { ".fbx", ".obj", ".dae", ".3ds", ".blend", ".ply", ".stl", ".x" };
        }
    }

    // -- Logging

    private static void LogSummary(AssetLoadContext context, Scene scene, int mats, int meshes, int skels, int anims)
    {
        int nodes = 0, attached = 0;
        foreach (var r in scene.Roots) Tally(r);

        Logger.Info(
            $"AssimpModelReader: '{context.Path}' parsed, upAxis={scene.SourceCoordinateSystem}, " +
            $"mpu={scene.SourceMetersPerUnit:0.###}, roots={scene.Roots.Count}, nodes={nodes}, " +
            $"meshes={meshes}, materials={mats}, skeletons={skels}, animations={anims}, payloads={attached}.");

        void Tally(SceneNode n)
        {
            nodes++;
            attached += n.Components.Count;
            foreach (var c in n.Children) Tally(c);
        }
    }
}