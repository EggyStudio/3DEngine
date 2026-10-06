using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Engine;

/// <summary>
/// Reads an Inter-Quake Model (<c>.iqm</c>, version 2) as raylib's <c>LoadIQM</c> and
/// <c>LoadModelAnimationsIQM</c> read one: its meshes with their skin into a scene, and its clips
/// into poses of every bone at each of the file's frames.
/// </summary>
/// <remarks>
/// Positions, normals and texture coordinates are floats, and joint indices, weights and colors
/// bytes, as raylib takes them, a weight of 255 being whole. Each triangle's corners are taken in
/// the reverse of the file's order, as raylib takes them, so faces turn the way raylib's do. A clip's
/// frames are the file's, as raylib counts them, whatever rate the file gives, and its bones are
/// named by the file's joints, or not named in a file of clips alone, which has none.
/// </remarks>
internal static class IqmModelReader
{
    private const int HeaderSize = 124;
    private const int JointSize = 48;
    private const int PoseSize = 88;

    // The vertex arrays raylib reads, by IQM's type, and the formats it reads them in.
    private const uint Position = 0, Texcoord = 1, Normal = 2, BlendIndexes = 4, BlendWeights = 5, Color = 6;
    private const uint UnsignedByte = 1, Float = 7;

    private readonly record struct Header(
        uint TextCount, uint TextOffset, uint MeshCount, uint MeshOffset, uint ArrayCount, uint VertexCount, uint ArrayOffset,
        uint TriangleCount, uint TriangleOffset, uint JointCount, uint JointOffset, uint PoseCount, uint PoseOffset,
        uint AnimCount, uint AnimOffset, uint FrameCount, uint FrameChannelCount, uint FrameOffset);

