using System.Numerics;
using System.Runtime.InteropServices;
using StbImageSharp;

namespace Engine;

public static partial class Engine3D
{
    private static MeshStore Meshes => Res<MeshStore>();

    // -- Meshes

    /// <summary>Uploads vertices and triangle indices as a mesh.</summary>
    /// <exception cref="ArgumentException">The indices are not whole triangles, or one is out of range.</exception>
    public static ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices) => UploadMesh(vertices, indices, null, null);

    /// <summary>
    /// Uploads vertices and triangle indices as a mesh, with a color at each vertex and a second
    /// texture coordinate at each, either of which may be null, as raylib's mesh has its
    /// <c>colors</c> and <c>texcoords2</c>.
    /// </summary>
    /// <remarks>
    /// The colors are sRGB, as every color is, and multiply the material's color and texture. The
    /// second texture coordinates are read by a shader of the program's own, as a lightmap's are.
    /// Each is a buffer of its own beside the vertices, a mesh given one carries the other at its
    /// default, white or zero, and a mesh given neither is drawn from its vertices alone.
    /// </remarks>
    /// <exception cref="ArgumentException">The indices are not whole triangles, one is out of range, or the colors or coordinates are not one a vertex.</exception>
    public static ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices, Color[]? colors, Vector2[]? texcoords2)
    {
        var positions = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) positions[i] = vertices[i].Position;
        var id = Meshes.Add(vertices, indices, colors is null && texcoords2 is null ? null : new MeshStore.Streams(colors, texcoords2));
        return new ModelMesh(id, vertices.Length, indices.Length / 3, BoundingBox.Around(positions));
    }

    /// <summary>
    /// Replaces a mesh's vertices with as many new ones, keeping its triangles, as raylib's
    /// <c>UpdateMeshBuffer</c> does for the vertex arrays.
    /// </summary>
    /// <remarks>The mesh's bounds are those it was made with.</remarks>
    /// <exception cref="ArgumentException">The count differs from the mesh's.</exception>
    public static void UpdateMeshVertices(ModelMesh mesh, ModelVertex[] vertices)
    {
        if (mesh.IsValid) Meshes.UpdateVertices(mesh.Id, vertices);
    }

    /// <summary>
    /// Writes <paramref name="data"/> into one of a mesh's arrays, starting <paramref name="offset"/>
    /// bytes in, by raylib's index for it: 0 its positions, 1 its texture coordinates and 2 its
    /// normals, as floats, 3 its colors, four bytes each, and 5 its second texture coordinates.
    /// </summary>
    /// <remarks>
    /// A mesh keeps its position, normal and texture coordinate together in one vertex, which the
    /// write changes in place, keeping the bounds it was made with, as <see cref="UpdateMeshVertices"/>
    /// does. Writing colors or second texture coordinates to a mesh without them gives it both, the
    /// rest of each white or zero, where raylib's mesh has no buffer to write into until rlgl loads
    /// one, as its lightmap example does. Tangents (4) and the indices (6) are not kept, so a write
    /// to either is left out with a warning, and bytes past the array's end are left out.
    /// </remarks>
    public static void UpdateMeshBuffer<T>(ModelMesh mesh, int index, ReadOnlySpan<T> data, int offset) where T : unmanaged
    {
        if (!mesh.IsValid || !Meshes.TryGetData(mesh.Id, out var vertices, out _)) return;
        var bytes = MemoryMarshal.AsBytes(data);
        switch (index)
        {
            case 0 or 1 or 2:
            {
                // Where the array sits in ModelVertex, and how many bytes it has a vertex.
                var (at, size) = index switch { 0 => (0, 12), 1 => (24, 8), _ => (12, 12) };
                var moved = (ModelVertex[])vertices.Clone();
                var array = new byte[moved.Length * size];
                var packed = MemoryMarshal.AsBytes(moved.AsSpan());
                var stride = Marshal.SizeOf<ModelVertex>();
                for (int i = 0; i < moved.Length; i++) packed.Slice(i * stride + at, size).CopyTo(array.AsSpan(i * size));
                if (!Write(array, bytes, offset)) return;
                for (int i = 0; i < moved.Length; i++) array.AsSpan(i * size, size).CopyTo(packed[(i * stride + at)..]);
                Meshes.UpdateVertices(mesh.Id, moved);
                break;
            }
            case 3 or 5:
            {
                var streams = Meshes.StreamsOf(mesh.Id);
                var colors = streams?.Colors is { } c ? (Color[])c.Clone() : Enumerable.Repeat(Color.White, vertices.Length).ToArray();
                var texcoords2 = streams?.Texcoords2 is { } t ? (Vector2[])t.Clone() : new Vector2[vertices.Length];
                var written = index == 3 ? Write(MemoryMarshal.AsBytes(colors.AsSpan()), bytes, offset) : Write(MemoryMarshal.AsBytes(texcoords2.AsSpan()), bytes, offset);
                if (written) Meshes.SetStreams(mesh.Id, new MeshStore.Streams(colors, texcoords2));
                break;
            }
            default:
                ApiLogger.Warn($"UpdateMeshBuffer: a mesh keeps no array {index}, its positions (0), texture coordinates (1), normals (2), colors (3) and second texture coordinates (5) being those it has, so it is left as it is.");
                break;
        }

        static bool Write(Span<byte> array, ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset >= array.Length) return false;
            bytes[..Math.Min(bytes.Length, array.Length - offset)].CopyTo(array[offset..]);
            return true;
        }
    }

    /// <summary>Frees a mesh. Drawing it afterward draws nothing.</summary>
    public static void UnloadMesh(ModelMesh mesh)
    {
        if (mesh.IsValid) Meshes.Remove(mesh.Id);
    }

    /// <summary>Makes a box centered on the origin, with a normal and texture coordinates per face.</summary>
    /// <remarks>
    /// Its corners, their texture coordinates and normals are raylib's, face by face in raylib's
    /// order (front, back, top, bottom, right, left), so a texture lies on each face as it does in
    /// raylib, which puts the first row of an image along a face's lower edge.
    /// </remarks>
    public static ModelMesh GenMeshCube(float width, float height, float length)
    {
        float x = width / 2, y = height / 2, z = length / 2;
        Vector3[] corners =
        [
            new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z),
            new(-x, -y, -z), new(-x, y, -z), new(x, y, -z), new(x, -y, -z),
            new(-x, y, -z), new(-x, y, z), new(x, y, z), new(x, y, -z),
            new(-x, -y, -z), new(x, -y, -z), new(x, -y, z), new(-x, -y, z),
            new(x, -y, -z), new(x, y, -z), new(x, y, z), new(x, -y, z),
            new(-x, -y, -z), new(-x, -y, z), new(-x, y, z), new(-x, y, -z),
        ];
        Vector2[] uvs =
        [
            new(0, 0), new(1, 0), new(1, 1), new(0, 1),
            new(1, 0), new(1, 1), new(0, 1), new(0, 0),
            new(0, 1), new(0, 0), new(1, 0), new(1, 1),
            new(1, 1), new(0, 1), new(0, 0), new(1, 0),
            new(1, 0), new(1, 1), new(0, 1), new(0, 0),
            new(0, 0), new(1, 0), new(1, 1), new(0, 1),
        ];
        Vector3[] normals = [Vector3.UnitZ, -Vector3.UnitZ, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitX, -Vector3.UnitX];

        var vertices = new ModelVertex[24];
        var indices = new uint[36];
        for (int i = 0; i < 24; i++) vertices[i] = new ModelVertex(corners[i], normals[i / 4], uvs[i]);
        for (uint f = 0; f < 6; f++) new[] { 4 * f, 4 * f + 1, 4 * f + 2, 4 * f, 4 * f + 2, 4 * f + 3 }.CopyTo(indices, f * 6);
        return UploadMesh(vertices, indices);
    }

    /// <summary>Makes a sphere centered on the origin, its poles on the Z axis, as raylib's is.</summary>
    /// <remarks>
    /// It is par_shapes' sphere, which raylib draws, so a texture wraps it as it does raylib's.
    /// Fewer than three rings or slices make no mesh, as raylib's.
    /// </remarks>
    public static ModelMesh GenMeshSphere(float radius, int rings, int slices)
    {
        if (rings < 3 || slices < 3) return default;
        var sphere = ParShape.Parametric((u, v) =>
        {
            float phi = (float)(u * ParShape.Pi), theta = (float)(v * 2 * ParShape.Pi);
            return new Vector3(MathF.Cos(theta) * MathF.Sin(phi), MathF.Sin(theta) * MathF.Sin(phi), MathF.Cos(phi));
        }, slices, rings);
        sphere.Scale(radius, radius, radius);
        return sphere.Upload();
    }

    /// <summary>Makes a flat rectangle on the XZ plane, centered on the origin, facing up.</summary>
    public static ModelMesh GenMeshPlane(float width, float length, int resX, int resZ)
    {
        resX = Math.Max(1, resX);
        resZ = Math.Max(1, resZ);
        var vertices = new ModelVertex[(resX + 1) * (resZ + 1)];
        for (int z = 0; z <= resZ; z++)
        for (int x = 0; x <= resX; x++)
        {
            var (u, v) = ((float)x / resX, (float)z / resZ);
            vertices[z * (resX + 1) + x] = new ModelVertex(new Vector3((u - 0.5f) * width, 0, (v - 0.5f) * length), Vector3.UnitY, new Vector2(u, v));
        }

        var indices = new List<uint>(resX * resZ * 6);
        for (int z = 0; z < resZ; z++)
        for (int x = 0; x < resX; x++)
        {
            uint a = (uint)(z * (resX + 1) + x), b = a + (uint)(resX + 1);
            indices.AddRange([a, b, b + 1, a, b + 1, a + 1]);
        }
        return UploadMesh(vertices, [.. indices]);
    }

    // -- Models

    /// <summary>
    /// Loads a model file through Assimp (glTF, FBX, OBJ, COLLADA and about forty more), with each
    /// material's base color and base color texture.
    /// </summary>
    /// <returns>The model, or an empty one when the file cannot be read, with the reason in the log.</returns>
    /// <remarks>
    /// Textures embedded in the file, as a <c>.glb</c> carries them, are decoded from it, and the
    /// rest are looked for beside the model file. Each gets mip levels, since a model is seen from
    /// any distance. MagicaVoxel's <c>.vox</c> files, Inter-Quake Models (<c>.iqm</c>) and Model 3D
    /// files (<c>.m3d</c>) are read as raylib reads them, an IQM with its skeleton, which Assimp
    /// leaves out.
    /// </remarks>
    public static Model LoadModel(string fileName)
    {
        if (ReadModelScene(fileName, "LoadModel") is not { } scene) return new Model();

        var directory = Path.GetDirectoryName(Path.GetFullPath(ResolveFile(fileName)!)) ?? ".";
        var materials = new List<ModelMaterial>();
        var materialIndex = new Dictionary<SceneMaterialPayload, int>();
        var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        var meshes = new List<ModelMesh>();
        var meshMaterial = new List<int>();
        var bones = ModelSkeleton.Bones(scene);
        var boneIndex = bones.Select((b, i) => (b.Name, i)).ToDictionary(x => x.Name, x => x.i, StringComparer.Ordinal);
        var skins = new List<SkinnedMesh>();
        var morphed = new List<(int Mesh, SceneMeshPayload Payload, Matrix4x4 World, string Node)>();

        void Visit(SceneNode node, Matrix4x4 parent)
        {
            var t = node.LocalTransform;
            var world = Matrix4x4.CreateScale(t.Scale) * Matrix4x4.CreateFromQuaternion(t.Rotation)
                        * Matrix4x4.CreateTranslation(t.Position) * parent;

            // A node lists each of its meshes with that mesh's material, and the mesh names its
            // material by path in its subset.
            var nodeMaterials = node.Components.OfType<SceneMaterialPayload>()
                .GroupBy(m => m.SourcePath).ToDictionary(g => g.Key, g => g.First());

            // A node lists each mesh, then its material, then its skin and skeleton when bones move it.
            SceneSkinPayload? skin = null;
            foreach (var component in node.Components)
            {
                if (component is SceneMeshPayload mesh)
                {
                    var path = mesh.Subsets.FirstOrDefault()?.MaterialPath;
                    meshes.Add(Bake(mesh, world));
                    meshMaterial.Add(MaterialFor(path is not null ? nodeMaterials.GetValueOrDefault(path) : null));
                    if (mesh.Morphs.Count > 0) morphed.Add((meshes.Count - 1, mesh, world, node.Name));
                    skin = null;
                }
                else if (component is SceneSkinPayload s) skin = s;
                else if (component is SceneSkeletonPayload skeleton && skin is not null && meshes.Count > 0)
                {
                    skins.Add(Skin(meshes.Count - 1, meshes[^1], skin, skeleton, world, boneIndex));
                    skin = null;
                }
            }

            foreach (var child in node.Children) Visit(child, world);
        }

        int MaterialFor(SceneMaterialPayload? material)
        {
            if (material is null)
            {
                materials.Add(new ModelMaterial(Color.White));
                return materials.Count - 1;
            }
            if (materialIndex.TryGetValue(material, out var index)) return index;

            var c = Vector4.Clamp(material.BaseColorFactor, Vector4.Zero, Vector4.One) * 255;
            materials.Add(new ModelMaterial(new Color((byte)c.X, (byte)c.Y, (byte)c.Z, (byte)c.W), TextureAt(material.BaseColorTexture))
            {
                Metallic = Math.Clamp(material.MetallicFactor, 0, 1),
                Roughness = Math.Clamp(material.RoughnessFactor, 0, 1),
                NormalMap = TextureAt(material.NormalTexture),
                NormalScale = material.NormalScale,
                MetallicRoughnessMap = TextureAt(material.MetallicRoughnessTexture),
                Emissive = Emissive(material.EmissiveFactor).Color,
                EmissiveIntensity = Emissive(material.EmissiveFactor).Intensity,
                EmissiveMap = TextureAt(material.EmissiveTexture),
                OcclusionMap = TextureAt(material.OcclusionTexture),
                OcclusionStrength = material.OcclusionStrength,
                AlphaMode = (MaterialAlphaMode)(byte)material.AlphaMode,
                AlphaCutoff = material.AlphaCutoff,
                DoubleSided = material.DoubleSided,
            });
            return materialIndex[material] = materials.Count - 1;
        }

        // A texture the file embeds or names beside it, loaded once however many materials use it.
        Texture2D TextureAt(SceneTextureRef? reference)
        {
            if (reference is not { AssetPath: var texturePath }) return default;
            var texture = default(Texture2D);
            if (scene.FindEmbeddedTexture(texturePath) is { } embedded)
            {
                var key = "embedded:" + texturePath;
                if (!textures.TryGetValue(key, out texture))
                {
                    texture = LoadEmbeddedTexture(embedded, fileName, texturePath);
                    GenTextureMipmaps(ref texture);
                    textures[key] = texture;
                }
            }
            else if (!texturePath.StartsWith('*'))
            {
                var full = Path.Combine(directory, texturePath.Replace('\\', Path.DirectorySeparatorChar));
                if (!textures.TryGetValue(full, out texture))
                {
                    texture = LoadTexture(full);
                    GenTextureMipmaps(ref texture);
                    textures[full] = texture;
                }
            }
            return texture;
        }

        foreach (var root in scene.Roots) Visit(root, Matrix4x4.Identity);

        // A mesh with morph targets is posed as a skinned mesh is, by one joint that never moves
        // when no skeleton holds it, its targets moving its vertices before any joint does.
        foreach (var (index, payload, world, node) in morphed)
        {
            var at = skins.FindIndex(s => s.Mesh == index);
            var skin = at >= 0 ? skins[at] : MorphOnly(index, meshes[index]);
            skin = WithMorphs(skin, payload, world, node);
            if (at >= 0) skins[at] = skin;
            else skins.Add(skin);
            Meshes.SetSkin(meshes[index].Id, new MeshStore.Skin(skin.Joints, skin.Weights, Math.Max(1, skin.BoneOfJoint.Length),
                PackMorphs(skin), skin.MorphNames.Length));
        }

        if (meshes.Count == 0)
            ApiLogger.Warn($"LoadModel: '{fileName}' has no meshes.");

        var rest = ModelSkeleton.Pose(bones, ModelSkeleton.NodesByName(scene), n => n.LocalTransform);
        var bindPose = new Transform[bones.Length];
        for (int b = 0; b < bones.Length; b++)
        {
            if (!Matrix4x4.Decompose(rest[b], out var scale, out var rotation, out var position))
                (scale, rotation, position) = (Vector3.One, Quaternion.Identity, rest[b].Translation);
            bindPose[b] = new Transform { Position = position, Rotation = rotation, Scale = scale };
        }

        var model = new Model
        {
            Meshes = [.. meshes],
            Materials = [.. materials],
            MeshMaterial = [.. meshMaterial],
            Bones = bones,
            BindPose = bindPose,
            Skins = [.. skins],
            OwnedTextures = [.. textures.Values.Where(t => t.IsValid)],
        };
        // A file that rests a target at a weight other than 0 is shown so from the start.
        if (skins.Any(s => s.MorphWeights.Any(w => w != 0))) Pose(model, bindPose);
        return model;
    }

    // A skin of one still joint holding every vertex, for a mesh with morph targets and no skeleton.
    private static SkinnedMesh MorphOnly(int index, ModelMesh mesh)
    {
        Meshes.TryGetData(mesh.Id, out var rest, out _);
        var weights = new float[rest.Length * 4];
        for (int v = 0; v < rest.Length; v++) weights[v * 4] = 1;
        return new SkinnedMesh(index, rest, new ushort[rest.Length * 4], weights, [], []);
    }

    // A skin with a mesh's morph targets, moved into the model's space by the node's transform as
    // its vertices were, a delta turned and scaled but not moved.
    private static SkinnedMesh WithMorphs(SkinnedMesh skin, SceneMeshPayload payload, Matrix4x4 world, string node)
    {
        var targets = payload.Morphs;
        var positions = new Vector3[targets.Count][];
        var normals = new Vector3[]?[targets.Count];
        // A normal's delta turns as the normals did, by the inverse of the transform turned over.
        var normalMatrix = Matrix4x4.Invert(world, out var inverse) ? Matrix4x4.Transpose(inverse) : world;
        for (int t = 0; t < targets.Count; t++)
        {
            positions[t] = [.. targets[t].PositionDeltas.Select(d => Vector3.TransformNormal(d, world))];
            normals[t] = targets[t].NormalDeltas is { } n ? [.. n.Select(d => Vector3.TransformNormal(d, normalMatrix))] : null;
        }
        return skin with
        {
            Node = node,
            MorphNames = [.. targets.Select(t => t.Name)],
            MorphWeights = [.. targets.Select(t => t.Weight)],
            MorphPositions = positions,
            MorphNormals = normals,
        };
    }

    // A skin's morph targets as the GPU reads them, each vertex's position and normal deltas as two
    // float4, target after target.
    private static Vector4[] PackMorphs(SkinnedMesh skin)
    {
        var count = skin.Rest.Length;
        var packed = new Vector4[skin.MorphNames.Length * count * 2];
        for (int t = 0; t < skin.MorphNames.Length; t++)
            for (int v = 0; v < count; v++)
            {
                packed[(t * count + v) * 2] = new Vector4(skin.MorphPositions[t][v], 0);
                packed[(t * count + v) * 2 + 1] = new Vector4(skin.MorphNormals[t]?[v] ?? Vector3.Zero, 0);
            }
        return packed;
    }

    // A mesh's skin, with each joint's matrix from the model's space at rest to the joint's own.
    // The file's inverse bind matrices take the mesh's own space, which baking moved the vertices
    // out of by the node's transform, so that is undone first.
    private static SkinnedMesh Skin(int index, ModelMesh mesh, SceneSkinPayload skin, SceneSkeletonPayload skeleton, Matrix4x4 meshWorld,
        Dictionary<string, int> boneIndex)
    {
        Meshes.TryGetData(mesh.Id, out var rest, out _);
        if (!Matrix4x4.Invert(meshWorld, out var toMesh)) toMesh = Matrix4x4.Identity;

        var boneOfJoint = new int[skeleton.JointNames.Length];
        var fromRest = new Matrix4x4[boneOfJoint.Length];
        for (int j = 0; j < boneOfJoint.Length; j++)
        {
            boneOfJoint[j] = boneIndex[skeleton.JointNames[j]];
            fromRest[j] = toMesh * skeleton.InverseBindMatrices[j];
        }
        // The GPU poses it when there is one, and the CPU otherwise.
        Meshes.SetSkin(mesh.Id, new MeshStore.Skin(skin.JointIndices, skin.JointWeights, boneOfJoint.Length));
        return new SkinnedMesh(index, rest, skin.JointIndices, skin.JointWeights, boneOfJoint, fromRest);
    }

    private static Texture2D LoadEmbeddedTexture(SceneEmbeddedTexture embedded, string fileName, string texturePath)
    {
        if (embedded.Rgba is { } rgba)
            return LoadTextureFromImage(new Image(rgba, embedded.Width, embedded.Height));

        try
        {
            var result = ImageResult.FromMemory(embedded.Encoded!, ColorComponents.RedGreenBlueAlpha);
            return LoadTextureFromImage(new Image(result.Data, result.Width, result.Height));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ApiLogger.Warn($"LoadModel: '{fileName}' embeds a texture ({texturePath}, {embedded.FormatHint}) that could not be decoded: {ex.Message}");
            return default;
        }
    }

    /// <summary>Makes a model of one mesh with a white material.</summary>
    public static Model LoadModelFromMesh(ModelMesh mesh) => new()
    {
        Meshes = [mesh],
        Materials = [new ModelMaterial(Color.White)],
        MeshMaterial = [0],
    };

    /// <summary>Frees a model's meshes and the textures it loaded. Textures assigned to it afterward are the program's to free.</summary>
    public static void UnloadModel(Model model)
    {
        foreach (var mesh in model.Meshes) UnloadMesh(mesh);
        foreach (var texture in model.OwnedTextures) UnloadTexture(texture);
    }

    /// <summary>
    /// The model again, posed apart from it. Each skinned mesh is a mesh of its own, at rest, and
    /// the meshes no bone moves, the materials and their textures, the bones and the bind pose are
    /// the model's, so many copies of one file cost a load and a skinned mesh each.
    /// </summary>
    /// <remarks>A copy is freed with <see cref="UnloadPosedCopy"/>, before the model it copies.</remarks>
    internal static Model PosedCopy(Model model)
    {
        var meshes = (ModelMesh[])model.Meshes.Clone();
        foreach (var skin in model.Skins)
        {
            var mesh = model.Meshes[skin.Mesh];
            if (!Meshes.TryGetData(mesh.Id, out _, out var indices)) continue;
            var id = Meshes.Add((ModelVertex[])skin.Rest.Clone(), indices, Meshes.StreamsOf(mesh.Id));
            Meshes.SetSkin(id, new MeshStore.Skin(skin.Joints, skin.Weights, skin.BoneOfJoint.Length));
            meshes[skin.Mesh] = mesh with { Id = id };
        }
        return new Model
        {
            Meshes = meshes,
            Materials = (ModelMaterial[])model.Materials.Clone(),
            MeshMaterial = model.MeshMaterial,
            Transform = model.Transform,
            Bones = model.Bones,
            BindPose = model.BindPose,
            Skins = model.Skins,
        };
    }

    /// <summary>Frees the meshes <see cref="PosedCopy"/> made for <paramref name="copy"/>, leaving what it shares with <paramref name="of"/>.</summary>
    internal static void UnloadPosedCopy(Model copy, Model of)
    {
        for (int i = 0; i < copy.Meshes.Length; i++)
            if (copy.Meshes[i].Id != of.Meshes[i].Id) UnloadMesh(copy.Meshes[i]);
    }

    /// <summary>Whether <paramref name="model"/> has meshes that are loaded.</summary>
    public static bool IsModelValid(Model model) => model.IsValid && model.Meshes.All(m => Meshes.Contains(m.Id));

    /// <summary>The box around every mesh of a model, in its own space before <see cref="Model.Transform"/>.</summary>
    public static BoundingBox GetModelBoundingBox(Model model)
    {
        if (!model.IsValid) return default;
        var corners = model.Meshes.SelectMany(m => new[] { m.Bounds.Min, m.Bounds.Max }).ToArray();
        return BoundingBox.Around(corners);
    }

    // -- Drawing models, through the camera BeginMode3D set

    /// <summary>Draws a model at a position, scaled the same on every axis, with its colors multiplied by <paramref name="tint"/>.</summary>
    public static void DrawModel(Model model, Vector3 position, float scale, Color tint) =>
        DrawModelEx(model, position, Vector3.UnitY, 0, new Vector3(scale), tint);

    /// <summary>Draws a model at a position, rotated by <paramref name="rotationAngle"/> degrees around <paramref name="rotationAxis"/>, and scaled.</summary>
    public static void DrawModelEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint)
    {
        var axis = rotationAxis == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(rotationAxis);
        var world = model.Transform * Matrix4x4.CreateScale(scale)
                    * Matrix4x4.CreateFromAxisAngle(axis, float.DegreesToRadians(rotationAngle))
                    * Matrix4x4.CreateTranslation(position);

        for (int i = 0; i < model.Meshes.Length; i++)
        {
            var material = model.Materials.Length == 0 ? new ModelMaterial(Color.White)
                : model.Materials[Math.Clamp(model.MeshMaterial.ElementAtOrDefault(i), 0, model.Materials.Length - 1)];
            // A tint with alpha fades the model as raylib's does, even one its file calls opaque.
            var mode = tint.A < 255 && material.AlphaMode == MaterialAlphaMode.Opaque ? MaterialAlphaMode.Blend : material.AlphaMode;
            DrawMesh(model.Meshes[i], material with { Color = Multiply(material.Color, tint), AlphaMode = mode }, world);
        }
    }

    // A model's meshes recorded at a world matrix through a camera, into the window, for the systems
    // that draw what entities hold rather than through BeginMode3D.
    internal static void RecordModel(Model model, Matrix4x4 world, Matrix4x4 viewProjection, int target = 0)
    {
        var placed = model.Transform * world;
        var draws = Res<ModelDrawList>();
        for (int i = 0; i < model.Meshes.Length; i++)
        {
            if (!model.Meshes[i].IsValid) continue;
            var material = model.Materials.Length == 0 ? new ModelMaterial(Color.White)
                : model.Materials[Math.Clamp(model.MeshMaterial.ElementAtOrDefault(i), 0, model.Materials.Length - 1)];
            draws.Add(MeshDraw(model.Meshes[i], material, placed) with { ViewProjection = viewProjection, Target = target });
        }
    }

    // Whether the flat API's app is the one holding world, which the systems that call it check.
    internal static bool Holds(World world) => _app is { } app && ReferenceEquals(app.World, world);

    /// <summary>Draws a model's triangle edges in one color, at a position and scaled the same on every axis.</summary>
    public static void DrawModelWires(Model model, Vector3 position, float scale, Color tint) =>
        DrawModelWiresEx(model, position, Vector3.UnitY, 0, new Vector3(scale), tint);

    /// <summary>Draws a model's triangle edges in one color, rotated (degrees) and scaled.</summary>
    /// <remarks>
    /// The edges are lines in the immediate pass, one per edge however many triangles share it,
    /// built each call from the mesh's arrays, so a dense model costs its edge count every frame.
    /// </remarks>
    public static void DrawModelWiresEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint)
    {
        var axis = rotationAxis == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(rotationAxis);
        var world = model.Transform * Matrix4x4.CreateScale(scale)
                    * Matrix4x4.CreateFromAxisAngle(axis, float.DegreesToRadians(rotationAngle))
                    * Matrix4x4.CreateTranslation(position);

        var edges = new HashSet<(uint, uint)>();
        for (int index = 0; index < model.Meshes.Length; index++)
        {
            var mesh = model.Meshes[index];
            if (!Meshes.TryGetData(mesh.Id, out var vertices, out var indices)) continue;
            // A mesh the GPU poses keeps its vertices at rest, so its wires are posed here.
            if (model.GpuPoses.TryGetValue(index, out var joints) && model.Skins.FirstOrDefault(s => s.Mesh == index) is { } skin)
                vertices = PoseOnCpu(skin, joints);
            edges.Clear();
            for (int i = 0; i < indices.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    uint a = indices[i + k], b = indices[i + (k + 1) % 3];
                    if (edges.Add(a < b ? (a, b) : (b, a)))
                        DrawLine3D(Vector3.Transform(vertices[a].Position, world), Vector3.Transform(vertices[b].Position, world), tint);
                }
        }
    }

    /// <summary>Draws each of a model's vertices as a point, at a position and scaled the same on every axis.</summary>
    public static void DrawModelPoints(Model model, Vector3 position, float scale, Color tint) =>
        DrawModelPointsEx(model, position, Vector3.UnitY, 0, new Vector3(scale), tint);

    /// <summary>Draws a model as a point at each corner of its triangles, rotated (degrees) and scaled.</summary>
    /// <remarks>
    /// As raylib's, the model is drawn in point mode with both faces, and the point mode and culling
    /// it had are put back after. A device that cannot draw triangles as points draws it filled.
    /// </remarks>
    public static void DrawModelPointsEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint)
    {
        var (pointMode, culling) = (_rlPointMode, _rlCulling);
        rlEnablePointMode();
        rlDisableBackfaceCulling();
        DrawModelEx(model, position, rotationAxis, rotationAngle, scale, tint);
        (_rlPointMode, _rlCulling) = (pointMode, culling);
        ApplyRlCulling();
    }

    /// <summary>A white material with no maps, as a model made from a mesh starts with.</summary>
    public static ModelMaterial LoadMaterialDefault() => new(Color.White);

    /// <summary>The materials of a model file, without its meshes, as raylib's reads a .mtl or a glTF's materials.</summary>
    /// <remarks>
    /// The textures the materials name are loaded with them and stay loaded, for the program to
    /// unload with <see cref="UnloadTexture"/> once no material it keeps uses them.
    /// </remarks>
    public static ModelMaterial[] LoadMaterials(string fileName)
    {
        var model = LoadModel(fileName);
        foreach (var mesh in model.Meshes) UnloadMesh(mesh);
        return [.. model.Materials];
    }

    /// <summary>Unloads a material's five maps and its shader, which <see cref="UnloadModel"/> leaves loaded for the materials that may share them.</summary>
    /// <remarks>
    /// A map or shader of none is passed over, as raylib passes over its defaults. A model's own
    /// textures, loaded with its file, are unloaded with the model, so a material of
    /// <see cref="LoadMaterials"/> or one the program made is the one to unload this way.
    /// </remarks>
    public static void UnloadMaterial(ModelMaterial material)
    {
        foreach (var map in new[] { material.Texture, material.NormalMap, material.MetallicRoughnessMap, material.EmissiveMap, material.OcclusionMap })
            UnloadTexture(map);
        UnloadShader(material.Shader);
    }

    /// <summary>Whether a material's maps are loaded textures, or none, so it can be drawn with.</summary>
    public static bool IsMaterialValid(ModelMaterial material)
    {
        static bool Ok(Texture2D map) => !map.IsValid || IsTextureValid(map);
        return Ok(material.Texture) && Ok(material.NormalMap) && Ok(material.MetallicRoughnessMap) && Ok(material.EmissiveMap) && Ok(material.OcclusionMap);
    }

    /// <summary>
    /// Sets one of a material's maps by raylib's name for it: the color for <see cref="MaterialMapIndex.Albedo"/>,
    /// and the normal, metallic-roughness, emissive or occlusion map for the others it has.
    /// </summary>
    public static void SetMaterialTexture(ref ModelMaterial material, MaterialMapIndex mapType, Texture2D texture)
    {
        switch (mapType)
        {
            case MaterialMapIndex.Albedo: material = material with { Texture = texture }; break;
            case MaterialMapIndex.Normal: material.NormalMap = texture; break;
            case MaterialMapIndex.Metalness or MaterialMapIndex.Roughness: material.MetallicRoughnessMap = texture; break;
            case MaterialMapIndex.Emission: material.EmissiveMap = texture; break;
            case MaterialMapIndex.Occlusion: material.OcclusionMap = texture; break;
            default: ApiLogger.Warn($"SetMaterialTexture: a material has no {mapType} map, so it is left as it is."); break;
        }
    }

    /// <summary>Has one of a model's meshes drawn with one of its materials, by their indices.</summary>
    public static void SetModelMeshMaterial(Model model, int meshId, int materialId)
    {
        if ((uint)meshId >= (uint)model.MeshMaterial.Length || (uint)materialId >= (uint)model.Materials.Length) return;
        model.MeshMaterial[meshId] = materialId;
    }

    /// <summary>Draws one mesh with a material and a model to world transform.</summary>
    public static void DrawMesh(ModelMesh mesh, ModelMaterial material, Matrix4x4 transform)
    {
        if (!mesh.IsValid) return;
        Res<ModelDrawList>().Add(MeshDraw(mesh, material, transform));
    }

    /// <summary>Draws one mesh many times, once at each model to world transform, as one instanced draw.</summary>
    /// <remarks>
    /// The copies share one snapshot of the material's shader values, so with a shader of the
    /// program's own they are drawn by one call as well, and its vertex stage tells them apart by
    /// <c>SV_InstanceID</c>, counted from 0.
    /// </remarks>
    public static void DrawMeshInstanced(ModelMesh mesh, ModelMaterial material, ReadOnlySpan<Matrix4x4> transforms)
    {
        if (!mesh.IsValid || transforms.IsEmpty) return;
        var first = MeshDraw(mesh, material, transforms[0]);
        var draws = System.Buffers.ArrayPool<ModelDraw>.Shared.Rent(transforms.Length);
        try
        {
            for (int i = 0; i < transforms.Length; i++) draws[i] = first with { World = _rlTransform.IsIdentity ? transforms[i] : transforms[i] * _rlTransform };
            Res<ModelDrawList>().AddRange(draws.AsSpan(0, transforms.Length));
        }
        finally
        {
            System.Buffers.ArrayPool<ModelDraw>.Shared.Return(draws);
        }
    }

    // A mesh's draw with a material, its shader's values taken as they are now, moved by rlgl's
    // matrix stack as raylib's DrawMesh moves it.
    private static ModelDraw MeshDraw(ModelMesh mesh, ModelMaterial material, Matrix4x4 transform)
    {
        if (!_rlTransform.IsIdentity) transform *= _rlTransform;
        return WithRlState(MeshDrawOf(mesh, material, transform));
    }

    private static ModelDraw MeshDrawOf(ModelMesh mesh, ModelMaterial material, Matrix4x4 transform)
    {
        var texture = material.Texture.IsValid ? material.Texture.Id : 0;
        var shader = material.Shader.IsValid && Draws(material.Shader) ? material.Shader.Id : 0;
        return new ModelDraw(mesh.Id, transform, DrawList.Transform, material.Color, texture, DrawList.Target,
            shader, shader == 0 ? null : UniformSnapshot(material.Shader),
            material.Metallic, material.Roughness,
            material.NormalMap.IsValid ? material.NormalMap.Id : 0, material.NormalScale,
            material.MetallicRoughnessMap.IsValid ? material.MetallicRoughnessMap.Id : 0,
            Linear(material.Emissive) * material.EmissiveIntensity,
            material.EmissiveMap.IsValid ? material.EmissiveMap.Id : 0,
            material.OcclusionMap.IsValid ? material.OcclusionMap.Id : 0,
            material.OcclusionStrength,
            material.AlphaMode, material.AlphaCutoff, texture != 0 && Textures.IsTranslucent(texture), material.DoubleSided,
            shader == 0 ? null : TextureSnapshot(material.Shader), material.CastsShadows);
    }

    /// <summary>Draws a box's edges.</summary>
    public static void DrawBoundingBox(BoundingBox box, Color color) =>
        DrawCubeWiresV((box.Min + box.Max) / 2, box.Max - box.Min, color);

    // An sRGB color's light, which a draw's emission is given in.
    private static Vector3 Linear(Color color)
    {
        static float Decode(byte value)
        {
            var c = value / 255f;
            return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
        return new Vector3(Decode(color.R), Decode(color.G), Decode(color.B));
    }

    // A linear color as sRGB bytes and the intensity that takes it past 1, which a file's
    // emission can need.
    private static (Color Color, float Intensity) Emissive(Vector3 linear)
    {
        static byte Encode(float c) =>
            (byte)MathF.Round(255 * (c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f));
        var peak = MathF.Max(1, MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z)));
        var c = Vector3.Clamp(linear / peak, Vector3.Zero, Vector3.One);
        return (new Color(Encode(c.X), Encode(c.Y), Encode(c.Z)), peak);
    }

    private static Color Multiply(Color a, Color b) =>
        new((byte)(a.R * b.R / 255), (byte)(a.G * b.G / 255), (byte)(a.B * b.B / 255), (byte)(a.A * b.A / 255));

    // Moves a file's mesh into the model's space by its node's transform, so the model needs no
    // hierarchy at draw time.
    private static ModelMesh Bake(SceneMeshPayload mesh, Matrix4x4 world)
    {
        // Normals turn by the inverse of the transform turned over, so one stays across its surface
        // where a node scales it more one way than another, and are kept of unit length.
        var normalMatrix = Matrix4x4.Invert(world, out var inverse) ? Matrix4x4.Transpose(inverse) : world;
        var vertices = new ModelVertex[mesh.Positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var normal = mesh.Normals is { } normals && i < normals.Length ? Vector3.TransformNormal(normals[i], normalMatrix) : Vector3.UnitY;
            var uv = mesh.Uv0 is { } uvs && i < uvs.Length ? uvs[i] : Vector2.Zero;
            vertices[i] = new ModelVertex(Vector3.Transform(mesh.Positions[i], world),
                normal == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(normal), uv);
        }

        var indices = new uint[mesh.Indices.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = (uint)mesh.Indices[i];
        // A file's colors as raylib reads them, each channel times 255, and its second texture
        // coordinates, where it has them for every vertex.
        var colors = mesh.Colors is { } fileColors && fileColors.Length == vertices.Length
            ? Array.ConvertAll(fileColors, c => new Color(Byte(c.X), Byte(c.Y), Byte(c.Z), Byte(c.W)))
            : null;
        var texcoords2 = mesh.Uv1 is { } uv1 && uv1.Length == vertices.Length ? uv1 : null;
        return UploadMesh(vertices, indices, colors, texcoords2);
    }

    private static byte Byte(float unit) => (byte)Math.Clamp(MathF.Round(unit * 255), 0, 255);
}
