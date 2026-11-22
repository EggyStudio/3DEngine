using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Each_Edge_Is_Open_Or_Closed_As_The_Places_Of_Its_Ends_Say_In_A_Land_Of_Blocks_And_In_Triangles_That_Share_Places()
    {
        // The open edges the pool's corners carry, which field_splat.slang keeps a cell in front
        // beyond, found as a dictionary keyed by the two places of an edge's ends finds them, so
        // the field splats the same cells. A land of blocks whose faces keep vertices of their
        // own, triangles drawn among few places so an edge is shared by one, two or more, and
        // places at zero and at negative zero, which are the same place.
        var (land, landIndices) = BlockLand(12, 9);
        var landOpen = SceneFieldRenderer.OpenEdges(land, landIndices);
        landOpen.Should().Equal(OpenByPlaces(land, landIndices));
        landOpen.Should().Contain(true, "the land's foot is open with no floor under it").And.Contain(false, "its faces meet along their sides");

        var random = new Random(7);
        ModelVertex[] few = [.. Enumerable.Range(0, 40).Select(_ => Vertex(random.Next(4), random.Next(3), random.Next(4)))];
        uint[] drawn = [.. Enumerable.Range(0, 3 * 300 + 2).Select(_ => (uint)random.Next(few.Length))];
        SceneFieldRenderer.OpenEdges(few, drawn).Should().Equal(OpenByPlaces(few, drawn), "the two indices past the last whole triangle are left out");

        ModelVertex[] zeros = [Vertex(0, 0, 0), Vertex(1, 0, 0), Vertex(1, 0, 1), Vertex(-0f, 0, -0f), Vertex(1, -0f, 1), Vertex(0, 0, 1), Vertex(0, 0, 0)];
        uint[] zerosDrawn = [0, 1, 2, 3, 4, 5, 6, 6, 1];
        SceneFieldRenderer.OpenEdges(zeros, zerosDrawn).Should().Equal(OpenByPlaces(zeros, zerosDrawn))
            .And.Equal([false, true, false, false, true, true, true, false, false], "an edge shared by three is closed, and one from a place to itself open");
    }

    private static ModelVertex Vertex(float x, float y, float z) => new(new Vector3(x, y, z), Vector3.UnitY, Vector2.Zero);

    // An edge open where no other edge has ends at the same two places, keyed by the places.
    private static bool[] OpenByPlaces(ModelVertex[] vertices, uint[] indices)
    {
        var count = indices.Length / 3 * 3;
        var shared = new Dictionary<(Vector3, Vector3), int>();
        (Vector3, Vector3) Edge(int i)
        {
            var (a, b) = (vertices[indices[i]].Position, vertices[indices[i / 3 * 3 + (i % 3 + 1) % 3]].Position);
            return a.X < b.X || a.X == b.X && (a.Y < b.Y || a.Y == b.Y && a.Z <= b.Z) ? (a, b) : (b, a);
        }
        for (int i = 0; i < count; i++) shared[Edge(i)] = shared.GetValueOrDefault(Edge(i)) + 1;
        return [.. Enumerable.Range(0, count).Select(i => shared[Edge(i)] == 1)];
    }

    // A land of columns of blocks a side across, each as high as a hash says up to the height
    // given, meshed by the faces of its blocks that meet the air but the floor under it, each face
    // four vertices of its own.
    private static (ModelVertex[] Vertices, uint[] Indices) BlockLand(int side, int highest)
    {
        int Height(int x, int z) => x < 0 || z < 0 || x >= side || z >= side ? 0 : 1 + (x * 7 + z * 13 + x * z) % highest;
        bool Solid(int x, int y, int z) => y >= 0 && y < Height(x, z);
        var (vertices, indices) = (new List<ModelVertex>(), new List<uint>());
        (int X, int Y, int Z)[] sides = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];
        for (int x = 0; x < side; x++)
            for (int z = 0; z < side; z++)
                for (int y = 0; y < Height(x, z); y++)
                    foreach (var (dx, dy, dz) in sides)
                    {
                        if (Solid(x + dx, y + dy, z + dz) || dy < 0 && y == 0) continue;
                        var normal = new Vector3(dx, dy, dz);
                        var (u, v) = dx != 0 ? (Vector3.UnitY, Vector3.UnitZ) : dy != 0 ? (Vector3.UnitZ, Vector3.UnitX) : (Vector3.UnitX, Vector3.UnitY);
                        if (dx + dy + dz < 0) (u, v) = (v, u);
                        var middle = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) + normal * 0.5f;
                        var first = (uint)vertices.Count;
                        foreach (var (a, b) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                            vertices.Add(new ModelVertex(middle + u * (a * 0.5f) + v * (b * 0.5f), normal, Vector2.Zero));
                        indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
                    }
        return ([.. vertices], [.. indices]);
    }
}