    /// <summary>Reads a file's meshes, skin and skeleton into a scene, as <c>LoadModel</c> reads a model file.</summary>
    /// <exception cref="InvalidOperationException">The bytes are not an IQM file of version 2, or reach past their end.</exception>
    public static Scene Read(ReadOnlySpan<byte> data, string name)
    {
        var header = ReadHeader(data);
        var text = Slice(data, header.TextOffset, header.TextCount);

        // Each joint a node under its parent, so a model's bones are found by name as a glTF's are.
        var jointNames = new string[header.JointCount];
        var parents = new int[header.JointCount];
        var locals = new Transform[header.JointCount];
        var inverseBinds = new Matrix4x4[header.JointCount];
        var worlds = new Matrix4x4[header.JointCount];
        var root = new SceneNode { Name = name, SourcePath = "/" + name };
        var jointNodes = new SceneNode[header.JointCount];
        var joints = Slice(data, header.JointOffset, header.JointCount * JointSize);
        for (int j = 0; j < jointNames.Length; j++)
        {
            var at = joints[(j * JointSize)..];
            jointNames[j] = Text(text, BinaryPrimitives.ReadUInt32LittleEndian(at));
            parents[j] = BinaryPrimitives.ReadInt32LittleEndian(at[4..]);
            if (parents[j] >= j)
                throw new InvalidOperationException($"joint {j} names {parents[j]} as its parent, which does not come before it");
            locals[j] = new Transform
            {
                Position = new Vector3(Single(at, 8), Single(at, 12), Single(at, 16)),
                Rotation = Quaternion.Normalize(new Quaternion(Single(at, 20), Single(at, 24), Single(at, 28), Single(at, 32))),
                Scale = new Vector3(Single(at, 36), Single(at, 40), Single(at, 44)),
            };
            worlds[j] = TransformPropagation.ToMatrix(locals[j]) * (parents[j] >= 0 ? worlds[parents[j]] : Matrix4x4.Identity);
            if (!Matrix4x4.Invert(worlds[j], out inverseBinds[j])) inverseBinds[j] = Matrix4x4.Identity;

            jointNodes[j] = new SceneNode { Name = jointNames[j], SourcePath = "/" + jointNames[j], LocalTransform = locals[j] };
            (parents[j] >= 0 ? jointNodes[parents[j]].Children : root.Children).Add(jointNodes[j]);
        }
        var skeleton = new SceneSkeletonPayload
        {
            Name = name,
            JointNames = jointNames,
            ParentIndices = parents,
            InverseBindMatrices = inverseBinds,
            LocalBindTransforms = locals,
        };

        // The vertex arrays, whole, each mesh taking its run of them.
        var count = (int)header.VertexCount;
        Vector3[]? positions = null, normals = null;
        Vector2[]? texcoords = null;
        byte[]? blendIndexes = null, blendWeights = null, colors = null;
        var arrays = Slice(data, header.ArrayOffset, header.ArrayCount * 20);
        for (int a = 0; a < header.ArrayCount; a++)
        {
            var at = arrays[(a * 20)..];
            var type = BinaryPrimitives.ReadUInt32LittleEndian(at);
            var format = BinaryPrimitives.ReadUInt32LittleEndian(at[8..]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(at[12..]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(at[16..]);
            switch (type)
            {
                case Position: positions = Vectors3(Floats(data, offset, header.VertexCount, size, format, 3)); break;
                case Normal: normals = Vectors3(Floats(data, offset, header.VertexCount, size, format, 3)); break;
                case Texcoord: texcoords = Vectors2(Floats(data, offset, header.VertexCount, size, format, 2)); break;
                case BlendIndexes: blendIndexes = Bytes(data, offset, header.VertexCount, size, format); break;
                case BlendWeights: blendWeights = Bytes(data, offset, header.VertexCount, size, format); break;
                case Color: colors = Bytes(data, offset, header.VertexCount, size, format); break;
            }
        }
        if (positions is null) throw new InvalidOperationException("it has no positions");

        var triangles = Slice(data, header.TriangleOffset, header.TriangleCount * 12);
        var meshes = Slice(data, header.MeshOffset, header.MeshCount * 24);
        for (int m = 0; m < header.MeshCount; m++)
        {
            var at = meshes[(m * 24)..];
            var meshName = Text(text, BinaryPrimitives.ReadUInt32LittleEndian(at));
            var material = Text(text, BinaryPrimitives.ReadUInt32LittleEndian(at[4..]));
            var firstVertex = (int)BinaryPrimitives.ReadUInt32LittleEndian(at[8..]);
            var vertexCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(at[12..]);
            var firstTriangle = BinaryPrimitives.ReadUInt32LittleEndian(at[16..]);
            var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(at[20..]);
            if (firstVertex + (long)vertexCount > count || firstTriangle + (long)triangleCount > header.TriangleCount)
                throw new InvalidOperationException($"mesh '{meshName}' reaches past the file's vertices or triangles");

            var indices = new int[triangleCount * 3];
            for (int t = 0; t < triangleCount; t++)
            {
                var corner = triangles[(int)((firstTriangle + t) * 12)..];
                for (int k = 0; k < 3; k++)
                {
                    var index = (long)BinaryPrimitives.ReadUInt32LittleEndian(corner[((2 - k) * 4)..]) - firstVertex;
                    if (index < 0 || index >= vertexCount)
                        throw new InvalidOperationException($"mesh '{meshName}' names a vertex outside its own");
                    indices[t * 3 + k] = (int)index;
                }
            }

            var meshPositions = positions.AsSpan(firstVertex, vertexCount).ToArray();
            var path = $"/{name}/{meshName}";
            var materialPath = material.Length > 0 ? $"{path}/{material}" : null;
            var node = new SceneNode { Name = meshName, SourcePath = path };
            node.Components.Add(new SceneMeshPayload
            {
                Name = meshName,
                Positions = meshPositions,
                Normals = normals?.AsSpan(firstVertex, vertexCount).ToArray(),
                Uv0 = texcoords?.AsSpan(firstVertex, vertexCount).ToArray(),
                Colors = colors is null ? null : [.. Enumerable.Range(firstVertex, vertexCount).Select(v =>
                    new Vector4(colors[v * 4], colors[v * 4 + 1], colors[v * 4 + 2], colors[v * 4 + 3]) / 255f)],
                Indices = indices,
                Subsets = [new SceneMeshSubset(meshName, 0, indices.Length, materialPath)],
                LocalBounds = SceneBounds.FromPositions(meshPositions),
            });
            // raylib's material is its default with the file's material as the texture beside it.
            if (materialPath is not null)
                node.Components.Add(new SceneMaterialPayload { Name = material, SourcePath = materialPath, BaseColorTexture = new SceneTextureRef(material) });
            if (blendIndexes is not null && blendWeights is not null && jointNames.Length > 0)
            {
                var jointIndices = new ushort[vertexCount * 4];
                var weights = new float[vertexCount * 4];
                for (int i = 0; i < jointIndices.Length; i++)
                {
                    jointIndices[i] = Math.Min(blendIndexes[firstVertex * 4 + i], (byte)(jointNames.Length - 1));
                    weights[i] = blendWeights[firstVertex * 4 + i] / 255f;
                }
                node.Components.Add(new SceneSkinPayload { SkeletonPath = root.SourcePath, JointIndices = jointIndices, JointWeights = weights });
                node.Components.Add(skeleton);
            }
            root.Children.Add(node);
        }

        var scene = new Scene { Name = name };
        scene.Roots.Add(root);
        return scene;
    }

    /// <summary>Reads a file's clips, each bone's pose in the model's space at every frame the file has.</summary>
    /// <exception cref="InvalidOperationException">The bytes are not an IQM file of version 2, or reach past their end.</exception>
    public static ModelAnimation[] ReadAnimations(ReadOnlySpan<byte> data)
    {
        var header = ReadHeader(data);
        var text = Slice(data, header.TextOffset, header.TextCount);
        var poses = Slice(data, header.PoseOffset, header.PoseCount * PoseSize);
        var frames = Slice(data, header.FrameOffset, header.FrameCount * header.FrameChannelCount * 2);
        var joints = header.JointCount == header.PoseCount ? Slice(data, header.JointOffset, header.JointCount * JointSize) : default;

        var bones = new BoneInfo[header.PoseCount];
        for (int p = 0; p < bones.Length; p++)
        {
            var parent = BinaryPrimitives.ReadInt32LittleEndian(poses[(p * PoseSize)..]);
            if (parent >= p) throw new InvalidOperationException($"pose {p} names {parent} as its parent, which does not come before it");
            bones[p] = new BoneInfo(joints.IsEmpty ? "" : Text(text, BinaryPrimitives.ReadUInt32LittleEndian(joints[(p * JointSize)..])), parent);
        }

        var clips = new ModelAnimation[header.AnimCount];
        var anims = Slice(data, header.AnimOffset, header.AnimCount * 20);
        Span<float> values = stackalloc float[10];
        for (int a = 0; a < clips.Length; a++)
        {
            var at = anims[(a * 20)..];
            var first = BinaryPrimitives.ReadUInt32LittleEndian(at[4..]);
            var frameCount = BinaryPrimitives.ReadUInt32LittleEndian(at[8..]);
            if (first + (long)frameCount > header.FrameCount)
                throw new InvalidOperationException($"clip {a} reaches past the file's frames");

            var framePoses = new Transform[frameCount][];
            var channel = (int)(first * header.FrameChannelCount);
            for (int f = 0; f < frameCount; f++)
            {
                // Each of a pose's ten channels is its offset, and its scale times the frame's next
                // value where the pose's mask has the channel's bit, as raylib reads them.
                var worlds = new Matrix4x4[bones.Length];
                framePoses[f] = new Transform[bones.Length];
                for (int p = 0; p < bones.Length; p++)
                {
                    var pose = poses[(p * PoseSize)..];
                    var mask = BinaryPrimitives.ReadUInt32LittleEndian(pose[4..]);
                    for (int c = 0; c < 10; c++)
                    {
                        values[c] = Single(pose, 8 + c * 4);
                        if ((mask & (1u << c)) == 0) continue;
                        if (channel >= header.FrameCount * header.FrameChannelCount)
                            throw new InvalidOperationException($"clip {a} reads past the file's frames");
                        values[c] += BinaryPrimitives.ReadUInt16LittleEndian(frames[(channel * 2)..]) * Single(pose, 48 + c * 4);
                        channel++;
                    }
                    var local = new Transform
                    {
                        Position = new Vector3(values[0], values[1], values[2]),
                        Rotation = Quaternion.Normalize(new Quaternion(values[3], values[4], values[5], values[6])),
                        Scale = new Vector3(values[7], values[8], values[9]),
                    };
                    // The pose in the model's space, the parent's taken as the bind pose's is.
                    var parent = bones[p].Parent;
                    worlds[p] = TransformPropagation.ToMatrix(local) * (parent >= 0 ? worlds[parent] : Matrix4x4.Identity);
                    if (!Matrix4x4.Decompose(worlds[p], out var scale, out var rotation, out var position))
                        (scale, rotation, position) = (Vector3.One, Quaternion.Identity, worlds[p].Translation);
                    framePoses[f][p] = new Transform { Position = position, Rotation = rotation, Scale = scale };
                }
            }

            clips[a] = new ModelAnimation
            {
                Name = Text(text, BinaryPrimitives.ReadUInt32LittleEndian(at)),
                Bones = [.. bones],
                FramePoses = framePoses,
            };
        }
        return clips;
    }

    private static Header ReadHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize || !data[..16].SequenceEqual("INTERQUAKEMODEL\0"u8))
            throw new InvalidOperationException("it is not an Inter-Quake Model");
        var version = BinaryPrimitives.ReadUInt32LittleEndian(data[16..]);
        if (version != 2) throw new InvalidOperationException($"it is IQM version {version}, where 2 is read");
        // The counts and offsets after the version, its size and its flags, leaving out the adjacency.
        var fields = data[16..HeaderSize].ToArray();
        uint At(int field) => BinaryPrimitives.ReadUInt32LittleEndian(fields[(field * 4)..]);
        return new Header(At(3), At(4), At(5), At(6), At(7), At(8), At(9), At(10), At(11), At(13), At(14), At(15), At(16),
            At(17), At(18), At(19), At(20), At(21));
    }

