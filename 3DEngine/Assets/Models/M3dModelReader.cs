// Model 3D files (.m3d), read as raylib reads them through m3d.h, written again in C#. Altered from
// the original, which is C, to read the binary form alone, without its voxels, shapes and labels,
// and to hand its meshes to a scene as raylib's LoadM3D builds them.
//
// The MIT License (MIT)
// Copyright (C) 2020 bzt (bztsrc@gitlab)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace Engine;

/// <summary>
/// Reads a Model 3D file (<c>.m3d</c>) as raylib's <c>LoadM3D</c> and <c>LoadModelAnimationsM3D</c>
/// read one: a mesh for each run of faces of one material into a scene, and its actions into poses
/// of every bone at every 17 milliseconds of each.
/// </summary>
/// <remarks>
/// <para>
/// A mesh has three vertices of its own for each face, scaled by the file's scale, its texture
/// coordinates counted from the bottom as raylib flips them, and its normals made from its faces
/// where the file gives none. A skeleton gains a last bone, <c>NO BONE</c>, that never moves and
/// holds every vertex no bone holds, as raylib's does, so <c>BoneCount</c> is one more than the
/// file's.
/// </para>
/// <para>
/// Colors are raylib's. A mesh of no material is white at each vertex the file gives no color, and
/// a mesh of a material has colors only where a vertex of it has none, the rest clear black, as
/// raylib's loader has it. Textures come from the file's own assets or from beside it, a name
/// without an extension taken as a PNG first. The text form, voxels, shapes and labels are not read.
/// </para>
/// </remarks>
internal static class M3dModelReader
{
    /// <summary>The milliseconds between two of a clip's frames, raylib's <c>M3D_ANIMDELAY</c>.</summary>
    public const int FrameMilliseconds = 17;

    private const string NoBone = "NO BONE";

    private readonly record struct Vertex(Vector4 Value, uint Color, int Skin);
    private readonly record struct Bone(int Parent, string Name, int Position, int Orientation);
    private readonly record struct Skin(int[] Bones, float[] Weights);
    private readonly record struct Face(int Material, int[] Vertices, int[] Texcoords, int[] Normals);
    private readonly record struct Transform3(int Bone, int Position, int Orientation);
    private readonly record struct Frame(uint Milliseconds, Transform3[] Transforms);
    private readonly record struct Action(string Name, uint Milliseconds, Frame[] Frames);
    private sealed record Material(string Name, List<(byte Type, uint Color, float Number, string? Texture)> Properties);

    private sealed class File3D
    {
        public float Scale = 1;
        public List<Vertex> Vertices = [];
        public Vector2[] Texcoords = [];
        public Bone[] Bones = [];
        public Skin[] Skins = [];
        public List<Material> Materials = [];
        public List<Face> Faces = [];
        public List<Action> Actions = [];
        public Dictionary<string, byte[]> Assets = new(StringComparer.Ordinal);
    }

    /// <summary>Reads a file's meshes, materials and skeleton into a scene, as <c>LoadModel</c> reads a model file.</summary>
    /// <exception cref="InvalidOperationException">The bytes are not a binary Model 3D file, or reach past their end.</exception>
    public static Scene Read(ReadOnlySpan<byte> data, string name)
    {
        var file = Parse(data);
        var scene = new Scene { Name = name };
        var root = new SceneNode { Name = name, SourcePath = "/" + name };
        scene.Roots.Add(root);

        // The bones as nodes under their parents, then the bone that never moves.
        SceneSkeletonPayload? skeleton = null;
        if (file.Bones.Length > 0)
        {
            var count = file.Bones.Length + 1;
            var (names, parents, locals) = (new string[count], new int[count], new Transform[count]);
            var (inverseBinds, worlds, nodes) = (new Matrix4x4[count], new Matrix4x4[count], new SceneNode[count]);
            for (int b = 0; b < count; b++)
            {
                var bone = b < file.Bones.Length ? file.Bones[b] : new Bone(-1, NoBone, -1, -1);
                if (bone.Parent >= b) throw new InvalidOperationException($"bone {b} names {bone.Parent} as its parent, which does not come before it");
                (names[b], parents[b]) = (bone.Name, bone.Parent);
                locals[b] = b < file.Bones.Length ? Local(file, bone.Position, bone.Orientation) : Transform.Identity;
                worlds[b] = TransformPropagation.ToMatrix(locals[b]) * (bone.Parent >= 0 ? worlds[bone.Parent] : Matrix4x4.Identity);
                if (!Matrix4x4.Invert(worlds[b], out inverseBinds[b])) inverseBinds[b] = Matrix4x4.Identity;
                nodes[b] = new SceneNode { Name = names[b], SourcePath = "/" + names[b], LocalTransform = locals[b] };
                (bone.Parent >= 0 ? nodes[bone.Parent].Children : root.Children).Add(nodes[b]);
            }
            skeleton = new SceneSkeletonPayload
            {
                Name = name, JointNames = names, ParentIndices = parents, InverseBindMatrices = inverseBinds, LocalBindTransforms = locals,
            };
            // The model's bones, whether or not a mesh is skinned by them, so the file's clips fit it.
            root.Components.Add(skeleton);
        }

        var normals = MadeNormals(file);
        for (int first = 0; first < file.Faces.Count;)
        {
            var material = file.Faces[first].Material;
            int end = first;
            while (end < file.Faces.Count && file.Faces[end].Material == material) end++;
            root.Children.Add(MeshNode(file, scene, name, first, end, normals, skeleton));
            first = end;
        }
        return scene;
    }

