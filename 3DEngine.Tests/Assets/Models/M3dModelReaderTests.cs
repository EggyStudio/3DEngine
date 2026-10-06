using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Assets.Models;

/// <summary>Model 3D files, read into a scene and clips as raylib's loader reads them.</summary>
[Trait("Category", "Unit")]
public sealed class M3dModelReaderTests
{
    // An uncompressed file of a green triangle with texture coordinates and no normals, its corner
    // at the tip held by a tip bone one unit up a root bone, and an action of 34 milliseconds whose
    // key at its end lifts the tip one unit more. Coordinates are floats, colors four bytes, and
    // every index and string offset one byte.
    internal static byte[] Triangle()
    {
        var strings = "tri\0\0\0\0root\0tip\0mat\0wave\0"u8.ToArray();
        const byte Root = 7, Tip = 12, Mat = 16, Wave = 20, None = 255;
        const uint Types = 2 | (2 << 6) | (3 << 18) | (3 << 20);

        static byte[] Chunk(string magic, Action<BinaryWriter> write)
        {
            using var body = new MemoryStream();
            using (var w = new BinaryWriter(body, System.Text.Encoding.ASCII, leaveOpen: true)) write(w);
            using var chunk = new MemoryStream();
            using var c = new BinaryWriter(chunk);
            c.Write(System.Text.Encoding.ASCII.GetBytes(magic));
            c.Write((uint)(body.Length + 8));
            c.Write(body.ToArray());
            return chunk.ToArray();
        }

        // Three corners, then the bones' rest positions, the turn of none, and the tip lifted.
        (Vector4 Value, uint Color, byte Skin)[] vertices =
        [
            (new(0, 0, 0, 1), 0xff0000ff, 0), (new(1, 0, 0, 1), 0xff0000ff, 0), (new(0, 1, 0, 1), 0xff0000ff, 1),
            (new(0, 0, 0, 0), 0, None), (new(0, 0, 0, 1), 0, None), (new(0, 1, 0, 0), 0, None), (new(0, 2, 0, 0), 0, None),
        ];
        var chunks = new List<byte[]>
        {
            Chunk("TMAP", w => { foreach (var f in new float[] { 0, 0, 1, 0, 0, 1 }) w.Write(f); }),
            Chunk("VRTS", w =>
            {
                foreach (var (value, color, skin) in vertices)
                {
                    w.Write(value.X); w.Write(value.Y); w.Write(value.Z); w.Write(value.W);
                    w.Write(color);
                    w.Write(skin);
                }
            }),
            Chunk("BONE", w => w.Write(new byte[] { 2, 2, None, Root, 3, 4, 0, Tip, 5, 4, 0, 1 })),
            Chunk("MTRL", w => { w.Write(Mat); w.Write((byte)0); w.Write(0xff00ff00u); }),
            Chunk("MESH", w => w.Write(new byte[] { 0x00, Mat, 0x31, 0, 0, 1, 1, 2, 2 })),
            Chunk("ACTN", w =>
            {
                w.Write(Wave);
                w.Write((ushort)2);
                w.Write(34u);
                w.Write(0u);
                w.Write((byte)0);
                w.Write(34u);
                w.Write(new byte[] { 1, 1, 6, 4 });
            }),
        };

        using var head = new MemoryStream();
        using (var h = new BinaryWriter(head, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            h.Write("HEAD"u8);
            h.Write((uint)(16 + strings.Length));
            h.Write(1f);
            h.Write(Types);
            h.Write(strings);
            foreach (var chunk in chunks) h.Write(chunk);
            h.Write("OMD3"u8);
        }
        using var file = new MemoryStream();
        using var f = new BinaryWriter(file);
        f.Write("3DMO"u8);
        f.Write((uint)(head.Length + 8));
        f.Write(head.ToArray());
        return file.ToArray();
    }

    [Fact]
    public void A_Mesh_Is_Its_Faces_Corners_With_Raylibs_Texture_Coordinates_And_Made_Normals()
    {
        var root = M3dModelReader.Read(Triangle(), "tri").Roots.Single();
        root.Children.Select(n => n.Name).Should().Equal("root", "NO BONE", "mat");

        var node = root.Children[2];
        var mesh = node.Components.OfType<SceneMeshPayload>().Single();
        mesh.Positions.Should().Equal(Vector3.Zero, Vector3.UnitX, Vector3.UnitY);
        mesh.Indices.Should().Equal(0, 1, 2);
        mesh.Uv0.Should().Equal([new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0)], "raylib counts them from the bottom");
        mesh.Normals.Should().OnlyContain(n => n == Vector3.UnitZ, "a face with no normals is given its own");
        mesh.Colors.Should().BeNull("every corner of a mesh of a material has a color, so raylib keeps none");
        node.Components.OfType<SceneMaterialPayload>().Single().BaseColorFactor.Should().Be(new Vector4(0, 1, 0, 1));

        var skin = node.Components.OfType<SceneSkinPayload>().Single();
        skin.JointIndices.Where((_, i) => i % 4 == 0).Should().Equal((ushort[])[0, 0, 1]);
        skin.JointWeights.Where((_, i) => i % 4 == 0).Should().Equal([1f, 1f, 1f]);
        var skeleton = node.Components.OfType<SceneSkeletonPayload>().Single();
        skeleton.JointNames.Should().Equal("root", "tip", "NO BONE");
        skeleton.ParentIndices.Should().Equal(-1, 0, -1);
    }

    [Fact]
    public void An_Action_Is_Posed_Every_17_Milliseconds_Between_Its_Keys()
    {
        var clip = M3dModelReader.ReadAnimations(Triangle()).Single();

        clip.Name.Should().Be("wave");
        clip.Bones.Select(b => b.Name).Should().Equal("root", "tip", "NO BONE");
        clip.KeyframeCount.Should().Be(2, "34 milliseconds hold two frames of 17");
        clip.KeyframePoses[0][1].Position.Y.Should().BeApproximately(1, 1e-5f);
        clip.KeyframePoses[1][1].Position.Y.Should().BeApproximately(1.5f, 1e-5f, "17 milliseconds is halfway to the key that lifts the tip");
        clip.KeyframePoses[1][2].Should().Be(Transform.Identity, "the last bone never moves");
    }

    [Fact]
    public void A_File_That_Is_Not_A_Binary_Model_3D_Is_Refused()
    {
        var text = () => M3dModelReader.Read("3dmo model"u8.ToArray(), "text");
        text.Should().Throw<InvalidOperationException>().WithMessage("*in text*");

        var other = () => M3dModelReader.Read("INTERQUAKEMODEL\0"u8.ToArray(), "other");
        other.Should().Throw<InvalidOperationException>().WithMessage("*not a Model 3D file*");

        var cut = Triangle();
        cut[^1] = 0;
        var unended = () => M3dModelReader.Read(cut, "tri");
        unended.Should().Throw<InvalidOperationException>().WithMessage("*no end chunk*");
    }
}
