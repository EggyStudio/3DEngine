using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Assets.Models;

/// <summary>Inter-Quake Models, read into a scene and clips as raylib's loader reads them.</summary>
[Trait("Category", "Unit")]
public sealed class IqmModelReaderTests
{
    // A triangle of three vertices held by two joints, a root at the origin and a tip one unit up
    // it, the corner at the tip's held by the tip alone, and a clip of two frames whose second
    // lifts the tip one unit more. With joints left out, it is a file of the clip alone, which
    // names no bones, and with the mesh left out one with no mesh.
    internal static byte[] Triangle(bool joints = true, bool mesh = true)
    {
        var text = "\0tri\0root\0tip\0wave\0"u8.ToArray();
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write(new byte[124]);
        var header = new uint[27];
        header[0] = 2;

        void At(int field, uint value) => header[field] = value;
        uint Here() => (uint)stream.Position;

        At(3, (uint)text.Length);
        At(4, Here());
        w.Write(text);
        while (stream.Position % 4 != 0) w.Write((byte)0);

        if (mesh)
        {
            At(5, 1);
            At(6, Here());
            foreach (var value in new uint[] { 1, 0, 0, 3, 0, 1 }) w.Write(value);

            // Position, texture coordinate, normal, joint indices and weights, each array's
            // header then its values.
            At(7, 5);
            At(8, 3);
            At(9, Here());
            var arrays = Here();
            w.Write(new byte[5 * 20]);
            var offsets = new uint[5];
            offsets[0] = Here();
            foreach (var f in new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }) w.Write(f);
            offsets[1] = Here();
            foreach (var f in new float[] { 0, 0, 1, 0, 0, 1 }) w.Write(f);
            offsets[2] = Here();
            foreach (var f in new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1 }) w.Write(f);
            offsets[3] = Here();
            w.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0 });
            offsets[4] = Here();
            w.Write(new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0 });
            var end = Here();
            stream.Position = arrays;
            var kinds = new (uint Type, uint Format, uint Size)[] { (0, 7, 3), (1, 7, 2), (2, 7, 3), (4, 1, 4), (5, 1, 4) };
            for (int a = 0; a < 5; a++)
                foreach (var value in new[] { kinds[a].Type, 0u, kinds[a].Format, kinds[a].Size, offsets[a] }) w.Write(value);
            stream.Position = end;

            At(10, 1);
            At(11, Here());
            foreach (var value in new uint[] { 0, 1, 2 }) w.Write(value);
        }

        if (joints)
        {
            At(13, 2);
            At(14, Here());
            void Joint(uint name, int parent, float y)
            {
                w.Write(name);
                w.Write(parent);
                foreach (var f in new float[] { 0, y, 0, 0, 0, 0, 1, 1, 1, 1 }) w.Write(f);
            }
            Joint(5, -1, 0);
            Joint(10, 0, 1);
        }

        // The tip's pose moves its y by half the frame's value, 0 and then 2.
        At(15, 2);
        At(16, Here());
        void Pose(int parent, uint mask, float y, float yScale)
        {
            w.Write(parent);
            w.Write(mask);
            foreach (var f in new float[] { 0, y, 0, 0, 0, 0, 1, 1, 1, 1 }) w.Write(f);
            foreach (var f in new float[] { 0, yScale, 0, 0, 0, 0, 0, 0, 0, 0 }) w.Write(f);
        }
        Pose(-1, 0, 0, 0);
        Pose(0, 0x2, 1, 0.5f);

        At(17, 1);
        At(18, Here());
        w.Write(14u);
        w.Write(0u);
        w.Write(2u);
        w.Write(24f);
        w.Write(0u);

        At(19, 2);
        At(20, 1);
        At(21, Here());
        w.Write((ushort)0);
        w.Write((ushort)2);

        At(1, Here());
        stream.Position = 0;
        w.Write("INTERQUAKEMODEL\0"u8);
        foreach (var value in header) w.Write(value);
        return stream.ToArray();
    }

    [Fact]
    public void A_Mesh_Turns_Its_Corners_As_Raylib_Does_And_Is_Held_By_Its_Joints()
    {
        var scene = IqmModelReader.Read(Triangle(), "tri");
        var root = scene.Roots.Single();
        root.Children.Select(n => n.Name).Should().Equal("root", "tri");
        root.Children[0].Children.Single().Name.Should().Be("tip", "a joint hangs from its parent's node");

        var node = root.Children[1];
        var mesh = node.Components.OfType<SceneMeshPayload>().Single();
        mesh.Indices.Should().Equal([2, 1, 0], "each triangle's corners are taken in the reverse of the file's order");
        mesh.Uv0.Should().Equal(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1));

        var skin = node.Components.OfType<SceneSkinPayload>().Single();
        skin.JointIndices.Should().Equal((ushort[])[0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0]);
        skin.JointWeights.Where((_, i) => i % 4 == 0).Should().Equal([1f, 1f, 1f], "a weight of 255 is whole");

        var skeleton = node.Components.OfType<SceneSkeletonPayload>().Single();
        skeleton.JointNames.Should().Equal("root", "tip");
        skeleton.ParentIndices.Should().Equal(-1, 0);
        skeleton.InverseBindMatrices[1].Translation.Should().Be(new Vector3(0, -1, 0), "the tip rests one unit up");
    }

    [Fact]
    public void A_Clip_Poses_Each_Bone_In_The_Models_Space_At_Each_Of_The_Files_Frames()
    {
        var clip = IqmModelReader.ReadAnimations(Triangle()).Single();

        clip.Name.Should().Be("wave");
        clip.Bones.Should().Equal(new BoneInfo("root", -1), new BoneInfo("tip", 0));
        clip.KeyframeCount.Should().Be(2, "the file's frames are the clip's, whatever rate it gives");
        clip.KeyframePoses[0][1].Position.Y.Should().BeApproximately(1, 1e-5f);
        clip.KeyframePoses[1][1].Position.Y.Should().BeApproximately(2, 1e-5f, "half of the frame's 2 lifts the tip");

        IqmModelReader.ReadAnimations(Triangle(joints: false, mesh: false)).Single().Bones
            .Should().Equal([new BoneInfo("", -1), new BoneInfo("", 0)], "a file of clips alone names no bones");
    }

    [Fact]
    public void A_File_That_Is_Not_An_Inter_Quake_Model_Is_Refused()
    {
        var act = () => IqmModelReader.Read("VOX \u0096\0\0\0"u8.ToArray(), "box");
        act.Should().Throw<InvalidOperationException>().WithMessage("*not an Inter-Quake Model*");

        var cut = Triangle()[..200];
        var truncated = () => IqmModelReader.Read(cut, "tri");
        truncated.Should().Throw<InvalidOperationException>().WithMessage("*past its 200*");
    }
}