    /// <summary>Reads a file's actions, each bone posed in the model's space at every 17 milliseconds, as raylib samples them.</summary>
    /// <exception cref="InvalidOperationException">The bytes are not a binary Model 3D file, or reach past their end.</exception>
    public static ModelAnimation[] ReadAnimations(ReadOnlySpan<byte> data)
    {
        var file = Parse(data);
        if (file.Bones.Length == 0) return [];

        var bones = new BoneInfo[file.Bones.Length + 1];
        for (int b = 0; b < file.Bones.Length; b++) bones[b] = new BoneInfo(file.Bones[b].Name, file.Bones[b].Parent);
        bones[^1] = new BoneInfo(NoBone, -1);

        var clips = new List<ModelAnimation>();
        foreach (var action in file.Actions)
        {
            var frames = new Transform[action.Milliseconds / FrameMilliseconds][];
            for (int f = 0; f < frames.Length; f++)
            {
                var pose = Pose(file, action, (uint)(f * FrameMilliseconds));
                var worlds = new Matrix4x4[bones.Length];
                frames[f] = new Transform[bones.Length];
                for (int b = 0; b < file.Bones.Length; b++)
                {
                    var parent = bones[b].Parent;
                    worlds[b] = TransformPropagation.ToMatrix(pose[b]) * (parent >= 0 ? worlds[parent] : Matrix4x4.Identity);
                    if (!Matrix4x4.Decompose(worlds[b], out var scale, out var rotation, out var position))
                        (scale, rotation, position) = (Vector3.One, Quaternion.Identity, worlds[b].Translation);
                    frames[f][b] = new Transform { Position = position, Rotation = rotation, Scale = scale };
                }
                frames[f][^1] = Transform.Identity;
            }
            clips.Add(new ModelAnimation { Name = action.Name, Bones = [.. bones], FramePoses = frames });
        }
        return [.. clips];
    }

    // A bone's transform from its parent's, its position scaled as the mesh's are.
    private static Transform Local(File3D file, int position, int orientation)
    {
        var p = Value(file, position);
        var q = Value(file, orientation);
        var rotation = new Quaternion(q.X, q.Y, q.Z, q.W);
        return new Transform
        {
            Position = new Vector3(p.X, p.Y, p.Z) * file.Scale,
            Rotation = rotation.LengthSquared() > 0 ? Quaternion.Normalize(rotation) : Quaternion.Identity,
            Scale = Vector3.One,
        };
    }

    private static Vector4 Value(File3D file, int vertex) =>
        (uint)vertex < (uint)file.Vertices.Count ? file.Vertices[vertex].Value : throw new InvalidOperationException($"it names vertex {vertex} of {file.Vertices.Count}");