    private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> data, uint offset, long length)
    {
        if (length == 0) return default;
        if (offset > data.Length || length > data.Length - offset)
            throw new InvalidOperationException($"it names {length} bytes at {offset}, past its {data.Length}");
        return data.Slice((int)offset, (int)length);
    }

    // A name in the file's text, which runs to its first zero byte.
    private static string Text(ReadOnlySpan<byte> text, uint offset)
    {
        if (offset >= text.Length) return "";
        var rest = text[(int)offset..];
        var end = rest.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? rest : rest[..end]);
    }

    private static float Single(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);

    private static float[] Floats(ReadOnlySpan<byte> data, uint offset, uint vertices, uint size, uint format, int expected)
    {
        if (format != Float || size != expected)
            throw new InvalidOperationException($"an array of {size} values of format {format} stands where {expected} floats are read");
        var bytes = Slice(data, offset, vertices * (long)size * 4);
        var values = new float[vertices * size];
        for (int i = 0; i < values.Length; i++) values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes[(i * 4)..]);
        return values;
    }

    private static byte[] Bytes(ReadOnlySpan<byte> data, uint offset, uint vertices, uint size, uint format)
    {
        if (format != UnsignedByte || size != 4)
            throw new InvalidOperationException($"an array of {size} values of format {format} stands where four bytes are read");
        return Slice(data, offset, vertices * 4L).ToArray();
    }

    private static Vector3[] Vectors3(float[] values)
    {
        var vectors = new Vector3[values.Length / 3];
        for (int i = 0; i < vectors.Length; i++) vectors[i] = new Vector3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
        return vectors;
    }

    private static Vector2[] Vectors2(float[] values)
    {
        var vectors = new Vector2[values.Length / 2];
        for (int i = 0; i < vectors.Length; i++) vectors[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
        return vectors;
    }
}
