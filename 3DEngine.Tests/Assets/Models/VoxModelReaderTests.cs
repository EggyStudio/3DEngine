using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Assets.Models;

/// <summary>MagicaVoxel's files, read into a scene as raylib's loader reads them.</summary>
[Trait("Category", "Unit")]
public sealed class VoxModelReaderTests
{
    // A file of version 150 with a MAIN chunk holding a model 2 by 1 by 1 of two voxels side by
    // side along x, colored by palette entries 1 and 2.
    private static byte[] TwoVoxels()
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        void Chunk(string id, byte[] content)
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes(id));
            w.Write(content.Length);
            w.Write(0);
            w.Write(content);
        }
        w.Write("VOX "u8);
        w.Write(150);
        w.Write("MAIN"u8);
        w.Write(0);
        w.Write(0);
        Chunk("SIZE", [2, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]);
        Chunk("XYZI", [2, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 2]);
        var palette = new byte[256 * 4];
        (palette[0], palette[1], palette[2], palette[3]) = (255, 0, 0, 255);
        (palette[4], palette[5], palette[6], palette[7]) = (0, 0, 255, 255);
        Chunk("RGBA", palette);
        return stream.ToArray();
    }

    [Fact]
    public void Each_Face_No_Voxel_Covers_Is_A_Quad_Colored_From_The_Palette()
    {
        var mesh = (SceneMeshPayload)VoxModelReader.Read(TwoVoxels(), "pair").Roots.Single().Components.Single();

        mesh.Positions.Should().HaveCount(10 * 4, "two cubes show ten faces, the two between them hidden");
        mesh.Indices.Should().HaveCount(10 * 6);
        mesh.Colors!.Distinct().Should().BeEquivalentTo([new Vector4(1, 0, 0, 1), new Vector4(0, 0, 1, 1)]);
        // A quarter of a unit each, MagicaVoxel's y turned to depth from the back of a volume
        // rounded up to sixteen.
        mesh.LocalBounds.Min.Should().Be(new Vector3(0, 0, 3.75f));
        mesh.LocalBounds.Max.Should().Be(new Vector3(0.5f, 0.25f, 4f));
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            var (a, b, c) = (mesh.Positions[mesh.Indices[i]], mesh.Positions[mesh.Indices[i + 1]], mesh.Positions[mesh.Indices[i + 2]]);
            Vector3.Dot(Vector3.Cross(b - a, c - a), mesh.Normals![mesh.Indices[i]]).Should().BeGreaterThan(0, "each face is counterclockwise from outside, as raylib's are");
        }
    }

    [Fact]
    public void A_File_That_Is_Not_MagicaVoxels_Is_Refused()
    {
        var act = () => VoxModelReader.Read("NOPE0000"u8, "bad");
        act.Should().Throw<InvalidOperationException>();
    }
}