    // m3d's m3d_pose: each bone at its rest, moved by every frame up to the time, then a part of the
    // way to the next frame, the position in a line and the orientation by m3d's approximation of a
    // turn, a normalized blend.
    private static Transform[] Pose(File3D file, Action action, uint milliseconds)
    {
        var count = file.Bones.Length;
        var pose = new (int Position, int Orientation, Vector4 P, Vector4 Q)[count];
        for (int b = 0; b < count; b++)
            pose[b] = (file.Bones[b].Position, file.Bones[b].Orientation, Value(file, file.Bones[b].Position), Value(file, file.Bones[b].Orientation));
        if (action.Milliseconds > 0) milliseconds %= action.Milliseconds;

        int j = 0;
        uint last = 0;
        for (; j < action.Frames.Length && action.Frames[j].Milliseconds <= milliseconds; j++)
        {
            last = action.Frames[j].Milliseconds;
            foreach (var t in action.Frames[j].Transforms)
                if ((uint)t.Bone < (uint)count) pose[t.Bone] = (t.Position, t.Orientation, Value(file, t.Position), Value(file, t.Orientation));
        }

        if (last != milliseconds && action.Frames.Length > 0)
        {
            var next = action.Frames[j % action.Frames.Length];
            float t = last >= next.Milliseconds ? 1 : (float)(milliseconds - last) / (next.Milliseconds - last);
            var toward = ((int Position, int Orientation)[])[.. pose.Select(p => (p.Position, p.Orientation))];
            foreach (var tr in next.Transforms)
                if ((uint)tr.Bone < (uint)count) toward[tr.Bone] = (tr.Position, tr.Orientation);
            for (int b = 0; b < count; b++)
            {
                if (toward[b].Position != pose[b].Position)
                    pose[b].P = Vector4.Lerp(pose[b].P, Value(file, toward[b].Position), t);
                if (toward[b].Orientation != pose[b].Orientation)
                {
                    var (p, f) = (pose[b].Q, Value(file, toward[b].Orientation));
                    var d = Vector4.Dot(p, f);
                    var s = d < 0 ? -1f : 1f;
                    d = MathF.Abs(d);
                    var c = t - 0.5f;
                    var u = t + t * c * (t - 1) * ((1.0904f + d * (-3.2452f + d * (3.55645f - d * 1.43519f))) * c * c + (0.848013f + d * (-1.06021f + d * 0.215638f)));
                    pose[b].Q = Vector4.Normalize(p + u * (s * f - p));
                }
            }
        }

        var transforms = new Transform[count];
        for (int b = 0; b < count; b++)
        {
            var rotation = new Quaternion(pose[b].Q.X, pose[b].Q.Y, pose[b].Q.Z, pose[b].Q.W);
            transforms[b] = new Transform
            {
                Position = new Vector3(pose[b].P.X, pose[b].P.Y, pose[b].P.Z) * file.Scale,
                Rotation = rotation.LengthSquared() > 0 ? Quaternion.Normalize(rotation) : Quaternion.Identity,
                Scale = Vector3.One,
            };
        }
        return transforms;
    }

    // Each vertex's normal made as m3d makes it where a face of it has none, the faces' normals
    // around it averaged, or null where every face has its own.
    private static Vector3[]? MadeNormals(File3D file)
    {
        if (file.Faces.All(f => f.Normals[0] >= 0)) return null;
        var sums = new Vector3[file.Vertices.Count];
        foreach (var face in file.Faces)
        {
            var (a, b, c) = (Position(file, face.Vertices[0]), Position(file, face.Vertices[1]), Position(file, face.Vertices[2]));
            var normal = Vector3.Cross(b - a, c - a);
            if (normal != Vector3.Zero) normal = Vector3.Normalize(normal);
            foreach (var v in face.Vertices) sums[v] += normal;
        }
        for (int v = 0; v < sums.Length; v++)
            if (sums[v] != Vector3.Zero) sums[v] = Vector3.Normalize(sums[v]);
        return sums;
    }

    private static Vector3 Position(File3D file, int vertex)
    {
        var v = Value(file, vertex);
        return new Vector3(v.X, v.Y, v.Z);
    }

    // The faces from first to end, all of one material, as one mesh of three vertices to a face.
    private static SceneNode MeshNode(File3D file, Scene scene, string name, int first, int end, Vector3[]? made, SceneSkeletonPayload? skeleton)
    {
        var materialIndex = file.Faces[first].Material;
        var material = materialIndex >= 0 && materialIndex < file.Materials.Count ? file.Materials[materialIndex] : null;
        var corners = (end - first) * 3;
        var positions = new Vector3[corners];
        var normals = new Vector3[corners];
        var uvs = new Vector2[corners];

        // raylib's colors: white where a mesh has no material, and where it has one, a buffer of
        // them only if a vertex of it has no color, which then stays clear black.
        bool anyUncolored = false;
        for (int f = first; f < end; f++)
            foreach (var v in file.Faces[f].Vertices)
                if (file.Vertices[v].Color == 0) anyUncolored = true;
        Vector4[]? colors = material is null || anyUncolored ? new Vector4[corners] : null;
        if (colors is not null && material is null) Array.Fill(colors, Vector4.One);

        var withSkin = skeleton is not null && file.Skins.Length > 0;
        var joints = withSkin ? new ushort[corners * 4] : null;
        var weights = withSkin ? new float[corners * 4] : null;
        var noBone = (ushort)file.Bones.Length;

        for (int f = first, at = 0; f < end; f++)
        {
            var face = file.Faces[f];
            for (int k = 0; k < 3; k++, at++)
            {
                var vertex = file.Vertices[face.Vertices[k]];
                positions[at] = new Vector3(vertex.Value.X, vertex.Value.Y, vertex.Value.Z) * file.Scale;
                if (face.Texcoords[k] >= 0 && face.Texcoords[k] < file.Texcoords.Length)
                    uvs[at] = file.Texcoords[face.Texcoords[k]] with { Y = 1 - file.Texcoords[face.Texcoords[k]].Y };
                normals[at] = face.Normals[k] >= 0 ? Position(file, face.Normals[k]) : made?[face.Vertices[k]] ?? Vector3.UnitY;
                if (colors is not null && (vertex.Color & 0xff000000) != 0)
                    colors[at] = new Vector4(vertex.Color & 0xff, (vertex.Color >> 8) & 0xff, (vertex.Color >> 16) & 0xff, vertex.Color >> 24) / 255f;
                if (joints is null || weights is null) continue;
                if (vertex.Skin >= 0 && vertex.Skin < file.Skins.Length)
                {
                    var skin = file.Skins[vertex.Skin];
                    for (int j = 0; j < 4; j++)
                    {
                        var bone = skin.Bones[j];
                        (joints[at * 4 + j], weights[at * 4 + j]) = bone >= 0 && bone < file.Bones.Length ? ((ushort)bone, skin.Weights[j]) : ((ushort)0, 0f);
                    }
                }
                else (joints[at * 4], weights[at * 4]) = (noBone, 1f);
            }
        }

        var meshName = material?.Name ?? "default";
        var path = $"/{name}/{meshName}";
        var node = new SceneNode { Name = meshName, SourcePath = path };
        var materialPath = material is null ? null : path + "/material";
        node.Components.Add(new SceneMeshPayload
        {
            Name = meshName,
            Positions = positions,
            Normals = normals,
            Uv0 = uvs,
            Colors = colors,
            Indices = [.. Enumerable.Range(0, corners)],
            Subsets = [new SceneMeshSubset(meshName, 0, corners, materialPath)],
            LocalBounds = SceneBounds.FromPositions(positions),
        });
        if (material is not null) node.Components.Add(MaterialPayload(file, scene, material, materialPath!));
        if (joints is not null && weights is not null && skeleton is not null)
        {
            node.Components.Add(new SceneSkinPayload { SkeletonPath = "/" + name, JointIndices = joints, JointWeights = weights });
            node.Components.Add(skeleton);
        }
        return node;
    }

    // raylib's reading of a material: its diffuse, emissive, roughness and metalness, and the maps it
    // names, which this engine's material has.
    private static SceneMaterialPayload MaterialPayload(File3D file, Scene scene, Material material, string path)
    {
        static Vector4 Rgba(uint c) => new Vector4(c & 0xff, (c >> 8) & 0xff, (c >> 16) & 0xff, c >> 24) / 255f;
        Vector4 baseColor = Vector4.One;
        Vector3 emissive = Vector3.Zero;
        float metallic = 0, roughness = 1;
        SceneTextureRef? albedo = null, emission = null, normal = null, occlusion = null;
        foreach (var (type, color, number, texture) in material.Properties)
        {
            switch (type)
            {
                case 0: baseColor = Rgba(color); break;
                case 4: emissive = new Vector3(Rgba(color).X, Rgba(color).Y, Rgba(color).Z); break;
                case 64: roughness = number; break;
                case 65: metallic = number; break;
                case 128: albedo = Texture(file, scene, texture); break;
                case 129: occlusion = Texture(file, scene, texture); break;
                case 132: emission = Texture(file, scene, texture); break;
                case 134 or 136: normal = Texture(file, scene, texture); break;
            }
        }
        return new SceneMaterialPayload
        {
            Name = material.Name, SourcePath = path, BaseColorFactor = baseColor, BaseColorTexture = albedo, EmissiveFactor = emissive,
            EmissiveTexture = emission, MetallicFactor = metallic, RoughnessFactor = roughness, NormalTexture = normal, OcclusionTexture = occlusion,
        };
    }

    // A texture by name: the file's own asset of that name, embedded in the scene, or a file beside
    // the model, a name without an extension taken as a PNG.
    private static SceneTextureRef? Texture(File3D file, Scene scene, string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (file.Assets.TryGetValue(name, out var bytes))
        {
            var index = scene.EmbeddedTextures.FindIndex(t => t.FileName == name);
            if (index < 0)
            {
                scene.EmbeddedTextures.Add(new SceneEmbeddedTexture(name, bytes, "png", 0, 0, null));
                index = scene.EmbeddedTextures.Count - 1;
            }
            return new SceneTextureRef("*" + index);
        }
        return new SceneTextureRef(name.Length < 5 || name[^4] != '.' ? name + ".png" : name);
    }

    private static File3D Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 4 && data[..4].SequenceEqual("3dmo"u8))
            throw new InvalidOperationException("it is a Model 3D in text, where the binary form is read");
        if (data.Length < 8 || !data[..4].SequenceEqual("3DMO"u8))
            throw new InvalidOperationException("it is not a Model 3D file");

        var length = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(data[4..]), (uint)data.Length);
        var body = data[8..length];
        if (body.Length >= 8 && body[..4].SequenceEqual("PRVW"u8))
            body = body[(int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(body[4..]), (uint)body.Length)..];
        byte[] raw;
        if (body.Length >= 4 && body[..4].SequenceEqual("HEAD"u8)) raw = body.ToArray();
        else
        {
            try
            {
                using var inflate = new ZLibStream(new MemoryStream(body.ToArray()), CompressionMode.Decompress);
                using var inflated = new MemoryStream();
                inflate.CopyTo(inflated);
                raw = inflated.ToArray();
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidOperationException($"its compressed body could not be read: {ex.Message}");
            }
            if (raw.Length < 4 || !raw.AsSpan(0, 4).SequenceEqual("HEAD"u8)) throw new InvalidOperationException("its body does not begin with a header");
        }
        return ParseChunks(raw);
    }

    private static File3D ParseChunks(byte[] raw)
    {
        if (raw.Length < 20) throw new InvalidOperationException("its header is cut short");
        var file = new File3D();
        var headLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4));
        var scale = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(8));
        file.Scale = scale > 0 ? scale : 1;
        var types = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12));
        int Size(int shift, bool optional = false)
        {
            var size = 1 << (int)((types >> shift) & 3);
            return optional && size == 8 ? 0 : size;
        }
        var (vc, vi, si, ci, ti, bi, nb, sk, fc) = (Size(0), Size(2), Size(4), Size(6, true), Size(8, true), Size(10, true), Size(12), Size(14, true), Size(16, true));
        if (vi > 4 || si > 4) throw new InvalidOperationException("it gives an index size past four bytes");
        if (raw.Length < 4 || !raw.AsSpan(raw.Length - 4).SequenceEqual("OMD3"u8)) throw new InvalidOperationException("it has no end chunk");

        string? Str(ref int at)
        {
            var offset = Index(raw, ref at, si);
            if (offset <= 0 || 16 + offset >= raw.Length) return null;
            var start = 16 + offset;
            var end = Array.IndexOf(raw, (byte)0, start);
            return Encoding.UTF8.GetString(raw, start, (end < 0 ? raw.Length : end) - start);
        }

        // The file's own assets first, which a material may name before them.
        for (int chunk = headLength; chunk + 8 <= raw.Length && !raw.AsSpan(chunk, 4).SequenceEqual("OMD3"u8);)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(chunk + 4));
            if (size < 8 || chunk + size > raw.Length) break;
            if (raw.AsSpan(chunk, 4).SequenceEqual("ASET"u8) && size > 8 + si)
            {
                var at = chunk + 8;
                if (Str(ref at) is { } assetName) file.Assets[assetName] = raw.AsSpan(at, chunk + size - at).ToArray();
            }
            chunk += size;
        }

        uint[] cmap = [];
        for (int chunk = headLength; chunk + 8 <= raw.Length && !raw.AsSpan(chunk, 4).SequenceEqual("OMD3"u8);)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(chunk + 4));
            if (size < 8 || chunk + size > raw.Length) throw new InvalidOperationException($"a chunk of {size} bytes at {chunk} reaches past the file's end");
            var magic = Encoding.ASCII.GetString(raw, chunk, 4);
            var end = chunk + size;
            var at = chunk + 8;
            switch (magic)
            {
                case "CMAP":
                    cmap = new uint[(size - 8) / 4];
                    for (int i = 0; i < cmap.Length; i++) cmap[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(at + i * 4));
                    break;
                case "TMAP":
                    file.Texcoords = new Vector2[(size - 8) / (2 * vc)];
                    for (int i = 0; i < file.Texcoords.Length; i++, at += 2 * vc)
                        file.Texcoords[i] = new Vector2(Unsigned(raw, at, vc), Unsigned(raw, at + vc, vc));
                    break;
                case "VRTS":
                {
                    var record = ci + sk + 4 * vc;
                    while (at + record <= end)
                    {
                        var value = new Vector4(Signed(raw, at, vc), Signed(raw, at + vc, vc), Signed(raw, at + 2 * vc, vc), Signed(raw, at + 3 * vc, vc));
                        at += 4 * vc;
                        var color = Color(raw, at, ci, cmap);
                        at += ci;
                        var skin = sk > 0 ? Index(raw, ref at, sk) : -1;
                        file.Vertices.Add(new Vertex(value, color, skin));
                    }
                    break;
                }
                case "BONE":
                {
                    if (bi == 0) break;
                    var boneCount = Index(raw, ref at, bi);
                    var skinCount = sk > 0 ? Index(raw, ref at, sk) : 0;
                    var bones = new List<Bone>();
                    for (int b = 0; b < boneCount && at < end; b++)
                    {
                        var parent = Index(raw, ref at, bi);
                        var boneName = Str(ref at) ?? $"bone {b}";
                        var position = Index(raw, ref at, vi);
                        var orientation = Index(raw, ref at, vi);
                        bones.Add(new Bone(parent, boneName, position, orientation));
                    }
                    file.Bones = [.. bones];
                    var skins = new List<Skin>();
                    var bytes = new byte[8];
                    for (int s = 0; s < skinCount && at < end; s++)
                    {
                        var (ids, weights) = (new[] { -1, -1, -1, -1 }, new float[4]);
                        Array.Clear(bytes);
                        if (nb == 1) bytes[0] = 255;
                        else { raw.AsSpan(at, nb).CopyTo(bytes); at += nb; }
                        for (int j = 0; j < nb; j++)
                        {
                            if (bytes[j] == 0) continue;
                            var bone = Index(raw, ref at, bi);
                            if (j < 4) (ids[j], weights[j]) = (bone, bytes[j] / 255f);
                        }
                        // Normalized over the bones that hold the vertex, as m3d's post-processing does.
                        var sum = 0f;
                        for (int j = 0; j < 4 && ids[j] >= 0 && weights[j] > 0; j++) sum += weights[j];
                        if (sum > 0) for (int j = 0; j < 4; j++) weights[j] /= sum;
                        skins.Add(new Skin(ids, weights));
                    }
                    file.Skins = [.. skins];
                    break;
                }
                case "MTRL":
                {
                    var materialName = Str(ref at) ?? $"material {file.Materials.Count}";
                    var properties = new List<(byte, uint, float, string?)>();
                    while (at < end)
                    {
                        var type = raw[at++];
                        if (type >= 128) { properties.Add((type, 0, 0, Str(ref at))); continue; }
                        switch (type)
                        {
                            case 0 or 1 or 2 or 4 or 5:
                                // A file whose colors have no size gives none, and reads none here.
                                properties.Add((type, Color(raw, at, ci, cmap), 0, null));
                                at += ci;
                                break;
                            case 8: properties.Add((type, raw[at++], 0, null)); break;
                            case 3 or 6 or 7 or 64 or 65 or 66 or 67 or 68:
                                properties.Add((type, 0, BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(at)), null));
                                at += 4;
                                break;
                            default: at = end; break;
                        }
                    }
                    if (file.Materials.All(m => m.Name != materialName)) file.Materials.Add(new Material(materialName, properties));
                    break;
                }
                case "MESH":
                {
                    var material = -1;
                    while (at < end)
                    {
                        int flags = raw[at++], corners = flags >> 4;
                        flags &= 15;
                        if (corners == 0)
                        {
                            var used = Str(ref at);
                            if (flags == 0) material = used is null ? -1 : file.Materials.FindIndex(m => m.Name == used);
                            continue;
                        }
                        if (corners != 3) throw new InvalidOperationException("it has a face of other than three corners, where raylib reads triangles");
                        var (vertices, texcoords, normals) = (new int[3], new[] { -1, -1, -1 }, new[] { -1, -1, -1 });
                        for (int k = 0; k < 3; k++)
                        {
                            vertices[k] = Index(raw, ref at, vi);
                            if ((uint)vertices[k] >= (uint)file.Vertices.Count) throw new InvalidOperationException($"a face names vertex {vertices[k]} of {file.Vertices.Count}");
                            if ((flags & 1) != 0) texcoords[k] = Index(raw, ref at, ti);
                            if ((flags & 2) != 0) normals[k] = Index(raw, ref at, vi);
                            if ((flags & 4) != 0) at += vi;
                        }
                        file.Faces.Add(new Face(material, vertices, texcoords, normals));
                    }
                    break;
                }
                case "ACTN":
                {
                    var actionName = Str(ref at) ?? $"action {file.Actions.Count}";
                    var frameCount = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(at));
                    at += 2;
                    if (frameCount < 1) break;
                    var duration = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(at));
                    at += 4;
                    var frames = new List<Frame>();
                    for (int f = 0; f < frameCount && at < end; f++)
                    {
                        var milliseconds = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(at));
                        at += 4;
                        var transformCount = fc > 0 ? Index(raw, ref at, fc) : 0;
                        var transforms = new Transform3[Math.Max(0, transformCount)];
                        for (int t = 0; t < transforms.Length; t++)
                            transforms[t] = new Transform3(Index(raw, ref at, bi), Index(raw, ref at, vi), Index(raw, ref at, vi));
                        frames.Add(new Frame(milliseconds, transforms));
                    }
                    file.Actions.Add(new Action(actionName, duration, [.. frames]));
                    break;
                }
            }
            chunk = end;
        }
        return file;
    }

    // A color of four bytes, or an index of one or two into the color map, or none.
    private static uint Color(byte[] raw, int at, int size, uint[] cmap) => size switch
    {
        1 => raw[at] < cmap.Length ? cmap[raw[at]] : 0,
        2 => BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(at)) is var index && index < cmap.Length ? cmap[index] : 0,
        4 => BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(at)),
        _ => 0,
    };

    // m3d's _m3d_getidx: an index of one, two or four bytes, the top two values of one or two bytes
    // standing for -2 and -1, which mean none.
    private static int Index(byte[] raw, ref int at, int size)
    {
        if (at + size > raw.Length) throw new InvalidOperationException("an index reaches past the file's end");
        int value = size switch
        {
            1 => raw[at] > 253 ? (sbyte)raw[at] : raw[at],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(at)) is var u && u > 65533 ? (short)u : u,
            4 => BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(at)),
            _ => 0,
        };
        at += size;
        return value;
    }

    // A coordinate of one, two, four or eight bytes, the narrow ones signed fractions of one.
    private static float Signed(byte[] raw, int at, int size) => size switch
    {
        1 => (sbyte)raw[at] / 127f,
        2 => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(at)) / 32767f,
        4 => BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(at)),
        _ => (float)BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(at)),
    };

    // A texture coordinate, the narrow ones unsigned fractions of one.
    private static float Unsigned(byte[] raw, int at, int size) => size switch
    {
        1 => raw[at] / 255f,
        2 => BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(at)) / 65535f,
        4 => BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(at)),
        _ => (float)BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(at)),
    };
}
